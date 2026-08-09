#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "Cfg.hpp"
#include "Log.hpp"

namespace MrpgCfg {

namespace {

struct Entry {
    char key[64];
    char val[192];
};

Entry s_entries[64];
int   s_count = 0;

// Trim in place, both ends, of anything the player's text editor might have left
// behind — including \r, because this file WILL be edited on Windows and saved
// with CRLF, and a value of "1\r" parses to 1 by luck with atoi but compares
// unequal to "1" with strcmp. That asymmetry is the kind of thing that makes a
// string setting silently ignored while an int setting beside it works.
char* Trim(char* s)
{
    while (*s == ' ' || *s == '\t' || *s == '\r' || *s == '\n') ++s;
    size_t n = strlen(s);
    while (n > 0) {
        char c = s[n - 1];
        if (c != ' ' && c != '\t' && c != '\r' && c != '\n') break;
        s[--n] = '\0';
    }
    return s;
}

const Entry* Find(const char* key)
{
    for (int i = 0; i < s_count; ++i)
        if (lstrcmpiA(s_entries[i].key, key) == 0)
            return &s_entries[i];
    return nullptr;
}

} // namespace

void Load(const char* dllDir)
{
    s_count = 0;

    char dir[MAX_PATH * 2];
    lstrcpynA(dir, dllDir, sizeof(dir));
    size_t n = strlen(dir);
    while (n > 0 && (dir[n - 1] == '\\' || dir[n - 1] == '/')) dir[--n] = '\0';
    char* slash = strrchr(dir, '\\');
    if (!slash) slash = strrchr(dir, '/');
    if (slash) *slash = '\0';

    char path[MAX_PATH * 2];
    _snprintf(path, sizeof(path) - 1, "%s\\MonsterRPGAudio.cfg", dir);
    path[sizeof(path) - 1] = '\0';

    FILE* fp = fopen(path, "rb");
    if (!fp) {
        MrpgLog::Write("cfg: no MonsterRPGAudio.cfg, using defaults for everything");
        return;
    }

    char line[512];
    int lineNo = 0;
    while (fgets(line, sizeof(line), fp)) {
        ++lineNo;
        char* p = Trim(line);
        if (!*p || *p == '#' || *p == ';') continue;

        char* eq = strchr(p, '=');
        if (!eq) {
            MrpgLog::Write("cfg: line %d ignored, no '=' in it: %s", lineNo, p);
            continue;
        }
        *eq = '\0';
        char* k = Trim(p);
        char* v = Trim(eq + 1);
        if (!*k) continue;

        if (s_count >= (int)(sizeof(s_entries) / sizeof(s_entries[0]))) {
            MrpgLog::Write("cfg: too many settings, ignoring from line %d on", lineNo);
            break;
        }
        lstrcpynA(s_entries[s_count].key, k, sizeof(s_entries[0].key));
        lstrcpynA(s_entries[s_count].val, v, sizeof(s_entries[0].val));
        ++s_count;
    }
    fclose(fp);

    MrpgLog::Write("cfg: %d setting%s loaded from MonsterRPGAudio.cfg",
                   s_count, s_count == 1 ? "" : "s");
}

int GetInt(const char* key, int defVal)
{
    const Entry* e = Find(key);
    if (!e) return defVal;

    char* end = nullptr;
    long v = strtol(e->val, &end, 10);
    if (end == e->val || (end && *end)) {
        MrpgLog::Write("cfg: %s=\"%s\" is not a whole number, using %d", key, e->val, defVal);
        return defVal;
    }
    return (int)v;
}

float GetFloat(const char* key, float defVal)
{
    const Entry* e = Find(key);
    if (!e) return defVal;

    char* end = nullptr;
    double v = strtod(e->val, &end);
    if (end == e->val || (end && *end)) {
        MrpgLog::Write("cfg: %s=\"%s\" is not a number, using %g", key, e->val, (double)defVal);
        return defVal;
    }
    return (float)v;
}

const char* GetStr(const char* key, const char* defVal)
{
    const Entry* e = Find(key);
    return e ? e->val : defVal;
}

} // namespace MrpgCfg
