/* ===========================================================================
 *  Zip.h - writing a .zip, which Setup needs for one job only.
 *
 *  Blockland reads an add-on either as a folder or as a .zip with the files at
 *  the top of the archive. README.txt asks for the zip as well as the folder,
 *  so Setup builds it rather than making anyone remember to.
 *
 *  This writes ordinary deflate-compressed zips - the same thing Windows'
 *  "Send to > Compressed folder" produces.
 * ======================================================================== */

#ifndef MONSTERRPG_ZIP_H
#define MONSTERRPG_ZIP_H

#include <windows.h>
#include "Common.h"

typedef struct ZipWriter ZipWriter;

/* Creates (or replaces) the archive. NULL if the file cannot be written. */
ZipWriter *ZipCreate(const wchar_t *path);

/* Adds everything inside srcDir at the top of the archive - the folder itself
 * is not a level in the zip, which is what Blockland expects. Working files
 * are skipped, exactly as CopyTree skips them. The callback is the same shape
 * as CopyTree's; returning FALSE stops the archive. */
BOOL ZipAddDirContents(ZipWriter *z, const wchar_t *srcDir,
                       CopyCallback cb, void *user);

/* Writes the table of contents and closes the file. The writer is freed
 * either way, so it must not be used afterwards. */
BOOL ZipFinish(ZipWriter *z);

/* Closes and deletes a half-written archive. Also frees the writer. */
void ZipAbort(ZipWriter *z);

#endif /* MONSTERRPG_ZIP_H */
