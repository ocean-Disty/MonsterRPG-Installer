# MonsterRPG for Blockland

An RPG mod for Blockland. Faster tick rate, plus optional ray traced audio with
3D voice chat.

All the code is in this repo, installer included, so you can read it before you
run it.

## Before you download: yes, some scanners flag this

**[Here is the VirusTotal scan.](https://www.virustotal.com/gui/file/ed5b4fb3769f93e29b2615d18865b15b8351a4953324faa59ffe48453140bae9?nocache=1)**
3 engines out of 65 call it malicious. Bkav, Elastic and Rising. The other 62,
Microsoft and BitDefender and Kaspersky and CrowdStrike among them, pass it.

That scan is of the green **Code** button download of this repo, so it is the
source and the built files together. Push anything and the hash changes, so
treat it as a snapshot rather than a permanent link. Scan your own copy if you
want to be sure of what you have.

Why it happens, honestly: MonsterRPG works by loading its DLL files into
Blockland while the game is running. The two small launcher programs that do
that use the same Windows calls every DLL injector uses, and a few scanners
score on that pattern alone. They are not wrong about what the code does. It is
how the mod works, and it is how Blockland dll mods have always worked.

What it is **not** is a downloader, whatever the label says. There is no
networking code in the installer at all. It imports no network library and
cannot open a connection, which you can check yourself from the source in this
repo.

The reason a Steam installer does not get flagged and this does is a code
signing certificate, which costs money every year. Until this is signed, expect
the occasional warning.

There is more on this, including how to check for yourself and how to report a
false positive, in [About antivirus warnings](#about-antivirus-warnings) further
down.

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

The short version is at the top. This is the detail.

### Which files, exactly

It is not the installer. The two files that trip the scanners are the mod
launchers:

```
BLTickRate/bin/BLTickRateLaunch.exe
MonsterRPGAudio/bin/MonsterRPGAudioLaunch.exe
```

Both use `VirtualAllocEx`, `WriteProcessMemory`, `CreateRemoteThread` and
`LoadLibraryA` together. That combination is the textbook signature for
injecting a DLL into another program, and it is the first thing any heuristic
looks for. It is also precisely what these two do: start Blockland paused,
write the DLL path into it, start a thread there to load it, then let the game
run. Nothing is hidden about that, the source is in `src/Launch.cpp`.

Upload those two on their own and they light up. Upload
`MonsterRPG Setup.exe` on its own and it does not.

### What it is not

The label says "downloader". It cannot download anything. The installer imports
no networking library at all, so there is no code in it that could open a
connection even if it wanted to. Check the import table of the .exe if you like,
or read `Installer/src/`.

### What would fix it

A code signing certificate, and realistically nothing else. None of these files
are signed. [SignPath](https://signpath.io/) and Microsoft's Azure Trusted
Signing both do free certificates for open source projects.

### What you can do now

- **Check it yourself** on [VirusTotal](https://www.virustotal.com) and look at
  *which* engines complain and what they say. Two or three generic hits out of
  sixty-five is ordinary for unsigned software. Twenty would not be.
- **Read the code.** All of it is here, installer included.
- **Build it yourself**, if you would rather trust your own compiler.
- **Report the false positive** if you use one of the affected scanners. Send
  them the individual launcher .exe, not the whole zip, because that is what
  they can actually act on:
  [Bkav](https://www.bkav.com/report-false-positive),
  [Rising](https://www.rising.com.cn/), Elastic via
  [their GitHub](https://github.com/elastic/protections-artifacts/issues).

If a scanner ever flags this with a *specific* trojan name instead of a generic
one, please open an issue. That would be worth looking at properly.

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
