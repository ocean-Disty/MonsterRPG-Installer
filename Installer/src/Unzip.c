/* ===========================================================================
 *  Unzip.c - see Unzip.h.
 *
 *  The archive is already in memory when this runs: it is a resource inside
 *  Setup.exe, which Windows has mapped for us. So there is no file reading
 *  here, only walking the bytes.
 *
 *  A zip is read from the back. The last thing in the file is a small record
 *  saying where the table of contents starts and how many entries it has. The
 *  table of contents then points at each file. That is why a zip can be
 *  appended to without rewriting it, and why the reading order looks odd.
 * ======================================================================== */

#define WIN32_LEAN_AND_MEAN

#include "Unzip.h"
#include "Common.h"

#include <zlib.h>
#include <stdio.h>
#include <string.h>

#define CHUNK (64 * 1024)

/* ------------------------------------------------------------------------ */

static unsigned Get16(const unsigned char *p)
{
    return (unsigned)p[0] | ((unsigned)p[1] << 8);
}

static unsigned Get32(const unsigned char *p)
{
    return (unsigned)p[0] | ((unsigned)p[1] << 8) |
           ((unsigned)p[2] << 16) | ((unsigned)p[3] << 24);
}

/* The end-of-central-directory record. It is 22 bytes plus a comment that is
 * almost always empty, so it is found by scanning backwards from the end. */
static const unsigned char *FindEnd(const unsigned char *data, size_t size)
{
    size_t maxBack = 22 + 0xFFFF;
    size_t i;

    if (size < 22)
        return NULL;
    if (maxBack > size)
        maxBack = size;

    for (i = 22; i <= maxBack; ++i) {
        const unsigned char *p = data + size - i;
        if (Get32(p) == 0x06054b50)
            return p;
    }
    return NULL;
}

static BOOL Utf8ToWide(const char *utf8, unsigned len, wchar_t *out, size_t cch)
{
    int got = MultiByteToWideChar(CP_UTF8, 0, utf8, (int)len, out, (int)cch - 1);

    if (got <= 0) {
        /* Not valid UTF-8. Older zip tools wrote names in the machine's own
         * code page instead, so that is the fallback rather than a failure. */
        got = MultiByteToWideChar(CP_ACP, 0, utf8, (int)len, out, (int)cch - 1);
        if (got <= 0)
            return FALSE;
    }

    out[got] = L'\0';
    return TRUE;
}

/* ------------------------------------------------------------------------ */

unsigned UnzipCount(const void *data, size_t size)
{
    const unsigned char *bytes = (const unsigned char *)data;
    const unsigned char *end = FindEnd(bytes, size);

    if (end == NULL)
        return 0;

    return Get16(end + 10);
}

BOOL UnzipToFolder(const void *data, size_t size, const wchar_t *destDir,
                   UnzipProgress cb, void *user)
{
    const unsigned char *bytes = (const unsigned char *)data;
    const unsigned char *end;
    const unsigned char *walk;
    unsigned count, dirOffset, i;
    unsigned char *outBuf = NULL;
    BOOL ok = TRUE;

    end = FindEnd(bytes, size);
    if (end == NULL)
        return FALSE;

    count     = Get16(end + 10);
    dirOffset = Get32(end + 16);

    if (dirOffset >= size)
        return FALSE;

    if (!EnsureDir(destDir))
        return FALSE;

    outBuf = (unsigned char *)LocalAlloc(LPTR, CHUNK);
    if (outBuf == NULL)
        return FALSE;

    walk = bytes + dirOffset;

    for (i = 0; i < count && ok; ++i) {
        unsigned nameLen, extraLen, commentLen, method;
        unsigned compSize, rawSize, localOffset;
        const unsigned char *local;
        const unsigned char *payload;
        wchar_t name[MAX_PATH * 2];
        wchar_t full[MAX_PATH * 3];
        wchar_t *slash;
        HANDLE out;

        /* Past the end, or not a central directory entry: the archive is
         * damaged and guessing would only make it worse. */
        if ((size_t)(walk - bytes) + 46 > size || Get32(walk) != 0x02014b50) {
            ok = FALSE;
            break;
        }

        method      = Get16(walk + 10);
        compSize    = Get32(walk + 20);
        rawSize     = Get32(walk + 24);
        nameLen     = Get16(walk + 28);
        extraLen    = Get16(walk + 30);
        commentLen  = Get16(walk + 32);
        localOffset = Get32(walk + 42);

        if ((size_t)(walk - bytes) + 46 + nameLen > size) { ok = FALSE; break; }
        if (nameLen == 0 || nameLen >= MAX_PATH * 2)      { walk += 46 + nameLen + extraLen + commentLen; continue; }

        if (!Utf8ToWide((const char *)(walk + 46), nameLen, name,
                        sizeof(name) / sizeof(name[0]))) {
            ok = FALSE;
            break;
        }

        walk += 46 + nameLen + extraLen + commentLen;

        /* Zips use forward slashes. */
        for (slash = name; *slash != L'\0'; ++slash)
            if (*slash == L'/') *slash = L'\\';

        /* A folder entry, which exists only to carry the name. */
        {
            size_t nlen = wcslen(name);
            if (nlen > 0 && name[nlen - 1] == L'\\') {
                name[nlen - 1] = L'\0';
                if (IsSafeRelativePath(name)) {
                    PathJoin(full, sizeof(full) / sizeof(full[0]), destDir, name);
                    if (IsInsideFolder(destDir, full))
                        EnsureDir(full);
                }
                continue;
            }
        }

        /* The same rule the uninstaller applies to its own log. A zip entry is
         * a file name written by somebody else, and "..\..\Windows\..." is a
         * perfectly legal thing to put in one. */
        if (!IsSafeRelativePath(name))
            continue;

        PathJoin(full, sizeof(full) / sizeof(full[0]), destDir, name);
        if (!IsInsideFolder(destDir, full))
            continue;

        /* Make the folder the file goes in. */
        {
            wchar_t parent[MAX_PATH * 3];
            wchar_t *cut;

            wcsncpy(parent, full, (sizeof(parent) / sizeof(parent[0])) - 1);
            parent[(sizeof(parent) / sizeof(parent[0])) - 1] = L'\0';
            cut = wcsrchr(parent, L'\\');
            if (cut != NULL) {
                *cut = L'\0';
                if (!EnsureDir(parent)) { ok = FALSE; break; }
            }
        }

        /* Local header: 30 bytes, then its own copy of the name and extra
         * field. The lengths there can differ from the central directory's,
         * so they have to be read again rather than reused. */
        if ((size_t)localOffset + 30 > size)          { ok = FALSE; break; }
        local = bytes + localOffset;
        if (Get32(local) != 0x04034b50)               { ok = FALSE; break; }

        payload = local + 30 + Get16(local + 26) + Get16(local + 28);
        if ((size_t)(payload - bytes) + compSize > size) { ok = FALSE; break; }

        out = CreateFileW(full, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS,
                          FILE_ATTRIBUTE_NORMAL, NULL);
        if (out == INVALID_HANDLE_VALUE) { ok = FALSE; break; }

        if (method == 0) {
            DWORD wrote = 0;
            if (compSize > 0 && !WriteFile(out, payload, compSize, &wrote, NULL))
                ok = FALSE;
        } else if (method == 8) {
            z_stream strm;
            int zrc = Z_OK;

            memset(&strm, 0, sizeof(strm));
            /* Negative window size: raw deflate, no zlib wrapper. A zip
             * carries its own header, so there is none to skip. */
            if (inflateInit2(&strm, -MAX_WBITS) != Z_OK) {
                ok = FALSE;
            } else {
                strm.next_in  = (Bytef *)payload;
                strm.avail_in = compSize;

                do {
                    DWORD wrote = 0;
                    unsigned produced;

                    strm.next_out  = outBuf;
                    strm.avail_out = CHUNK;

                    zrc = inflate(&strm, Z_NO_FLUSH);
                    if (zrc != Z_OK && zrc != Z_STREAM_END && zrc != Z_BUF_ERROR) {
                        ok = FALSE;
                        break;
                    }

                    produced = CHUNK - strm.avail_out;
                    if (produced > 0 && !WriteFile(out, outBuf, produced, &wrote, NULL)) {
                        ok = FALSE;
                        break;
                    }
                    if (produced == 0 && zrc == Z_BUF_ERROR) break;
                } while (zrc != Z_STREAM_END);

                if (ok && strm.total_out != rawSize)
                    ok = FALSE;          /* came out a different size than promised */

                inflateEnd(&strm);
            }
        } else {
            ok = FALSE;                  /* some other compression method */
        }

        CloseHandle(out);

        if (!ok) {
            DeleteFileW(full);
            break;
        }

        if (cb != NULL && !cb(user, name)) {
            ok = FALSE;
            break;
        }
    }

    LocalFree(outBuf);
    return ok;
}
