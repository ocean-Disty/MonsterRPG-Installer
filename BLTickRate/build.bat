@echo off
REM ===========================================================================
REM  Build BLTickRate from source.
REM
REM  You only need this if you want to change the code. A ready-made build is
REM  already in bin\.
REM
REM  Requires MinGW-w64 with 32-bit support. The easiest way to get it:
REM      1. install MSYS2 from https://www.msys2.org
REM      2. open "MSYS2 MINGW32" and run:
REM             pacman -S mingw-w64-i686-gcc
REM      3. run this file from that same MINGW32 shell, or add
REM         C:\msys64\mingw32\bin to your PATH and run it normally.
REM
REM  The DLL MUST be 32-bit. Blockland is a 32-bit program and will not load a
REM  64-bit DLL - that is what -m32 below is for.
REM ===========================================================================

setlocal EnableExtensions
set "HERE=%~dp0"

where g++ >nul 2>&1
if errorlevel 1 (
    echo.
    echo  ERROR: g++ was not found.
    echo.
    echo  Install MSYS2 ^(https://www.msys2.org^), then in the MSYS2 MINGW32 shell:
    echo      pacman -S mingw-w64-i686-gcc
    echo  Then either run this from that shell, or add C:\msys64\mingw32\bin
    echo  to your PATH.
    echo.
    pause
    exit /b 1
)

if not exist "%HERE%bin" mkdir "%HERE%bin"

echo Building BLTickRate.dll ...
g++ -m32 -shared -O2 -std=c++17 ^
    -o "%HERE%bin\BLTickRate.dll" "%HERE%src\BLTickRate.cpp" ^
    -static-libgcc -static-libstdc++ -lpsapi
if errorlevel 1 goto failed

echo Building BLTickRateLaunch.exe ...
g++ -m32 -O2 -std=c++17 ^
    -o "%HERE%bin\BLTickRateLaunch.exe" "%HERE%src\BLTickRateLaunch.cpp"
if errorlevel 1 goto failed

echo.
echo  BUILD SUCCESSFUL
echo    bin\BLTickRate.dll
echo    bin\BLTickRateLaunch.exe
echo.
echo  Now just run BLTickRate.bat
echo.
endlocal
exit /b 0

:failed
echo.
echo  BUILD FAILED - see the messages above.
echo  The most common cause is a 64-bit-only g++; you need 32-bit support ^(-m32^).
echo.
pause
endlocal
exit /b 1
