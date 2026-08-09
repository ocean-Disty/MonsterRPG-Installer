# The installer

`MonsterRPG Setup.exe` at the top of the repo is built from this folder.
Players never need to open it.

## The three programs

Setup carries the other two inside itself as resources, so the download is one
program plus the folders it copies. That's why `build.bat` builds them in this
order:

1. `MonsterRPG.exe`, the launcher. Setup writes it into the game folder as
   `Blockland MonsterRPG.exe`, next to `Blockland.exe`. It finds the installed
   mods and starts the game through whichever one loads first, which pulls in
   the rest.
2. `Uninstaller.exe`. Setup writes it out as
   `Blockland MonsterRPG Uninstaller.exe`.
3. `MonsterRPG Setup.exe`, with the first two embedded.

Both output names are picked so a folder sorted by name groups them:

```
Blockland MonsterRPG Uninstaller.exe
Blockland MonsterRPG.exe
Blockland.exe
```

A space sorts before a dot, so ` Uninstaller.exe` lands above `.exe`. The names
live in `Common.h` as `LAUNCHER_NAME` and `UNINSTALLER_NAME`, and the shortcut
names next to them follow the same trick. Change one of those and everything
else follows, including what the uninstaller looks for when the install log has
gone missing.

## Where files go

Not hardcoded. Setup reads `README.txt` from the folder above:

```
Client_MonsterRPG Documents -> Blockland -> Add-Ons
Rest is Documents -> Blockland

We usually want the Client_MonsterRPG zipped too in that folder.
```

Everything up to and including the `Blockland` step gets dropped, since the
player picks that folder themselves. What's left is the path inside it.

The zip line is honoured too. `Client_MonsterRPG` is installed as a folder and
also written out as `Client_MonsterRPG.zip` beside it, because Blockland reads
add-ons either way.

## Finding the game

Setup checks all of these and keeps the best answer, not the first:

1. `Documents\Blockland`
2. Steam's install path from the registry, then `steamapps\common\Blockland`
3. The usual folder shapes on every fixed drive
4. Next to `Setup.exe`, in case the download was unzipped into the game folder
5. A bounded search of Documents and the Desktop

"Best" means a folder with `Blockland.exe` and `Add-Ons` together beats one with
only `Blockland.exe`. That's what settles it when the game is installed twice.

Custom installs just use Choose folder. The line under the box always says what
to look for, and Install stays greyed out until the folder is real.

## What it will not delete

Setup and the uninstaller both get their delete list from a text file:
`README.txt`, and the install log Setup leaves in the game folder. Text files
get edited, damaged, or written by a future version that got something wrong.
None of that is allowed to become "delete the wrong folder".

Every path is checked before anything is removed:

* `IsSafeRelativePath` rejects an empty path, a drive letter, a leading slash,
  any `..` step, any wildcard, and any step ending in a space or dot. Windows
  quietly strips trailing spaces and dots, so `Add-Ons ` and `Add-Ons` would hit
  the same folder while comparing as different strings.
* `IsProtectedGameItem` rejects `Blockland.exe`, `Add-Ons`, `base`, `config`,
  `saves` and the rest of Blockland's own. Nothing this installer creates is
  ever one of those.
* `IsInsideFolder` is the last check. Whatever the others decided, the thing
  being deleted still has to resolve to somewhere inside the game folder.
* `IsSafeShortcutPath` handles shortcut lines, which are full paths. They have
  to be a `.lnk` in the user's own Desktop or Start menu.

A line that fails gets skipped and shown on screen as `LEFT ALONE`. The
uninstaller then leaves itself and the log in place so it can be run again.

`tests\run-tests.bat` proves all of this. Run it after changing anything in
`Common.c`.

## Other things worth knowing

**Reinstalling is clean.** Setup clears out the previous version of each folder
before copying the new one, so a file the new version dropped can't be left
lying around mixed in with it. `.cfg` files are kept, so edited settings
survive. Only folders listed in the previous install log get cleared.

**One at a time.** A second Setup won't start. It brings the first one forward
and says so. Two copies writing into the same folder would each be deleting the
other's work halfway through.

**No admin.** The game folder, a shortcut or two, and one per-user registry
entry for the Apps and features list. That's the whole footprint.

**The audio is the only optional part.** It's the one thing that opens a network
port, so its box ships unticked. `BLTickRate` and `Client_MonsterRPG` have no
checkbox, since there's no useful game without them. To add a new optional part
later: one line in `KindOf` in `Setup.c`, and a checkbox in `Setup.rc`. Anything
not named there is installed always, which is the safe default.

**The uninstaller removes itself.** A running program can't delete its own file,
so it copies itself to temp. That copy waits for the first to close, deletes it
and the install log, then hands its own deletion to a short lived `cmd`.

`MoveFileEx` with `MOVEFILE_DELAY_UNTIL_REBOOT` looks like the obvious way to do
that last part, and it's wrong. It writes to a machine wide registry key, so it
needs admin, which this installer never asks for. Without admin the call fails
quietly and the helper sits in temp for good. Ten of them piled up during
testing before it got caught.

Self removal only runs when everything else worked. If anything is still there,
the uninstaller and the log stay put so it can be run again.

## Building

MSYS2 with 32-bit support:

```
pacman -S mingw-w64-i686-gcc mingw-w64-i686-zlib
```

Then `build.bat` from the MINGW32 shell, or with `C:\msys64\mingw32\bin` on your
PATH. 32-bit is deliberate: it runs on 32 and 64 bit Windows, and Blockland is
32-bit anyway.

The icon comes from `MonsterRPGIcon.png` via `tools\make-icon.ps1`, and only
gets rebuilt when `src\MonsterRPG.ico` is missing. Delete the `.ico` to force it.

## Files

```
src\Setup.c             the wizard, the folder search, the copying
src\Uninstall.c         the remover and the self removal
src\Launcher.c          finds installed mods, starts the game through one
src\Common.c            paths, copying, deleting, safety checks, README.txt parsing
src\Zip.c               writes the Client_MonsterRPG.zip that README.txt asks for
tests\SafetyTests.c     46 checks on the rules for what can be deleted
tools\make-icon.ps1     PNG to ICO
tools\make-release.ps1  builds the release zip
```
