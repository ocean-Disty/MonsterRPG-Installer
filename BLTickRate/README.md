# BLTickRate: a faster tick rate for Blockland

Blockland simulates the world 31.25 times a second. BLTickRate makes it 62.5
or 125 times a second instead.

Movement speed, gravity, jump height and projectile timings all stay exactly
the same. What changes is smoothness: input is sampled more often, fast objects
are less likely to pass through things, and positions update more precisely.

Nothing is installed and nothing on your disk is modified. The changes are
made to the game's memory as it starts and disappear when you close it. If you
launch Blockland normally, you get a completely normal Blockland.

## Install

1. Put the whole `BLTickRate` folder inside your Blockland folder, so it
   looks like this:

   ```
   Blockland\
       Blockland.exe
       BLTickRate\
           BLTickRate.bat      <- you run this
           BLTickRate.cfg
           bin\
           src\
   ```

2. Double-click `BLTickRate.bat`.

That's it. Blockland starts with the faster tick rate.

To go back to normal, just launch Blockland the way you always did. Nothing to
uninstall.

## Changing the speed

Open `BLTickRate.cfg` in Notepad and change one number:

| TickShift | Tick rate | Notes |
|-----------|-----------|-------|
| `5` | 31.25 tps | Blockland's normal speed, changes nothing |
| `4` | 62.5 tps | Smoother than stock, modest cost |
| `3` | 125 tps | The default, and what MonsterRPG servers run. See below |

**For MonsterRPG, leave it at 3.** Both ends have to be on the same number: a
client on 4 against a server on 3 moves at half speed with a jittering view,
and nothing on screen explains why. If the .cfg is missing the DLL uses 3 for
the same reason.

Save the file and restart the game. No rebuilding, no reinstalling.

On 125 tps: the game is doing four times as much simulation work per
second. A strong machine handles it; a modest one will stutter, and the stutter
usually shows up on the client first. If it feels worse rather than better,
go back to `4`. Higher is not automatically better.

## Playing with other people

Everyone in the game needs BLTickRate, set to the same TickShift.

The server and the client have to agree about how long a tick is. If they
don't, the mismatch is very obvious:

- A normal client on a BLTickRate server: movement and gravity run at half
  speed, and the view jitters. The client can only send about 31 inputs a
  second, so the faster server has nothing to advance you with on the ticks in
  between.
- Different TickShift values on each side: the same problem, scaled by however
  far apart the two settings are.

So this is for servers where everyone is running it, such as your own server or
a group that has all installed it. Joining a stranger's vanilla server with
BLTickRate running is not a good idea.

## Other mods in the same folder

If MonsterRPGAudio is installed beside this one, both are started together, and
the same is true from its launcher. **It does not matter which `.bat` you
double-click**. Both give you the same game with everything you installed
running.

BLTickRate is always loaded first, because it rewrites constants the engine
reads once during startup.

To start only BLTickRate, open `BLTickRate.bat` in Notepad and set `PAIRING=0`.

Any other mod that loads a DLL can join in by dropping a `bl_inject.cfg` in its
own folder. See `bl_inject.cfg` here for the format. Folders without that file
are ignored; nothing goes looking for DLLs on its own initiative.

## One extra step for server hosts

Blockland also limits how many network updates it sends per second, and that
limit is enforced in the compiled game scripts as well as in the engine.
BLTickRate raises the engine's limit, but the scripts will still pull it back
down to 32 while the game loads.

After your server has finished loading, set both of these:

```
$Pref::Net::PacketRateToClient = 64;   // use 128 if TickShift is 3
$Pref::Net::PacketRateToServer = 64;
```

Clients should set the same values. Without this the simulation speeds up but
the network doesn't, and you lose most of the benefit.

## If something looks wrong

Open `BLTickRate.log`. It is written every launch and says exactly
what happened.

`applied 25/25 patches`: working correctly.

`ABORTED: ... nothing was written`: this is the safe outcome, not a crash.
It almost always means Blockland has been updated. The patch locations are
specific to one build of `Blockland.exe`, so a game update moves them. The DLL
checks every location before writing anything, and if even one has changed it
writes nothing at all and lets the game run completely unmodified. You will
need a version of BLTickRate updated for the new build.

The game did not start at all: check that `bin\BLTickRate.dll` and
`bin\BLTickRateLaunch.exe` are both present, and that your antivirus has not
quarantined them. The launcher starts the game paused and loads a DLL into it,
which is a normal thing for a game mod to do but does look unusual to some
antivirus software.

## Building it yourself

You do not need to. `bin\` already has a working build. But the source is all
here and it is short.

1. Install [MSYS2](https://www.msys2.org)
2. Open the MSYS2 MINGW32 shell and run: `pacman -S mingw-w64-i686-gcc`
3. Run `build.bat` from that shell (or add `C:\msys64\mingw32\bin` to your
   PATH and run it normally)

The DLL must be 32-bit. Blockland is a 32-bit program and cannot load a
64-bit DLL. That is what `-m32` in `build.bat` is for.

Source files:

- `src/BLTickRate.cpp`, the patcher. Every changed location is listed with an
  explanation of what it is and why it matters.
- `src/BLTickRateLaunch.cpp`, which starts Blockland paused, loads the DLL, then
  lets it run.

## How it works, briefly

Blockland's engine has its tick length compiled in as constants: `32`
milliseconds, `0.032` seconds, masks and shifts derived from them. BLTickRate
finds each of those in memory and rewrites it for the tick length you asked
for, before the engine has started running.

Every location is checked against the exact instruction bytes expected there
first. If any single one disagrees, none of them are written. A
half-patched engine would be far worse than an unpatched one, so that case is
treated as a hard stop.

The DLL is loaded before the engine initialises, because some of the constants
are read once during startup and never again.

## Credits

The idea and the original 2019 proof of concept are CompMix's
("BLTickRate", Blockland forums). That version targeted a much older
`Blockland.exe` and none of its addresses survive in the current build. All
forty had moved, so this is a fresh port with the locations re-derived, plus
several the original did not cover.

Notably, the original left the engine's double-precision copy of the tick
length untouched, which makes gravity and acceleration run at the wrong speed,
and left the render-side interpolation at the old rate, which makes
first-person weapons jitter. Both are handled here.
