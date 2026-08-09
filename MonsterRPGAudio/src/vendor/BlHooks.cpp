
//////////////////////////////////////////////////
// RedoBlHooks Version 3.0


// Includes

#include <Windows.h>
#include <Psapi.h>
#include <map>

#include "BlHooks.hpp"
#include <cstring>   // <--- ADD THIS LINE for strlen, memcpy
#include <cstdarg>   // <--- ADD THIS LINE for va_start, va_end, etc.


// Scanned structures

BlFunctionDefIntern(tsh_BlPrintf);


// Sig Scanning

ADDR ImageBase;
ADDR ImageSize;

void tsh_i_InitScanner(){
	HMODULE module = GetModuleHandle(NULL);
	if(module) {
		MODULEINFO info;
		GetModuleInformation(GetCurrentProcess(), module, &info, sizeof(MODULEINFO));
		ImageBase = (ADDR)info.lpBaseOfDll;
		ImageSize = info.SizeOfImage;
	}
}

bool tsh_i_CompareData(BYTE *data, BYTE *pattern, char* mask){
	for (; *mask; ++data, ++pattern, ++mask){
		if (*mask=='x' && *data!=*pattern)
			return false;
	}
	return (*mask)==0;
}

ADDR tsh_i_FindPattern(ADDR imageBase, ADDR imageSize, BYTE *pattern, char *mask){
	for (ADDR i=imageBase; i < imageBase+imageSize; i++){
		if(tsh_i_CompareData((PBYTE)i, pattern, mask)){
			return i;
		}
	}
	return 0;
}

// Convert a text-style pattern into code-style
void tsh_i_PatternTextToCode(char* text, char** opatt, char** omask) {
	unsigned int len = strlen(text);
	char* patt = (char*)malloc(len);
	char* mask = (char*)malloc(len);
	
	int outidx = 0;
	int val = 0;
	bool uk = false;
	for(unsigned int i=0; i<len; i++){
		char c = text[i];
		if(c=='?'){
			uk = true;
		}else if(c>='0' && c<='9'){
			val = (val<<4) + (c-'0');
		}else if(c>='A' && c<='F'){
			val = (val<<4) + (c-'A'+10);
		}else if(c>='a' && c<='f'){
			val = (val<<4) + (c-'a'+10);
		}else if(c==' '){
			patt[outidx] = uk ? 0 : val;
			mask[outidx] = uk ? '?' : 'x';
			val = 0;
			uk = false;
			outidx++;
		}
	}
	
	patt[outidx] = uk ? 0 : val;
	mask[outidx] = uk ? '?' : 'x';
	outidx++;
	patt[outidx] = 0;
	mask[outidx] = 0;
	
	*opatt = patt;
	*omask = mask;
}


// Public functions for sig scanning

// Scan using code-style pattern
ADDR tsh_ScanCode(char* pattern, char* mask) {
	return tsh_i_FindPattern(ImageBase, ImageSize-strlen(mask), (BYTE*)pattern, mask);
}

// Scan using a text-style pattern
ADDR tsh_ScanText(char* text) {
	char* patt;
	char* mask;
	tsh_i_PatternTextToCode(text, &patt, &mask);
	
	ADDR res = tsh_ScanCode(patt, mask);
	
	free(patt);
	free(mask);
	
	return res;
}


// Call Patching and Hooking

// Remove protection from address
//std::map<ADDR, std::pair<ADDR, ADDR>> tsh_DeprotectedAddresses;

void tsh_DeprotectAddress(ADDR length, ADDR location) {
	DWORD oldProtection;
	VirtualProtect((void*)location, length, PAGE_EXECUTE_READWRITE, &oldProtection);
	//tsh_DeprotectedAddresses[location] = {length, oldProtection};
}

// Patch a string of bytes by deprotecting and then overwriting
void tsh_PatchBytes(ADDR length, ADDR location, BYTE* repl) {
	tsh_DeprotectAddress(length, location);
	memcpy((void*)location, (void*)repl, (size_t)length);
}

void tsh_PatchByte(ADDR location, BYTE value) {
	tsh_PatchBytes(location, 1, &value);
}

void tsh_ReplaceInt(ADDR addr, int rval) {
	tsh_PatchBytes(4, addr, (BYTE*)(&rval));
}

int tsh_i_CallOffset(ADDR instr, ADDR func) {
	return func - (instr+4);
}

void tsh_i_ReplaceCall(ADDR instr, ADDR target) {
	tsh_ReplaceInt(instr, tsh_i_CallOffset(instr, target));
}

void tsh_i_PatchCopy(ADDR dest, ADDR src, unsigned int len) {
	for(unsigned int i=0; i<len; i++){
		tsh_PatchByte(dest+i, *((BYTE*)(src+i)));
	}
}

void tsh_HookFunction(ADDR victim, ADDR detour, BYTE* origbytes) {
	memcpy(origbytes, (BYTE*)victim, 6); //save old data
	
	*(BYTE*)victim = 0xE9; //call
	*(ADDR*)(victim+1) = (detour - (victim+5)); // call offset
	*(BYTE*)(victim+5) = 0xC3; //retn
}

void tsh_UnhookFunction(ADDR victim, BYTE* origbytes){
	tsh_i_PatchCopy(victim, (ADDR)origbytes, 6); //restore old data
}

int tsh_PatchAllMatchesCode(ADDR len, char* patt, char* mask, char* replace, bool debugprint){
	int numpatched = 0;
	for(ADDR i=ImageBase; i<ImageBase+ImageSize-len; i++){
		if(tsh_i_CompareData((BYTE*)i, (BYTE*)patt, mask)){
			if(debugprint) BlPrintf("RedoBlHooks: Patching call at %08x", i);
			
			numpatched++;
			tsh_DeprotectAddress(i, len);
			for(ADDR c=0; c<len; c++){
				tsh_PatchByte(i+c, replace[c]);
			}
		}
	}
	return numpatched;
}

int tsh_PatchAllMatchesHex(ADDR len, char* text, char* replace, bool debugprint) {
	char* patt;
	char* mask;
	tsh_i_PatternTextToCode(text, &patt, &mask);
	
	int res = tsh_PatchAllMatchesCode(len, patt, mask, replace, debugprint);
	
	free(patt);
	free(mask);
	
	return res;
}


// Initialization

bool tsh_InitInternal(){
	tsh_i_InitScanner();
	
	BlScanFunctionText(tsh_BlPrintf, "8D 44 24 08 33 D2 50 FF 74 24 08 33 C9 E8 ? ? ? ? 83 C4 08 C3");
	
	return true;
}

bool tsh_DeinitInternal() {
	return true;
}

