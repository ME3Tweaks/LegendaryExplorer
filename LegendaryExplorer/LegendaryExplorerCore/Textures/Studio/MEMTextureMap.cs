using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorerCore.Compression;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Textures.Studio
{
    /// <summary>
    /// Precomputed texture map info (from Mass EFfect Modder "MEM")
    /// </summary>
    public static class MEMTextureMap
    {
        public static Dictionary<uint, TextureMapEntry> LoadTextureMap(MEGame game)
        {
            // Read the vanilla file name table
            List<string> packageNames;
            using (MemoryStream vanillaInfoStream = LegendaryExplorerCoreUtilities.LoadEmbeddedFile($"Precomputed.FileTable.vanilla{game}.bin"))
            {
                if (vanillaInfoStream.ReadStringLatin1(4) != @"MD5T")
                {
                    throw new Exception(@"Header of MD5 table doesn't match expected value!");
                }

                //Decompress
                int decompressedSize = vanillaInfoStream.ReadInt32();
                //var compressedSize = stream.Length - stream.Position;

                byte[] compressedBuffer = vanillaInfoStream.ReadToBuffer(vanillaInfoStream.Length - vanillaInfoStream.Position);
                byte[] decompressedBuffer = LZMA.Decompress(compressedBuffer, (uint)decompressedSize);
                if (decompressedBuffer.Length != decompressedSize)
                {
                    throw new Exception(@"Vanilla database failed to decompress");
                }

                //Read
                var table = new MemoryStream(decompressedBuffer);
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

            int magic = fs.ReadInt32();
            if (magic != 0x504D5443)
                throw new Exception(@"Invalid precomputed texture map! Wrong magic");
            uint decompSize = fs.ReadUInt32();
            byte[] compresssed = fs.ReadToBuffer(fs.ReadInt32());

            var texMap = new MemoryStream(LZMA.Decompress(compresssed, decompSize));
            texMap.Seek(8, SeekOrigin.Begin); // skip magic, ???

            int textureCount = texMap.ReadInt32();
            var map = new Dictionary<uint, TextureMapEntry>(textureCount);
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
                var tme = new TextureMapEntry
                {
                    Name = texMap.ReadStringLatin1(texMap.ReadByte()),
                    CRC = texMap.ReadUInt32(),
                    Width = texMap.ReadInt16(),
                    Height = texMap.ReadInt16(),
                    PixelFormat = (PixelFormat)texMap.ReadByte(),
                    Flags = texMap.ReadByte()
                };
                int countPackages = texMap.ReadInt16();
                for (int k = 0; k < countPackages; k++)
                {
                    var matched = new TextureMapPackageEntry
                    {
                        UIndex = texMap.ReadInt32()
                    };
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

        public class CompressedMipInfo
        {
            public StorageTypes StorageType { get; set; }
            public int CompressedSize { get; set; }
            public int UncompressedSize { get; set; }
            public int Offset { get; set; }
        }
    }
}
