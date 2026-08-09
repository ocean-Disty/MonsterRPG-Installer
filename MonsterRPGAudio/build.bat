@echo off
REM ===========================================================================
REM  Build MonsterRPGAudio from source.
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

REM ===========================================================================
REM  src\vendor\ is a COPY of RedoBlHooks / BlFuncs from Add-Ons\MONSTERRPG.
REM
REM  Copied rather than referenced because this folder is given to players and
REM  must build on a machine that has no server code on it at all. The check
REM  below only runs on a machine that DOES have the server tree - i.e. a
REM  developer's - and exists so the copy cannot drift silently.
REM ===========================================================================
set "UPSTREAM=%HERE%..\Add-Ons\MONSTERRPG"
if exist "%UPSTREAM%\BlFuncs.cpp" (
    for %%F in (BlHooks.hpp BlHooks.cpp BlFuncs.hpp BlFuncs.cpp) do (
        fc /b "%UPSTREAM%\%%F" "%HERE%src\vendor\%%F" >nul 2>&1
        if errorlevel 1 echo  WARNING: src\vendor\%%F differs from Add-Ons\MONSTERRPG\%%F
    )
)

echo Building MonsterRPGAudio.dll ...
g++ -m32 -shared -O2 -std=c++17 -Wall ^
    -o "%HERE%bin\MonsterRPGAudio.dll" ^
    "%HERE%src\MonsterRPGAudio.cpp" ^
    "%HERE%src\Log.cpp" ^
    "%HERE%src\Cfg.cpp" ^
    "%HERE%src\GpuProbe.cpp" ^
    "%HERE%src\Net.cpp" ^
    "%HERE%src\Audio.cpp" ^
    "%HERE%src\Wav.cpp" ^
    "%HERE%src\vendor\BlHooks.cpp" ^
    "%HERE%src\vendor\BlFuncs.cpp" ^
    -static-libgcc -static-libstdc++ -lpsapi -lws2_32 -lole32 -loleaut32 -lavrt -luuid
if errorlevel 1 goto failed

echo Building MonsterRPGAudioLaunch.exe ...
g++ -m32 -O2 -std=c++17 -Wall ^
    -o "%HERE%bin\MonsterRPGAudioLaunch.exe" "%HERE%src\Launch.cpp"
if errorlevel 1 goto failed

REM ===========================================================================
REM  MonsterRPGAudioProbe.exe is 64-BIT, and that is not a preference.
REM
REM  AMD's 32-bit Vulkan driver exposes NO ray-tracing extensions at all -
REM  measured on an RX 7900 XTX: 211 extensions from a 32-bit process with zero
REM  ray tracing among them, 220 from a 64-bit process with ray_query and
REM  acceleration_structure present. Blockland is 32-bit, so the capability
REM  question cannot be asked from inside it. See src\Probe64.cpp.
REM
REM  A 32-bit-only toolchain is NOT a build failure. The DLL treats a missing
REM  helper as "unknown", which is the honest answer, and nothing a player can
REM  hear depends on it.
REM ===========================================================================
set "GXX64="
where x86_64-w64-mingw32-g++ >nul 2>&1 && set "GXX64=x86_64-w64-mingw32-g++"
if not defined GXX64 if exist "C:\msys64\mingw64\bin\g++.exe" (
    REM  Its own bin MUST go on PATH, not just be named by full path: g++ there
    REM  links against DLLs in that same folder and will not even start without
    REM  it. The symptom is a compile that "fails" with no error message at all.
    set "PATH=C:\msys64\mingw64\bin;%PATH%"
    set "GXX64=C:\msys64\mingw64\bin\g++.exe"
)

if defined GXX64 (
    echo Building MonsterRPGAudioProbe.exe ^(64-bit^) ...
    REM  -static so the helper runs on a machine with no MSYS2 on it. It is a
    REM  player-facing binary; needing libgcc_s_seh-1.dll beside it would make
    REM  the GPU check fail on every computer but this one.
    "%GXX64%" -m64 -O2 -std=c++17 -Wall -static ^
        -o "%HERE%bin\MonsterRPGAudioProbe.exe" "%HERE%src\Probe64.cpp"
    if errorlevel 1 goto failed
) else (
    echo.
    echo  NOTE: no 64-bit g++ found, so bin\MonsterRPGAudioProbe.exe was not
    echo        rebuilt. Everything else is built and works. The graphics-card
    echo        check will report UNKNOWN unless a prebuilt helper is present.
    echo        To build it: pacman -S mingw-w64-x86_64-gcc
    echo.
)

echo.
echo  BUILD SUCCESSFUL
echo    bin\MonsterRPGAudio.dll
echo    bin\MonsterRPGAudioLaunch.exe
if exist "%HERE%bin\MonsterRPGAudioProbe.exe" echo    bin\MonsterRPGAudioProbe.exe
echo.
echo  Now just run MonsterRPGAudio.bat
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
