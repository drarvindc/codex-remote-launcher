@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "DIAGNOSTIC=%SCRIPT_DIR%issue-diagnostics.ps1"
set "REPORT=%SCRIPT_DIR%codex-remote-diagnostics.txt"

if not exist "%DIAGNOSTIC%" (
    echo The diagnostic script is missing:
    echo %DIAGNOSTIC%
    echo.
    echo Please copy this message into the GitHub issue.
    echo.
    pause
    endlocal
    exit /b 1
)

echo Starting Codex Remote Launcher diagnostic...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%DIAGNOSTIC%" -NoPause
set "EXIT_CODE=%ERRORLEVEL%"
echo.

if "%EXIT_CODE%"=="0" (
    echo Diagnostic completed.
    echo.
    echo Report:
    echo %REPORT%
    echo.
    echo Please open that file and paste its contents into the GitHub issue.
) else (
    echo The diagnostic could not run.
    echo.
    echo Possible reason:
    echo Windows PowerShell execution may be restricted by security policy.
    echo.
    echo Please copy the error shown above into the GitHub issue.
)

echo.
pause
endlocal
exit /b %EXIT_CODE%
