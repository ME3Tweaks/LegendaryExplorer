#include "DirectXTex/DirectXTexP.h"

#include "TexConverter.h"

#include "DirectXTex/DirectXTex.h"
#include <locale>
#include <codecvt>
#include <string>

#define EXTENSION_DDS ".dds"
#define EXTENSION_PNG ".png"
#define EXTENSION_TGA ".tga"

#ifdef WIN32
#include <string.h>
#define strcasecmp _stricmp
#else
#include <strings.h>
#endif

std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> StringConverter;

const char* FindExtension(const char* filename) {
	const char* result = nullptr;
	const char* character = filename;
	while (*character != '\0') {
		if (*character == '.') {
			result = character;
		}
		character++;
	}
	return result;
}

HRESULT Initialize() {
	HRESULT hr = S_OK;
	
	return hr;
}

HRESULT Dispose() {
	
	return S_OK;
}

HRESULT ConvertTexture(const TextureBuffer* inputBuffer, const TextureBuffer* outputBuffer) {

	if (DirectX::IsCompressed(inputBuffer->Format)) {
		if (DirectX::IsCompressed(outputBuffer->Format)) {
			// Compressed -> Compressed Conversion
			// 
			// I won't implement this unless we really need it. It would decompress to an intermediate uncompressed image, and then recompress.
			return E_NOTIMPL;
		}
		else {
			// Decompression

			// TODO: Implement!
			return E_NOTIMPL;
		}
	}
	else {
		if (DirectX::IsCompressed(outputBuffer->Format)) {
			// Compression

			// TODO: Implement!
			return E_NOTIMPL;
		}
		else {
			// Conversion

			// TODO: Implement!
			return E_NOTIMPL;
		}
	}
}

HRESULT SaveTexture(const TextureBuffer* inputBuffer, const char* outputFilename) {

	if (inputBuffer == nullptr || inputBuffer->PixelData == nullptr || outputFilename == nullptr) {
		return E_INVALIDARG; // No buffer, no data in buffer, no filename
	}

	DirectX::Image image = { inputBuffer->Width, inputBuffer->Height, inputBuffer->Format, 0, 0, inputBuffer->PixelData };
	DirectX::ComputePitch(inputBuffer->Format, inputBuffer->Width, inputBuffer->Height, image.rowPitch, image.slicePitch, DirectX::CP_FLAGS::CP_FLAGS_NONE);
	
	const char* extension = FindExtension(outputFilename);
	if (extension == nullptr) {
		return E_INVALIDARG; // No extension
	}
	else if (strcasecmp(extension, EXTENSION_DDS) == 0) {
		return DirectX::SaveToDDSFile(image, DirectX::DDS_FLAGS_NONE, StringConverter.from_bytes(outputFilename).c_str());
	}
	else if (strcasecmp(extension, EXTENSION_PNG) == 0) {
		return DirectX::SaveToWICFile(image, DirectX::WIC_FLAGS::WIC_FLAGS_NONE, DirectX::GetWICCodec(DirectX::WIC_CODEC_PNG), StringConverter.from_bytes(outputFilename).c_str());
	}
	else if (strcasecmp(extension, EXTENSION_TGA) == 0) {
		return DirectX::SaveToTGAFile(image, StringConverter.from_bytes(outputFilename).c_str());
	}
	else {
		return E_INVALIDARG; // Unknown extension
	}
}

HRESULT LoadTexture(const char* inputFilename, TextureBuffer* outputBuffer) {

	if (inputFilename == nullptr || outputBuffer == nullptr || outputBuffer->PixelData != nullptr) {
		return E_INVALIDARG; // No filename, no output buffer, or output buffer already has data
	}

	outputBuffer->_ScratchImage = new DirectX::ScratchImage();

	HRESULT hr = S_OK;

	const char* extension = FindExtension(inputFilename);
	if (extension == nullptr) {
		return E_INVALIDARG; // No extension
	}
	else if (strcasecmp(extension, EXTENSION_DDS) == 0) {
		hr = DirectX::LoadFromDDSFile(StringConverter.from_bytes(inputFilename).c_str(), DirectX::DDS_FLAGS_NONE, nullptr, *outputBuffer->_ScratchImage);
	}
	else if (strcasecmp(extension, EXTENSION_PNG) == 0) {
		hr = DirectX::LoadFromWICFile(StringConverter.from_bytes(inputFilename).c_str(), DirectX::WIC_FLAGS_NONE, nullptr, *outputBuffer->_ScratchImage);
	}
	else if (strcasecmp(extension, EXTENSION_TGA) == 0) {
		hr = DirectX::LoadFromTGAFile(StringConverter.from_bytes(inputFilename).c_str(), nullptr, *outputBuffer->_ScratchImage);
	}
	else {
		return E_INVALIDARG; // Unknown extension
	}

	if (FAILED(hr)) {
		delete outputBuffer->_ScratchImage;
		return hr;
	}

	outputBuffer->PixelData = outputBuffer->_ScratchImage->GetPixels();
	outputBuffer->PixelDataLength = outputBuffer->_ScratchImage->GetPixelsSize();
	outputBuffer->Width = outputBuffer->_ScratchImage->GetMetadata().width;
	outputBuffer->Height = outputBuffer->_ScratchImage->GetMetadata().height;
	outputBuffer->Format = outputBuffer->_ScratchImage->GetMetadata().format;
	return S_OK;
}

HRESULT FreePixelData(TextureBuffer* textureBuffer) {
	
	if (textureBuffer == nullptr || textureBuffer->_ScratchImage == nullptr) {
		return E_INVALIDARG; // No buffer or no data in buffer
	}

	textureBuffer->_ScratchImage->Release();
	textureBuffer->_ScratchImage = nullptr;
	textureBuffer->PixelData = nullptr;
	textureBuffer->PixelDataLength = 0;
	
	return S_OK;
}