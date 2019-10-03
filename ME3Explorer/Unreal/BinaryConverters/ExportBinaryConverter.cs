using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gammtek.Conduit.Extensions.IO;
using ME3Explorer.Packages;
using ME3Explorer.Unreal.Classes;
using StreamHelpers;
using static ME3Explorer.Unreal.BinaryConverters.ObjectBinary;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public static class ExportBinaryConverter
    {
        public static ObjectBinary ConvertPostPropBinary(ExportEntry export, MEGame newGame)
        {
            if (export.getBinaryData().Length == 0)
            {
                return new GenericObjectBinary(new byte[0]);
            }

            if (From(export) is ObjectBinary objbin)
            {
                return objbin;
            }

            if (export.IsTexture())
            {
                return new GenericObjectBinary(ConvertTexture2D(export, newGame));
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
                        return new GenericObjectBinary(Array.Empty<byte>());
                    }
                    else if (export.Game == MEGame.UDK && newGame != MEGame.UDK)
                    {
                        return new GenericObjectBinary(new byte[8]);
                    }
                    break;
            }

            //no conversion neccesary
            return new GenericObjectBinary(export.getBinaryData());
        }

        public static byte[] ConvertPrePropBinary(ExportEntry export, MEGame newGame)
        {
            if (export.HasStack)
            {
                var ms = new MemoryStream(export.Data);
                int node = ms.ReadInt32();
                int stateNode = ms.ReadInt32();
                ulong probeMask = ms.ReadUInt64();
                if (export.Game == MEGame.ME3)
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

                var os = new MemoryStream();
                os.WriteInt32(node);
                os.WriteInt32(stateNode);
                os.WriteUInt64(probeMask);
                if (newGame == MEGame.ME3)
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

            return export.Data.Slice(0, export.GetPropertyStart());
        }

        public static byte[] ConvertTexture2D(ExportEntry export, MEGame newGame, List<int> offsets = null, StorageTypes newStorageType = StorageTypes.empty)
        {
            MemoryStream bin = new MemoryStream(export.getBinaryData());
            if (bin.Length == 0)
            {
                return bin.ToArray();
            }
            var os = new MemoryStream();

            if (export.Game != MEGame.ME3)
            {
                bin.Skip(16);
            }
            if (newGame != MEGame.ME3)
            {
                os.WriteZeros(16);//includes fileOffset, but that will be set during save
            }

            int mipCount = bin.ReadInt32();
            os.WriteInt32(mipCount);
            List<Texture2DMipInfo> mips = Texture2D.GetTexture2DMipInfos(export, export.GetProperty<NameProperty>("TextureFileCacheName")?.Value);
            int offsetIdx = 0;
            for (int i = 0; i < mipCount; i++)
            {
                var storageType = (StorageTypes)bin.ReadInt32();
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
                        texture = bin.ReadToBuffer(compressedSize);
                        if (offsets != null)
                        {
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
                            texture = Texture2D.GetTextureData(mips[i], false); //copy in external textures
                        }
                        else
                        {
                            texture = Array.Empty<byte>();
                        }
                        break;

                }

                int width = bin.ReadInt32();
                int height = bin.ReadInt32();

                os.WriteInt32((int)storageType);
                os.WriteInt32(uncompressedSize);
                os.WriteInt32(compressedSize);
                os.WriteInt32(fileOffset);//fileOffset will be fixed during save
                os.WriteFromBuffer(texture);
                os.WriteInt32(width);
                os.WriteInt32(height);

            }

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

            if (export.Game == MEGame.ME3)
            {
                bin.Skip(4);
            }
            if (newGame == MEGame.ME3)
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
