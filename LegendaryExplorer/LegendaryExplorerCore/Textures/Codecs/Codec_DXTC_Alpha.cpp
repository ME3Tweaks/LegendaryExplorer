//===============================================================================
// Copyright (c) 2017 Pawel Kolodziejski <aquadran at users.sourceforge.net>
// Copyright (c) 2007-2016  Advanced Micro Devices, Inc. All rights reserved.
// Copyright (c) 2004-2006 ATI Technologies Inc.
//===============================================================================
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
//////////////////////////////////////////////////////////////////////////////

#include "Common.h"
#include "CompressonatorXCodec.h"

static void EncodeAlphaBlock(CMP_DWORD compressedBlock[2], CMP_BYTE nEndpoints[2], CMP_BYTE nIndices[BLOCK_SIZE_4X4])
{
    compressedBlock[0] = ((int)nEndpoints[0]) | (((int)nEndpoints[1])<<8);
    compressedBlock[1] = 0;

    for(int i = 0; i < BLOCK_SIZE_4X4; i++)
    {
        if(i < 5)
            compressedBlock[0] |= (nIndices[i] & 0x7) << (16 + (i * 3));
        else if(i > 5)
            compressedBlock[1] |= (nIndices[i] & 0x7) << (2 + (i-6) * 3);
        else
        {
            compressedBlock[0] |= (nIndices[i] & 0x1) << 31;
            compressedBlock[1] |= (nIndices[i] & 0x6) >> 1;
        }
    }
}

#ifdef __cplusplus
extern "C" {
#endif

CMP_API void CompressAlphaBlock(CMP_BYTE alphaBlock[BLOCK_SIZE_4X4], CMP_DWORD compressedBlock[2])
{
	CMP_BYTE nEndpoints[2][2];
	CMP_BYTE nIndices[2][BLOCK_SIZE_4X4];
	float fError8 = CompBlock1X(alphaBlock, BLOCK_SIZE_4X4, nEndpoints[0], nIndices[0], 8, false, true, 8, 0, true);
	float fError6 = (fError8 == 0.f) ? FLT_MAX : CompBlock1X(alphaBlock, BLOCK_SIZE_4X4, nEndpoints[1], nIndices[1], 6, true, true, 8, 0, true);
	if (fError8 <= fError6)
		EncodeAlphaBlock(compressedBlock, nEndpoints[0], nIndices[0]);
	else
		EncodeAlphaBlock(compressedBlock, nEndpoints[1], nIndices[1]);
}

#ifdef __cplusplus
};
#endif

static void GetCompressedAlphaRamp(CMP_BYTE alpha[8], CMP_DWORD compressedBlock[2])
{
	alpha[0] = (CMP_BYTE)(compressedBlock[0] & 0xff);
	alpha[1] = (CMP_BYTE)((compressedBlock[0] >> 8) & 0xff);

	if (alpha[0] > alpha[1])
	{
		// 8-alpha block:  derive the other six alphas.
		// Bit code 000 = alpha_0, 001 = alpha_1, others are interpolated.
		alpha[2] = static_cast<CMP_BYTE>((6 * alpha[0] + 1 * alpha[1] + 3) / 7);    // bit code 010
		alpha[3] = static_cast<CMP_BYTE>((5 * alpha[0] + 2 * alpha[1] + 3) / 7);    // bit code 011
		alpha[4] = static_cast<CMP_BYTE>((4 * alpha[0] + 3 * alpha[1] + 3) / 7);    // bit code 100
		alpha[5] = static_cast<CMP_BYTE>((3 * alpha[0] + 4 * alpha[1] + 3) / 7);    // bit code 101
		alpha[6] = static_cast<CMP_BYTE>((2 * alpha[0] + 5 * alpha[1] + 3) / 7);    // bit code 110
		alpha[7] = static_cast<CMP_BYTE>((1 * alpha[0] + 6 * alpha[1] + 3) / 7);    // bit code 111
	}
	else
	{
		// 6-alpha block.
		// Bit code 000 = alpha_0, 001 = alpha_1, others are interpolated.
		alpha[2] = static_cast<CMP_BYTE>((4 * alpha[0] + 1 * alpha[1] + 2) / 5);  // Bit code 010
		alpha[3] = static_cast<CMP_BYTE>((3 * alpha[0] + 2 * alpha[1] + 2) / 5);  // Bit code 011
		alpha[4] = static_cast<CMP_BYTE>((2 * alpha[0] + 3 * alpha[1] + 2) / 5);  // Bit code 100
		alpha[5] = static_cast<CMP_BYTE>((1 * alpha[0] + 4 * alpha[1] + 2) / 5);  // Bit code 101
		alpha[6] = 0;                                      // Bit code 110
		alpha[7] = 255;                                    // Bit code 111
	}
}

#ifdef __cplusplus
extern "C" {
#endif

//
// This function decompresses a block
//
CMP_API void DecompressAlphaBlock(CMP_BYTE alphaBlock[BLOCK_SIZE_4X4], CMP_DWORD compressedBlock[2])
{
    CMP_BYTE alpha[8];
    GetCompressedAlphaRamp(alpha, compressedBlock);

    for(int i = 0; i < BLOCK_SIZE_4X4; i++)
    {
        CMP_DWORD index;
        if(i < 5)
            index = (compressedBlock[0] & (0x7 << (16 + (i * 3)))) >> (16 + (i * 3));
        else if(i > 5)
            index = (compressedBlock[1] & (0x7 << (2 + (i-6)*3))) >> (2 + (i-6)*3);
        else
        {
            index = (compressedBlock[0] & 0x80000000) >> 31;
            index |= (compressedBlock[1] & 0x3) << 1;
        }

        alphaBlock[i] = alpha[index];
    }
}

#ifdef __cplusplus
};
#endif

#define EXPLICIT_ALPHA_PIXEL_MASK 0xf
#define EXPLICIT_ALPHA_PIXEL_BPP 4
void CompressExplicitAlphaBlock(CMP_BYTE alphaBlock[BLOCK_SIZE_4X4], CMP_DWORD compressedBlock[2])
{
	int i;
	compressedBlock[0] = compressedBlock[1] = 0;
	for (i = 0; i < 16; i++)
	{
		int v = alphaBlock[i];
		v = (v + 7 - (v >> 4));
		v >>= 4;
		if (v < 0)
			v = 0;
		if (v > 0xf)
			v = 0xf;
		if (i < 8)
			compressedBlock[0] |= v << (4 * i);
		else
			compressedBlock[1] |= v << (4 * (i - 8));
	}
}

//
// This function decompresses an explicit alpha block (DXT3)
//
void DecompressExplicitAlphaBlock(CMP_BYTE alphaBlock[BLOCK_SIZE_4X4], CMP_DWORD compressedBlock[2])
{
    for(int i=0;i<16;i++)
    {
        int nBlock = i < 8 ? 0 : 1;
        CMP_BYTE cAlpha = (CMP_BYTE) ((compressedBlock[nBlock] >> ((i % 8) * EXPLICIT_ALPHA_PIXEL_BPP)) & EXPLICIT_ALPHA_PIXEL_MASK);
        alphaBlock[i] = (CMP_BYTE) ((cAlpha << EXPLICIT_ALPHA_PIXEL_BPP) | cAlpha);
    }
}
