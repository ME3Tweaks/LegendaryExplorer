#include <windows.h>
#include <stdio.h>
#pragma pack(1)

HINSTANCE t_hInst;
HINSTANCE hLThis = 0;
HINSTANCE hL = 0;
FARPROC p[72] = { 0 };

BYTE pattern[] = {
	0x8B, 0x11, 0x8B, 0x42, 0x0C,
	0x57, 0x56, 0xFF, 0xD0,
	0x8B, 0xC3, // <-- move eax,ebx; offset 0x9; will be replaced with 0xB0 0x01 to get mov al,1;
	0x8B, 0x4D, 0xF4, 0x64,
	0x89, 0x0D, 0x00, 0x00, 0x00,
	0x00, 0x59, 0x5F, 0x5E, 0x5B,
	0x8B, 0xE5, 0x5D, 0xC2, 0x08,
	0x00, 0xCC, 0xCC, 0xCC, 0x8B,
	0x41, 0x04, 0x56, 0x85, 0xC0
};

BYTE pattern2[] = {
	0x8B, 0x45, 0x0C,                       // mov     eax, [ebp+arg_4]
	0xC7, 0x00, 0x01, 0x00, 0x00, 0x00,     // mov     dword ptr [eax], 1
	0x5D,                                   // pop     ebp
	0xC2, 0x08, 0x00,                       // retn    8
	0x8B, 0x4D, 0x0C,                       // mov     ecx, [ebp+arg_4]
	0xC7, 0x01, 0x01, 0x00, 0x00, 0x00,     // mov     dword ptr [ecx], 1
	0x5D,                                   // pop     ebp
	0xC2, 0x08, 0x00,                       // retn    8
	0xCC, 0xCC, 0xCC, 0xCC, 0xCC
};


bool DataCompare(const BYTE* OpCodes, const BYTE* Mask, const char* StrMask)
{
	while (*StrMask)
	{
		if(*StrMask == 'x' && *OpCodes != *Mask )
		{
			return false;
		}
		
		++StrMask;
		++OpCodes;
		++Mask;
	}
	
	return true;
}

DWORD FindPattern(DWORD StartAddress, DWORD CodeLen, BYTE* Mask, char* StrMask, unsigned short ignore)
{
	unsigned short Ign = 0;
	DWORD i = 0;

	while (Ign <= ignore)
	{
		if(DataCompare((BYTE*)(StartAddress + i++), Mask,StrMask))
		{
			++Ign;
		}
		else if (i>=CodeLen)
		{
			return 0;
		}
	}

	return StartAddress + i - 1;
}

void GetAdresses()
{
	hLThis = t_hInst;
	hL = LoadLibrary(L"binkw32.dll.bak");

	p[0] = GetProcAddress(hL,"_BinkBufferBlit@12");
	p[1] = GetProcAddress(hL,"_BinkBufferCheckWinPos@12");
	p[2] = GetProcAddress(hL,"_BinkBufferClear@8");
	p[3] = GetProcAddress(hL,"_BinkBufferClose@4");
	p[4] = GetProcAddress(hL,"_BinkBufferGetDescription@4");
	p[5] = GetProcAddress(hL,"_BinkBufferGetError@0");
	p[6] = GetProcAddress(hL,"_BinkBufferLock@4");
	p[7] = GetProcAddress(hL,"_BinkBufferOpen@16");
	p[8] = GetProcAddress(hL,"_BinkBufferSetDirectDraw@8");
	p[9] = GetProcAddress(hL,"_BinkBufferSetHWND@8");
	p[10] = GetProcAddress(hL,"_BinkBufferSetOffset@12");
	p[11] = GetProcAddress(hL,"_BinkBufferSetResolution@12");
	p[12] = GetProcAddress(hL,"_BinkBufferSetScale@12");
	p[13] = GetProcAddress(hL,"_BinkBufferUnlock@4");
	p[14] = GetProcAddress(hL,"_BinkCheckCursor@20");
	p[15] = GetProcAddress(hL,"_BinkClose@4");
	p[16] = GetProcAddress(hL,"_BinkCloseTrack@4");
	p[17] = GetProcAddress(hL,"_BinkControlBackgroundIO@8");
	p[18] = GetProcAddress(hL,"_BinkControlPlatformFeatures@8");
	p[19] = GetProcAddress(hL,"_BinkCopyToBuffer@28");
	p[20] = GetProcAddress(hL,"_BinkCopyToBufferRect@44");
	p[21] = GetProcAddress(hL,"_BinkDDSurfaceType@4");
	p[22] = GetProcAddress(hL,"_BinkDX8SurfaceType@4");
	p[23] = GetProcAddress(hL,"_BinkDX9SurfaceType@4");
	p[24] = GetProcAddress(hL,"_BinkDoFrame@4");
	p[25] = GetProcAddress(hL,"_BinkDoFrameAsync@12");
	p[26] = GetProcAddress(hL,"_BinkDoFrameAsyncWait@8");
	p[27] = GetProcAddress(hL,"_BinkDoFramePlane@8");
	p[28] = GetProcAddress(hL,"_BinkGetError@0");
	p[29] = GetProcAddress(hL,"_BinkGetFrameBuffersInfo@8");
	p[30] = GetProcAddress(hL,"_BinkGetKeyFrame@12");
	p[31] = GetProcAddress(hL,"_BinkGetPalette@4");
	p[32] = GetProcAddress(hL,"_BinkGetRealtime@12");
	p[33] = GetProcAddress(hL,"_BinkGetRects@8");
	p[34] = GetProcAddress(hL,"_BinkGetSummary@8");
	p[35] = GetProcAddress(hL,"_BinkGetTrackData@8");
	p[36] = GetProcAddress(hL,"_BinkGetTrackID@8");
	p[37] = GetProcAddress(hL,"_BinkGetTrackMaxSize@8");
	p[38] = GetProcAddress(hL,"_BinkGetTrackType@8");
	p[39] = GetProcAddress(hL,"_BinkGoto@12");
	p[40] = GetProcAddress(hL,"_BinkIsSoftwareCursor@8");
	p[41] = GetProcAddress(hL,"_BinkLogoAddress@0");
	p[42] = GetProcAddress(hL,"_BinkNextFrame@4");
	p[43] = GetProcAddress(hL,"_BinkOpen@8");
	p[44] = GetProcAddress(hL,"_BinkOpenDirectSound@4");
	p[45] = GetProcAddress(hL,"_BinkOpenMiles@4");
	p[46] = GetProcAddress(hL,"_BinkOpenTrack@8");
	p[47] = GetProcAddress(hL,"_BinkOpenWaveOut@4");
	p[48] = GetProcAddress(hL,"_BinkPause@8");
	p[49] = GetProcAddress(hL,"_BinkRegisterFrameBuffers@8");
	p[50] = GetProcAddress(hL,"_BinkRequestStopAsyncThread@4");
	p[51] = GetProcAddress(hL,"_BinkRestoreCursor@4");
	p[52] = GetProcAddress(hL,"_BinkService@4");
	p[53] = GetProcAddress(hL,"_BinkSetError@4");
	p[54] = GetProcAddress(hL,"_BinkSetFrameRate@8");
	p[55] = GetProcAddress(hL,"_BinkSetIO@4");
	p[56] = GetProcAddress(hL,"_BinkSetIOSize@4");
	p[57] = GetProcAddress(hL,"_BinkSetMemory@8");
	p[58] = GetProcAddress(hL,"_BinkSetMixBinVolumes@20");
	p[59] = GetProcAddress(hL,"_BinkSetMixBins@16");
	p[60] = GetProcAddress(hL,"_BinkSetPan@12");
	p[61] = GetProcAddress(hL,"_BinkSetSimulate@4");
	p[62] = GetProcAddress(hL,"_BinkSetSoundOnOff@8");
	p[63] = GetProcAddress(hL,"_BinkSetSoundSystem@8");
	p[64] = GetProcAddress(hL,"_BinkSetSoundTrack@8");
	p[65] = GetProcAddress(hL,"_BinkSetVideoOnOff@8");
	p[66] = GetProcAddress(hL,"_BinkSetVolume@12");
	p[67] = GetProcAddress(hL,"_BinkShouldSkip@4");
	p[68] = GetProcAddress(hL,"_BinkStartAsyncThread@8");
	p[69] = GetProcAddress(hL,"_BinkWait@4");
	p[70] = GetProcAddress(hL,"_BinkWaitStopAsyncThread@4");
	p[71] = GetProcAddress(hL,"_RADTimerRead@0");
}

DWORD WINAPI Start(LPVOID lpParam)
{
	//FILE* Log;
	//fopen_s ( &Log, "binkw32log.txt", "w" );
	//fprintf(Log, "Autopatcher by Warranty Voider\n");

	GetAdresses();

	//fprintf(Log, "Got adresses\n");

	DWORD patch1, patch2;

	int count = 0;

	while((patch1 = FindPattern(0x401000, 0xE52000, pattern, "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 0)) == 0 && count++ < 10)
	{
		//fprintf(Log, "Trying again...\n");
		Sleep(300);
	}

	if(patch1 != 0)
	{
		//fprintf(Log, "Patch position 1: 0x%x\n", patch1);

		DWORD dwProtect;
		VirtualProtect( (void*)(patch1 + 9), 0x2, PAGE_READWRITE, &dwProtect );
		BYTE* p = (BYTE *)(patch1 + 9);
		*p++ = 0xB0;
		*p = 0x01;
		VirtualProtect( (void*)(patch1 + 9), 0x2, dwProtect, &dwProtect );

		//fprintf(Log, "Patch position 1: Patched\n");
	}

	count = 0;

	while((patch2 = FindPattern(0x401000, 0xE52000, pattern2, "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 0)) == 0 && count++ < 10)
	{
		//fprintf(Log, "Trying again...\n");
		Sleep(300);
	}

	if(patch2 != 0)
	{
		//fprintf(Log, "Patch position 2: 0x%x\n", patch2);
		
		DWORD dwProtect;
		VirtualProtect((void*)patch2, 0x16, PAGE_READWRITE, &dwProtect);
		
		BYTE* p = (BYTE*)(patch2 + 5);
		*p++ = 0;
		*p++ = 0;
		*p++ = 0;
		*p = 0;
		
		p = (BYTE *)(patch2 + 0x12);
		*p++ = 0;
		*p++ = 0;
		*p++ = 0;
		*p = 0;
		
		VirtualProtect((void*)patch2, 0x16, dwProtect, &dwProtect);
		
		//fprintf(Log, "Patch position 2: Patched\n");
	}

	fclose ( Log );

	return 0;
}

BOOL WINAPI DllMain(HINSTANCE hInst,DWORD reason,LPVOID)
{
	if (reason == DLL_PROCESS_ATTACH)
	{
		t_hInst = hInst;
		DWORD dwThreadId, dwThrdParam = 1;
		HANDLE hThread;
		hThread = CreateThread(NULL,0, Start, &dwThrdParam, 0, &dwThreadId);
	}

	if (reason == DLL_PROCESS_DETACH)
	{
		FreeLibrary(hL);
	}

	return 1;
}

// _BinkBufferBlit@12
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkBufferBlit(int a1, int a2, int a3)
{
	__asm
	{
		jmp p[0*4];
	}
}

// _BinkBufferCheckWinPos@12
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkBufferCheckWinPos(int a1, int a2, int a3)
{
	__asm
	{
		jmp p[1*4];
	}
}

// _BinkBufferClear@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkBufferClear(int a1, int a2)
{
	__asm
	{
		jmp p[2*4];
	}
}

// _BinkBufferClose@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkBufferClose(int a1)
{
	__asm
	{
		jmp p[3*4];
	}
}

// _BinkBufferGetDescription@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkBufferGetDescription(int a1)
{
	__asm
	{
		jmp p[4*4];
	}
}

// _BinkBufferGetError@0
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkBufferGetError()
{
	__asm
	{
		jmp p[5*4];
	}
}

// _BinkBufferLock@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkBufferLock(int a1)
{
	__asm
	{
		jmp p[6*4];
	}
}

// _BinkBufferOpen@16
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkBufferOpen(int a1,int a2,int a3,int a4)
{
	__asm
	{
		jmp p[7*4];
	}
}

// _BinkBufferSetDirectDraw@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkBufferSetDirectDraw(int a1, int a2)
{
	__asm
	{
		jmp p[8*4];
	}
}

// _BinkBufferSetHWND@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkBufferSetHWND(int a1, int a2)
{
	__asm
	{
		jmp p[9*4];
	}
}

// _BinkBufferSetOffset@12
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkBufferSetOffset(int a1, int a2, int a3)
{
	__asm
	{
		jmp p[10*4];
	}
}

// _BinkBufferSetResolution@12
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkBufferSetResolution(int a1, int a2, int a3)
{
	__asm
	{
		jmp p[11*4];
	}
}

// _BinkBufferSetScale@12
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkBufferSetScale(int a1, int a2, int a3)
{
	__asm
	{
		jmp p[12*4];
	}
}

// _BinkBufferUnlock@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkBufferUnlock(int a1)
{
	__asm
	{
		jmp p[13*4];
	}
}

// _BinkCheckCursor@20
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkCheckCursor(int a1,int a2,int a3,int a4,int a5)
{
	__asm
	{
		jmp p[14*4];
	}
}

// _BinkClose@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkClose(int a1)
{
	__asm
	{
		jmp p[15*4];
	}
}

// _BinkCloseTrack@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkCloseTrack(int a1)
{
	__asm
	{
		jmp p[16*4];
	}
}

// _BinkControlBackgroundIO@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkControlBackgroundIO(int a1, int a2)
{
	__asm
	{
		jmp p[17*4];
	}
}

// _BinkControlPlatformFeatures@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkControlPlatformFeatures(int a1, int a2)
{
	__asm
	{
		jmp p[18*4];
	}
}

// _BinkCopyToBuffer@28
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkCopyToBuffer(int a1,int a2,int a3,int a4,int a5,int a6,int a7)
{
	__asm
	{
		jmp p[19*4];
	}
}

// _BinkCopyToBufferRect@44
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkCopyToBufferRect(int a1,int a2,int a3,int a4,int a5,int a6,int a7,int a8,int a9,int a10,int a11)
{
	__asm
	{
		jmp p[20*4];
	}
}

// _BinkDDSurfaceType@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkDDSurfaceType(int a1)
{
	__asm
	{
		jmp p[21*4];
	}
}

// _BinkDX8SurfaceType@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkDX8SurfaceType(int a1)
{
	__asm
	{
		jmp p[22*4];
	}
}

// _BinkDX9SurfaceType@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkDX9SurfaceType(int a1)
{
	__asm
	{
		jmp p[23*4];
	}
}

// _BinkDoFrame@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkDoFrame(int a1)
{
	__asm
	{
		jmp p[24*4];
	}
}

// _BinkDoFrameAsync@12
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkDoFrameAsync(int a1, int a2, int a3)
{
	__asm
	{
		jmp p[25*4];
	}
}

// _BinkDoFrameAsyncWait@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkDoFrameAsyncWait(int a1, int a2)
{
	__asm
	{
		jmp p[26*4];
	}
}

// _BinkDoFramePlane@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkDoFramePlane(int a1, int a2)
{
	__asm
	{
		jmp p[27*4];
	}
}

// _BinkGetError@0
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkGetError()
{
	__asm
	{
		jmp p[28*4];
	}
}

// _BinkGetFrameBuffersInfo@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkGetFrameBuffersInfo(int a1, int a2)
{
	__asm
	{
		jmp p[29*4];
	}
}

// _BinkGetKeyFrame@12
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkGetKeyFrame(int a1, int a2, int a3)
{
	__asm
	{
		jmp p[30*4];
	}
}

// _BinkGetPalette@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkGetPalette(int a1)
{
	__asm
	{
		jmp p[31*4];
	}
}

// _BinkGetRealtime@12
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkGetRealtime(int a1, int a2, int a3)
{
	__asm
	{
		jmp p[32*4];
	}
}

// _BinkGetRects@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkGetRects(int a1, int a2)
{
	__asm
	{
		jmp p[33*4];
	}
}

// _BinkGetSummary@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkGetSummary(int a1, int a2)
{
	__asm
	{
		jmp p[34*4];
	}
}

// _BinkGetTrackData@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkGetTrackData(int a1, int a2)
{
	__asm
	{
		jmp p[35*4];
	}
}

// _BinkGetTrackID@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkGetTrackID(int a1, int a2)
{
	__asm
	{
		jmp p[36*4];
	}
}

// _BinkGetTrackMaxSize@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkGetTrackMaxSize(int a1, int a2)
{
	__asm
	{
		jmp p[37*4];
	}
}

// _BinkGetTrackType@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkGetTrackType(int a1, int a2)
{
	__asm
	{
		jmp p[38*4];
	}
}

// _BinkGoto@12
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkGoto(int a1, int a2, int a3)
{
	__asm
	{
		jmp p[39*4];
	}
}

// _BinkIsSoftwareCursor@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkIsSoftwareCursor(int a1, int a2)
{
	__asm
	{
		jmp p[40*4];
	}
}

// _BinkLogoAddress@0
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkLogoAddress()
{
	__asm
	{
		jmp p[41*4];
	}
}

// _BinkNextFrame@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkNextFrame(int a1)
{
	__asm
	{
		jmp p[42*4];
	}
}

// _BinkOpen@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkOpen(int a1, int a2)
{
	__asm
	{
		jmp p[43*4];
	}
}

// _BinkOpenDirectSound@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkOpenDirectSound(int a1)
{
	__asm
	{
		jmp p[44*4];
	}
}

// _BinkOpenMiles@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkOpenMiles(int a1)
{
	__asm
	{
		jmp p[45*4];
	}
}

// _BinkOpenTrack@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkOpenTrack(int a1, int a2)
{
	__asm
	{
		jmp p[46*4];
	}
}

// _BinkOpenWaveOut@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkOpenWaveOut(int a1)
{
	__asm
	{
		jmp p[47*4];
	}
}

// _BinkPause@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkPause(int a1, int a2)
{
	__asm
	{
		jmp p[48*4];
	}
}

// _BinkRegisterFrameBuffers@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkRegisterFrameBuffers(int a1, int a2)
{
	__asm
	{
		jmp p[49*4];
	}
}

// _BinkRequestStopAsyncThread@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkRequestStopAsyncThread(int a1)
{
	__asm
	{
		jmp p[50*4];
	}
}

// _BinkRestoreCursor@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkRestoreCursor(int a1)
{
	__asm
	{
		jmp p[51*4];
	}
}

// _BinkService@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkService(int a1)
{
	__asm
	{
		jmp p[52*4];
	}
}

// _BinkSetError@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkSetError(int a1)
{
	__asm
	{
		jmp p[53*4];
	}
}

// _BinkSetFrameRate@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkSetFrameRate(int a1, int a2)
{
	__asm
	{
		jmp p[54*4];
	}
}

// _BinkSetIO@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkSetIO(int a1)
{
	__asm
	{
		jmp p[55*4];
	}
}

// _BinkSetIOSize@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkSetIOSize(int a1)
{
	__asm
	{
		jmp p[56*4];
	}
}

// _BinkSetMemory@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkSetMemory(int a1, int a2)
{
	__asm
	{
		jmp p[57*4];
	}
}

// _BinkSetMixBinVolumes@20
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkSetMixBinVolumes(int a1,int a2,int a3,int a4,int a5)
{
	__asm
	{
		jmp p[58*4];
	}
}

// _BinkSetMixBins@16
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkSetMixBins(int a1,int a2,int a3,int a4)
{
	__asm
	{
		jmp p[59*4];
	}
}

// _BinkSetPan@12
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkSetPan(int a1, int a2, int a3)
{
	__asm
	{
		jmp p[60*4];
	}
}

// _BinkSetSimulate@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkSetSimulate(int a1)
{
	__asm
	{
		jmp p[61*4];
	}
}

// _BinkSetSoundOnOff@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkSetSoundOnOff(int a1, int a2)
{
	__asm
	{
		jmp p[62*4];
	}
}

// _BinkSetSoundSystem@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkSetSoundSystem(int a1, int a2)
{
	__asm
	{
		jmp p[63*4];
	}
}

// _BinkSetSoundTrack@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkSetSoundTrack(int a1, int a2)
{
	__asm
	{
		jmp p[64*4];
	}
}

// _BinkSetVideoOnOff@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkSetVideoOnOff(int a1, int a2)
{
	__asm
	{
		jmp p[65*4];
	}
}

// _BinkSetVolume@12
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkSetVolume(int a1, int a2, int a3)
{
	__asm
	{
		jmp p[66*4];
	}
}

// _BinkShouldSkip@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkShouldSkip(int a1)
{
	__asm
	{
		jmp p[67*4];
	}
}

// _BinkStartAsyncThread@8
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkStartAsyncThread(int a1, int a2)
{
	__asm
	{
		jmp p[68*4];
	}
}

// _BinkWait@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkWait(int a1)
{
	__asm
	{
		jmp p[69*4];
	}
}

// _BinkWaitStopAsyncThread@4
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall BinkWaitStopAsyncThread(int a1)
{
	__asm
	{
		jmp p[70*4];
	}
}

// _RADTimerRead@0
extern "C" __declspec(dllexport) __declspec(naked) void __stdcall RADTimerRead()
{
	__asm
	{
		jmp p[71*4];
	}
}
