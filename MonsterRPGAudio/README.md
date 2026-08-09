# MonsterRPGAudio

Ray-traced game audio for MonsterRPG, played natively. No browser, no website,
no second window.

Sound in MonsterRPG is traced against the actual geometry of the world on the
server: what is between you and a noise, how big the room is, what the walls are
made of. Until now the only way to hear that was to keep a browser tab open.
This plays it inside the game.

**Nothing is installed and nothing on your disk is modified.** The DLL is loaded
into the game's memory as it starts and disappears when you close it. Launch
Blockland the way you always did and you get a completely normal Blockland.

## Install

1. Put the whole `MonsterRPGAudio` folder inside your Blockland folder, so it
   looks like this:

   ```
   Blockland\
       Blockland.exe
       MonsterRPGAudio\
           MonsterRPGAudio.bat      <- you run this
           MonsterRPGAudio.cfg
           bin\
           src\
   ```

2. Double-click `MonsterRPGAudio.bat`.

To go back to normal, just launch Blockland the way you always did. Nothing to
uninstall.

## Do I need a special graphics card?

**No.** This comes up a lot, so plainly: the ray tracing happens on the
**server's** graphics card, and the result is sent to you as numbers. A
ten-year-old GPU hears exactly what a brand new one hears.

The log does check your card and prints a verdict, and that check is about
something else entirely: whether the tracing could one day be done on *your*
machine instead of the server's, to take load off the server. That needs
hardware ray tracing (NVIDIA RTX 3000 or newer, AMD RX 6000 or newer) and **it
is not built yet**. A line in the log saying `NO_RT` or `TOO_OLD` does not mean
you are missing anything.

## What works right now

This is an early build. It loads, identifies your machine, and connects itself
to the game's console, and then deliberately stops.

| | |
|---|---|
| Loads and stays out of the way | yes |
| Reports your GPU in the log | yes |
| Actually plays audio | **not yet**, that is the next phase |

So today it changes nothing you can hear. You will still get sound the normal
way, or through the website if you use it. Installing now costs nothing and
means you are ready when the audio half lands.

## Settings

Open `MonsterRPGAudio.cfg` in Notepad. Every setting is commented in the file
itself. Save and restart the game.

## Playing with other people

Unlike BLTickRate, this is **entirely yours**. It does not change how the game
simulates and it does not need anyone else to have it. Other players are not
affected by whether you run it, and you can join any server with it running.
on a server that is not MonsterRPG it opens no socket and does nothing at all.

## Running BLTickRate too, it just works

If BLTickRate is installed in the same Blockland folder, both are started
together. **It does not matter which `.bat` you double-click**. Running
`BLTickRate.bat` and running `MonsterRPGAudio.bat` give you exactly the same
game, with everything you installed loaded. There is nothing to configure and
nothing to remember.

BLTickRate goes first, because it rewrites engine constants that are read once
during startup. That ordering is fixed, not alphabetical.

**One reason you might not want this.** BLTickRate changes how fast the world
simulates, and everyone on the server has to be running it at the same setting.
a normal client on a BLTickRate server moves at half speed and the view jitters.
If you keep it installed for your own server and want to visit somebody else's,
open `MonsterRPGAudio.bat` in Notepad and set `PAIRING=0` for that trip. That
starts MonsterRPGAudio on its own, which is always safe on any server.

### Adding a third mod

Any other mod that loads a DLL can join in without anyone editing anything.
Drop a `bl_inject.cfg` in its folder:

```
dll=bin\WhateverItIsCalled.dll
order=30
name=WhateverItIsCalled
```

Lower `order` is injected earlier. Folders without that file are ignored
entirely. This never goes looking for DLLs to load on its own initiative.

## If something looks wrong

Open `MonsterRPGAudio.log`. It is rewritten every launch and says exactly what
happened, in order, with timings.

`state: READY` means working correctly.

`state: INERT, could not bind to the console`. This is the safe outcome, not a
crash. It almost always means Blockland has been updated: the DLL finds the
game's console by searching for known byte patterns, and a game update moves
them. Nothing is written, the game runs completely unmodified, and you need a
version of MonsterRPGAudio updated for the new build.

`gpu: verdict NO_RT`, see the section above. Not a problem.

**The game did not start at all**: check that `bin\MonsterRPGAudio.dll` and
`bin\MonsterRPGAudioLaunch.exe` are both present, and that your antivirus has
not quarantined them. The launcher starts the game paused and loads a DLL into
it, which is a normal thing for a game mod to do but does look unusual to some
antivirus software.

## Building it yourself

You do not need to. `bin\` already has a working build. But the source is all
here.

1. Install [MSYS2](https://www.msys2.org)
2. Open the MSYS2 MINGW32 shell and run: `pacman -S mingw-w64-i686-gcc`
3. Run `build.bat` from that shell (or add `C:\msys64\mingw32\bin` to your
   PATH and run it normally)

The DLL must be 32-bit. Blockland is a 32-bit program and cannot load a 64-bit
DLL. That is what `-m32` in `build.bat` is for.

Source files:

- `src/MonsterRPGAudio.cpp`: entry point, and the awkward business of finding a
  moment when it is safe to talk to the game's console.
- `src/GpuProbe.cpp`: the graphics card check, and a long comment about why it
  matters less than its name suggests.
- `src/Cfg.cpp`, `src/Log.cpp`: settings and the log.
- `src/Launch.cpp`: starts Blockland paused, loads the DLLs, lets it run.
- `src/vendor/`: RedoBlHooks and BlFuncs, the standard Blockland DLL binding
  layer. A copy, so this folder builds without the server code present.

## How it works, briefly

The launcher starts `Blockland.exe` suspended, writes the DLL path into it,
runs `LoadLibrary` there, and only then lets the game run. That is the same
timing as editing the executable's import table, except nothing on disk is
touched, so "uninstalling" is just not using this launcher.

The DLL then waits for the game window to appear, which is how it knows the
engine has finished starting, and asks to be called once from the game's own
message loop so it can register its console functions on the right thread.

## Design

`Add-Ons/MONSTERRPG/AUDIORT_NATIVE_PLAN.md` is the full design and the phase
list, including everything not built yet and why it is ordered the way it is.
