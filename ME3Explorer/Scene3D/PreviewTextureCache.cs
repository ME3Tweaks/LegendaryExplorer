using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Direct3D11;
using ME3Explorer.Packages;

namespace ME3Explorer.Scene3D
{
    /// <summary>
    /// Represents a snapshot of the state of a texture entry in a <see cref="PreviewTextureCache"/>.
    /// Values of this structure should never be modified as they will not affect anything.
    /// To make modifications, call methods on the <see cref="PreviewTextureCache"/>.
    /// </summary>
    public class PreviewTextureState
    {
        /// <summary>
        /// The cached Direct3D11 texture view. Only non-null if <see cref="State"/> = <see cref="PreviewTextureCache.StateCode.Loaded"/>.
        /// </summary>
        public ShaderResourceView Texture { get; private set; }

        /// <summary>
        /// The error message that was encountered trying to load the texture. Only set if <see cref="State"/> = <see cref="PreviewTextureCache.StateCode.Error"/>.
        /// </summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// The state that the texture entry was in upon creation of this structure.
        /// </summary>
        public PreviewTextureCache.StateCode State { get; private set; }

        /// <summary>
        /// Creates a new PreviewTextureState with the given values.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="texture"></param>
        /// <param name="errormessage"></param>
        public PreviewTextureState(PreviewTextureCache.StateCode state, ShaderResourceView texture, string errormessage)
        {
            State = state;
            Texture = texture;
            ErrorMessage = errormessage;
        }
    }

    /// <summary>
    /// Loads and caches textures for a Direct3D11 renderer
    /// </summary>
    public class PreviewTextureCache : IDisposable
    {
        /// <summary>
        /// The possible states that a texture entry in a <see cref="PreviewTextureCache"/> can be in.
        /// </summary>
        public enum StateCode
        {
            /// <summary>
            /// This should never be seen and it indicates a programming error. Probably somebody didn't initialize the <see cref="State"/>.
            /// </summary>
            Invalid = 0,
            /// <summary>
            /// The texture is loaded and ready to be used. Find it in the <see cref="Texture"/> member.
            /// </summary>
            Loaded = 1,
            /// <summary>
            /// The texture's pcc export is being searched for.
            /// </summary>
            Searching = 2,
            /// <summary>
            /// No pcc export could be found exposing this texture.
            /// </summary>
            NotFound = 3,
            /// <summary>
            /// An exception or error prevented this texture from loading. Find the message in the <see cref="ErrorMessage"/> member.
            /// </summary>
            Error = 4
        }

        /// <summary>
        /// Stores a texture and load state in the cache.
        /// </summary>
        private class PreviewTextureEntry : IDisposable
        {
            /// <summary>
            /// The full path of the texture;
            /// </summary>
            public string FullPath = "";

            /// <summary>
            /// What state this entry is in. (See <see cref="StateCode"/>)
            /// </summary>
            public StateCode State = StateCode.Invalid; // So we can tell if someone forgets to initialize this

            /// <summary>
            /// The Direct3D ShaderResourceView for binding to shaders.
            /// </summary>
            public ShaderResourceView TextureView = null;

            /// <summary>
            /// The Direct3D texture for SHaderResourceView creation.
            /// </summary>
            public Texture2D Texture = null;

            /// <summary>
            /// Contains a description of any error encountered during loading.
            /// </summary>
            public string ErrorMessage = "";

            /// <summary>
            /// Creates a new cache entry for the given texture.
            /// </summary>
            /// <param name="fullpath">The full path of the texture.</param>
            public PreviewTextureEntry(string fullpath)
            {
                FullPath = fullpath;
                State = StateCode.Searching;
            }

            /// <summary>
            /// Disposes <see cref="TextureView"/> and <see cref="Texture"/> if they have been loaded.
            /// </summary>
            public void Dispose()
            {
                TextureView?.Dispose();
                Texture?.Dispose();
            }
        }

        /// <summary>
        /// Stores where a texture is located.
        /// </summary>
        private struct TextureLocation
        {
            /// <summary>
            /// The full name of the texture.
            /// </summary>
            public string FullName;

            /// <summary>
            /// The full path to the containing PCC file.
            /// </summary>
            public string PCC;

            /// <summary>
            /// The export ID of the texture at the located PCC file.
            /// </summary>
            public int ExportID;

            /// <summary>
            /// Creates a new TextureLocation.
            /// </summary>
            /// <param name="fullname">The full name of the texture.</param>
            /// <param name="pcc">The full path to the containing PCC file.</param>
            /// <param name="exportid">The export ID of the texture at the located PCC file.</param>
            public TextureLocation(string fullname, string pcc, int exportid)
            {
                FullName = fullname;
                PCC = pcc;
                ExportID = exportid;
            }
        }

        /// <summary>
        /// The DIrect3D11 device to create textures and resource views with.
        /// </summary>
        public Device Device { get; private set; } = null;

        /// <summary>
        /// The PCC file paths to scan for textures.
        /// </summary>
        private List<string> pccs; // NOTE: once set, only access from loader thread.

        /// <summary>
        /// A list of all the known textures and where they are.
        /// </summary>
        private Dictionary<string, TextureLocation> knownTextures;

        /// <summary>
        /// Creates a new PreviewTextureCache.
        /// </summary>
        /// <param name="device">The DIrect3D11 device to create textures and resource views with.</param>
        /// <param name="pccfiles">The PCC file paths to scan for textures.</param>
        public PreviewTextureCache(Device device, List<string> pccfiles)
        {
            Device = device;
            pccs = new List<string>(pccfiles);
        }

        /// <summary>
        /// Stops the texture loader thread, waits for it to exit, and then disposes all the textures and resource views.
        /// </summary>
        public void Dispose()
        {
            StopWait();
            lock (cacheLock) // Nobody should be holding this if the loader thread is dead, but better safe than sorry, amiright?
            {
                foreach (string key in cache.Keys)
                {
                    cache[key].Dispose();
                }
                cache.Clear();
            }
        }

        #region Thread-safe texture cache
        /// <summary>
        /// The synchronization object that will be locked upon when accessing or modifying the texture cache.
        /// </summary>
        private object cacheLock = new object();

        /// <summary>
        /// Stores loaded textures by their full name.
        /// </summary>
        private Dictionary<string, PreviewTextureEntry> cache = new Dictionary<string, PreviewTextureEntry>();
        
        /// <summary>
        /// Queues a texture for eventual loading.
        /// </summary>
        /// <param name="fullpath">The full path of the texture to search for and load.</param>
        public void LoadTexture(string fullpath)
        {
            //Console.WriteLine("[TEXCACHE] : Queued texture " + fullpath);
            lock (cacheLock)
            {
                if (cache.ContainsKey(fullpath))
                {
                    return;
                }
                cache.Add(fullpath, new PreviewTextureEntry(fullpath));
                Resume();
            }
        }

        /// <summary>
        /// Gets the current state of the given texture name.
        /// </summary>
        /// <param name="fullpath">The full path of the texture to return information on.</param>
        /// <returns>A <see cref="PreviewTextureState"/> representing the state of the cache entry at the time that this method returns, or <see cref="null"/> if there are no cache entries for the given full path.</returns>
        public PreviewTextureState GetTexture(string fullpath)
        {
            lock (cacheLock)
            {
                if (cache.ContainsKey(fullpath))
                {
                    return new PreviewTextureState(cache[fullpath].State, cache[fullpath].TextureView, cache[fullpath].ErrorMessage);
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Disposes and removes a texture from the cache.
        /// </summary>
        /// <param name="fullpath">The full path of the texture to unload.</param>
        public void FlushTexture(string fullpath)
        {
            lock (cacheLock)
            {
                if (cache.ContainsKey(fullpath))
                {
                    cache[fullpath].Dispose();
                    cache.Remove(fullpath);
                }
            }
        }

        /// <summary>
        /// Disposes all previews and removes all textures from the cache.
        /// </summary>
        public void FlushAll()
        {
            lock (cacheLock)
            {
                foreach (PreviewTextureEntry tex in cache.Values)
                {
                    tex.Dispose();
                }
                cache.Clear();
            }
        }
        #endregion

        #region Thread-safe loader thread control
        /// <summary>
        /// The synchronization object that will be locked upon when accessing or modifying the state of the texture loader thread.
        /// </summary>
        private object threadStatusLock = new object();

        /// <summary>
        /// The texture loader thread.
        /// </summary>
        private System.Threading.Thread loaderThread = null;

        /// <summary>
        /// Whether the texture loader thread is running.
        /// </summary>
        private bool loaderRunning = false;

        /// <summary>
        /// Whether the texture loader thread should stop running.
        /// </summary>
        private bool stopLoader = false;

        /// <summary>
        /// Whether the texture loader thread is idle (waiting for a signal on <see cref="resumeEvent"/>).
        /// </summary>
        private bool idle = false;

        /// <summary>
        /// Whether the texture loader thread is currently running.
        /// </summary>
        public bool LoaderRunning
        {
            get
            {
                lock (threadStatusLock)
                {
                    return loaderRunning;
                }
            }
            private set
            {
                lock(threadStatusLock)
                {
                    loaderRunning = value;
                }
            }
        }

        /// <summary>
        /// Instructs the texture loader to please exit sometime soon.
        /// </summary>
        public void StopLoader()
        {
            lock (threadStatusLock)
            {
                if (!loaderRunning)
                {
                    return;
                }
                stopLoader = true;
                if (idle)
                {
                    idle = false;
                    resumeEvent.Set();
                }
            }
        }

        /// <summary>
        /// Instructs the texture loader thread to exit, then waits for it to do so.
        /// </summary>
        public void StopWait()
        {
            StopLoader();
            if (loaderRunning) // WARNING: don't use LoaderRunning or else deadlock on threadStatusLock!!
            {
                loaderThread.Join();
            }
        }

        /// <summary>
        /// Whether the texture loader thread should exit.
        /// </summary>
        private bool shouldStop
        {
            get
            {
                lock (threadStatusLock)
                {
                    return stopLoader;
                }
            }
        }

        /// <summary>
        /// Immediately aborts the texture loader thread.
        /// </summary>
        public void AbortLoader()
        {
            lock (threadStatusLock)
            {
                if (!loaderRunning)
                {
                    return;
                }
                loaderThread.Abort();
            }
        }

        /// <summary>
        /// Starts the texture laoder thread.
        /// </summary>
        public void StartLoader()
        {
            lock (threadStatusLock)
            {
                if (loaderRunning)
                {
                    return;
                }
                loaderRunning = true;
                stopLoader = false;
                loaderThread = new System.Threading.Thread(LoaderThreadEntrypoint);
                loaderThread.Name = "DirectX Texture Loader Thread";
                loaderThread.Start();
            }
        }

        /// <summary>
        /// Whether the texture loader thread is waiting for a <see cref="Resume"/> signal because it has run out of texture loads to execute.
        /// </summary>
        public bool Idle
        {
            get
            {
                lock (threadStatusLock)
                {
                    return idle;
                }
            }
        }

        /// <summary>
        /// Signals the texture loader thread to check again for pending texture loads or whether it should exit.
        /// </summary>
        private void Resume()
        {
            lock (threadStatusLock)
            {
                if (idle)
                {
                    //Console.WriteLine("[TEXCACHE] : Sending wakeup signal");
                    idle = false;
                    resumeEvent.Set();
                }
            }
        }

        /// <summary>
        /// When it is idle, the texture loader thread waits for a signal on this to check again for pending texture loads or whether it should exit.
        /// </summary>
        private System.Threading.AutoResetEvent resumeEvent = new System.Threading.AutoResetEvent(false); // Signal this when the loader thread is idle so it resumes loading.
        #endregion

        #region Loader Thread
        /// <summary>
        /// The entry point of the texture loader thread.
        /// </summary>
        private void LoaderThreadEntrypoint()
        {
            while (!shouldStop)
            {
                if (knownTextures == null)
                {
                    ScanTextures();
                }
                // Look for each texture in the knownTextures texture location cache
                lock (cacheLock)
                {
                    if (knownTextures != null) // NOTE: we must do this in the event that knownTextures became null between the above ScanTextures and this lock.
                    {                          // We shouldn't call ScanTextures in the lock either because that will lock for quite a long time, freezing the UI thread.
                        foreach (PreviewTextureEntry entry in cache.Values)
                        {
                            if (entry.State == StateCode.Searching)
                            {
                                if (knownTextures.ContainsKey(entry.FullPath.ToLower()))
                                {
                                    TextureLocation l = knownTextures[entry.FullPath.ToLower()];
                                    try
                                    {
                                        using (ME3Package pcc = MEPackageHandler.OpenME3Package(l.PCC))
                                        {
                                            Unreal.Classes.Texture2D metex = new Unreal.Classes.Texture2D(pcc, l.ExportID);
                                            Texture2DDescription desc = new Texture2DDescription();
                                            entry.Texture = metex.generatePreviewTexture(Device, out desc);
                                            entry.TextureView = new ShaderResourceView(Device, entry.Texture);
                                            entry.State = StateCode.Loaded;
                                            //Console.WriteLine("[TEXCACHE] : Loaded texture " + entry.FullPath + " from " + l.PCC);
                                        }

                                    }
                                    catch (Exception e)
                                    {
                                        entry.State = StateCode.Error;
                                        entry.ErrorMessage = e.ToString();
                                    }
                                }
                                else
                                {
                                    entry.State = StateCode.NotFound;
                                }
                            }
                        }
                    }
                }

                lock (threadStatusLock)
                {
                    idle = true;
                    //Console.WriteLine("[TEXCACHE] : Idling...");
                    if (shouldStop)
                    {
                        break;
                    }
                }
                resumeEvent.WaitOne(); // Wait to be woken up for more loading
                //Console.WriteLine("[TEXCACHE] : Woke up!");
            }
            //Console.WriteLine("[TEXCACHE] : Loader done.");
            LoaderRunning = false;
        }

        /// <summary>
        /// Scans the list of <see cref="pccs"/> for texture entries and caches their location and full name in <see cref="knownTextures"/> for future lookup.
        /// </summary>
        private void ScanTextures()
        {
            //Console.WriteLine("[TEXCACHE] : Starting texture search...");
            foreach (string pccpath in pccs)
            {
                using (ME3Package pcc = MEPackageHandler.OpenME3Package(pccpath))
                {
                    //Console.WriteLine("[TEXCACHE] : Scanning <" + System.IO.Path.GetFileName(pccpath) + "> for textures.");
                    foreach (IExportEntry export in pcc.Exports)
                    {
                        if (export.ClassName == "Texture2D")
                        {
                            lock (cacheLock)
                            {
                                if (knownTextures == null)
                                {
                                    knownTextures = new Dictionary<string, TextureLocation>(); // important because the user could flush the cache at any time.
                                }
                                if (knownTextures.ContainsKey(export.GetFullPath.ToLower()))
                                {
                                    knownTextures[export.GetFullPath.ToLower()] = new TextureLocation(export.GetFullPath.ToLower(), pccpath, export.Index);
                                }
                                else
                                {
                                    knownTextures.Add(export.GetFullPath.ToLower(), new TextureLocation(export.GetFullPath.ToLower(), pccpath, export.Index));
                                }
                                foreach (PreviewTextureEntry entry in cache.Values)
                                {
                                    if (entry.State == StateCode.Searching && knownTextures.ContainsKey(entry.FullPath.ToLower()))
                                    {
                                        TextureLocation l = knownTextures[entry.FullPath.ToLower()];
                                        try
                                        {
                                            using (ME3Package texpcc = MEPackageHandler.OpenME3Package(l.PCC))
                                            {
                                                Unreal.Classes.Texture2D metex = new Unreal.Classes.Texture2D(texpcc, l.ExportID);
                                                Texture2DDescription desc = new Texture2DDescription();
                                                entry.Texture = metex.generatePreviewTexture(Device, out desc);
                                                entry.TextureView = new ShaderResourceView(Device, entry.Texture);
                                                entry.State = StateCode.Loaded;
                                                //Console.WriteLine("[TEXCACHE] : Loaded texture " + entry.FullPath + " from " + l.PCC);
                                            }

                                        }
                                        catch (Exception e)
                                        {
                                            entry.State = StateCode.Error;
                                            entry.ErrorMessage = e.ToString();
                                        }

                                    }
                                }
                            }
                        }
                    }
                }
                if (shouldStop)
                {
                    break;
                }
            }
            /*lock (cacheLock)
            {
                Console.WriteLine("[TEXCACHE] : Texture search complete! Found " + knownTextures.Count + " textures.");
            }*/
        }
        #endregion
    }
}
