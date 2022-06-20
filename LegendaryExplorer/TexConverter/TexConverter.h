#pragma once

/**
 * Exports C functions for texture conversion.
 */

#include "DirectXTex/DirectXTex.h"

#define EXPORT __declspec(dllexport)

#ifdef __cplusplus
extern "C" {
#endif

typedef struct {
	unsigned char* PixelData;
	size_t PixelDataLength;
	uint32_t Width;
	uint32_t Height;
	DXGI_FORMAT Format;
	DirectX::ScratchImage* _ScratchImage;
} TextureBuffer;

/**
 * Initializes the static resources used by TexConverter.
 */
HRESULT EXPORT Initialize();

/**
 * Disposes the static resources used by TexConverter.
 */
HRESULT EXPORT Dispose();

/**
 * @brief Converts the pixel data in the given input buffer to the given output buffer.
 * 
 * Note that the output `TextureBuffer` is overwritten, including a new pixel buffer that must must be freed using `FreePixelData(...)`.
 * 
 * @param inputBuffer The texture to convert.
 * @param outputBuffer The buffer to write the converted pixel data to.
 */
HRESULT EXPORT ConvertTexture(const TextureBuffer* inputBuffer, TextureBuffer* outputBuffer);

/**
 * @brief Saves the given texture to the given filename.
 * 
 * The output file format is determined by the extension of the given filename.
 * 
 * @param inputBuffer The texture to save.
 * @param outputFilename The filename to save to.
 */
HRESULT EXPORT SaveTexture(const TextureBuffer* inputBuffer, const char* outputFilename);

/**
 * @brief Loads the texture data from the given filename into the given output buffer.
 * 
 * Note that the output `TextureBuffer` is overwritten, including a new pixel buffer that must must be freed using `FreePixelData(...)`.
 * 
 * @param inputFilename The filename of the texture to load.
 * @param outputBuffer The buffer to load the texture into. If the output buffer's `Format` is not `DXGI_FORMAT_UNKNOWN`, the data will be converted to the output buffer's `Format`.
 */
HRESULT EXPORT LoadTexture(const char* inputFilename, TextureBuffer* outputBuffer);

/**
 * @brief Loads the texture data from the given buffer into the given output buffer.
 *
 * Note that the output `TextureBuffer` is overwritten, including a new pixel buffer that must must be freed using `FreePixelData(...)`.
 *
 * @param input buffer data array
 * @param bufferSize The size of the buffer data array
 * @param imageType 1 = DDS, 2 = PNG, 3 = TGA, all other values are invalid.
 * @param outputBuffer The buffer to load the texture into. If the output buffer's `Format` is not `DXGI_FORMAT_UNKNOWN`, the data will be converted to the output buffer's `Format`.
 */
HRESULT EXPORT LoadTextureFromMemory(const void* inputDataBuffer, size_t bufferSize, int imageType, TextureBuffer* outputBuffer);


/**
 * @brief Frees the pixel data that was created by `LoadTexture(...)` or `ConvertTexture(...)`.
 * 
 * @param textureBuffer The texture buffer containing the pixels to free.
 */
HRESULT EXPORT FreePixelData(TextureBuffer* textureBuffer);

#ifdef __cplusplus
}
#endif
