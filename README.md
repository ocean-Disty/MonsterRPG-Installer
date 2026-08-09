# MonsterRPG for Blockland

An RPG mod for Blockland. Faster tick rate, plus optional ray traced audio with
3D voice chat.

All the code is in this repo, installer included, so you can read it before you
run it.

## Install it

**[Download the zip here.](../../releases/latest)** It's under "Assets" at the
bottom of the release.

1. Open your Downloads folder.
2. Right click the zip you just downloaded. Pick **Extract All**, then click
   **Extract**.
3. A folder opens up. Double click **MonsterRPG Setup.exe** inside it.
4. Windows might show a blue box saying "Windows protected your PC". Click
   **More info**, then **Run anyway**. It says that because the file isn't
   signed, which costs money.
5. Read the first screen, then click **Continue**.
6. Setup usually finds Blockland by itself. If the text under the box is green,
   click **Install**. If it's red, click **Choose folder...** and pick the
   folder that has `Blockland.exe` and `Add-Ons` sitting in it.
7. When it's done, click **Close**.

To play, open your Blockland folder and double click
**Blockland MonsterRPG.exe**. There's a MonsterRPG shortcut in your Start menu
as well.

## Uninstall it

Open your Blockland folder and double click **Uninstall MonsterRPG.exe**. Click
**Remove**, then **Yes**.

It's also listed in Settings > Apps > Installed apps if you'd rather do it from
there.

## If something goes wrong

**Setup can't find Blockland.** Click "Choose folder..." and browse to the
folder with `Blockland.exe` in it. Usually that's Documents\Blockland.

**Your antivirus complains.** Read the next section. Uninstalling takes it all
back off.

**Double clicking Setup does nothing.** It's already open somewhere. Check your
taskbar.

**The game won't start after installing.** Open `BLTickRate.log` in your
Blockland folder. It says what happened.

## What it does to your PC

- Copies folders into your Blockland folder and puts two programs next to
  `Blockland.exe`. It doesn't change or delete anything that was already there.
- When you launch through MonsterRPG, it attaches its DLL files to
  `Blockland.exe` and runs that code in the game's memory. That's normal for
  Blockland mods, but antivirus software sometimes flags it.
- Nothing keeps running once you close the game. Launch Blockland the way you
  always did and you get stock Blockland.
- Adds a Start menu shortcut and a Windows uninstall entry. It never asks for
  admin.
- **MonsterRPG Audio is unticked by default.** It's the only part that opens a
  network port and listens on it, since that's how audio comes in and your mic
  goes out. Tick it if you want sound and voice chat.

Setup shows all this on its first screen, before it asks you anything.

## For developers

```
Installer/           setup, uninstaller, launcher (C, Win32)
BLTickRate/          tick rate mod (C++, injected DLL)
MonsterRPGAudio/     audio and voice chat (C++, injected DLL)
Client_MonsterRPG/   the UI, a normal add-on (TorqueScript)
README.txt           tells Setup where each folder goes
```

Built DLLs are in each `bin/`, so you don't have to compile anything to use it.

Building needs MinGW-w64 with 32-bit support, since Blockland is 32-bit:

```
pacman -S mingw-w64-i686-gcc mingw-w64-i686-zlib mingw-w64-x86_64-gcc
```

```
Installer\build.bat               MonsterRPG Setup.exe
BLTickRate\build.bat              BLTickRate.dll
MonsterRPGAudio\build.bat         MonsterRPGAudio.dll
Installer\tests\run-tests.bat     path safety checks, run these if you touch Common.c
Installer\tools\make-release.ps1  builds the release zip
```

Setup reads its install paths out of `README.txt` instead of having them
hardcoded, so adding a folder to the download is a one line change.

Notes on how the installer works, and the rules for what it will and won't
delete, are in [Installer/README.md](Installer/README.md).
