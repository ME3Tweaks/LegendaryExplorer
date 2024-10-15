using System;
using System.Diagnostics;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
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
        public class TextureEntry : IDisposable
        {
            /// <summary>
            /// Texture export for this cache entry
            /// </summary>
            //public ExportEntry TextureExport { get; set; }
            public string InstanceFullPath { get; }

            /// <summary>
            /// The Direct3D ShaderResourceView for binding to shaders.
            /// </summary>
            public readonly ShaderResourceView TextureView;

            /// <summary>
            /// The Direct3D texture for ShaderResourceView creation.
            /// </summary>
            public readonly Texture2D Texture;

            /// <summary>
            /// The time this object was last accessed.
            /// </summary>
            public DateTime LastUsageTime = DateTime.Now;

            public readonly bool IsTextureCube;

            /// <summary>
            /// Creates a new cache entry for the given texture.
            /// </summary>
            public TextureEntry(RenderContext renderContext, ExportEntry export)
            {
                MemoryAnalyzer.AddTrackedMemoryItem($"PreviewTexture {export.ObjectName}", new WeakReference(this));
                InstanceFullPath = export.InstancedFullPath;
                IsTextureCube = export.ClassName == "TextureCube";

                Texture = IsTextureCube ? renderContext.LoadUnrealTextureCube(export) : renderContext.LoadUnrealTexture(export);
                TextureView = new ShaderResourceView(renderContext.Device, Texture);
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
            foreach (TextureEntry e in AssetCache)
            {
                e.Dispose();
            }
            AssetCache.Clear();
        }

        /// <summary>
        /// Stores loaded textures by their full name.
        /// </summary>
        public ObservableCollectionExtended<TextureEntry> AssetCache { get; } = new();

        /// <summary>
        /// Queues a texture for eventual loading.
        /// </summary>
        public TextureEntry LoadTexture(ExportEntry export)
        {
            foreach (TextureEntry e in AssetCache)
            {
                // Same full paths are assumed to be identical. Leaving this here in case this needs changing for some reason.
                if (/*e.TextureExport.FileRef.FilePath == export.FileRef.FilePath && */e.InstanceFullPath == export.InstancedFullPath)
                {
                    e.LastUsageTime = DateTime.Now;
                    return e;
                }
            }
            try
            {
                var entry = new TextureEntry(RenderContext, export);
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
