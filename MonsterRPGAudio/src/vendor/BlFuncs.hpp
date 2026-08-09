
//////////////////////////////////////////////////
// BlFuncs Version 1.0

#ifndef _H_BLFUNCS
#define _H_BLFUNCS

// Require BlHooks to be included before this header
#ifndef _H_BLHOOKS
	#error "BlFuncs.hpp: You must include BlHooks.hpp first"
#else

typedef const char * (*tsf_StringCallback)(ADDR, signed int, const char *[]);
typedef signed int   (*tsf_IntCallback   )(ADDR, signed int, const char *[]);
typedef float        (*tsf_FloatCallback )(ADDR, signed int, const char *[]);
typedef void         (*tsf_VoidCallback  )(ADDR, signed int, const char *[]);
typedef bool         (*tsf_BoolCallback  )(ADDR, signed int, const char *[]);

/* These functions are used for tsf_BlCon__executefSimObj.
   They refer to a special buffer for the argument stack.
   For tsf_BlCon__executef, you need to use your own buffers. */
char *tsf_GetIntArg(int);
char *tsf_GetFloatArg(float);
char *tsf_ScriptThis(ADDR);

const char *tsf_Eval(const char *);
const char *tsf_Evalf(const char *, ...);

ADDR tsf_FindObject(unsigned int);
ADDR tsf_FindObject(const char *);

ADDR tsf_LookupNamespace(const char *, const char *);

const char *tsf_GetDataField(ADDR, const char *, const char *);
void tsf_SetDataField(ADDR, const char *, const char *, const char *);

const char *tsf_GetVar(const char *);

void tsf_AddVarInternal(const char *, signed int, void *);
void tsf_AddVar(const char *, const char **);
void tsf_AddVar(const char *, signed int *);
void tsf_AddVar(const char *, float *);
void tsf_AddVar(const char *, bool *);

ADDR tsf_AddConsoleFuncInternal(const char *, const char *, const char *, signed int, const char *, signed int, signed int);
void tsf_AddConsoleFunc(const char *, const char *, const char *, tsf_StringCallback, const char *, signed int, signed int);
void tsf_AddConsoleFunc(const char *, const char *, const char *, tsf_IntCallback, const char *, signed int, signed int);
void tsf_AddConsoleFunc(const char *, const char *, const char *, tsf_FloatCallback, const char *, signed int, signed int);
void tsf_AddConsoleFunc(const char *, const char *, const char *, tsf_VoidCallback, const char *, signed int, signed int);
void tsf_AddConsoleFunc(const char *, const char *, const char *, tsf_BoolCallback, const char *, signed int, signed int);

bool tsf_InitInternal();

extern ADDR tsf_mCacheSequence;
extern ADDR tsf_mCacheAllocator;
extern ADDR tsf_gIdDictionary;
extern ADDR tsf_gEvalState_globalVars;

BlFunctionDefExtern(const char *, __stdcall, tsf_BlStringTable__insert, const char *, bool);
BlFunctionDefExtern(ADDR, __fastcall, tsf_BlNamespace__find, const char *, const char *);
BlFunctionDefExtern(ADDR, __thiscall, tsf_BlNamespace__createLocalEntry, ADDR, const char *);
BlFunctionDefExtern(void, __thiscall, tsf_BlDataChunker__freeBlocks, ADDR);
BlFunctionDefExtern(const char *, , tsf_BlCon__evaluate, ADDR, signed int, const char **);
BlFunctionDefExtern(const char *, , tsf_BlCon__executef, signed int, ...);
BlFunctionDefExtern(const char *, , tsf_BlCon__executefSimObj, ADDR *, signed int, ...);
BlFunctionDefExtern(const char *, __thiscall, tsf_BlCon__getVariable, const char *);
BlFunctionDefExtern(void, __thiscall, tsf_BlDictionary__addVariable, ADDR *, const char *, signed int, void *);
BlFunctionDefExtern(ADDR *, __thiscall, tsf_BlSim__findObject_name, const char *);
BlFunctionDefExtern(char *, __stdcall, tsf_BlStringStack__getArgBuffer, unsigned int);
BlFunctionDefExtern(const char *, __thiscall, tsf_BlSimObject__getDataField, ADDR, const char *, const char *);
BlFunctionDefExtern(void, __thiscall, tsf_BlSimObject__setDataField, ADDR, const char *, const char *, const char *);
BlFunctionDefExtern(char *, __fastcall, tsf_BlCon__getReturnBuffer, unsigned int);


// Function short names

#define BlEval tsf_Eval
#define BlEvalf tsf_Evalf

#define BlIntArg tsf_GetIntArg
#define BlFloatArg tsf_GetFloatArg
#define BlThisArg tsf_GetThisArg
#define BlStringArg(x) tsf_GetStringArg((char*)x)
#define BlReturnBuffer tsf_BlCon__getReturnBuffer

#define BlObject tsf_FindObject
#define BlNamespace tsf_LookupNamespace

#define BlGetField tsf_GetDataField
#define BlSetField tsf_SetDataField

#define BlGetVar tsf_GetVar
#define BlAddVar tsf_AddVar

#define BlAddFunction tsf_AddConsoleFunc

#define __22ND_ARGUMENT(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15, a16, a17, a18, a19, a20, a21, a22, ...) a22
#define __NUM_LIST(...) __22ND_ARGUMENT(dummy, ##__VA_ARGS__, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0)
#define BlCall(...) \
	tsf_BlCon__executef(__NUM_LIST(__VA_ARGS__), __VA_ARGS__)
#define BlCallObj(obj, ...) \
	tsf_BlCon__executefSimObj((ADDR*)obj, __NUM_LIST(__VA_ARGS__), __VA_ARGS__)

#define BlFuncsInit() if(!tsf_InitInternal()) { return false; }
#define BlFuncsDeinit() if(!tsf_DeinitInternal()) { return false; }


#endif
#endif
