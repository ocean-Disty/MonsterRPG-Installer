@echo off
REM ===========================================================================
REM  MonsterRPGAudio - start Blockland with ray-traced game audio
REM
REM  Just double-click this file.
REM
REM  It looks for Blockland.exe in the folder above this one, then in this one.
REM  If your copy lives somewhere else, set BLOCKLAND_DIR below.
REM
REM  Anything you type after the file name is passed to the game.
REM ===========================================================================

setlocal EnableExtensions

REM ===========================================================================
REM  IF IT CANNOT FIND YOUR GAME, PUT THE PATH ON THE LINE BELOW.
REM
REM  Everything after the = and inside the quotes, no trailing backslash, e.g.
REM      set "BLOCKLAND_DIR=C:\Program Files (x86)\Steam\steamapps\common\Blockland"
REM
REM  Not sure where it is? Right-click your Blockland shortcut -> Properties,
REM  and copy the "Start in" box.
REM ===========================================================================
set "BLOCKLAND_DIR="

REM ===========================================================================
REM  RUNNING BLTickRate AS WELL? IT JUST WORKS.
REM
REM  If BLTickRate is installed in the same Blockland folder, it is started too
REM  - and the same is true the other way round, so it does not matter which
REM  .bat you double-click. Both give you exactly the same game with everything
REM  you installed running. Nothing to configure.
REM
REM  Adding some OTHER mod that loads a DLL? It can join in without anyone
REM  editing this file: drop a bl_inject.cfg in its folder containing
REM      dll=bin\WhateverItIsCalled.dll
REM      order=30
REM
REM  PAIRING=0 turns all of that off and starts ONLY MonsterRPGAudio. The one
REM  reason you might want that: BLTickRate changes how fast the world
REM  simulates, and everyone on the server has to be running it at the same
REM  setting or you move at half speed and the view jitters. If you have it
REM  installed for your own server and want to visit somebody else's, set this
REM  to 0 for that trip.
REM ===========================================================================
set "PAIRING=1"

set "HERE=%~dp0"
set "HEREDIR=%HERE:~0,-1%"

REM A trailing backslash breaks ARGUMENT QUOTING, not path lookup: the backslash
REM escapes the closing quote, so "C:\Games\Blockland\" passed as an argument
REM swallows everything after it. Normalised at :have_dir with %%~f.

if not "%BLOCKLAND_DIR%"=="" (
    if exist "%BLOCKLAND_DIR%\Blockland.exe" goto have_dir
    echo.
    echo  BLOCKLAND_DIR is set in MonsterRPGAudio.bat, but there is no
    echo  Blockland.exe here:
    echo      %BLOCKLAND_DIR%
    echo.
    echo  Check the path is the FOLDER that contains Blockland.exe, not the .exe
    echo  itself and not a shortcut.
    echo.
    pause
    exit /b 1
)

REM Look one folder up first (the normal layout: Blockland\MonsterRPGAudio\)
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
echo  FIX 1 (easiest) - move this whole MonsterRPGAudio folder into your
echo  Blockland folder, so it looks like this:
echo.
echo      Blockland\
echo          Blockland.exe
echo          MonsterRPGAudio\
echo              MonsterRPGAudio.bat     ^<- this file
echo.
echo  FIX 2 - tell it where the game is:
echo      1. right-click MonsterRPGAudio.bat  -^>  Edit
echo      2. find the line that reads:  set "BLOCKLAND_DIR="
echo      3. put your Blockland FOLDER inside the quotes, for example:
echo         set "BLOCKLAND_DIR=D:\Games\Blockland"
echo      4. save, then run MonsterRPGAudio.bat again
echo.
pause
exit /b 1

:have_dir
REM Normalise: drops any trailing backslash and resolves relative paths, so the
REM quoted argument passed to the launcher can never be mangled.
for %%I in ("%BLOCKLAND_DIR%\.") do set "BLOCKLAND_DIR=%%~fI"

set "EXE=%BLOCKLAND_DIR%\Blockland.exe"
set "DLL=%HEREDIR%\bin\MonsterRPGAudio.dll"
set "INJ=%HEREDIR%\bin\MonsterRPGAudioLaunch.exe"

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

REM AUTO tells the launcher to find every participating mod folder beside
REM Blockland.exe and load them all, in the right order (BLTickRate first - it
REM rewrites constants the engine reads once during startup). The discovery is
REM done in the launcher rather than here because cmd is bad at directory
REM scanning and worse at sorting; see src\Pairing.h.
set "DLLS=AUTO"
if "%PAIRING%"=="0" set "DLLS=%DLL%"

REM -client is NOT optional. Started with no arguments at all, Blockland.exe
REM exits again within a few seconds - which looks exactly like "the mod broke
REM my game" and is very hard to tell apart from a crash. Learned next door in
REM BLTickRate.bat; do not "tidy" this away.
if "%~1"=="" (
    "%INJ%" "%EXE%" "%DLLS%" "%BLOCKLAND_DIR%" -client
) else (
    "%INJ%" "%EXE%" "%DLLS%" "%BLOCKLAND_DIR%" %*
)

echo.
echo  If something looks wrong, open MonsterRPGAudio.log in this folder - it
echo  says exactly what happened and why.
echo.
endlocal
