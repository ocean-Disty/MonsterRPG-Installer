@echo off
REM ===========================================================================
REM  Builds and runs the safety checks.
REM
REM  These are the rules that decide what the installer and the uninstaller are
REM  allowed to delete. Run this after changing anything in Common.c.
REM ===========================================================================

setlocal EnableExtensions
set "HERE=%~dp0"
set "OUT=%HERE%..\build"

where gcc >nul 2>&1
if errorlevel 1 (
    echo  ERROR: gcc was not found. See Installer\build.bat for how to get it.
    pause
    exit /b 1
)

if not exist "%OUT%" mkdir "%OUT%"

echo Building the tests ...
gcc -m32 -municode -O1 -Wall -Wextra -o "%OUT%\SafetyTests.exe" ^
    "%HERE%SafetyTests.c" "%HERE%..\src\Common.c" ^
    -lole32 -loleaut32 -luuid -lshell32 -ladvapi32 -static-libgcc
if errorlevel 1 (
    echo  BUILD FAILED
    pause
    exit /b 1
)

echo.
"%OUT%\SafetyTests.exe"
set "RC=%ERRORLEVEL%"

echo.
if "%RC%"=="0" (
    echo  Safe to ship.
) else (
    echo  DO NOT SHIP THIS BUILD - a safety check failed above.
)

endlocal & exit /b %RC%
