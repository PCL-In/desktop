<#
.SYNOPSIS
    一键构建 PCL-In 单文件 exe，用于本地测试。

.DESCRIPTION
    与 GitHub Actions 发布流程使用同一套参数：Release + 单文件 + --no-self-contained。

    默认会把 metadata.json 里的自报版本临时改成 1.0.3，构建完再原样还原。
    这样做是为了避免刚构建出来的测试版被启动器提示更新到线上版本，点一下就把你的测试版覆盖掉了。
    要保留仓库里的原始版本号，加 -KeepVersion。

.PARAMETER Architecture
    x64（默认）或 ARM64。

.PARAMETER Configuration
    默认 Release。

.PARAMETER Version
    测试版自报版本号，默认 1.0.3。必须大于当前线上最新版本，否则启动器会提示更新。

.PARAMETER KeepVersion
    不修改 metadata.json，直接使用仓库里的版本号。

.PARAMETER Output
    输出目录，默认为 build\artifact。

.EXAMPLE
    .\build.cmd
    .\Build.ps1 -Architecture ARM64
    .\Build.ps1 -KeepVersion
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Architecture = 'x64',

    [ValidateSet('Debug', 'Release', 'Beta', 'CI')]
    [string]$Configuration = 'Release',

    [string]$Version = '1.0.3',

    [switch]$KeepVersion,

    [string]$Output,

    # 由 build.cmd 转发，只用于控制构建结束后是否 pause，脚本本身不使用
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

function Write-Step([string]$Text) { Write-Host ''; Write-Host ('==> ' + $Text) -ForegroundColor Cyan }
function Write-Ok([string]$Text) { Write-Host ('    ' + $Text) -ForegroundColor Green }
function Write-Warn([string]$Text) { Write-Host ('    ' + $Text) -ForegroundColor Yellow }

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'Plain Craft Launcher 2\Plain Craft Launcher 2.csproj'
$metadataPath = Join-Path $repoRoot 'Plain Craft Launcher 2\metadata.json'
if (-not $Output) { $Output = Join-Path $repoRoot 'build\artifact' }

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

$metadataBackup = $null
$failure = $null
try {
    if ($KeepVersion) {
        Write-Step '使用仓库里的原始版本号'
    }
    else {
        Write-Step ('临时把自报版本改为 ' + $Version + '（构建完自动还原）')
        $parts = $Version.Split('.')
        if ($parts.Count -lt 3) { throw ('版本号格式不正确：' + $Version) }
        $code = [int]$parts[0] * 1000000 + [int]$parts[1] * 10000 + [int]$parts[2] * 100
        $metadataBackup = [System.IO.File]::ReadAllBytes($metadataPath)
        $text = [System.IO.File]::ReadAllText($metadataPath)
        $text = ([regex]'("base"\s*:\s*")[^"]*(")').Replace($text, ('${1}' + $Version + '${2}'), 1)
        $text = ([regex]'("code"\s*:\s*)\d+').Replace($text, ('${1}' + $code), 1)
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
Write-Host '直接双击这个 exe 就能测试：顶部导航第五个图标（聊天气泡）就是聊天室。' -ForegroundColor Cyan
Write-Host '注意：内嵌浏览器的用户数据在 %LOCALAPPDATA%\PCLIn\WebView2，删掉它等于清空聊天页的登录状态。' -ForegroundColor DarkGray