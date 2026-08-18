#include "WwiseAudioPlayer.h"

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <atomic>
#include <memory>
#include <mutex>
#include <thread>
#include <unordered_map>
#include <vector>

#include <AK/SoundEngine/Common/AkMemoryMgr.h>
#include <AK/SoundEngine/Common/AkModule.h>
#include <AK/SoundEngine/Common/IAkStreamMgr.h>
#include <AK/SoundEngine/Common/AkStreamMgrModule.h>
#include <AK/SoundEngine/Common/AkSoundEngine.h>
#include <AK/MusicEngine/Common/AkMusicEngine.h>

// Statically links + registers the Vorbis decoder codec. Mass Effect Legendary Edition's Wwise
// audio (WEM files pulled from AFCs) is Vorbis-encoded, and unlike effect plugins, an event whose
// source uses an unregistered *codec* fails to post entirely rather than silently skipping it.
#include <AK/Plugin/AkVorbisDecoderFactory.h>

// Required memory allocation hooks (Wwise delegates these to the host application). These must be
// defined in namespace AK at global scope - the SDK static libs reference them by that exact
// mangled name (AK::AllocHook, etc.), not as free functions.
namespace AK
{
    void* AllocHook(size_t in_size) { return malloc(in_size); }
    void FreeHook(void* in_ptr) { free(in_ptr); }
    void* VirtualAllocHook(void* in_pMemAddress, size_t in_size, DWORD in_dwAllocationType, DWORD in_dwProtect)
    {
        return VirtualAlloc(in_pMemAddress, in_size, in_dwAllocationType, in_dwProtect);
    }
    void VirtualFreeHook(void* in_pMemAddress, size_t in_size, DWORD in_dwFreeType)
    {
        VirtualFree(in_pMemAddress, in_size, in_dwFreeType);
    }
}

namespace
{
    // ------------------------------------------------------------------------------------------
    // Memory-backed Low-Level IO. Legendary Explorer already has every byte it needs (AFC data,
    // pcc-embedded data) sitting in memory before playback starts, so instead of writing temp
    // files to disk like a normal Wwise Low-Level IO implementation would, streamed WEM sources
    // are served directly out of buffers registered ahead of time via RegisterStream(), keyed by
    // the Wwise file/source ID the engine asks for. SoundBanks never go through this path at all -
    // they're loaded directly via AK::SoundEngine::LoadBank()'s in-memory overload.
    // ------------------------------------------------------------------------------------------
    class MemoryIOHook : public AK::StreamMgr::IAkFileLocationResolver, public AK::StreamMgr::IAkIOHookBlocking
    {
    public:
        AKRESULT Init()
        {
            AK::StreamMgr::SetFileLocationResolver(this);

            AkDeviceSettings deviceSettings;
            AK::StreamMgr::GetDefaultDeviceSettings(deviceSettings);
            m_deviceId = AK::StreamMgr::CreateDevice(deviceSettings, this);
            return m_deviceId != AK_INVALID_DEVICE_ID ? AK_Success : AK_Fail;
        }

        void Term()
        {
            if (AK::IAkStreamMgr::Get() != nullptr && m_deviceId != AK_INVALID_DEVICE_ID)
            {
                AK::StreamMgr::DestroyDevice(m_deviceId);
                m_deviceId = AK_INVALID_DEVICE_ID;
            }
        }

        void RegisterStream(AkFileID id, const unsigned char* data, unsigned int size)
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            auto entry = std::make_unique<StreamEntry>();
            entry->data.assign(data, data + size);
            m_streams[id] = std::move(entry);
        }

        void ClearStreams()
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            m_streams.clear();
        }

        // ---- IAkFileLocationResolver ----

        AKRESULT Open(const AkOSChar* /*in_pszFileName*/, AkOpenMode /*in_eOpenMode*/, AkFileSystemFlags* /*in_pFlags*/,
            bool& /*io_bSyncOpen*/, AkFileDesc& /*io_fileDesc*/) override
        {
            // Banks are always loaded via the in-memory LoadBank() overload, so the engine never
            // needs to open a bank by filename through the Low-Level IO.
            return AK_FileNotFound;
        }

        AKRESULT Open(AkFileID in_fileID, AkOpenMode /*in_eOpenMode*/, AkFileSystemFlags* /*in_pFlags*/,
            bool& io_bSyncOpen, AkFileDesc& io_fileDesc) override
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            auto it = m_streams.find(in_fileID);
            if (it == m_streams.end())
                return AK_FileNotFound;

            io_bSyncOpen = true;
            io_fileDesc.iFileSize = static_cast<AkInt64>(it->second->data.size());
            io_fileDesc.uSector = 0;
            io_fileDesc.deviceID = m_deviceId;
            io_fileDesc.pCustomParam = nullptr;
            io_fileDesc.uCustomParamSize = 0;
            io_fileDesc.hFile = reinterpret_cast<AkFileHandle>(it->second.get());
            return AK_Success;
        }

        // ---- IAkLowLevelIOHook (base of IAkIOHookBlocking) ----

        AKRESULT Close(AkFileDesc& /*in_fileDesc*/) override
        {
            // Entries are owned by m_streams and freed via ClearStreams(), not per-file.
            return AK_Success;
        }

        AkUInt32 GetBlockSize(AkFileDesc& /*in_fileDesc*/) override
        {
            return 1; // memory-backed: no alignment constraints
        }

        void GetDeviceDesc(AkDeviceDesc& out_deviceDesc) override
        {
            out_deviceDesc = {};
            out_deviceDesc.deviceID = m_deviceId;
            out_deviceDesc.bCanRead = true;
            out_deviceDesc.bCanWrite = false;
        }

        AkUInt32 GetDeviceData() override
        {
            return 0;
        }

        // ---- IAkIOHookBlocking ----

        AKRESULT Read(AkFileDesc& in_fileDesc, const AkIoHeuristics& /*in_heuristics*/, void* out_pBuffer,
            AkIOTransferInfo& io_transferInfo) override
        {
            auto* entry = reinterpret_cast<StreamEntry*>(in_fileDesc.hFile);
            if (entry == nullptr)
                return AK_Fail;

            const uint64_t pos = io_transferInfo.uFilePosition;
            const uint32_t size = io_transferInfo.uRequestedSize;
            if (pos + size > entry->data.size())
                return AK_Fail;

            memcpy(out_pBuffer, entry->data.data() + pos, size);
            return AK_Success;
        }

        AKRESULT Write(AkFileDesc& /*in_fileDesc*/, const AkIoHeuristics& /*in_heuristics*/, void* /*in_pData*/,
            AkIOTransferInfo& /*io_transferInfo*/) override
        {
            return AK_NotImplemented;
        }

    private:
        struct StreamEntry
        {
            std::vector<unsigned char> data;
        };

        std::unordered_map<AkFileID, std::unique_ptr<StreamEntry>> m_streams;
        std::mutex m_mutex;
        AkDeviceID m_deviceId = AK_INVALID_DEVICE_ID;
    };

    // ------------------------------------------------------------------------------------------
    // Global state
    // ------------------------------------------------------------------------------------------
    std::atomic<bool> g_initialized{ false };
    std::atomic<bool> g_initBankLoaded{ false };
    MemoryIOHook g_lowLevelIO;

    std::atomic<bool> g_pumpThreadRunning{ false };
    std::thread g_pumpThread;

    std::mutex g_bankMutex;
    AkBankID g_eventBankId = AK_INVALID_BANK_ID;

    constexpr AkGameObjectID kPlaybackGameObject = 1;
    constexpr AkGameObjectID kListenerGameObject = 2;

    void RenderPumpThreadProc()
    {
        while (g_pumpThreadRunning.load(std::memory_order_relaxed))
        {
            AK::SoundEngine::RenderAudio();
            Sleep(20);
        }
    }

    void UnloadCurrentEventBank()
    {
        std::lock_guard<std::mutex> lock(g_bankMutex);
        if (g_eventBankId != AK_INVALID_BANK_ID)
        {
            AK::SoundEngine::StopAll(kPlaybackGameObject);
            AK::SoundEngine::UnloadBank(g_eventBankId, nullptr);
            g_eventBankId = AK_INVALID_BANK_ID;
        }
    }
}

HRESULT Initialize()
{
    if (g_initialized.load())
        return S_OK;

    AkMemSettings memSettings; // constructor fills in sane defaults
    if (AK::MemoryMgr::Init(&memSettings) != AK_Success)
        return E_FAIL;

    AkStreamMgrSettings stmSettings;
    AK::StreamMgr::GetDefaultSettings(stmSettings);
    if (AK::StreamMgr::Create(stmSettings) == nullptr)
        return E_FAIL;

    if (g_lowLevelIO.Init() != AK_Success)
        return E_FAIL;

    AkInitSettings initSettings;
    AkPlatformInitSettings platformSettings;
    AK::SoundEngine::GetDefaultInitSettings(initSettings);
    AK::SoundEngine::GetDefaultPlatformInitSettings(platformSettings);
    if (AK::SoundEngine::Init(&initSettings, &platformSettings) != AK_Success)
        return E_FAIL;

    AkMusicSettings musicSettings;
    AK::MusicEngine::GetDefaultInitSettings(musicSettings);
    if (AK::MusicEngine::Init(&musicSettings) != AK_Success)
        return E_FAIL;

    // A real game always has at least one listener (typically the camera) registered before doing
    // anything else with the sound engine - several internal codepaths assume one exists.
    if (AK::SoundEngine::RegisterGameObj(kListenerGameObject, "PreviewListener") != AK_Success)
        return E_FAIL;
    if (AK::SoundEngine::AddDefaultListener(kListenerGameObject) != AK_Success)
        return E_FAIL;
    if (AK::SoundEngine::RegisterGameObj(kPlaybackGameObject, "PreviewPlayback") != AK_Success)
        return E_FAIL;

    g_pumpThreadRunning.store(true);
    g_pumpThread = std::thread(RenderPumpThreadProc);

    g_initialized.store(true);
    return S_OK;
}

HRESULT Dispose()
{
    if (!g_initialized.load())
        return S_OK;

    UnloadCurrentEventBank();
    g_lowLevelIO.ClearStreams();

    g_pumpThreadRunning.store(false);
    if (g_pumpThread.joinable())
        g_pumpThread.join();

    AK::MusicEngine::Term();

    if (AK::SoundEngine::IsInitialized())
        AK::SoundEngine::Term();

    g_lowLevelIO.Term();
    if (AK::IAkStreamMgr::Get() != nullptr)
        AK::IAkStreamMgr::Get()->Destroy();

    if (AK::MemoryMgr::IsInitialized())
        AK::MemoryMgr::Term();

    g_initialized.store(false);
    g_initBankLoaded.store(false);
    return S_OK;
}

HRESULT LoadInitBank(const unsigned char* data, unsigned int size)
{
    if (!g_initialized.load())
        return E_NOT_VALID_STATE;
    if (g_initBankLoaded.load())
        return S_OK;

    AkBankID bankId = AK_INVALID_BANK_ID;
    AKRESULT result = AK::SoundEngine::LoadBank(data, size, AK_DEFAULT_POOL_ID, bankId);
    if (result != AK_Success)
        return E_FAIL;

    g_initBankLoaded.store(true);
    return S_OK;
}

HRESULT LoadEventBank(const unsigned char* data, unsigned int size)
{
    if (!g_initialized.load() || !g_initBankLoaded.load())
        return E_NOT_VALID_STATE;

    UnloadCurrentEventBank();

    std::lock_guard<std::mutex> lock(g_bankMutex);
    AkBankID bankId = AK_INVALID_BANK_ID;
    AKRESULT result = AK::SoundEngine::LoadBank(data, size, AK_DEFAULT_POOL_ID, bankId);
    if (result != AK_Success)
        return E_FAIL;

    g_eventBankId = bankId;
    return S_OK;
}

HRESULT RegisterStream(unsigned int wemId, const unsigned char* data, unsigned int size)
{
    if (!g_initialized.load())
        return E_NOT_VALID_STATE;

    g_lowLevelIO.RegisterStream(static_cast<AkFileID>(wemId), data, size);
    return S_OK;
}

HRESULT ClearStreams()
{
    if (!g_initialized.load())
        return E_NOT_VALID_STATE;

    AK::SoundEngine::StopAll(kPlaybackGameObject);
    // Give the engine a few render passes to actually release any in-flight streaming file
    // handles before the backing buffers are freed out from under it.
    for (int i = 0; i < 4; i++)
    {
        AK::SoundEngine::RenderAudio();
        Sleep(20);
    }

    g_lowLevelIO.ClearStreams();
    return S_OK;
}

HRESULT PostEvent(unsigned int eventId, unsigned int* outPlayingId)
{
    if (!g_initialized.load())
        return E_NOT_VALID_STATE;
    if (outPlayingId == nullptr)
        return E_POINTER;

    AkPlayingID playingId = AK::SoundEngine::PostEvent(static_cast<AkUniqueID>(eventId), kPlaybackGameObject);
    if (playingId == AK_INVALID_PLAYING_ID)
        return E_FAIL;

    *outPlayingId = static_cast<unsigned int>(playingId);
    return S_OK;
}

HRESULT StopPlaying(unsigned int playingId)
{
    if (!g_initialized.load())
        return E_NOT_VALID_STATE;

    AK::SoundEngine::StopPlayingID(static_cast<AkPlayingID>(playingId));
    return S_OK;
}

HRESULT StopAll()
{
    if (!g_initialized.load())
        return E_NOT_VALID_STATE;

    AK::SoundEngine::StopAll(kPlaybackGameObject);
    return S_OK;
}
