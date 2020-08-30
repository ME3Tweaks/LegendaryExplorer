using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal.Classes;
using SharpDX;
using SharpDX.Direct3D11;
using static ME3Explorer.TextureViewerExportLoader;
using Texture2D = SharpDX.Direct3D11.Texture2D;

namespace ME3Explorer.Scene3D
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

        /// <summary>
        /// The DIrect3D11 device to create textures and resource views with.
        /// </summary>
        public Device Device { get; }

        /// <summary>
        /// Creates a new PreviewTextureCache.
        /// </summary>
        /// <param name="device">The DIrect3D11 device to create textures and resource views with.</param>
        public PreviewTextureCache(Device device)
        {
            Device = device;
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
        public readonly List<PreviewTextureEntry> cache = new List<PreviewTextureEntry>();

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
        /// <param name="pcc">The full path of the pcc where the texture export is.</param>
        /// <param name="exportid"></param>
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
            PreviewTextureEntry entry = new PreviewTextureEntry(export);
            ME3ExplorerCore.Unreal.Classes.Texture2D metex = new ME3ExplorerCore.Unreal.Classes.Texture2D(export);
            try
            {
                if (preloadedMipInfo != null && metex.Export != preloadedMipInfo.Export) throw new Exception();
                entry.Texture = metex.generatePreviewTexture(Device, out Texture2DDescription _, preloadedMipInfo, decompressedTextureData);
                entry.TextureView = new ShaderResourceView(Device, entry.Texture);
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
