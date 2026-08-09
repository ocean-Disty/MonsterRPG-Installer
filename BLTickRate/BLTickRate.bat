@echo off
REM ===========================================================================
REM  BLTickRate - start Blockland with a faster engine tick rate
REM
REM  Just double-click this file.
REM
REM  It looks for Blockland.exe in the folder above this one, then in this one.
REM  If your copy lives somewhere else, set BLOCKLAND_DIR below.
REM
REM  Anything you type after the file name is passed to the game, e.g.
REM      BLTickRate.bat -dedicated -port 28000
REM ===========================================================================

setlocal EnableExtensions

REM ===========================================================================
REM  IF BLTickRate CANNOT FIND YOUR GAME, PUT THE PATH ON THE LINE BELOW.
REM
REM  Everything after the = and inside the quotes, no trailing backslash, e.g.
REM      set "BLOCKLAND_DIR=C:\Program Files (x86)\Steam\steamapps\common\Blockland"
REM      set "BLOCKLAND_DIR=D:\Games\Blockland"
REM
REM  Not sure where it is? Right-click your Blockland shortcut -> Properties,
REM  and copy the "Start in" box.
REM ===========================================================================
set "BLOCKLAND_DIR="

REM ===========================================================================
REM  OTHER MODS IN THE SAME BLOCKLAND FOLDER ARE STARTED TOO.
REM
REM  If MonsterRPGAudio is installed beside this, it is loaded as well - and the
REM  same is true the other way round, so it does not matter which .bat you
REM  double-click. Both give you the same game with everything you installed
REM  running. Nothing to configure.
REM
REM  PAIRING=0 starts ONLY BLTickRate.
REM ===========================================================================
set "PAIRING=1"

set "HERE=%~dp0"
set "HEREDIR=%HERE:~0,-1%"

REM A trailing backslash IS a problem, but not for path lookup - Windows is
REM happy with "...\Blockland\\Blockland.exe". It breaks ARGUMENT QUOTING: the
REM backslash escapes the closing quote, so "C:\Games\Blockland\" passed as an
REM argument swallows everything after it and the launch fails with error 267.
REM It is normalised at :have_dir with for %%~f, which also handles relative
REM paths. Substring expansion is deliberately not used - cmd mis-parses it
REM when the variable is empty and kills the whole script.

if not "%BLOCKLAND_DIR%"=="" (
    if exist "%BLOCKLAND_DIR%\Blockland.exe" goto have_dir
    echo.
    echo  BLOCKLAND_DIR is set in BLTickRate.bat, but there is no Blockland.exe here:
    echo      %BLOCKLAND_DIR%
    echo.
    echo  Check the path is the FOLDER that contains Blockland.exe, not the .exe
    echo  itself and not a shortcut.
    echo.
    pause
    exit /b 1
)

REM Look one folder up first (the normal layout: Blockland\BLTickRate\)
if exist "%HEREDIR%\..\Blockland.exe" (
    for %%I in ("%HEREDIR%\..") do set "BLOCKLAND_DIR=%%~fI"
    goto have_dir
)
REM Then this folder, in case someone dropped it straight into Blockland\
if exist "%HEREDIR%\Blockland.exe" (
    set "BLOCKLAND_DIR=%HEREDIR%"
    goto have_dir
)

echo.
echo  ==========================================================
echo   Could not find Blockland.exe.
echo  ==========================================================
echo.
echo  I looked in:
echo      %HEREDIR%\..
echo      %HEREDIR%
echo.
echo  FIX 1 (easiest) - move this whole BLTickRate folder into your
echo  Blockland folder, so it looks like this:
echo.
echo      Blockland\
echo          Blockland.exe
echo          BLTickRate\
echo              BLTickRate.bat     ^<- this file
echo.
echo  FIX 2 - tell it where the game is:
echo      1. right-click BLTickRate.bat  -^>  Edit  (or Open with Notepad)
echo      2. find the line that reads:  set "BLOCKLAND_DIR="
echo      3. put your Blockland FOLDER inside the quotes, for example:
echo         set "BLOCKLAND_DIR=D:\Games\Blockland"
echo      4. save, then run BLTickRate.bat again
echo.
echo  Don't know where Blockland is? Right-click your Blockland
echo  shortcut, choose Properties, and copy the "Start in" box.
echo.
pause
exit /b 1

:have_dir
REM Normalise: drops any trailing backslash and resolves relative paths, so the
REM quoted argument passed to the launcher can never be mangled.
for %%I in ("%BLOCKLAND_DIR%\.") do set "BLOCKLAND_DIR=%%~fI"

set "EXE=%BLOCKLAND_DIR%\Blockland.exe"
set "DLL=%HEREDIR%\bin\BLTickRate.dll"
set "INJ=%HEREDIR%\bin\BLTickRateLaunch.exe"

if not exist "%DLL%" (
    echo  Missing: %DLL%
    echo  Run build.bat, or re-download the release with the bin folder included.
    pause
    exit /b 1
)
if not exist "%INJ%" (
    echo  Missing: %INJ%
    echo  Run build.bat, or re-download the release with the bin folder included.
    pause
    exit /b 1
)

REM -client is NOT optional. Started with no arguments at all, Blockland.exe
REM exits again within a few seconds - it patches fine, the log even says
REM "applied 25/25", and then the process is simply gone. That looks exactly
REM like "the mod broke my game" and is very hard to tell apart from a crash,
REM so the normal no-argument case explicitly asks for the client.
REM AUTO tells the launcher to find every participating mod folder beside
REM Blockland.exe and load them all, in the right order (this one first - it
REM rewrites constants the engine reads once during startup). Discovery lives in
REM the launcher rather than here because cmd is bad at directory scanning and
REM worse at sorting; see src\Pairing.h.
set "DLLS=AUTO"
if "%PAIRING%"=="0" set "DLLS=%DLL%"

if "%~1"=="" (
    "%INJ%" "%EXE%" "%DLLS%" "%BLOCKLAND_DIR%" -client
) else (
    "%INJ%" "%EXE%" "%DLLS%" "%BLOCKLAND_DIR%" %*
)

echo.
echo  If the game did not speed up, open BLTickRate.log - it says exactly
echo  what happened and why.
echo.
endlocal
