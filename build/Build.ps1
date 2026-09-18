<#
.SYNOPSIS
    一键构建 PCL-In 单文件 exe，用于本地测试。

.DESCRIPTION
    与 GitHub Actions 发布流程使用同一套参数：Release + 单文件 + --no-self-contained。

    默认会自动推断一个「比线上最新版大一点」的版本号，临时写进 metadata.json，构建完再原样还原。
    推断顺序：GitHub Releases 最新 tag → 本地 git tag → metadata.json 的 base，取其中最大者 +1 个 patch。
    这样做是为了避免刚构建出来的测试版被启动器提示更新到线上版本，点一下就把你的测试版覆盖掉了。
    要保留仓库里的原始版本号，加 -KeepVersion；要显式指定就传 -Version。

.PARAMETER Architecture
    x64（默认）或 ARM64。

.PARAMETER Configuration
    默认 Release。

.PARAMETER Version
    测试版自报版本号。不传则自动推断（线上最新版本 +1 个 patch），必须大于当前线上最新版本，否则启动器会提示更新。

.PARAMETER KeepVersion
    不修改 metadata.json，直接使用仓库里的版本号。

.PARAMETER ResolveVersionOnly
    只推断并打印版本号，不做任何修改、不构建。用于检查版本推断逻辑。

.PARAMETER Output
    输出目录，默认为 build\artifact。

.NOTES
    仅适用于 https://github.com/PCL-In 项目（PCL-In/desktop），不做通用化。

.EXAMPLE
    .\build.cmd
    .\Build.ps1 -Architecture ARM64
    .\Build.ps1 -KeepVersion
    .\Build.ps1 -ResolveVersionOnly
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Architecture = 'x64',

    [ValidateSet('Debug', 'Release', 'Beta', 'CI')]
    [string]$Configuration = 'Release',

    [string]$Version,

    [switch]$KeepVersion,

    [switch]$ResolveVersionOnly,

    [string]$Output,

    # 由 build.cmd 转发，只用于控制构建结束后是否 pause，脚本本身不使用
    [switch]$NoPause
)

Write-Host ''
Write-Host '本脚本仅适用于 https://github.com/PCL-In 项目（PCL-In/desktop）。' -ForegroundColor DarkGray
Write-Host '它会临时把 metadata.json 的自报版本改成“比线上最新版大一点”，构建结束自动还原。' -ForegroundColor DarkGray

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch { }

function Write-Step([string]$Text) { Write-Host ''; Write-Host ('==> ' + $Text) -ForegroundColor Cyan }
function Write-Ok([string]$Text) { Write-Host ('    ' + $Text) -ForegroundColor Green }
function Write-Warn([string]$Text) { Write-Host ('    ' + $Text) -ForegroundColor Yellow }

# 把 "1.2.3" 变成 "1.2.4"；格式不认识就返回 $null
function Get-BumpedPatchVersion([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $null }
    $v = ($Text.Trim() -replace '^[vV]', '')
    $p = $v.Split('.')
    if ($p.Count -lt 3) { return $null }
    $major = 0
    $minor = 0
    $patch = 0
    if (-not [int]::TryParse($p[0], [ref]$major)) { return $null }
    if (-not [int]::TryParse($p[1], [ref]$minor)) { return $null }
    if (-not [int]::TryParse(($p[2] -replace '[^0-9].*$', ''), [ref]$patch)) { return $null }
    return ('' + $major + '.' + $minor + '.' + ($patch + 1))
}

# 在一堆版本号里取最大者（按 semver 前三段比较）
function Get-MaxVersion([string[]]$List) {
    $best = $null
    $bestParts = $null
    foreach ($item in $List) {
        if ([string]::IsNullOrWhiteSpace($item)) { continue }
        $v = ($item -replace '^[vV]', '').Trim()
        $p = $v.Split('.')
        if ($p.Count -lt 3) { continue }
        $nums = @()
        $ok = $true
        for ($i = 0; $i -lt 3; $i++) {
            $n = 0
            if (-not [int]::TryParse($p[$i], [ref]$n)) { $ok = $false; break }
            $nums += $n
        }
        if (-not $ok) { continue }
        if ($null -eq $bestParts) { $best = $v; $bestParts = $nums; continue }
        for ($i = 0; $i -lt 3; $i++) {
            if ($nums[$i] -gt $bestParts[$i]) { $best = $v; $bestParts = $nums; break }
            if ($nums[$i] -lt $bestParts[$i]) { break }
        }
    }
    return $best
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'Plain Craft Launcher 2\Plain Craft Launcher 2.csproj'
$metadataPath = Join-Path $repoRoot 'Plain Craft Launcher 2\metadata.json'
if (-not $Output) { $Output = Join-Path $repoRoot 'build\artifact' }

# 推断测试版版本号：取「线上最新 Release / 本地 git tag / metadata base」里的最大值，+1 个 patch
function Get-NextTestVersion {
    $candidates = @()
    $source = ''
    try {
        $rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/PCL-In/desktop/releases/latest' -Headers @{ 'User-Agent' = 'PCL-In-Build' } -TimeoutSec 12
        if ($rel.tag_name) {
            $candidates += [string]$rel.tag_name
            $source = 'GitHub Releases 最新 tag ' + $rel.tag_name
        }
    }
    catch {
        Write-Warn ('读取 GitHub 最新 Release 失败：' + $_.Exception.Message)
    }
    # 注意：本仓库的 tag 里混着上游 PCL CE 的版本（如 v2.15.1-beta.1），
    # 所以优先只取与 metadata base 同一大版本的 tag，取不到再退回全部 tag。
    $baseVersion = ''
    try {
        $match = ([regex]'"base"\s*:\s*"([^"]+)"').Match([System.IO.File]::ReadAllText($metadataPath))
        if ($match.Success) { $baseVersion = $match.Groups[1].Value }
    }
    catch { }
    try {
        $tags = @()
        if ($baseVersion) {
            $baseMajor = $baseVersion.Split('.')[0]
            $tags = & git -C $repoRoot tag --list ('v' + $baseMajor + '.*')
        }
        if (-not $tags) { $tags = & git -C $repoRoot tag --list 'v*' }
        if ($tags) { $candidates += $tags }
    }
    catch { }
    if ($baseVersion) { $candidates += $baseVersion }
    $max = Get-MaxVersion $candidates
    if (-not $max) { return $null }
    if (-not $source) { $source = '本地 git tag / metadata.json' }
    Write-Ok ('版本来源：' + $source + ' → ' + $max)
    return (Get-BumpedPatchVersion $max)
}

Write-Step '检查构建环境'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host '找不到 dotnet 命令，请先安装 .NET SDK 10：https://dotnet.microsoft.com/download' -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $projectPath)) {
    Write-Host ('找不到项目文件：' + $projectPath) -ForegroundColor Red
    exit 1
}
Write-Ok ('dotnet ' + (& dotnet --version))

if ($ResolveVersionOnly) {
    Write-Step '仅推断测试版版本号（不修改任何文件、不构建）'
    $resolved = if ($Version) { $Version } else { Get-NextTestVersion }
    if (-not $resolved) {
        Write-Host '推断失败，请显式指定，例如 -Version 1.0.4' -ForegroundColor Red
        exit 1
    }
    Write-Ok ('推断结果：' + $resolved)
    exit 0
}

$metadataBackup = $null
$failure = $null
try {
    if ($KeepVersion) {
        Write-Step '使用仓库里的原始版本号'
    }
    else {
        if (-not $Version) {
            Write-Step '自动推断测试版版本号（线上最新版 +1 个 patch）'
            $Version = Get-NextTestVersion
            if (-not $Version) { throw '无法推断测试版版本号，请显式指定，例如 -Version 1.0.4' }
        }
        Write-Step ('临时把自报版本改为 ' + $Version + '（构建完自动还原）')
        $parts = $Version.Split('.')
        if ($parts.Count -lt 3) { throw ('版本号格式不正确：' + $Version) }
        $code = [int]$parts[0] * 1000000 + [int]$parts[1] * 10000 + [int]$parts[2] * 100
        $metadataBackup = [System.IO.File]::ReadAllBytes($metadataPath)
        $text = [System.IO.File]::ReadAllText($metadataPath)
        $text = ([regex]'("base"\s*:\s*")[^"]*(")').Replace($text, ('${1}' + $Version + '${2}'), 1)
        $text = ([regex]'("code"\s*:\s*)d+').Replace($text, ('${1}' + $code), 1)
        [System.IO.File]::WriteAllText($metadataPath, $text)
        Write-Ok ('version.base = ' + $Version + '，version.code = ' + $code)
    }

    Write-Step ('开始构建（' + $Configuration + ' / ' + $Architecture + ' / 单文件，首次构建需要几分钟）')
    if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }
    New-Item -ItemType Directory -Path $Output -Force | Out-Null

    $publishArgs = @(
        'publish', $projectPath,
        '-c', $Configuration,
        ('-p:Platform=' + $Architecture),
        '-p:DeleteExistingFiles=true',
        '-o', $Output,
        '--no-self-contained'
    )
    Write-Host ('    dotnet ' + ($publishArgs -join ' ')) -ForegroundColor DarkGray
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw ('构建失败，dotnet 退出码：' + $LASTEXITCODE) }
}
catch {
    $failure = $_
}
finally {
    if ($null -ne $metadataBackup) {
        [System.IO.File]::WriteAllBytes($metadataPath, $metadataBackup)
        Write-Ok 'metadata.json 已还原'
    }
}

if ($null -ne $failure) {
    Write-Host ''
    Write-Host ('构建失败：' + $failure.Exception.Message) -ForegroundColor Red
    Write-Host '元数据已还原，可以修正问题后重新运行。' -ForegroundColor DarkGray
    exit 1
}

$exe = Get-ChildItem -Path $Output -Filter '*.exe' | Select-Object -First 1
if (-not $exe) {
    Write-Host '构建结束了，但输出目录里没有 exe，请检查上面的日志。' -ForegroundColor Red
    exit 1
}
$hash = (Get-FileHash $exe.FullName -Algorithm SHA256).Hash.ToLower()

Write-Step '构建完成'
Write-Ok ('文件   ' + $exe.FullName)
Write-Ok ('大小   ' + [math]::Round($exe.Length / 1MB, 2) + ' MB')
Write-Ok ('SHA256 ' + $hash)
Write-Host ''
Write-Host '注意：内嵌浏览器的用户数据在 %LOCALAPPDATA%\PCLIn\WebView2，删掉它等于清空聊天页的登录状态。' -ForegroundColor DarkGray