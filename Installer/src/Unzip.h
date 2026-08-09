/* ===========================================================================
 *  Unzip.h - reading a .zip, which Setup needs for one job only.
 *
 *  The standalone build of Setup carries the whole download inside itself as a
 *  zip resource, so that people can grab one file and run it. This unpacks
 *  that resource into a temporary folder, and everything after that behaves
 *  exactly as if the folders had been sitting beside the .exe all along.
 *
 *  The ordinary build has no such resource and never calls this.
 *
 *  Reads what Zip.c writes and what Windows' own "Send to > Compressed folder"
 *  writes: stored and deflated entries, names in UTF-8.
 * ======================================================================== */

#ifndef MONSTERRPG_UNZIP_H
#define MONSTERRPG_UNZIP_H

#include <windows.h>

/* Called once per file as it comes out, with the path inside the archive.
 * Returning FALSE stops the unpacking. */
typedef BOOL (*UnzipProgress)(void *user, const wchar_t *nameInZip);

/* Counts the files in the archive without unpacking anything, so a progress
 * bar can be sized first. 0 if the data is not a zip. */
unsigned UnzipCount(const void *data, size_t size);

/* Unpacks everything into destDir, creating folders as needed.
 *
 * Entry names are checked the same way install-log paths are: anything with a
 * "..", a drive letter or a leading slash in it is refused, and every file has
 * to land inside destDir once Windows has resolved the path. A zip is just a
 * list of file names someone else wrote, so it gets the same suspicion. */
BOOL UnzipToFolder(const void *data, size_t size, const wchar_t *destDir,
                   UnzipProgress cb, void *user);

#endif /* MONSTERRPG_UNZIP_H */
