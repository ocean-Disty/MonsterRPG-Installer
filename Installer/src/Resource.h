/* ===========================================================================
 *  Resource.h - the numbers that tie the .rc files to the code.
 * ======================================================================== */

#ifndef MONSTERRPG_RESOURCE_H
#define MONSTERRPG_RESOURCE_H

#define VER_MAJOR       1
#define VER_MINOR       0
#define VER_PATCH       0
#define VER_STRING      "1.0.0"
#define VER_STRING_W   L"1.0.0"

#define PRODUCT_NAME    "MonsterRPG for Blockland"
#define COMPANY_NAME    "MonsterRPG"

#define IDI_APP                 101

/* Setup carries the other two programs inside itself so the download is one
 * file plus the folders it installs. These are their resource numbers. */
#define IDR_LAUNCHER_EXE        201
#define IDR_UNINSTALLER_EXE     202

/* Setup's window */
#define IDD_SETUP              1000

#define IDC_HEAD_ICON          1001
#define IDC_HEAD_TITLE         1002
#define IDC_HEAD_SUB           1003

#define IDC_STEP1              1010
#define IDC_FOLDER             1011
#define IDC_BROWSE             1012
#define IDC_FOLDER_STATE       1013

#define IDC_STEP2              1020
#define IDC_PARTS_ALWAYS       1021
#define IDC_CHK_AUDIO          1022
#define IDC_AUDIO_NOTE         1023
#define IDC_CHK_DESKTOP        1024

/* The page that says what installing this actually does to the game. It gets
 * a page to itself rather than a corner of the options screen: it is the one
 * thing somebody deciding whether to trust this needs to read. */
#define IDC_INTRO_TITLE        1050
#define IDC_INTRO_TEXT         1051

#define IDC_PROGRESS           1030
#define IDC_STATUS             1031
#define IDC_LOG                1032

#define IDC_INSTALL            1040
#define IDC_PLAY               1041

/* The uninstaller's window */
#define IDD_UNINSTALL          2000

#define IDC_U_HEAD_ICON        2001
#define IDC_U_HEAD_TITLE       2002
#define IDC_U_HEAD_SUB         2003
#define IDC_U_SUMMARY          2004
#define IDC_U_LIST             2005
#define IDC_U_KEEPSAVES        2006
#define IDC_U_REMOVE           2007
#define IDC_U_PROGRESS         2008
#define IDC_U_STATUS           2009

#endif /* MONSTERRPG_RESOURCE_H */
