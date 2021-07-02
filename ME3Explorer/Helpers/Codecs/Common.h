//===============================================================================
// Copyright (c) 2017 Pawel Kolodziejski <aquadran at users.sourceforge.net>
// Copyright (c) 2007-2014  Advanced Micro Devices, Inc. All rights reserved.
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

#ifndef COMMON_H
#define COMMON_H

#include <string>

typedef unsigned int        CMP_DWORD;         ///< A 32-bit integer format.
typedef unsigned short      CMP_WORD;          ///< A 16-bit integer format.
typedef unsigned char       CMP_BYTE;          ///< An 8-bit integer format.
typedef char                CMP_CHAR;          ///< An 8-bit char    format.
typedef float               CMP_FLOAT;         ///< A 32-bit float   format.

#define BLOCK_SIZE_4X4        16
#define BLOCK_SIZE_4X4X4      64

#define RGBA8888_CHANNEL_A    3
#define RGBA8888_CHANNEL_R    2
#define RGBA8888_CHANNEL_G    1
#define RGBA8888_CHANNEL_B    0

#define RGBA8888_OFFSET_A (RGBA8888_CHANNEL_A * 8)
#define RGBA8888_OFFSET_R (RGBA8888_CHANNEL_R * 8)
#define RGBA8888_OFFSET_G (RGBA8888_CHANNEL_G * 8)
#define RGBA8888_OFFSET_B (RGBA8888_CHANNEL_B * 8)

#define BYTE_MASK        0x00ff

#define BYTE_MAXVAL 255
#define BYTE_MAX_FLOAT 255.0f
#define CONVERT_FLOAT_TO_BYTE(f) static_cast<CMP_BYTE>(((f) * BYTE_MAX_FLOAT) + 0.5)
#define CONVERT_BYTE_TO_FLOAT(b) (b) / BYTE_MAX_FLOAT

#ifndef max
#define max(a,b)            (((a) > (b)) ? (a) : (b))
#endif

#ifndef min
#define min(a,b)            (((a) < (b)) ? (a) : (b))
#endif

#if defined(WIN32) || defined(_WIN64)
#define CMP_API __declspec(dllexport)
#else
#define CMP_API
#endif

#endif // !COMMON_H
