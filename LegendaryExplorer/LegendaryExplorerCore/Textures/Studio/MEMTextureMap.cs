using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Textures.Studio
{
    /// <summary>
    /// Precomputed texture map info (from Mass EFfect Modder "MEM")
    /// </summary>
    public class MEMTextureMap
    {
        public static Dictionary<uint, TextureMapEntry> LoadTextureMap(MEGame game)
        {
            // Read the vanilla file name table
            List<string> packageNames = null;
            {
                using var vanillaInfoStream = LegendaryExplorerCoreUtilities.LoadEmbeddedFile($"Precomputed.FileTable.vanilla{game}.bin");
                if (vanillaInfoStream.ReadStringLatin1(4) != @"MD5T")
                {
                    throw new Exception(@"Header of MD5 table doesn't match expected value!");
                }

                //Decompress
                var decompressedSize = vanillaInfoStream.ReadInt32();
                //var compressedSize = stream.Length - stream.Position;

                var compressedBuffer = vanillaInfoStream.ReadToBuffer(vanillaInfoStream.Length - vanillaInfoStream.Position);
                var decompressedBuffer = LZMA.Decompress(compressedBuffer, (uint)decompressedSize);
                if (decompressedBuffer.Length != decompressedSize)
                {
                    throw new Exception(@"Vanilla database failed to decompress");
                }

                //Read
                MemoryStream table = new MemoryStream(decompressedBuffer);
                int numEntries = table.ReadInt32();
                packageNames = new List<string>(numEntries);
                //Package names
                for (int i = 0; i < numEntries; i++)
                {
                    //Read entry
                    packageNames.Add(table.ReadStringLatin1Null().Replace('/', '\\').TrimStart('\\'));
                }
            }


            using var fs = LegendaryExplorerCoreUtilities.LoadEmbeddedFile($"Precomputed.TextureMap.vanilla{game}Map.bin");


            // read the precomputed vanilla texture map.
            // this map will help identify vanilla textures

            var magic = fs.ReadInt32();
            if (magic != 0x504D5443)
                throw new Exception(@"Invalid precomputed texture map! Wrong magic");
            var decompSize = fs.ReadUInt32();
            byte[] compresssed = fs.ReadToBuffer(fs.ReadInt32());

            var texMap = new MemoryStream(LZMA.Decompress(compresssed, decompSize));
            texMap.Seek(8, SeekOrigin.Begin); // skip magic, ???

            var textureCount = texMap.ReadInt32();
            Dictionary<uint, TextureMapEntry> map = new Dictionary<uint, TextureMapEntry>(textureCount);
            for (int i = 0; i < textureCount; i++)
            {
                var entry = TextureMapEntry.ReadTextureMapEntry(texMap, game, packageNames);
                map[entry.CRC] = entry;
            }

            return map;
        }

        /// <summary>
        /// Entry for the MEM Texture Map
        /// </summary>
        public class TextureMapEntry
        {

            public static TextureMapEntry ReadTextureMapEntry(MemoryStream texMap, MEGame game, List<string> packageNames)
            {
                TextureMapEntry tme = new TextureMapEntry();
                tme.Name = texMap.ReadStringLatin1(texMap.ReadByte());
                tme.CRC = texMap.ReadUInt32();
                tme.Width = texMap.ReadInt16();
                tme.Height = texMap.ReadInt16();
                tme.PixelFormat = (PixelFormat)texMap.ReadByte();
                tme.Flags = texMap.ReadByte();
                int countPackages = texMap.ReadInt16();
                for (int k = 0; k < countPackages; k++)
                {
                    var matched = new TextureMapPackageEntry();
                    matched.UIndex = texMap.ReadInt32();
                    if (game == MEGame.ME1)
                    {
                        var isSlaveTexture = texMap.ReadInt16();
                        if (isSlaveTexture != -1)
                        {
                            matched.ME1IsSlave = true; //This file has external mips that point to a master package file
                            matched.MasterPackageName = texMap.ReadStringLatin1Null();
                        }
                        matched.ME1TextureOffset = texMap.ReadUInt32();
                    }
                    matched.NumEmptyMips = texMap.ReadByte();
                    matched.NumMips = texMap.ReadByte();
                    //matched.IsMovieTexture = (texture.flags == TextureProperty::TextureTypes::Movie);
                    var packageIndex = texMap.ReadInt16();
                    matched.RelativePackagePath = packageNames[packageIndex].Replace("\\", "/"); // Not sure what pkgs is
                    matched.PackageName = Path.GetFileName(matched.RelativePackagePath);
                    tme.ContainingPackages.Add(matched);
                }

                return tme;
            }

            public short Width { get; set; }

            public short Height { get; set; }

            public List<TextureMapPackageEntry> ContainingPackages { get; } = new();
            /// <summary>
            /// Expected object CRC
            /// </summary>
            public uint CRC { get; set; }

            public int Flags { get; set; }

            public PixelFormat PixelFormat { get; set; }

            public string Name { get; set; }
        }

        ///// <summary>
        ///// Describes where a texture can be found
        ///// </summary>
        //public class TextureMapPackageEntry
        //{
        //    /// <summary>
        //    /// In-package UIndex
        //    /// </summary>
        //    public int UIndex { get; set; }
        //    /// <summary>
        //    /// The number of mips
        //    /// </summary>
        //    public int NumMips { get; set; }
        //    /// <summary>
        //    /// The number of empty mips
        //    /// </summary>
        //    public int NumEmptyMips { get; set; }

        //    /// <summary>
        //    /// Contains offset and size information about the compressed mips
        //    /// </summary>
        //    public List<CompressedMipInfo> CompressedMipInfos = new List<CompressedMipInfo>(7);

        //    /// <summary>
        //    /// If this entry is for TextureMovie
        //    /// </summary>
        //    public bool IsMovieTexture { get; set; }
        //    /// <summary>
        //    /// Relative path to the package
        //    /// </summary>
        //    public string RelativePackagePath { get; set; }
        //    /// <summary>
        //    /// The name of the package
        //    /// </summary>
        //    public string PackageName { get; set; }
        //    /// <summary>
        //    /// ME1 Only: If this package references a master package
        //    /// </summary>
        //    public bool IsSlave { get; set; }
        //    /// <summary>
        //    /// ME1 Only: The name of the Master Package that contains the higher mips
        //    /// </summary>
        //    public string MasterPackageName { get; set; }
        //    /// <summary>
        //    /// ME1 Only: The master package offset (?)
        //    /// </summary>
        //    public uint TextureOffset { get; set; }
        //    /// <summary>
        //    /// Instance-specific CRC of the top non-empty mip
        //    /// </summary>
        //    public uint CRC { get; set; }
        //}

        public class CompressedMipInfo
        {
            public int CompressedSize { get; set; }
            public int Offset { get; set; }
        }
    }
}
