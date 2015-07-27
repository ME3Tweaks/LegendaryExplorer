#define _WIN32_WINNT 0x0501
#include <windows.h>
#include "lzo1x.h"

BOOL WINAPI DllMain(HINSTANCE hin, DWORD reason, LPVOID lpvReserved) { return TRUE; }

#define LZO_EXPORT __declspec(dllexport)

#define HEAP_ALLOC(var, size) \
	lzo_align_t __LZO_MMODEL var [ ((size) + (sizeof(lzo_align_t) - 1)) / sizeof(lzo_align_t) ]

static HEAP_ALLOC(wrkmem, LZO1X_999_MEM_COMPRESS);

LZO_EXPORT int LZODecompress(unsigned char *src, unsigned int src_len, unsigned char *dst, unsigned int *dst_len)
{
	int status;

	status = lzo_init();
	if (status != LZO_E_OK)
		return status;

	status = lzo1x_decompress(src, src_len, dst, (lzo_uint *)dst_len, NULL);

	return status;
}

LZO_EXPORT int LZOCompress(unsigned char *src, unsigned int src_len, unsigned char *dst, unsigned int *dst_len)
{
	int status;

	status = lzo_init();
	if (status != LZO_E_OK)
		return status;

	status = lzo1x_999_compress(src, src_len, dst, (lzo_uint *)dst_len, wrkmem);

	return status;
}
