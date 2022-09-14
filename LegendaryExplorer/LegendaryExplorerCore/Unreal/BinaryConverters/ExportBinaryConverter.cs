using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Classes;
using static LegendaryExplorerCore.Unreal.BinaryConverters.ObjectBinary;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public static class ExportBinaryConverter
    {
        public static ObjectBinary ConvertPostPropBinary(ExportEntry export, MEGame newGame, PropertyCollection newProps)
        {
            if (export.propsEnd() == export.DataSize)
            {
                return Array.Empty<byte>();
            }

            if (export.IsTexture())
            {
                return ConvertTexture2D(export, newGame);
            }

            if (From(export) is ObjectBinary objbin)
            {
                if (objbin is AnimSequence animSeq)
                {
                    animSeq.UpdateProps(newProps, newGame);
                }
                return objbin;
            }

            switch (export.ClassName)
            {
                case "DirectionalLightComponent":
                case "PointLightComponent":
                case "SkyLightComponent":
                case "SphericalHarmonicLightComponent":
                case "SpotLightComponent":
                case "DominantSpotLightComponent":
                case "DominantPointLightComponent":
                case "DominantDirectionalLightComponent":
                    if (newGame == MEGame.UDK)
                    {
                        return Array.Empty<byte>();
                    }
                    else if (export.Game == MEGame.UDK)
                    {
                        return new byte[8];
                    }
                    break;
            }

            //no conversion neccesary
            return export.GetBinaryData();
        }

        public static byte[] ConvertPrePropBinary(ExportEntry export, MEGame newGame)
        {
            if (export.HasStack)
            {
                var ms = new MemoryStream(export.Data);
                int node = ms.ReadInt32();
                int stateNode = 0;
                if (export.Game != MEGame.UDK)
                {
                    stateNode = ms.ReadInt32();
                }
                ulong probeMask = ms.ReadUInt64();
                if (export.Game >= MEGame.ME3)
                {
                    ms.SkipInt16();
                }
                else
                {
                    ms.SkipInt32();
                }

                int count = ms.ReadInt32();
                byte[] stateStack = ms.ReadToBuffer(count * 12);
                int offset = 0;
                if (node != 0)
                {
                    offset = ms.ReadInt32();
                }

                int netIndex = ms.ReadInt32();

                using var os = MemoryManager.GetMemoryStream();
                os.WriteInt32(node);
                if (newGame != MEGame.UDK)
                {
                    os.WriteInt32(stateNode);
                }
                os.WriteUInt64(probeMask);
                if (newGame >= MEGame.ME3)
                {
                    os.WriteUInt16(0);
                }
                else
                {
                    os.WriteUInt32(0);
                }
                os.WriteInt32(count);
                os.WriteFromBuffer(stateStack);
                if (node != 0)
                {
                    os.WriteInt32(offset);
                }
                os.WriteInt32(netIndex);

                return os.ToArray();
            }

            switch (export.ClassName)
            {
                case "DominantSpotLightComponent":
                case "DominantDirectionalLightComponent":
                    //todo: do conversion for these
                    break;
            }

            return export.DataReadOnly.Slice(0, export.GetPropertyStart()).ToArray();
        }

        public static byte[] ConvertTexture2D(ExportEntry export, MEGame newGame, List<int> offsets = null, StorageTypes newStorageType = StorageTypes.empty)
        {
            MemoryStream bin = export.GetReadOnlyBinaryStream();
            if (bin.Length == 0)
            {
                return bin.ToArray();
            }
            using var os = MemoryManager.GetMemoryStream();

            if (!export.Game.IsGame3())
            {
                bin.Skip(8);
                bin.Skip(bin.ReadInt32()); // Skip the thumbnail
                bin.Skip(4); // Skip fileOffset
            }
            if (!newGame.IsGame3())
            {
                os.WriteZeros(16);//includes fileOffset, but that will be set during save
            }

            int mipCount = bin.ReadInt32();
            long mipCountPosition = os.Position;
            os.WriteInt32(mipCount);
            List<Texture2DMipInfo> mips = Texture2D.GetTexture2DMipInfos(export, export.GetProperty<NameProperty>("TextureFileCacheName")?.Value);
            int offsetIdx = 0;
            int trueMipCount = 0;
            for (int i = 0; i < mipCount; i++)
            {
                var storageType = (StorageTypes)bin.ReadInt32(); // The existing storage type
                int uncompressedSize = bin.ReadInt32();
                int compressedSize = bin.ReadInt32();
                int fileOffset = bin.ReadInt32();
                byte[] texture;
                switch (storageType)
                {
                    case StorageTypes.pccUnc:
                        texture = bin.ReadToBuffer(uncompressedSize);
                        break;
                    case StorageTypes.pccLZO:
                    case StorageTypes.pccZlib:
                    case StorageTypes.pccOodle:
                        texture = bin.ReadToBuffer(compressedSize);

                        // Todo: This needs to be handled in the caller if it's handled there
                        texture = TextureCompression.ConvertTextureCompression(texture, uncompressedSize, ref storageType, newGame, true);
                        compressedSize = texture.Length;
                        if (offsets != null)
                        {
                            //the caller has transferred this mip, so use the provided data
                            texture = Array.Empty<byte>();
                            fileOffset = offsets[offsetIdx++];
                            compressedSize = offsets[offsetIdx] - fileOffset;
                            if (newStorageType != StorageTypes.empty)
                            {
                                storageType = newStorageType;
                            }
                        }
                        break;
                    case StorageTypes.empty:
                        texture = new byte[0];
                        break;
                    default:
                        if (export.Game != newGame)
                        {
                            storageType &= (StorageTypes)~StorageFlags.externalFile;
                            texture = Texture2D.GetTextureData(mips[i], export.Game, export.Game != MEGame.UDK ? MEDirectories.GetDefaultGamePath(export.Game) : null,false); //copy in external textures
                            if (storageType != StorageTypes.pccUnc)
                            {
                                texture = TextureCompression.ConvertTextureCompression(texture, uncompressedSize, ref storageType, newGame, true); // Convert the storage type to work with the listed game
                                compressedSize = texture.Length;
                            }
                        }
                        else
                        {
                            texture = Array.Empty<byte>();
                        }
                        break;

                }

                int width = bin.ReadInt32();
                int height = bin.ReadInt32();
                if (newGame == MEGame.UDK && storageType == StorageTypes.empty)
                {
                    continue;
                }
                trueMipCount++;
                os.WriteInt32((int)storageType);
                os.WriteInt32(uncompressedSize);
                os.WriteInt32(compressedSize);
                os.WriteInt32(fileOffset);//fileOffset will be fixed during save
                os.WriteFromBuffer(texture);
                os.WriteInt32(width);
                os.WriteInt32(height);

            }

            long postMipPosition = os.Position;
            os.JumpTo(mipCountPosition);
            os.WriteInt32(trueMipCount);
            os.JumpTo(postMipPosition);

            int unk1 = 0;
            if (export.Game != MEGame.UDK)
            {
                unk1 = bin.ReadInt32();
            }
            if (newGame != MEGame.UDK)
            {
                os.WriteInt32(unk1);
            }

            Guid textureGuid = export.Game != MEGame.ME1 ? bin.ReadGuid() : Guid.NewGuid();

            if (newGame != MEGame.ME1)
            {
                os.WriteGuid(textureGuid);
            }

            if (export.Game == MEGame.UDK)
            {
                bin.Skip(32);
            }
            if (newGame == MEGame.UDK)
            {
                os.WriteZeros(4 * 8);
            }

            if (export.Game == MEGame.ME3 || export.Game.IsLEGame())
            {
                bin.Skip(4);
            }
            if (newGame == MEGame.ME3 || newGame.IsLEGame())
            {
                os.WriteInt32(0);
            }
            if (export.ClassName == "LightMapTexture2D")
            {
                int lightMapFlags = 0;
                if (export.Game >= MEGame.ME3)
                {
                    lightMapFlags = bin.ReadInt32();
                }
                if (newGame >= MEGame.ME3)
                {
                    os.WriteInt32(0);//LightMapFlags noflag
                }
            }

            return os.ToArray();
        }
    }

}
