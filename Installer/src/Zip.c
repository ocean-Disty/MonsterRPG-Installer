/* ===========================================================================
 *  Zip.c - see Zip.h.
 *
 *  The format is the original one from 1989 and has not needed to change for
 *  what this does: each file is written with a small header in front of it,
 *  and a table of contents listing all of them goes at the end.
 *
 *  Sizes and the checksum are not known until a file has been compressed, and
 *  they sit in the header that was already written. Rather than buffer whole
 *  files in memory to work them out first, the header is written with zeros
 *  and patched afterwards by seeking back to it. That is what the "fixups"
 *  below are.
 * ======================================================================== */

#define WIN32_LEAN_AND_MEAN

#include "Zip.h"

#include <zlib.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <wchar.h>

#define ZIP_MAX_ENTRIES 8192
#define CHUNK           (64 * 1024)

typedef struct {
    char     name[512];         /* UTF-8, forward slashes */
    unsigned nameLen;
    unsigned crc;
    unsigned compSize;
    unsigned rawSize;
    unsigned localOffset;
    unsigned short dosTime;
    unsigned short dosDate;
} ZipEntry;

struct ZipWriter {
    HANDLE    file;
    wchar_t   path[MAX_PATH * 2];
    ZipEntry *entries;
    unsigned  count;
    BOOL      failed;
};

/* ------------------------------------------------------------------------ */

static void Put16(unsigned char *p, unsigned v)
{
    p[0] = (unsigned char)(v & 0xFF);
    p[1] = (unsigned char)((v >> 8) & 0xFF);
}

static void Put32(unsigned char *p, unsigned v)
{
    p[0] = (unsigned char)(v & 0xFF);
    p[1] = (unsigned char)((v >> 8) & 0xFF);
    p[2] = (unsigned char)((v >> 16) & 0xFF);
    p[3] = (unsigned char)((v >> 24) & 0xFF);
}

static BOOL WriteAll(HANDLE h, const void *data, DWORD len)
{
    DWORD done = 0;
    const unsigned char *p = (const unsigned char *)data;

    while (done < len) {
        DWORD wrote = 0;
        if (!WriteFile(h, p + done, len - done, &wrote, NULL) || wrote == 0)
            return FALSE;
        done += wrote;
    }
    return TRUE;
}

static unsigned CurrentOffset(HANDLE h)
{
    LARGE_INTEGER zero, pos;
    zero.QuadPart = 0;
    if (!SetFilePointerEx(h, zero, &pos, FILE_CURRENT))
        return 0;
    return (unsigned)pos.QuadPart;
}

static BOOL SeekTo(HANDLE h, unsigned offset)
{
    LARGE_INTEGER at;
    at.QuadPart = offset;
    return SetFilePointerEx(h, at, NULL, FILE_BEGIN);
}

/* MS-DOS packed date and time, which is what the format stores. Anything
 * before 1980 cannot be represented; those get 1980-01-01. */
static void FileTimeToDos(const FILETIME *ft, unsigned short *dosDate,
                          unsigned short *dosTime)
{
    FILETIME local;
    SYSTEMTIME st;

    if (!FileTimeToLocalFileTime(ft, &local) || !FileTimeToSystemTime(&local, &st) ||
        st.wYear < 1980) {
        *dosDate = (unsigned short)((1 << 5) | 1);   /* 1980-01-01 */
        *dosTime = 0;
        return;
    }

    *dosDate = (unsigned short)(((st.wYear - 1980) << 9) | (st.wMonth << 5) | st.wDay);
    *dosTime = (unsigned short)((st.wHour << 11) | (st.wMinute << 5) | (st.wSecond / 2));
}

/* ------------------------------------------------------------------------ */

ZipWriter *ZipCreate(const wchar_t *path)
{
    ZipWriter *z = (ZipWriter *)LocalAlloc(LPTR, sizeof(ZipWriter));
    if (z == NULL)
        return NULL;

    z->entries = (ZipEntry *)LocalAlloc(LPTR, sizeof(ZipEntry) * ZIP_MAX_ENTRIES);
    if (z->entries == NULL) {
        LocalFree(z);
        return NULL;
    }

    wcsncpy(z->path, path, (sizeof(z->path) / sizeof(z->path[0])) - 1);

    z->file = CreateFileW(path, GENERIC_READ | GENERIC_WRITE, 0, NULL,
                          CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (z->file == INVALID_HANDLE_VALUE) {
        LocalFree(z->entries);
        LocalFree(z);
        return NULL;
    }

    return z;
}

static BOOL ZipAddOne(ZipWriter *z, const wchar_t *srcFile, const wchar_t *nameInZip)
{
    unsigned char header[30];
    ZipEntry *e;
    HANDLE in;
    FILETIME ft;
    z_stream strm;
    unsigned char *inBuf = NULL;
    unsigned char *outBuf = NULL;
    BOOL ok = FALSE;
    int zrc;
    unsigned here;
    int i;

    if (z->failed || z->count >= ZIP_MAX_ENTRIES)
        return FALSE;

    e = &z->entries[z->count];
    memset(e, 0, sizeof(*e));

    /* Names inside a zip use forward slashes, always. */
    {
        wchar_t tmp[MAX_PATH * 2];
        int need;

        wcsncpy(tmp, nameInZip, (sizeof(tmp) / sizeof(tmp[0])) - 1);
        tmp[(sizeof(tmp) / sizeof(tmp[0])) - 1] = L'\0';
        for (i = 0; tmp[i] != L'\0'; ++i)
            if (tmp[i] == L'\\') tmp[i] = L'/';

        need = WideCharToMultiByte(CP_UTF8, 0, tmp, -1, e->name,
                                   (int)sizeof(e->name), NULL, NULL);
        if (need <= 0)
            return FALSE;
        e->nameLen = (unsigned)(need - 1);
    }

    in = CreateFileW(srcFile, GENERIC_READ, FILE_SHARE_READ, NULL,
                     OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (in == INVALID_HANDLE_VALUE)
        return FALSE;

    if (GetFileTime(in, NULL, NULL, &ft))
        FileTimeToDos(&ft, &e->dosDate, &e->dosTime);

    e->localOffset = CurrentOffset(z->file);

    memset(header, 0, sizeof(header));
    Put32(header + 0,  0x04034b50);
    Put16(header + 4,  20);          /* needs a reader that understands deflate */
    Put16(header + 6,  0x0800);      /* the name above is UTF-8 */
    Put16(header + 8,  8);           /* deflate */
    Put16(header + 10, e->dosTime);
    Put16(header + 12, e->dosDate);
    /* checksum and both sizes stay zero here and are patched in below */
    Put16(header + 26, e->nameLen);
    Put16(header + 28, 0);

    if (!WriteAll(z->file, header, sizeof(header)) ||
        !WriteAll(z->file, e->name, e->nameLen)) {
        CloseHandle(in);
        z->failed = TRUE;
        return FALSE;
    }

    inBuf  = (unsigned char *)LocalAlloc(LPTR, CHUNK);
    outBuf = (unsigned char *)LocalAlloc(LPTR, CHUNK);
    if (inBuf == NULL || outBuf == NULL)
        goto done;

    memset(&strm, 0, sizeof(strm));
    /* Negative window size means "no zlib wrapper" - a zip carries its own
     * header and checksum and must not have zlib's on top of it. */
    if (deflateInit2(&strm, Z_DEFAULT_COMPRESSION, Z_DEFLATED, -MAX_WBITS,
                     8, Z_DEFAULT_STRATEGY) != Z_OK)
        goto done;

    e->crc = (unsigned)crc32(0L, Z_NULL, 0);

    for (;;) {
        DWORD got = 0;
        int flush;

        if (!ReadFile(in, inBuf, CHUNK, &got, NULL)) {
            deflateEnd(&strm);
            goto done;
        }

        e->rawSize += got;
        if (got > 0)
            e->crc = (unsigned)crc32(e->crc, inBuf, got);

        strm.next_in  = inBuf;
        strm.avail_in = got;
        flush = (got == 0) ? Z_FINISH : Z_NO_FLUSH;

        do {
            strm.next_out  = outBuf;
            strm.avail_out = CHUNK;

            zrc = deflate(&strm, flush);
            if (zrc == Z_STREAM_ERROR) {
                deflateEnd(&strm);
                goto done;
            }

            {
                unsigned produced = CHUNK - strm.avail_out;
                if (produced > 0) {
                    if (!WriteAll(z->file, outBuf, produced)) {
                        deflateEnd(&strm);
                        goto done;
                    }
                    e->compSize += produced;
                }
            }
        } while (strm.avail_out == 0);

        if (flush == Z_FINISH)
            break;
    }

    deflateEnd(&strm);

    /* Go back and fill in what we only know now. */
    here = CurrentOffset(z->file);
    if (!SeekTo(z->file, e->localOffset + 14))
        goto done;

    {
        unsigned char patch[12];
        Put32(patch + 0, e->crc);
        Put32(patch + 4, e->compSize);
        Put32(patch + 8, e->rawSize);
        if (!WriteAll(z->file, patch, sizeof(patch)))
            goto done;
    }

    if (!SeekTo(z->file, here))
        goto done;

    z->count++;
    ok = TRUE;

done:
    if (inBuf  != NULL) LocalFree(inBuf);
    if (outBuf != NULL) LocalFree(outBuf);
    CloseHandle(in);
    if (!ok)
        z->failed = TRUE;
    return ok;
}

static BOOL ZipAddDirInner(ZipWriter *z, const wchar_t *srcDir,
                           const wchar_t *prefix, CopyCallback cb, void *user)
{
    wchar_t pattern[MAX_PATH * 2];
    wchar_t child[MAX_PATH * 2];
    wchar_t rel[MAX_PATH * 2];
    WIN32_FIND_DATAW fd;
    HANDLE h;
    BOOL ok = TRUE;

    PathJoin(pattern, sizeof(pattern) / sizeof(pattern[0]), srcDir, L"*");

    h = FindFirstFileW(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE)
        return FALSE;

    do {
        if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0)
            continue;

        PathJoin(child, sizeof(child) / sizeof(child[0]), srcDir, fd.cFileName);

        if (prefix[0] != L'\0')
            _snwprintf(rel, sizeof(rel) / sizeof(rel[0]), L"%s\\%s", prefix, fd.cFileName);
        else
            _snwprintf(rel, sizeof(rel) / sizeof(rel[0]), L"%s", fd.cFileName);
        rel[(sizeof(rel) / sizeof(rel[0])) - 1] = L'\0';

        if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
            if (!ZipAddDirInner(z, child, rel, cb, user)) {
                ok = FALSE;
                break;
            }
            continue;
        }

        if (IsWorkingFile(fd.cFileName))
            continue;

        if (!ZipAddOne(z, child, rel)) {
            ok = FALSE;
            break;
        }

        if (cb != NULL && !cb(user, rel)) {
            ok = FALSE;
            break;
        }
    } while (FindNextFileW(h, &fd));

    FindClose(h);
    return ok;
}

BOOL ZipAddDirContents(ZipWriter *z, const wchar_t *srcDir,
                       CopyCallback cb, void *user)
{
    if (z == NULL || !DirExists(srcDir))
        return FALSE;
    return ZipAddDirInner(z, srcDir, L"", cb, user);
}

BOOL ZipFinish(ZipWriter *z)
{
    unsigned start, size;
    unsigned i;
    BOOL ok = TRUE;

    if (z == NULL)
        return FALSE;

    if (z->failed) {
        ZipAbort(z);
        return FALSE;
    }

    start = CurrentOffset(z->file);

    for (i = 0; i < z->count && ok; ++i) {
        ZipEntry *e = &z->entries[i];
        unsigned char rec[46];

        memset(rec, 0, sizeof(rec));
        Put32(rec + 0,  0x02014b50);
        Put16(rec + 4,  20);
        Put16(rec + 6,  20);
        Put16(rec + 8,  0x0800);
        Put16(rec + 10, 8);
        Put16(rec + 12, e->dosTime);
        Put16(rec + 14, e->dosDate);
        Put32(rec + 16, e->crc);
        Put32(rec + 20, e->compSize);
        Put32(rec + 24, e->rawSize);
        Put16(rec + 28, e->nameLen);
        Put32(rec + 38, 0);            /* nothing special about the file */
        Put32(rec + 42, e->localOffset);

        ok = WriteAll(z->file, rec, sizeof(rec)) &&
             WriteAll(z->file, e->name, e->nameLen);
    }

    size = CurrentOffset(z->file) - start;

    if (ok) {
        unsigned char end[22];
        memset(end, 0, sizeof(end));
        Put32(end + 0,  0x06054b50);
        Put16(end + 8,  z->count);
        Put16(end + 10, z->count);
        Put32(end + 12, size);
        Put32(end + 16, start);
        ok = WriteAll(z->file, end, sizeof(end));
    }

    CloseHandle(z->file);
    LocalFree(z->entries);
    LocalFree(z);

    return ok;
}

void ZipAbort(ZipWriter *z)
{
    wchar_t path[MAX_PATH * 2];

    if (z == NULL)
        return;

    wcscpy(path, z->path);

    CloseHandle(z->file);
    LocalFree(z->entries);
    LocalFree(z);

    DeleteFileW(path);
}
