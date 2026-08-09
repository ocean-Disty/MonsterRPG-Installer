@echo off
REM ===========================================================================
REM  Build the MonsterRPG installer.
REM
REM  You only need this if you are changing the installer. A finished
REM  "MonsterRPG Setup.exe" already sits one folder up, next to the folders it
REM  installs, and that is the whole thing people download.
REM
REM  Requires MinGW-w64 with 32-bit support - the same toolchain the mods
REM  themselves are built with:
REM      1. install MSYS2 from https://www.msys2.org
REM      2. open "MSYS2 MINGW32" and run:
REM             pacman -S mingw-w64-i686-gcc mingw-w64-i686-zlib
REM      3. run this file from that shell, or add C:\msys64\mingw32\bin to your
REM         PATH and run it normally.
REM
REM  THE ORDER BELOW MATTERS.
REM
REM  Setup carries MonsterRPG.exe and the uninstaller inside itself, so those
REM  two have to exist before Setup is compiled. That is why they are built
REM  into build\ first and Setup.rc points at them.
REM
REM  32-bit is deliberate. A 32-bit program runs on both 32- and 64-bit
REM  Windows, and Blockland is 32-bit anyway, so there is no machine this can
REM  run on that could not also run the game.
REM ===========================================================================

setlocal EnableExtensions
set "HERE=%~dp0"
set "SRC=%HERE%src"
set "OUT=%HERE%build"
set "DIST=%HERE%.."

where gcc >nul 2>&1
if errorlevel 1 (
    echo.
    echo  ERROR: gcc was not found.
    echo.
    echo  Install MSYS2 ^(https://www.msys2.org^), then in the MSYS2 MINGW32 shell:
    echo      pacman -S mingw-w64-i686-gcc mingw-w64-i686-zlib
    echo  Then either run this from that shell, or add C:\msys64\mingw32\bin
    echo  to your PATH.
    echo.
    pause
    exit /b 1
)

if not exist "%OUT%" mkdir "%OUT%"

REM ---------------------------------------------------------------------------
REM  The icon. Windows will not take a .png, so the picture is repacked into a
REM  .ico holding it at nine sizes. Only rebuilt when it is missing - delete
REM  src\MonsterRPG.ico to force it.
REM ---------------------------------------------------------------------------
if not exist "%SRC%\MonsterRPG.ico" (
    echo Building the icon from MonsterRPGIcon.png ...
    powershell -NoProfile -ExecutionPolicy Bypass -File "%HERE%tools\make-icon.ps1" ^
        "%DIST%\MonsterRPGIcon.png" "%SRC%\MonsterRPG.ico"
    if errorlevel 1 goto failed
)

set "CFLAGS=-m32 -municode -O2 -s -Wall -Wextra -mwindows -static-libgcc"
set "LIBS=-lcomctl32 -lole32 -loleaut32 -luuid -lshell32 -lshlwapi -ladvapi32 -lgdi32 -luser32"

REM ---------------------------------------------------------------------------
REM  1. The launcher - what the player double-clicks to start the game.
REM
REM     Built without a space in the name so the resource line in Setup.rc can
REM     point at it plainly. Setup writes it into the game folder under its
REM     real name, "Blockland MonsterRPG.exe" - LAUNCHER_NAME in Common.h.
REM ---------------------------------------------------------------------------
echo Building MonsterRPG.exe ...
windres -I "%SRC%" "%SRC%\Launcher.rc" -O coff -o "%OUT%\Launcher.res"
if errorlevel 1 goto failed
gcc %CFLAGS% -o "%OUT%\MonsterRPG.exe" ^
    "%SRC%\Launcher.c" "%SRC%\Common.c" "%OUT%\Launcher.res" %LIBS%
if errorlevel 1 goto failed

REM ---------------------------------------------------------------------------
REM  2. The uninstaller. Setup writes it into the game folder under its real
REM     name, "Blockland MonsterRPG Uninstaller.exe"; here it is plain so
REM     the resource line in Setup.rc stays simple.
REM ---------------------------------------------------------------------------
echo Building the uninstaller ...
windres -I "%SRC%" "%SRC%\Uninstall.rc" -O coff -o "%OUT%\Uninstall.res"
if errorlevel 1 goto failed
gcc %CFLAGS% -o "%OUT%\Uninstaller.exe" ^
    "%SRC%\Uninstall.c" "%SRC%\Common.c" "%OUT%\Uninstall.res" %LIBS%
if errorlevel 1 goto failed

REM ---------------------------------------------------------------------------
REM  3. Setup, with those two inside it. It is written straight into the folder
REM     above, because that is where it has to live: beside README.txt and the
REM     folders it copies.
REM
REM     "build.bat standalone" builds a second, much bigger Setup that also has
REM     the mod folders zipped up inside it, so the download is one file. That
REM     one needs build\payload.zip to exist already; make-release.ps1 packs it
REM     and then calls this. The plain build is the one that goes in the
REM     repository, which is why the .exe there is half a megabyte and not
REM     forty.
REM ---------------------------------------------------------------------------
if /I "%~1"=="standalone" goto standalone

echo Building MonsterRPG Setup.exe ...
windres -I "%SRC%" "%SRC%\Setup.rc" -O coff -o "%OUT%\Setup.res"
if errorlevel 1 goto failed
gcc %CFLAGS% -o "%DIST%\MonsterRPG Setup.exe" ^
    "%SRC%\Setup.c" "%SRC%\Common.c" "%SRC%\Zip.c" "%SRC%\Unzip.c" ^
    "%OUT%\Setup.res" %LIBS% -lz
if errorlevel 1 goto failed

echo.
echo  BUILD SUCCESSFUL
echo    %DIST%\MonsterRPG Setup.exe
echo.
echo  That one file plus the folders beside it is the whole download.
echo.
endlocal
exit /b 0

:standalone
if not exist "%OUT%\payload.zip" (
    echo.
    echo  ERROR: %OUT%\payload.zip is missing.
    echo.
    echo  The standalone build packs the mod folders inside the .exe, so that
    echo  zip has to be made first. Run Installer\tools\make-release.ps1, which
    echo  packs it and then calls this.
    echo.
    pause
    exit /b 1
)

echo Building the standalone MonsterRPG Setup.exe ...
windres -I "%SRC%" -DWITH_PAYLOAD "%SRC%\Setup.rc" -O coff -o "%OUT%\SetupFull.res"
if errorlevel 1 goto failed
gcc %CFLAGS% -o "%OUT%\MonsterRPG Setup (standalone).exe" ^
    "%SRC%\Setup.c" "%SRC%\Common.c" "%SRC%\Zip.c" "%SRC%\Unzip.c" ^
    "%OUT%\SetupFull.res" %LIBS% -lz
if errorlevel 1 goto failed

echo.
echo  BUILD SUCCESSFUL
echo    %OUT%\MonsterRPG Setup (standalone).exe
echo.
echo  That single file is the whole download. Nothing needs to sit beside it.
echo.
endlocal
exit /b 0

:failed
echo.
echo  BUILD FAILED - see the messages above.
echo  The usual cause is a 64-bit-only gcc; 32-bit support ^(-m32^) is required.
echo.
pause
endlocal
exit /b 1
