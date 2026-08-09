# MonsterRPG for Blockland

An RPG mod for Blockland. Faster tick rate, plus optional ray traced audio with
3D voice chat.

All the code is in this repo, installer included, so you can read it before you
run it.

## Install it

**[Go to the latest release.](../../releases/latest)** Under "Assets" there are
two files. Most people want the first one.

| File | What it's for |
|---|---|
| `MonsterRPG-Setup-1.0.0.exe` | Just want to play. One file, nothing to unzip. |
| `MonsterRPG-1.0.0.zip` | The same installer plus all the source code. |

### The one file way

1. Click `MonsterRPG-Setup-1.0.0.exe` to download it. It's about 40 MB.
2. Open your Downloads folder and double click it.
3. Windows might show a blue box saying "Windows protected your PC". Click
   **More info**, then **Run anyway**. It says that because the file isn't
   signed, which costs money.
4. Read the first screen, then click **Continue**.
5. Setup usually finds Blockland by itself. If the text under the box is green,
   click **Install**. If it's red, click **Choose folder...** and pick the
   folder that has `Blockland.exe` and `Add-Ons` sitting in it.
6. When it's done, click **Close**.

Everything is inside that one .exe. It unpacks itself to a temporary folder
while it runs and clears that up afterwards.

### The zip way

Same thing, but the mod folders and all the source sit loose next to the
installer so you can read them first.

1. Download `MonsterRPG-1.0.0.zip`.
2. Right click it in your Downloads folder. Pick **Extract All**, then
   **Extract**.
3. A folder opens up. Double click **MonsterRPG Setup.exe** inside it.
4. Carry on from step 3 above.

To play, open your Blockland folder and double click
**Blockland MonsterRPG.exe**. There's a MonsterRPG shortcut in your Start menu
as well, and one on your Desktop if you left that box ticked.

## Uninstall it

Open your Blockland folder and double click
**Blockland MonsterRPG Uninstaller.exe**. It sits directly above
Blockland MonsterRPG.exe in the list, so it's easy to find. Click **Remove**,
then **Yes**.

If you took the Desktop shortcut, there's a **MonsterRPG Uninstaller** shortcut
sitting next to the MonsterRPG one.

It's also listed in Settings > Apps > Installed apps if you'd rather do it from
there.

## If something goes wrong

**Setup can't find Blockland.** Click "Choose folder..." and browse to the
folder with `Blockland.exe` in it. Usually that's Documents\Blockland.

**Your antivirus complains.** See "About antivirus warnings" below.

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

## About antivirus warnings

Some scanners flag this. On the last check, 3 out of 65 on VirusTotal did, with
generic labels like "Downloader" rather than a match on anything specific.

Here is the honest reason. The installer writes program files to your disk, and
the mod works by loading DLLs into a running game. Those two things together
are also the shape of a dropper, and a few engines score on shape. There is no
network code anywhere in the installer at all: it imports no networking library
and cannot make a connection. You can check that yourself, the source is here.

The real reason it gets flagged and, say, a Steam installer doesn't, is that
this isn't signed with a code signing certificate. Those cost money per year.
Until it is signed, expect the occasional warning.

What you can do:

- **Check it yourself.** Upload the file to [VirusTotal](https://www.virustotal.com)
  and look at *which* engines complain. Two or three generic hits out of sixty-five
  is normal for unsigned software. Twenty would not be.
- **Read the code.** All of it is in this repository, including the installer.
- **Build it yourself** from source, if you'd rather trust your own compiler.
- **Report the false positive** if you use one of the affected scanners. They
  all take submissions:
  [Bkav](https://www.bkav.com/report-false-positive),
  [Rising](https://www.rising.com.cn/), Elastic via
  [their GitHub](https://github.com/elastic/protections-artifacts/issues).

If a scanner ever flags this with a *specific* trojan name rather than a
generic one, please open an issue. That would be worth looking at properly.

## For developers

```
Installer/           setup, uninstaller, launcher (C, Win32)
BLTickRate/          tick rate mod (C++, injected DLL)
MonsterRPGAudio/     audio and voice chat (C++, injected DLL)
Client_MonsterRPG/   the UI, a normal add-on (TorqueScript)
README.txt           tells Setup where each folder goes
```

Built DLLs are in each `bin/`, so you don't have to compile anything to use it.

`build.bat` makes the ordinary Setup, which reads the folders sitting beside
it. That's the one in this repo, and it's half a megabyte. The 40 MB standalone
is the same program with the mod folders zipped inside it as a resource, built
by `Installer\tools\make-release.ps1`, and it's only ever attached to a release.
Keeping it out of the repo is why cloning this isn't a 40 MB download per
rebuild.

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
