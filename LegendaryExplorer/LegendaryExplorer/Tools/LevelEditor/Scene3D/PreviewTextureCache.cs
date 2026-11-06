using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Texture2D = SharpDX.Direct3D11.Texture2D;
using UnrealTexture = LegendaryExplorerCore.Unreal.Classes.Texture2D;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// Loads and caches textures for a Direct3D11 renderer
/// </summary>
public class PreviewTextureCache : IDisposable
{
    /// <summary>
    /// Stores a texture and load state in the cache.
    /// </summary>
    public class TextureEntry : IDisposable
    {
        /// <summary>
        /// Texture export for this cache entry
        /// </summary>
        //public ExportEntry TextureExport { get; set; }
        public string InstanceFullPath { get; }
        public ExportEntry Export { get; }

        /// <summary>
        /// What's resident, reading raw texel values. This requests no detail, so draws of a texture that can stream use <see cref="GetView"/>.
        /// </summary>
        public ShaderResourceView ResidentView => resources?.View;

        /// <summary>
        /// For textures holding sRGB-encoded color, a view that converts to linear when sampled, as the game's shaders expect.
        /// Otherwise, the same as <see cref="ResidentView"/>.
        /// </summary>
        public ShaderResourceView ResidentLinearView => resources?.LinearView;

        /// <summary>
        /// <see cref="ResidentView"/>, for a draw to bind. This requests the detail the draw needs.
        /// </summary>
        /// <param name="pixels">The draw's size, from <see cref="MeshRenderContext.TexturePixels"/></param>
        public ShaderResourceView GetView(float pixels)
        {
            RequestDetail(pixels, pixels);
            return ResidentView;
        }

        /// <summary>
        /// <see cref="ResidentLinearView"/>, for a draw to bind. This requests the detail the draw needs.
        /// </summary>
        /// <param name="pixels">The draw's size, from <see cref="MeshRenderContext.TexturePixels"/></param>
        public ShaderResourceView GetLinearView(float pixels)
        {
            RequestDetail(pixels, pixels);
            return ResidentLinearView;
        }

        /// <summary>
        /// <see cref="ResidentLinearView"/>, for a draw to bind from an atlas such as a light-map page. A draw's size says nothing about
        /// the detail its part of an atlas needs, so this requests the whole mip chain, though only as urgently as the draw is large.
        /// </summary>
        /// <param name="pixels">The draw's size, from <see cref="MeshRenderContext.TexturePixels"/></param>
        public ShaderResourceView GetLinearAtlasView(float pixels)
        {
            RequestDetail(MeshRenderContext.FullChainPixels, pixels);
            return ResidentLinearView;
        }

        /// <summary>
        /// The Direct3D texture for ShaderResourceView creation.
        /// </summary>
        public Texture2D Texture => resources?.Texture;

        private readonly MeshRenderContext context;
        private TextureResources resources;
        internal StreamedTextureSource StreamingSource;
        internal int ResidentMip;
        internal int RequestedMip;
        internal long RequestedFrame = -1;
        internal float Priority;
        internal long RetryFrame;
        internal int StreamingFailures;

        private void RequestDetail(float pixels, float priority)
        {
            if (!context.Wireframe) context.TextureCache.RequestDetail(this, pixels, priority);
        }

        internal void Adopt(TextureResources replacement, int mip)
        {
            var previous = resources;
            resources = replacement;
            ResidentMip = mip;
            StreamingFailures = 0;
            if (mip == 0) StreamingSource = null;
            previous?.Dispose();
        }

        /// <summary>
        /// Whether a replacement whose largest mip is <paramref name="mip"/> stops above the smallest mip that's resident
        /// </summary>
        internal bool WouldLoseMips(TextureResources replacement, int mip) =>
            mip + replacement.Texture.Description.MipLevels < ResidentMip + resources.Texture.Description.MipLevels;

        /// <summary>
        /// The time this object was last accessed.
        /// </summary>
        public DateTime LastUsageTime = DateTime.Now;

        public readonly bool IsTextureCube;

        /// <summary>
        /// How the game samples this texture outside [0,1]. (The texture's AddressX and AddressY)
        /// </summary>
        public TextureAddressMode AddressU { get; private set; } = TextureAddressMode.Wrap;
        public TextureAddressMode AddressV { get; private set; } = TextureAddressMode.Wrap;

        /// <summary>
        /// Creates a new cache entry for the given texture.
        /// </summary>
        public TextureEntry(MeshRenderContext renderContext, ExportEntry export)
        {
            context = renderContext;
            MemoryAnalyzer.AddTrackedMemoryItem($"PreviewTexture {export.ObjectName}", new WeakReference(this));
            Export = export;
            InstanceFullPath = export.InstancedFullPath;
            IsTextureCube = export.ClassName == "TextureCube";
            try { Load(renderContext); }
            catch { Dispose(); throw; }
        }

        private void Load(MeshRenderContext renderContext)
        {
            if (!IsTextureCube)
            {
                PropertyCollection props = Export.GetProperties();
                AddressU = GetAddressMode(props.GetProp<EnumProperty>("AddressX"));
                AddressV = GetAddressMode(props.GetProp<EnumProperty>("AddressY"));
            }

            UnrealTexture parsed = null;
            if (renderContext.TextureStreamingEnabled && renderContext.TextureDataOverride is null)
            {
                StreamingSource = StreamedTextureSource.TryCreate(Export, out parsed);
            }
            if (StreamingSource is { } source)
            {
                try
                {
                    resources = source.Load(renderContext.Device, source.InitialMip, CancellationToken.None);
                    ResidentMip = source.InitialMip;
                    return;
                }
                catch (Exception e)
                {
                    //this is the mip streaming starts from, but a full load can still show the ones above it
                    Debug.WriteLine($"Could not stream texture {InstanceFullPath}: {e.Message}");
                    StreamingSource = null;
                }
            }
            MeshRenderContext.LoadedTexture loaded = IsTextureCube ? renderContext.LoadUnrealTextureCube(Export)
                : parsed is null ? renderContext.LoadUnrealTexture(Export) : renderContext.LoadUnrealTexture(parsed);
            resources = TextureResources.Create(renderContext.Device, loaded, IsTextureCube);
            ResidentMip = 0;
        }

        /// <summary>
        /// Rereads the edited export into this same entry, so that the materials and light-maps that hold it show the edit without being rebuilt.
        /// Call between frames. If it can't be read, the entry is left without resources, as if disposed
        /// </summary>
        internal virtual void Reload(MeshRenderContext renderContext)
        {
            Dispose();
            Load(renderContext);
        }

        private static TextureAddressMode GetAddressMode(EnumProperty textureAddress) => textureAddress?.Value.Name switch
        {
            "TA_Clamp" => TextureAddressMode.Clamp,
            "TA_Mirror" => TextureAddressMode.Mirror,
            _ => TextureAddressMode.Wrap //TA_Wrap is the default
        };

        /// <summary>
        /// Disposes <see cref="ResidentView"/> and <see cref="Texture"/> if they have been loaded.
        /// </summary>
        public void Dispose()
        {
            context.TextureCache.CancelStreaming(this);
            StreamingSource = null;
            RequestedFrame = -1;
            RetryFrame = 0;
            StreamingFailures = 0;
            resources?.Dispose();
            resources = null;
        }
    }

    public class FlipBookTextureEntry : TextureEntry
    {
        enum TextureFlipBookMethod
        {
            TFBM_UL_ROW,
            TFBM_UL_COL,
            TFBM_UR_ROW,
            TFBM_UR_COL,
            TFBM_LL_ROW,
            TFBM_LL_COL,
            TFBM_LR_ROW,
            TFBM_LR_COL,
            TFBM_RANDOM,
        }

        float FrameRate;
        int HorizontalImages;
        int VerticalImages;
        TextureFlipBookMethod FBMethod;

        float FrameTime;
        float HorizontalScale;
        float VerticalScale;

        int CurrentRow;
        int CurrentColumn;
        float LastFrameTime;

        public FlipBookTextureEntry(MeshRenderContext renderContext, ExportEntry export) : base(renderContext, export)
        {
            ReadFlipBook();
        }

        internal override void Reload(MeshRenderContext renderContext)
        {
            base.Reload(renderContext);
            ReadFlipBook();
            CurrentRow = CurrentColumn = 0;
        }

        private void ReadFlipBook()
        {
            var props = Export.GetProperties();
            FrameRate = props.GetProp<FloatProperty>("FrameRate")?.Value ?? 4f;
            HorizontalImages = props.GetProp<IntProperty>("HorizontalImages")?.Value ?? 1;
            VerticalImages = props.GetProp<IntProperty>("VerticalImages")?.Value ?? 1;
            if (!Enum.TryParse(props.GetProp<EnumProperty>("FBMethod")?.Value ?? "TFBM_UL_ROW", out FBMethod))
            {
                FBMethod = TextureFlipBookMethod.TFBM_UL_ROW;
            }

            FrameTime = props.GetProp<FloatProperty>("FrameTime")?.Value ?? (FrameRate > 0 ? 1f / FrameRate : 1f);

            HorizontalScale = 1f / HorizontalImages;
            VerticalScale = 1f / VerticalImages;
        }

        public void Tick(float currentTime)
        {
            if (Math.Abs(currentTime - LastFrameTime) > FrameTime)
            {
                LastFrameTime = currentTime;
                switch (FBMethod)
                {
                    case TextureFlipBookMethod.TFBM_UL_ROW:
                        if (CurrentColumn + 1 >= HorizontalImages)
                        {
                            if (CurrentRow + 1 >= VerticalImages)
                            {
                                CurrentRow = 0;
                            }
                            else
                            {
                                CurrentRow++;
                            }
                            CurrentColumn = 0;
                        }
                        else
                        {
                            CurrentColumn++;
                        }
                        break;
                    case TextureFlipBookMethod.TFBM_UL_COL:
                        if (CurrentRow + 1 >= VerticalImages)
                        {
                            if (CurrentColumn + 1 >= HorizontalImages)
                            {
                                CurrentColumn = 0;
                            }
                            else
                            {
                                CurrentColumn++;
                            }
                            CurrentRow = 0;
                        }
                        else
                        {
                            CurrentRow++;
                        }
                        break;
                    case TextureFlipBookMethod.TFBM_UR_ROW:
                        if (CurrentColumn - 1 < 0)
                        {
                            if (CurrentRow + 1 >= VerticalImages)
                            {
                                CurrentRow = 0;
                            }
                            else
                            {
                                CurrentRow++;
                            }
                            CurrentColumn = HorizontalImages - 1;
                        }
                        else
                        {
                            CurrentColumn--;
                        }
                        break;
                    case TextureFlipBookMethod.TFBM_UR_COL:
                        if (CurrentRow + 1 >= VerticalImages)
                        {
                            if (CurrentColumn - 1 < 0)
                            {
                                CurrentColumn = HorizontalImages - 1;
                            }
                            else
                            {
                                CurrentColumn--;
                            }
                            CurrentRow = 0;
                        }
                        else
                        {
                            CurrentRow++;
                        }
                        break;
                    case TextureFlipBookMethod.TFBM_LL_ROW:
                        if (CurrentColumn + 1 >= HorizontalImages)
                        {
                            if (CurrentRow - 1 < 0)
                            {
                                CurrentRow = VerticalImages - 1;
                            }
                            else
                            {
                                CurrentRow--;
                            }
                            CurrentColumn = 0;
                        }
                        else
                        {
                            CurrentColumn++;
                        }
                        break;
                    case TextureFlipBookMethod.TFBM_LL_COL:
                        if (CurrentRow - 1 < 0)
                        {
                            if (CurrentColumn + 1 >= HorizontalImages)
                            {
                                CurrentColumn = 0;
                            }
                            else
                            {
                                CurrentColumn++;
                            }
                            CurrentRow = VerticalImages - 1;
                        }
                        else
                        {
                            CurrentRow--;
                        }
                        break;
                    case TextureFlipBookMethod.TFBM_LR_ROW:
                        if (CurrentColumn - 1 < 0)
                        {
                            if (CurrentRow - 1 < 0)
                            {
                                CurrentRow = VerticalImages - 1;
                            }
                            else
                            {
                                CurrentRow--;
                            }
                            CurrentColumn = HorizontalImages - 1;
                        }
                        else
                        {
                            CurrentColumn--;
                        }
                        break;
                    case TextureFlipBookMethod.TFBM_LR_COL:
                        if (CurrentRow - 1 < 0)
                        {
                            if (CurrentColumn - 1 < 0)
                            {
                                CurrentColumn = HorizontalImages - 1;
                            }
                            else
                            {
                                CurrentColumn--;
                            }
                            CurrentRow = VerticalImages - 1;
                        }
                        else
                        {
                            CurrentRow--;
                        }
                        break;
                    case TextureFlipBookMethod.TFBM_RANDOM:
                        CurrentColumn = (int)MathF.Truncate(Random.Shared.NextSingle() * HorizontalImages);
                        CurrentRow = (int)MathF.Truncate(Random.Shared.NextSingle() * VerticalImages);
                        break;
                }
            }
        }

        public LinearColor GetTextureOffset(UniformExpressionRenderContext context)
        {
            Tick(context.CurrentTime);
            return new LinearColor(HorizontalScale * CurrentColumn, VerticalScale * CurrentRow, 0, 0);
        }

        public LinearColor GetTextureScale() => new(HorizontalScale, VerticalScale, 0, 0);
    }

    public MeshRenderContext RenderContext { get; }

    //a failed read may pass, such as another tool having the archive locked, but a missing or corrupt archive won't
    private const int MaxStreamingAttempts = 3;
    private const int StreamingRetryFrames = 600;

    private long streamingFrame;
    private StreamingJob streamingJob;
    //the entries requested since the last EndStreamingFrame. Only touched between and during frames, so it needs no lock
    private readonly HashSet<TextureEntry> requested = [];

    private sealed class StreamingJob
    {
        public TextureEntry Entry;
        public StreamedTextureSource Source;
        public int Mip;
        public readonly CancellationTokenSource Cancellation = new();
        public Task<TextureResources> Task;
    }

    /// <param name="pixels">The size that decides the mip</param>
    /// <param name="priority">Among a frame's requests, the largest is streamed first</param>
    internal void RequestDetail(TextureEntry entry, float pixels, float priority)
    {
        if (entry.StreamingSource is not { } source) return;
        int mip = source.MipForPixels(pixels);
        if (entry.RequestedFrame != streamingFrame)
        {
            entry.RequestedFrame = streamingFrame;
            entry.RequestedMip = mip;
            entry.Priority = priority;
            requested.Add(entry);
        }
        else
        {
            entry.RequestedMip = Math.Min(entry.RequestedMip, mip);
            entry.Priority = Math.Max(entry.Priority, priority);
        }
    }

    /// <summary>Publishes at most one completed replacement, strictly between frames. Never waits for I/O.</summary>
    internal void BeginStreamingFrame()
    {
        streamingFrame++;
        if (streamingJob?.Task.IsCompleted != true) return;
        var job = streamingJob;
        var entry = job.Entry;
        streamingJob = null;
        try
        {
            var ready = job.Task.GetAwaiter().GetResult();
            if (job.Cancellation.IsCancellationRequested || entry.StreamingSource != job.Source) ready.Dispose();
            else if (!entry.WouldLoseMips(ready, job.Mip)) entry.Adopt(ready, job.Mip);
            else if (TryScheduleRetry(entry)) ready.Dispose();
            else
            {
                //the mips it lacks only matter when minified, while the mip that was asked for matters up close
                LECLog.Warning($"Could not read all the mips of texture {entry.InstanceFullPath}, so it will alias when minified");
                entry.Adopt(ready, job.Mip);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            // Keep the resident texture, and don't retry every frame.
            if (entry.StreamingSource == job.Source && !TryScheduleRetry(entry))
            {
                entry.StreamingSource = null;
                LECLog.Warning($"Could not stream texture {entry.InstanceFullPath}, so it stays at low resolution: {e.Message}");
            }
        }
        finally { job.Cancellation.Dispose(); }
    }

    /// <returns>False if that was the entry's last attempt</returns>
    private bool TryScheduleRetry(TextureEntry entry)
    {
        if (++entry.StreamingFailures >= MaxStreamingAttempts) return false;
        entry.RetryFrame = streamingFrame + StreamingRetryFrames;
        return true;
    }

    /// <summary>
    /// Chooses among this frame's requests, deduplicated across all materials using an entry.
    /// One worker and at most one completed texture bound the CPU memory, I/O and driver pressure of work in flight.
    /// Resident detail is never dropped again, so VRAM grows towards the cost of full loads until the level closes.
    /// </summary>
    internal void EndStreamingFrame()
    {
        TextureEntry next = null;
        if (streamingJob is null)
        {
            foreach (var entry in requested)
            {
                if (entry.StreamingSource is null || entry.RequestedFrame != streamingFrame || entry.RequestedMip >= entry.ResidentMip
                    || streamingFrame < entry.RetryFrame) continue;
                if (next is null || entry.Priority > next.Priority) next = entry;
            }
        }
        requested.Clear();
        if (next is null) return;
        var job = new StreamingJob { Entry = next, Source = next.StreamingSource, Mip = next.RequestedMip };
        var device = RenderContext.Device;
        job.Task = Task.Run(() => job.Source.Load(device, job.Mip, job.Cancellation.Token));
        streamingJob = job;
    }

    private void CancelStreaming(TextureEntry entry)
    {
        if (streamingJob?.Entry == entry) streamingJob.Cancellation.Cancel();
    }

    /// <summary>Called only at scene teardown/cache reset, before the device is released.</summary>
    private void StopStreaming()
    {
        var job = streamingJob;
        streamingJob = null;
        if (job is null) return;
        job.Cancellation.Cancel();
        try { job.Task.GetAwaiter().GetResult().Dispose(); }
        catch (OperationCanceledException) { }
        catch (Exception e) { Debug.WriteLine($"Texture streaming stopped: {e.Message}"); }
        finally { job.Cancellation.Dispose(); }
    }

    /// <summary>
    /// Creates a new PreviewTextureCache.
    /// </summary>
    /// <param name="renderContext">The <see cref="RenderContext"/> to create texture and views for.</param>
    public PreviewTextureCache(MeshRenderContext renderContext)
    {
        this.RenderContext = renderContext;
    }

    /// <summary>
    /// Removes items from the cache that are over 1 minute old
    /// </summary>
    public void ExpungeStaleCacheItems()
    {
        lock (textureLoadLock)
        {
            TimeSpan oneMinute = TimeSpan.FromMinutes(1);
            foreach (var (key, entry) in AssetCache)
            {
                if (DateTime.Now - entry.LastUsageTime > oneMinute)
                {
                    entry.Dispose();
                    //Remove does not actually invalidate the enumerator
                    AssetCache.Remove(key);
                }
            }
        }
    }

    /// <summary>
    /// Stops streaming and disposes all the textures and resource views. The cache can be used again afterwards.
    /// </summary>
    public void Clear()
    {
        lock (textureLoadLock)
        {
            StopStreaming();
            requested.Clear();
            AssetCache.DisposeValuesAndClear();
        }
    }

    public void Dispose() => Clear();

    /// <summary>
    /// Reloads the cached textures read from edited exports in place. Call between frames.
    /// (A relinked import has a new path, so it's cached separately the next time it's loaded)
    /// </summary>
    /// <returns>Whether any cached texture was affected</returns>
    public bool Invalidate(IMEPackage package, IReadOnlySet<int> exports)
    {
        bool changed = false;
        lock (textureLoadLock)
        {
            foreach (var (key, entry) in AssetCache)
            {
                if (entry.Export?.FileRef != package || !exports.Contains(entry.Export.UIndex)) continue;
                changed = true;
                try
                {
                    entry.Reload(RenderContext);
                }
                catch (Exception e)
                {
                    //renders as missing, and is loaded afresh the next time it's asked for
                    Debug.WriteLine($"Could not reload texture {entry.InstanceFullPath}: {e.Message}");
                    entry.Dispose();
                    AssetCache.Remove(key);
                }
            }
        }
        return changed;
    }

    /// <summary>
    /// Stores loaded textures by <see cref="CacheKey"/>.
    /// </summary>
    private Dictionary<string, TextureEntry> AssetCache { get; } = [];

    /// <summary>
    /// Identifies a texture asset: copies of the same texture in different packages share it, and an import shares it with the export it resolves to
    /// </summary>
    private static string CacheKey(IEntry texture) => $"{texture.ClassName}|{texture.MemoryFullPath}";

    private readonly Lock textureLoadLock = new Lock();

    /// <summary>
    /// Queues a texture for eventual loading.
    /// </summary>
    public TextureEntry LoadTexture(IEntry textureEntry, PackageCache packageCache = null)
    {
        string ifp = textureEntry.InstancedFullPath;
        string cacheKey = CacheKey(textureEntry);
        lock (textureLoadLock)
        {
            //found without resolving an import
            if (AssetCache.TryGetValue(cacheKey, out TextureEntry entry))
            {
                entry.LastUsageTime = DateTime.Now;
                return entry;
            }
            if (textureEntry is ImportEntry import)
            {
                textureEntry = EffectiveLightingProperties.Resolve(import, packageCache);
            }
            if (textureEntry is ExportEntry textureExport)
            {
                try
                {
                    entry = textureExport.ClassName is "TextureFlipBook" ? new FlipBookTextureEntry(RenderContext, textureExport) : new TextureEntry(RenderContext, textureExport);
                    AssetCache.Add(cacheKey, entry);
                    return entry;
                }
                catch
                {
                    //just do the error path below
                }
            }
        }
        Debug.WriteLine($"Unable to resolve texture: {ifp}");
        return null;
    }
}
