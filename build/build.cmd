@echo off
chcp 65001 >nul
setlocal
set "PS1=%~dp0Build.ps1"
set "NOPAUSE="

:parse
if "%~1"=="" goto parsed
if /i "%~1"=="-NoPause" set "NOPAUSE=1"
shift
goto parse
:parsed

where pwsh >nul 2>nul
if errorlevel 1 (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%" %*
) else (
    pwsh -NoProfile -ExecutionPolicy Bypass -File "%PS1%" %*
)

set "RC=%ERRORLEVEL%"
echo.
if not "%RC%"=="0" (
    echo [FAILED] build failed, exit code %RC%
) else (
    echo [OK] build finished
)

if not defined NOPAUSE pause
exit /b %RC%
