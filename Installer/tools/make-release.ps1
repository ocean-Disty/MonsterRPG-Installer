# ===========================================================================
#  make-release.ps1 - build the two things people download.
#
#  Both go on the same GitHub Release, because they are for two different
#  people:
#
#    release\MonsterRPG-Setup-<version>.exe
#        One file. Download it, double-click it, done. The mod folders are
#        zipped up inside the .exe and unpacked to a temporary folder while it
#        runs. Nothing has to sit beside it.
#
#    release\MonsterRPG-<version>.zip
#        The same installer, the same folders loose beside it, and all the
#        source everything was built from. For anyone who would rather read
#        the code than take a stranger's .exe on faith, and for building it
#        yourself.
#
#  Source is in the zip on purpose. It costs almost nothing next to the
#  artwork, and it means the answer to "what does this program do?" sits in
#  the same folder as the program.
#
#  Usage:
#      powershell -ExecutionPolicy Bypass -File Installer\tools\make-release.ps1
#      ... -SkipBuild     package what is already built
# ===========================================================================

param(
    [string]$Version = '1.0.0',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)   # ...\Installer Files
$out  = Join-Path $root 'release'
$stage = Join-Path $out "MonsterRPG-$Version"
$zip   = Join-Path $out "MonsterRPG-$Version.zip"

Write-Host ""
Write-Host "Packaging MonsterRPG $Version" -ForegroundColor Cyan
Write-Host "  from $root"
Write-Host ""

# --- build ------------------------------------------------------------------
if (-not $SkipBuild) {
    Write-Host "Building the installer ..."
    & cmd /c "`"$root\Installer\build.bat`" < NUL 2>&1" | Out-String -Width 200 | Write-Host
    if (-not (Test-Path (Join-Path $root 'MonsterRPG Setup.exe'))) {
        throw "the build did not produce MonsterRPG Setup.exe"
    }

    Write-Host "Running the safety checks ..."
    $testOut = & cmd /c "`"$root\Installer\tests\run-tests.bat`" < NUL 2>&1" | Out-String -Width 200
    Write-Host $testOut
    # These decide what the installer is allowed to delete. A release must
    # never be cut from a build where they do not pass.
    if ($testOut -notmatch 'ALL \d+ CHECKS PASSED') {
        throw "the safety checks did not pass - REFUSING to package a release"
    }
}

# --- stage ------------------------------------------------------------------
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
if (Test-Path $zip)   { Remove-Item -Force $zip }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

# Everything a player needs, plus the source everything was built from.
$folders = @('BLTickRate', 'Client_MonsterRPG', 'MonsterRPGAudio', 'Installer')
$files   = @('MonsterRPG Setup.exe', 'README.txt', 'README.md', 'MonsterRPGIcon.png')

# The same things .gitignore keeps off GitHub are kept out of the download,
# and for the same reasons: private notes, stale logs, build leftovers.
$excludeDirs  = @('build', 'cache')
$excludeNames = @('startClaude.bat', 'CLAUDE.md', 'Thumbs.db', 'desktop.ini')

function Copy-Clean {
    param([string]$Source, [string]$Dest)

    New-Item -ItemType Directory -Force -Path $Dest | Out-Null

    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        if ($_.PSIsContainer) {
            if ($excludeDirs -contains $_.Name) { return }
            Copy-Clean $_.FullName (Join-Path $Dest $_.Name)
            return
        }

        $n = $_.Name
        if ($excludeNames -contains $n)        { return }
        if ($n -like '*.log')                  { return }
        if ($n -like '*.prepair-backup')       { return }
        if ($n -like '*.o' -or $n -like '*.res' -or $n -like '*.tmp') { return }
        if ($n -match '^\d{4}-\d{2}-\d{2}-')   { return }   # private transcripts

        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $Dest $n) -Force
    }
}

foreach ($f in $folders) {
    $src = Join-Path $root $f
    if (-not (Test-Path $src)) { throw "missing folder: $f" }
    Write-Host "  + $f"
    Copy-Clean $src (Join-Path $stage $f)
}

foreach ($f in $files) {
    $src = Join-Path $root $f
    if (-not (Test-Path $src)) { throw "missing file: $f" }
    Write-Host "  + $f"
    Copy-Item -LiteralPath $src -Destination (Join-Path $stage $f) -Force
}

# MonsterRPGAudio writes its downloaded sound into cache\, which is excluded
# above. The folders still have to exist for it to write into.
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'MonsterRPGAudio\cache\music') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'MonsterRPGAudio\cache\sfx')   | Out-Null

# --- sanity checks ----------------------------------------------------------
# A download missing any of these is broken in a way nobody would notice until
# a player reported it, so it is checked here instead.
$mustExist = @(
    'MonsterRPG Setup.exe',
    'README.txt',
    'BLTickRate\bin\BLTickRate.dll',
    'BLTickRate\BLTickRate.bat',
    'BLTickRate\bl_inject.cfg',
    'BLTickRate\src\BLTickRate.cpp',
    'MonsterRPGAudio\bin\MonsterRPGAudio.dll',
    'MonsterRPGAudio\MonsterRPGAudio.bat',
    'MonsterRPGAudio\bl_inject.cfg',
    'MonsterRPGAudio\src\MonsterRPGAudio.cpp',
    'Client_MonsterRPG\description.txt',
    'Installer\src\Setup.c',
    'Installer\build.bat'
)
foreach ($m in $mustExist) {
    if (-not (Test-Path (Join-Path $stage $m))) { throw "release is missing: $m" }
}

$leaked = Get-ChildItem -Recurse -File $stage | Where-Object {
    $_.Name -match '^\d{4}-\d{2}-\d{2}-' -or $_.Name -eq 'startClaude.bat' -or $_.Name -like '*.log'
}
if ($leaked) {
    $leaked | ForEach-Object { Write-Host ("  LEAKED: " + $_.FullName) -ForegroundColor Red }
    throw "private or stale files got into the release - REFUSING to zip"
}

# --- the source download ----------------------------------------------------
Write-Host ""
Write-Host "Compressing the source download ..."
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal

# --- the one-file installer -------------------------------------------------
# Everything except the Installer folder, since that is source rather than
# something to copy into a Blockland folder. Setup unpacks this at run time and
# then behaves exactly as if the folders had been sitting beside it.
Write-Host "Packing the payload for the standalone installer ..."

$payloadStage = Join-Path $out 'payload'
$payloadZip   = Join-Path $root 'Installer\build\payload.zip'
if (Test-Path $payloadStage) { Remove-Item -Recurse -Force $payloadStage }
if (Test-Path $payloadZip)   { Remove-Item -Force $payloadZip }
New-Item -ItemType Directory -Force -Path $payloadStage | Out-Null

foreach ($f in @('BLTickRate', 'Client_MonsterRPG', 'MonsterRPGAudio')) {
    Copy-Item -Recurse -LiteralPath (Join-Path $stage $f) -Destination $payloadStage
}
Copy-Item -LiteralPath (Join-Path $stage 'README.txt') -Destination $payloadStage

Compress-Archive -Path (Join-Path $payloadStage '*') -DestinationPath $payloadZip -CompressionLevel Optimal
Remove-Item -Recurse -Force $payloadStage

Write-Host "Building the standalone installer ..."
& cmd /c "`"$root\Installer\build.bat`" standalone < NUL 2>&1" | Out-String -Width 200 | Write-Host

$builtExe = Join-Path $root 'Installer\build\MonsterRPG Setup (standalone).exe'
if (-not (Test-Path $builtExe)) { throw "the standalone build did not produce an .exe" }

$standalone = Join-Path $out "MonsterRPG-Setup-$Version.exe"
if (Test-Path $standalone) { Remove-Item -Force $standalone }
Copy-Item -LiteralPath $builtExe -Destination $standalone

Remove-Item -Recurse -Force $stage
Remove-Item -Force $payloadZip

# --- done -------------------------------------------------------------------
$zipMb = (Get-Item $zip).Length / 1MB
$exeMb = (Get-Item $standalone).Length / 1MB

Write-Host ""
Write-Host ("  {0}   ({1:N1} MB)   one file, just run it" -f (Split-Path $standalone -Leaf), $exeMb) -ForegroundColor Green
Write-Host ("  {0}   ({1:N1} MB)   installer plus all the source" -f (Split-Path $zip -Leaf), $zipMb) -ForegroundColor Green
Write-Host ""
Write-Host "Both are in $out. Attach both to a GitHub Release tagged v$Version."
Write-Host ""
