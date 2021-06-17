using System;
using System.Collections.Generic;
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
            public ExportEntry TextureExport;

            /// <summary>
            /// The Direct3D ShaderResourceView for binding to shaders.
            /// </summary>
            public ShaderResourceView TextureView;

            /// <summary>
            /// The Direct3D texture for SHaderResourceView creation.
            /// </summary>
            public Texture2D Texture;

            /// <summary>
            /// Creates a new cache entry for the given texture.
            /// </summary>
            public PreviewTextureEntry(ExportEntry export)
            {
                MemoryAnalyzer.AddTrackedMemoryItem($"PreviewTexture {export.ObjectName}",new WeakReference(this));
                TextureExport = export;
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
        /// Disposes all the textures and resource views.
        /// </summary>
        public void Dispose()
        {
            foreach (PreviewTextureEntry e in cache)
            {
                e.Dispose();
            }
            cache.Clear();
        }

        /// <summary>
        /// Stores loaded textures by their full name.
        /// </summary>
        public readonly List<PreviewTextureEntry> cache = new();

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
            foreach (PreviewTextureEntry e in cache)
            {
                if (e.TextureExport.FileRef.FilePath == export.FileRef.FilePath && e.TextureExport.UIndex == export.UIndex)
                {
                    return e;
                }
            }
            //using (var texpcc = MEPackageHandler.OpenMEPackage(pcc))
            //{
            var entry = new PreviewTextureEntry(export);
            var metex = new LegendaryExplorerCore.Unreal.Classes.Texture2D(export);
            try
            {
                if (preloadedMipInfo != null && metex.Export != preloadedMipInfo.Export) throw new Exception();
                entry.Texture = SharedToolControls.Scene3D.RenderContextExtensions.LoadUnrealTexture(this.RenderContext, new LegendaryExplorerCore.Unreal.Classes.Texture2D(export));
                entry.TextureView = new ShaderResourceView(this.RenderContext.Device, entry.Texture);
                cache.Add(entry);
                return entry;
            }
            catch
            {
                return null;
            }
            //}
        }
    }
}
