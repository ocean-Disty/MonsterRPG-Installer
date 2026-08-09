#pragma once

// =============================================================================
// Pairing.h — find every mod DLL that wants to be injected, in the right order
// =============================================================================
//
// SHARED FILE. Byte-identical copies live in:
//     Blockland\MonsterRPGAudio\src\Pairing.h
//     Blockland\BLTickRate\src\Pairing.h
//
// THE PROBLEM. Two mods, two folders, two launchers, and only one of them can
// create the process. Whichever .bat the player double-clicks, the other mod is
// simply absent — and the player has no way to know that except by noticing
// something they installed is not working. Asking them to remember which icon is
// "the one that starts both" is a support burden that never ends.
//
// THE ANSWER. Either launcher discovers the other. The player double-clicks
// whichever they like and gets everything they installed.
//
// WHY THIS IS IN C++ AND NOT IN THE .bat. Directory scanning, reading a small
// config, de-duplicating paths and sorting by priority are all things cmd does
// badly and fragilely — and a .bat that grows labels and gotos has already cost
// this project a debugging session over line endings. The .bat now passes one
// word, AUTO, and everything else happens here where it can be reasoned about.
//
// ── ORDER MATTERS, AND IS NOT ALPHABETICAL ───────────────────────────────────
//
// BLTickRate rewrites engine constants that are read ONCE during startup, so it
// wants to be in place as early as possible. MonsterRPGAudio does not care at
// all — it waits for a window before it does anything. Hence an explicit
// priority number rather than "whatever the directory listing gave us", which
// would have put BLTickRate first today by luck of the alphabet and silently
// stopped doing so the day someone adds a mod beginning with A.
//
// ── EXTENSIBLE WITHOUT EDITING THIS FILE ─────────────────────────────────────
//
// A third mod joins by dropping bl_inject.cfg in its own folder:
//
//     dll=bin\MyMod.dll
//     order=30
//     name=MyMod
//
// Nothing here has to change, and neither does either .bat. Unknown folders
// without that file are ignored entirely — this never goes looking for DLLs to
// load on its own initiative.

#include <windows.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>

namespace BlPair {

struct Entry {
    char path[MAX_PATH * 2];
    char name[64];
    int  order;
};

const int MAX_ENTRIES = 8;

// Mods this build knows by name. A folder listed here does not need a
// bl_inject.cfg — it works even against an older copy of the other mod that
// predates the convention, which is the whole reason the table exists alongside
// the config file rather than being replaced by it.
struct KnownMod {
    const char* folder;
    const char* dll;
    int         order;
};

inline const KnownMod* KnownMods(int& count)
{
    static const KnownMod mods[] = {
        // Patches constants the engine reads once at startup: earliest.
        { "BLTickRate",      "bin\\BLTickRate.dll",      10 },
        // Waits for a window before doing anything: order irrelevant, but fixed
        // so two runs on the same machine always produce the same command line.
        { "MonsterRPGAudio", "bin\\MonsterRPGAudio.dll", 20 },
    };
    count = (int)(sizeof(mods) / sizeof(mods[0]));
    return mods;
}

namespace detail {

inline void JoinPath(char* out, int outLen, const char* a, const char* b)
{
    _snprintf(out, outLen - 1, "%s\\%s", a, b);
    out[outLen - 1] = '\0';
}

inline bool FileExists(const char* p)
{
    DWORD a = GetFileAttributesA(p);
    return a != INVALID_FILE_ATTRIBUTES && !(a & FILE_ATTRIBUTE_DIRECTORY);
}

inline bool AlreadyHave(const Entry* list, int n, const char* path)
{
    for (int i = 0; i < n; ++i)
        if (lstrcmpiA(list[i].path, path) == 0) return true;
    return false;
}

inline char* Trim(char* s)
{
    while (*s == ' ' || *s == '\t' || *s == '\r' || *s == '\n') ++s;
    size_t n = strlen(s);
    while (n && (s[n-1]==' '||s[n-1]=='\t'||s[n-1]=='\r'||s[n-1]=='\n')) s[--n] = '\0';
    return s;
}

// Reads bl_inject.cfg out of one folder. Returns false when there is no usable
// entry, which includes "the file names a DLL that is not there" — a mod that
// has been half-deleted must be skipped, not injected as a missing path.
inline bool ReadInjectCfg(const char* folderPath, const char* folderName, Entry& out)
{
    char cfgPath[MAX_PATH * 2];
    JoinPath(cfgPath, sizeof(cfgPath), folderPath, "bl_inject.cfg");
    FILE* fp = fopen(cfgPath, "rb");
    if (!fp) return false;

    char dll[MAX_PATH] = {0};
    char name[64]      = {0};
    int  order         = 50;

    char line[512];
    while (fgets(line, sizeof(line), fp)) {
        char* p = Trim(line);
        if (!*p || *p == '#' || *p == ';') continue;
        char* eq = strchr(p, '=');
        if (!eq) continue;
        *eq = '\0';
        char* k = Trim(p);
        char* v = Trim(eq + 1);
        if      (!lstrcmpiA(k, "dll"))   lstrcpynA(dll, v, sizeof(dll));
        else if (!lstrcmpiA(k, "name"))  lstrcpynA(name, v, sizeof(name));
        else if (!lstrcmpiA(k, "order")) order = atoi(v);
    }
    fclose(fp);

    if (!dll[0]) return false;

    JoinPath(out.path, sizeof(out.path), folderPath, dll);
    if (!FileExists(out.path)) return false;

    lstrcpynA(out.name, name[0] ? name : folderName, sizeof(out.name));
    out.order = order;
    return true;
}

} // namespace detail

// Fills `out` with every DLL to inject, sorted by order (lowest first).
// Returns the count. `blocklandDir` is the folder containing Blockland.exe.
//
// Never fails: a machine with nothing installed but the caller's own mod
// produces exactly that one entry, which is the single-mod case working by the
// same path as the paired case rather than by a special one.
inline int Discover(const char* blocklandDir, Entry* out, int maxOut)
{
    int n = 0;

    int kn = 0;
    const KnownMod* known = KnownMods(kn);
    for (int i = 0; i < kn && n < maxOut; ++i) {
        char folder[MAX_PATH * 2];
        detail::JoinPath(folder, sizeof(folder), blocklandDir, known[i].folder);

        char dllPath[MAX_PATH * 2];
        detail::JoinPath(dllPath, sizeof(dllPath), folder, known[i].dll);
        if (!detail::FileExists(dllPath)) continue;
        if (detail::AlreadyHave(out, n, dllPath)) continue;

        lstrcpynA(out[n].path, dllPath, sizeof(out[n].path));
        lstrcpynA(out[n].name, known[i].folder, sizeof(out[n].name));
        out[n].order = known[i].order;
        ++n;
    }

    // Then anything that opted in by convention.
    char pattern[MAX_PATH * 2];
    detail::JoinPath(pattern, sizeof(pattern), blocklandDir, "*");

    WIN32_FIND_DATAA fd;
    HANDLE h = FindFirstFileA(pattern, &fd);
    if (h != INVALID_HANDLE_VALUE) {
        do {
            if (!(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)) continue;
            if (fd.cFileName[0] == '.') continue;

            char folder[MAX_PATH * 2];
            detail::JoinPath(folder, sizeof(folder), blocklandDir, fd.cFileName);

            Entry e;
            memset(&e, 0, sizeof(e));
            if (!detail::ReadInjectCfg(folder, fd.cFileName, e)) continue;
            if (detail::AlreadyHave(out, n, e.path)) continue;
            if (n >= maxOut) break;

            out[n++] = e;
        } while (FindNextFileA(h, &fd));
        FindClose(h);
    }

    // Insertion sort by order. Stable, and n is at most 8.
    for (int i = 1; i < n; ++i) {
        Entry key = out[i];
        int j = i - 1;
        while (j >= 0 && out[j].order > key.order) { out[j + 1] = out[j]; --j; }
        out[j + 1] = key;
    }

    return n;
}

} // namespace BlPair
