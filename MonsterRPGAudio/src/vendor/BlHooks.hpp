
//////////////////////////////////////////////////
// RedoBlHooks Version 3.0

#ifndef _H_BLHOOKS
#define _H_BLHOOKS


// Typedefs

typedef unsigned char BYTE;
typedef unsigned int  ADDR;


// Prototypes

bool tsh_InitInternal();
bool tsh_DeinitInternal();
ADDR tsh_ScanCode(char*, char*);
ADDR tsh_ScanText(char*);
void tsh_HookFunction(ADDR, ADDR, BYTE*);
void tsh_UnhookFunction(ADDR, BYTE*);
int  tsh_PatchAllMatches(unsigned int, char*, char*, char*, bool);
void tsh_PatchByte(ADDR, BYTE);
void tsh_PatchBytes(unsigned int, ADDR, BYTE*);
void tsh_PatchInt(ADDR, int);


// Debug print settings

#ifndef TSH_NO_DEBUG_PRINT
	#define tsh_DEBUGPRINT false
#else
	#define tsh_DEBUGPRINT true
#endif


// Function short names

// Use in code when the def is not shared in a header
#define BlFunctionDef(returnType, convention, name, ...) \
	typedef returnType (convention *tsh_##name##FnT)(__VA_ARGS__); \
	tsh_##name##FnT name;
// Use in header for shared function defs when a BlFunctionDefIntern exists in code
#define BlFunctionDefExtern(returnType, convention, name, ...) \
	typedef returnType (convention *tsh_##name##FnT)(__VA_ARGS__); \
	extern tsh_##name##FnT name;
// Use in code for shared function defs when a BlFunctionDefExtern exists in header
#define BlFunctionDefIntern(name) \
	tsh_##name##FnT name;

// Scan for and assign the pattern to the variable, or err and return if not found
#define BlScanFunctionCode(target, patt, mask) \
	target = (tsh_##target##FnT)tsh_ScanCode((char*)patt, (char*)mask); \
	if(!target){ \
		BlPrintf("RedoBlHooks | Cannot find function "#target"!"); \
		return false; \
	}else{ \
		if(tsh_DEBUGPRINT) BlPrintf("RedoBlHooks | Found function "#target" at %08x", (int)target); \
	}
#define BlScanFunctionText(target, text) \
	target = (tsh_##target##FnT)tsh_ScanText((char*)text); \
	if(!target){ \
		BlPrintf("RedoBlHooks | Cannot find function "#target"!"); \
		return false; \
	}else{ \
		if(tsh_DEBUGPRINT) BlPrintf("RedoBlHooks | Found function "#target" at %08x", (int)target); \
	}
#define BlScanCode(target, patt, mask) \
	target = tsh_ScanCode((char*)patt, (char*)mask); \
	if(!target){ \
		BlPrintf("RedoBlHooks | Cannot find pattern "#target"!"); \
		return false; \
	}else{ \
		if(tsh_DEBUGPRINT) BlPrintf("RedoBlHooks | Found "#target" at %08x", (int)target); \
	}
#define BlScanText(target, text) \
	target = tsh_ScanText((char*)text); \
	if(!target){ \
		BlPrintf("RedoBlHooks | Cannot find "#target"!"); \
		return false; \
	}else{ \
		if(tsh_DEBUGPRINT) BlPrintf("RedoBlHooks | Found "#target" at %08x", (int)target); \
	}

// Use in code to define the data and functions for hooking a function
#define BlFunctionHookDef(func) \
	BYTE tsh_BlFunctionHook##func##Data[6]; \
	void func##HookOn(){ tsh_HookFunction((ADDR)func, (ADDR)func##Hook, tsh_BlFunctionHook##func##Data); } \
	void func##HookOff(){ tsh_UnhookFunction((ADDR)func, tsh_BlFunctionHook##func##Data); }
// Use in code to initialize the hook once
#define BlFunctionHookInit(func) \
	tsh_DeprotectAddress(6, (ADDR)func); \
	func##HookOn();

// Replace all matches of the pattern with the given byte string
#define BlPatchAllMatchesCode(len, patt, mask, repl) \
	tsh_PatchAllMatchesCode((ADDR)len, (char*)patt, (char*)mask, (char*)repl, tsh_DEBUGPRINT);
#define BlPatchAllMatchesText(len, text, repl) \
	tsh_PatchAllMatchesCode((ADDR)len, (char*)text, (char*)repl, tsh_DEBUGPRINT);

// Deprotect and replace one byte
#define BlPatchByte(addr, byte) \
	tsh_PatchByte((ADDR)addr, (BYTE)byte);
// Deprotect and replace a byte string
#define BlPatchBytes(len, addr, repl) \
	tsh_PatchBytes((ADDR)len, (ADDR)addr, (BYTE*)repl);

// BlPrintf(char* format, ...)
#define BlPrintf(...) if(tsh_BlPrintf) { tsh_BlPrintf(__VA_ARGS__); }
// BlHooksInit() -> bool: success
#define BlHooksInit() if(!tsh_InitInternal()) { BlPrintf("BlHooksInit failed"); return false; }
// BlHooksDeinit() -> bool: success
#define BlHooksDeinit() if(!tsh_DeinitInternal()) { BlPrintf("BlHooksDeinit failed"); return false; }


// Scanned structures

BlFunctionDefExtern(void, , tsh_BlPrintf, const char*, ...);


#endif
