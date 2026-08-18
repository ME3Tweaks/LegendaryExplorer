using System.Runtime.InteropServices;
using System.Threading;

namespace LegendaryExplorerCore.Audio
{
    /// <summary>
    /// Thin P/Invoke wrapper around WwiseAudioPlayer.dll (see LegendaryExplorer\WwiseAudioPlayer\),
    /// a native Wwise Sound Engine host that plays Wwise events entirely from memory - the Init.bnk,
    /// the WwiseBank containing the event, and any AFC-backed WEM streams the event needs are all
    /// fetched by LegendaryExplorerCore and handed to the engine directly, with no on-disk staging.
    /// </summary>
    /// <remarks>
    /// This class only calls into the native engine; it has no knowledge of packages, exports, or
    /// how to resolve a WwiseEvent export to its bank/streams - see <see cref="WwiseEventPlayer"/>
    /// for that.
    /// </remarks>
    public static class WwiseAudioPlayer
    {
        private const string DLL = "WwiseAudioPlayer.dll";

        [DllImport(DLL, EntryPoint = "Initialize")]
        private static extern int WAP_Initialize();

        [DllImport(DLL, EntryPoint = "Dispose")]
        private static extern int WAP_Dispose();

        [DllImport(DLL, EntryPoint = "LoadInitBank")]
        private static extern unsafe int WAP_LoadInitBank(byte* data, uint size);

        [DllImport(DLL, EntryPoint = "LoadEventBank")]
        private static extern unsafe int WAP_LoadEventBank(byte* data, uint size);

        [DllImport(DLL, EntryPoint = "RegisterStream")]
        private static extern unsafe int WAP_RegisterStream(uint wemId, byte* data, uint size);

        [DllImport(DLL, EntryPoint = "ClearStreams")]
        private static extern int WAP_ClearStreams();

        [DllImport(DLL, EntryPoint = "PostEvent")]
        private static extern unsafe int WAP_PostEvent(uint eventId, uint* outPlayingId);

        [DllImport(DLL, EntryPoint = "StopPlaying")]
        private static extern int WAP_StopPlaying(uint playingId);

        [DllImport(DLL, EntryPoint = "StopAll")]
        private static extern int WAP_StopAll();

        private static bool _initialized;
        private static readonly Lock _syncObj = new();

        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                _initialized = true;
                Marshal.ThrowExceptionForHR(WAP_Initialize());
            }
        }

        /// <summary>
        /// Loads the game's global Init.bnk. Safe to call more than once - the native side no-ops
        /// after the first successful load, since only one init bank can ever be loaded.
        /// </summary>
        public static unsafe void LoadInitBank(byte[] data)
        {
            lock (_syncObj)
            {
                EnsureInitialized();
                fixed (byte* ptr = data)
                {
                    Marshal.ThrowExceptionForHR(WAP_LoadInitBank(ptr, (uint)data.Length));
                }
            }
        }

        /// <summary>
        /// Unloads any previously loaded event bank and loads the given one - the bank containing
        /// the event about to be played.
        /// </summary>
        public static unsafe void LoadEventBank(byte[] data)
        {
            lock (_syncObj)
            {
                EnsureInitialized();
                fixed (byte* ptr = data)
                {
                    Marshal.ThrowExceptionForHR(WAP_LoadEventBank(ptr, (uint)data.Length));
                }
            }
        }

        /// <summary>
        /// Registers the raw WEM bytes for a streamed audio source, keyed by its Wwise source/file ID
        /// (a WwiseStream export's Id property). Must be called for every stream an event might
        /// reference before <see cref="PostEvent"/>.
        /// </summary>
        public static unsafe void RegisterStream(uint wemId, byte[] data)
        {
            lock (_syncObj)
            {
                EnsureInitialized();
                fixed (byte* ptr = data)
                {
                    Marshal.ThrowExceptionForHR(WAP_RegisterStream(wemId, ptr, (uint)data.Length));
                }
            }
        }

        /// <summary>
        /// Stops everything currently playing and forgets all buffers registered via <see cref="RegisterStream"/>.
        /// </summary>
        public static void ClearStreams()
        {
            lock (_syncObj)
            {
                EnsureInitialized();
                Marshal.ThrowExceptionForHR(WAP_ClearStreams());
            }
        }

        /// <summary>
        /// Posts (plays) the event with the given ID, which must exist in the currently loaded event bank.
        /// </summary>
        /// <returns>The playing instance ID, usable with <see cref="StopPlaying"/>.</returns>
        public static unsafe uint PostEvent(uint eventId)
        {
            lock (_syncObj)
            {
                EnsureInitialized();
                uint playingId;
                Marshal.ThrowExceptionForHR(WAP_PostEvent(eventId, &playingId));
                return playingId;
            }
        }

        /// <summary>
        /// Stops the given playing instance (as returned by <see cref="PostEvent"/>).
        /// </summary>
        public static void StopPlaying(uint playingId)
        {
            lock (_syncObj)
            {
                if (!_initialized) return;
                WAP_StopPlaying(playingId);
            }
        }

        /// <summary>
        /// Stops everything currently playing.
        /// </summary>
        public static void StopAll()
        {
            lock (_syncObj)
            {
                if (!_initialized) return;
                WAP_StopAll();
            }
        }

        /// <summary>
        /// Shuts down the native Wwise Sound Engine and frees all resources. Not required to be called
        /// on app exit - process teardown is sufficient - but available for tests or long-running
        /// hosts that want to release the engine explicitly.
        /// </summary>
        public static void Shutdown()
        {
            lock (_syncObj)
            {
                if (!_initialized) return;
                WAP_Dispose();
                _initialized = false;
            }
        }
    }
}
