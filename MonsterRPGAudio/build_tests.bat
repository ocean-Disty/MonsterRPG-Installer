@echo off
REM ===========================================================================
REM  Offline checks that need neither Blockland nor a sound card.
REM
REM  These exist because "it compiles and I can hear something" is not evidence
REM  that a spatialiser is correct. A sign error in the head basis puts every
REM  sound behind you; an ITD of the wrong polarity swaps your ears; and both
REM  still sound perfectly plausible until you try to point at something.
REM
REM  Run this after ANY change to Hrtf.hpp. It has already earned its place: the
REM  first run found an uninitialised delay line that made the output run away
REM  to 6.4e33, which the game would have played as a burst of noise.
REM ===========================================================================

setlocal EnableExtensions
set "HERE=%~dp0"

where g++ >nul 2>&1
if errorlevel 1 (
    echo  ERROR: g++ was not found. See build.bat for how to install it.
    pause
    exit /b 1
)

if not exist "%HERE%bin" mkdir "%HERE%bin"

echo Building TestHrtf ...
g++ -m32 -O2 -std=c++17 -Wall -o "%HERE%bin\TestHrtf.exe" "%HERE%src\TestHrtf.cpp"
if errorlevel 1 goto failed

"%HERE%bin\TestHrtf.exe"
if errorlevel 1 goto failed

echo Building TestReverb ...
g++ -m32 -O2 -std=c++17 -Wall -o "%HERE%bin\TestReverb.exe" "%HERE%src\TestReverb.cpp"
if errorlevel 1 goto failed

"%HERE%bin\TestReverb.exe"
if errorlevel 1 goto failed

echo.
echo  ALL TESTS PASSED
echo.
endlocal
exit /b 0

:failed
echo.
echo  TESTS FAILED
echo.
pause
endlocal
exit /b 1
