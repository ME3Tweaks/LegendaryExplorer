#pragma once

/**
 * Exports C functions for playing Wwise events using data fetched from Legendary Explorer
 * (AFC-backed WEM streams, WwiseBank bnk blobs, and the game's global Init.bnk), entirely
 * from memory - no game installation or on-disk Wwise project is required at runtime.
 */

#define WIN32_LEAN_AND_MEAN
#include <windows.h> // for HRESULT

#define EXPORT __declspec(dllexport)

#ifdef __cplusplus
extern "C" {
#endif

/**
 * Initializes the Wwise Sound Engine (memory manager, streaming manager, sound engine, music engine,
 * a default listener, and the Vorbis decoder). Must be called before any other function.
 * Safe to call multiple times; subsequent calls after a successful init are a no-op.
 */
HRESULT EXPORT Initialize();

/**
 * Shuts down the Wwise Sound Engine and frees all resources. Safe to call even if Initialize()
 * was never called or already failed.
 */
HRESULT EXPORT Dispose();

/**
 * Loads the game's global Init.bnk from memory. Must be called successfully once before any
 * event bank is loaded. Calling this again while an init bank is already loaded is a no-op
 * that returns S_OK without re-loading.
 */
HRESULT EXPORT LoadInitBank(const unsigned char* data, unsigned int size);

/**
 * Unloads any previously loaded event bank (and stops anything currently playing), then loads
 * the given bank - the one containing the event about to be played - from memory.
 */
HRESULT EXPORT LoadEventBank(const unsigned char* data, unsigned int size);

/**
 * Registers the raw WEM bytes for a streamed audio source, keyed by its Wwise source/file ID
 * (a WwiseStream export's "Id" property). Must be called for every stream an event might
 * reference before PostEvent() is called. The data is copied into native memory, so the
 * buffer passed in does not need to remain valid after this call returns.
 */
HRESULT EXPORT RegisterStream(unsigned int wemId, const unsigned char* data, unsigned int size);

/**
 * Stops everything currently playing and forgets all buffers registered via RegisterStream().
 */
HRESULT EXPORT ClearStreams();

/**
 * Posts (plays) the event with the given ID, which must exist in the currently loaded event bank.
 * outPlayingId receives the playing instance ID, usable with StopPlaying().
 */
HRESULT EXPORT PostEvent(unsigned int eventId, unsigned int* outPlayingId);

/**
 * Stops the given playing instance (as returned by PostEvent()).
 */
HRESULT EXPORT StopPlaying(unsigned int playingId);

/**
 * Stops everything currently playing.
 */
HRESULT EXPORT StopAll();

#ifdef __cplusplus
}
#endif
