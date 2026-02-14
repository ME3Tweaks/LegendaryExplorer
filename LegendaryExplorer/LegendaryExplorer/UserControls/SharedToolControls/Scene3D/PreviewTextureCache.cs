using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct3D11;
using System;
using System.Diagnostics;
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

            readonly float FrameRate;
            readonly int HorizontalImages;
            readonly int VerticalImages;
            readonly TextureFlipBookMethod FBMethod;

            readonly float FrameTime;
            readonly float HorizontalScale;
            readonly float VerticalScale;

            int CurrentRow;
            int CurrentColumn;
            float LastFrameTime;

            public FlipBookTextureEntry(RenderContext renderContext, ExportEntry export) : base(renderContext, export)
            {
                var props = export.GetProperties();
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
                var entry = export.ClassName == "TextureFlipBook" ? new FlipBookTextureEntry(RenderContext, export) : new TextureEntry(RenderContext, export);
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
