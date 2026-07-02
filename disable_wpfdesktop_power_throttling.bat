@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "PS1=%SCRIPT_DIR%tools\disable_wpfdesktop_power_throttling.ps1"

if not exist "%PS1%" (
    echo Missing script: %PS1%
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS1%" -ProjectRoot "%SCRIPT_DIR%"
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if "%EXIT_CODE%"=="0" (
    echo Power throttling exemption command finished.
) else (
    echo Power throttling exemption command failed with exit code %EXIT_CODE%.
)
pause
exit /b %EXIT_CODE%
