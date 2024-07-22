using System;
using System.Collections.Generic;
using System.Diagnostics;
using LegendaryExplorer.UnrealExtensions.Classes;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Classes;
using SharpDX.Direct3D11;
using Texture2D = SharpDX.Direct3D11.Texture2D;

namespace LegendaryExplorer.UserControls.SharedToolControls.Scene3D
{
    /// <summary>
    /// Loads and caches textures for a Direct3D11 renderer
    /// </summary>
    public class PreviewTextureCache : IDisposable
    {
        /// <summary>
        /// Stores a texture and load state in the cache.
        /// </summary>
        public class PreviewTextureEntry : IDisposable
        {
            /// <summary>
            /// Texture export for this cache entry
            /// </summary>
            //public ExportEntry TextureExport { get; set; }
            public string InstanceFullPath { get; set; }
            /// <summary>
            /// The Direct3D ShaderResourceView for binding to shaders.
            /// </summary>
            public ShaderResourceView TextureView;

            /// <summary>
            /// The Direct3D texture for ShaderResourceView creation.
            /// </summary>
            public Texture2D Texture;

            /// <summary>
            /// The time this object was last accessed.
            /// </summary>
            public DateTime LastUsageTime = DateTime.Now;

            /// <summary>
            /// Creates a new cache entry for the given texture.
            /// </summary>
            public PreviewTextureEntry(ExportEntry export)
            {
                MemoryAnalyzer.AddTrackedMemoryItem($"PreviewTexture {export.ObjectName}", new WeakReference(this));
                InstanceFullPath = export.InstancedFullPath;
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

        public RenderContext RenderContext { get; }

        /// <summary>
        /// Creates a new PreviewTextureCache.
        /// </summary>
        /// <param name="renderContext">The <see cref="RenderContext"/> to create texture and views for.</param>
        public PreviewTextureCache(RenderContext renderContext)
        {
            this.RenderContext = renderContext;
        }

        /// <summary>
        /// Removes items from the cache that are over 1 minute old
        /// </summary>
        public void ExpungeStaleCacheItems()
        {
            for (int i = AssetCache.Count - 1; i > 0; i--)
            {
                if (DateTime.Now - AssetCache[i].LastUsageTime > TimeSpan.FromMinutes(1))
                {
                    Debug.WriteLine($"Expunging PreviewTextureCache stale item: {AssetCache[i].InstanceFullPath}");
                    AssetCache[i].Dispose();
                    AssetCache.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Disposes all the textures and resource views.
        /// </summary>
        public void Dispose()
        {
            foreach (PreviewTextureEntry e in AssetCache)
            {
                e.Dispose();
            }
            AssetCache.Clear();
        }

        /// <summary>
        /// Stores loaded textures by their full name.
        /// </summary>
        public ObservableCollectionExtended<PreviewTextureEntry> AssetCache { get; } = new();

        /// <summary>
        /// Queues a texture for eventual loading.
        /// </summary>
        /// <param name="pcc">The full path of the pcc where the texture export is.</param>
        /// <param name="exportid"></param>
        //public PreviewTextureEntry LoadTexture(string pcc, int exportid)
        //{
        //    foreach (PreviewTextureEntry e in cache)
        //    {
        //        if (e.PCCPath == pcc && e.ExportID == exportid)
        //        {
        //            return e;
        //        }
        //    }
        //    using (var texpcc = MEPackageHandler.OpenMEPackage(pcc))
        //    {
        //        PreviewTextureEntry entry = new PreviewTextureEntry(pcc, exportid);
        //        Unreal.Classes.Texture2D metex = new Unreal.Classes.Texture2D(texpcc.getUExport(exportid));
        //        try
        //        {
        //            entry.Texture = metex.generatePreviewTexture(Device, out Texture2DDescription _);
        //            entry.TextureView = new ShaderResourceView(Device, entry.Texture);
        //            cache.Add(entry);
        //            return entry;
        //        } catch
        //        {
        //            return null;
        //        }
        //    }
        //}

        /// <summary>
        /// Queues a texture for eventual loading.
        /// </summary>
        public PreviewTextureEntry LoadTexture(ExportEntry export, Texture2DMipInfo preloadedMipInfo = null, byte[] decompressedTextureData = null)
        {
            foreach (PreviewTextureEntry e in AssetCache)
            {
                // Same full paths are assumed to be identical. Leaving this here in case this needs changing for some reason.
                if (/*e.TextureExport.FileRef.FilePath == export.FileRef.FilePath && */e.InstanceFullPath == export.InstancedFullPath)
                {
                    e.LastUsageTime = DateTime.Now;
                    return e;
                }
            }
            var entry = new PreviewTextureEntry(export);
            var metex = new LegendaryExplorerCore.Unreal.Classes.Texture2D(export);
            try
            {
                if (preloadedMipInfo != null && metex.Export != preloadedMipInfo.Export) throw new Exception();
                entry.Texture = this.RenderContext.LoadUnrealTexture(new LegendaryExplorerCore.Unreal.Classes.Texture2D(export));
                entry.TextureView = new ShaderResourceView(this.RenderContext.Device, entry.Texture);
                AssetCache.Add(entry);
                return entry;
            }
            catch
            {
                return null;
            }
        }
    }
}
