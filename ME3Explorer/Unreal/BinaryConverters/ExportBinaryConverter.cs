using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gammtek.Conduit.Extensions.IO;
using ME3Explorer.Packages;
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

            if (export.ClassName == "Model")
            {
                return new GenericObjectBinary(ConvertModel(export, newGame));
            }

            if (export.IsTexture())
            {
                return new GenericObjectBinary(ConvertTexture2D(export, newGame));
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
            List<EmbeddedTextureViewer.Texture2DMipInfo> mips = EmbeddedTextureViewer.GetTexture2DMipInfos(export, export.GetProperty<NameProperty>("TextureFileCacheName")?.Value);
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
                            texture = EmbeddedTextureViewer.GetTextureData(mips[i], false); //copy in external textures
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
            os.WriteInt32(bin.ReadInt32());
            Guid textureGuid = export.Game != MEGame.ME1 ? bin.ReadGuid() : Guid.NewGuid();

            if (newGame != MEGame.ME1)
            {
                os.WriteGuid(textureGuid);
            }

            if (newGame == MEGame.ME3)
            {
                os.WriteInt32(0);
                if (export.ClassName == "LightMapTexture2D")
                {
                    os.WriteInt32(0);//LightMapFlags noflag
                }
            }

            return os.ToArray();
        }

        private static byte[] ConvertModel(ExportEntry export, MEGame newGame)
        {
            MemoryStream bin = new MemoryStream(export.getBinaryData());
            var os = new MemoryStream();

            int count;
            os.WriteFromBuffer(bin.ReadToBuffer(28));
            os.WriteInt32(bin.ReadInt32());
            os.WriteInt32(count = bin.ReadInt32());
            os.WriteFromBuffer(bin.ReadToBuffer(count * 12));//Vectors
            os.WriteInt32(bin.ReadInt32());
            os.WriteInt32(count = bin.ReadInt32());
            os.WriteFromBuffer(bin.ReadToBuffer(count * 12));//Points
            os.WriteInt32(bin.ReadInt32());
            os.WriteInt32(count = bin.ReadInt32());
            os.WriteFromBuffer(bin.ReadToBuffer(count * 64));//Nodes
            os.WriteInt32(bin.ReadInt32());
            os.WriteInt32(count = bin.ReadInt32());
            for (int i = count; i > 0; i--)//surfs
            {
                os.WriteFromBuffer(bin.ReadToBuffer(56));
                if (export.Game == MEGame.ME3)
                {
                    bin.SkipInt32();
                }
                if (newGame == MEGame.ME3)
                {
                    os.WriteInt32(1);//iLightmassIndex 
                }
            }

            int size = bin.ReadInt32();
            os.WriteInt32(newGame == MEGame.ME3 ? 16 : 24);
            os.WriteInt32(count = bin.ReadInt32());
            for (int i = count; i > 0; i--) //verts
            {
                os.WriteFromBuffer(bin.ReadToBuffer(8));
                int shadowX = bin.ReadInt32();
                int shadowY = bin.ReadInt32();
                os.WriteInt32(shadowX);
                os.WriteInt32(shadowY);

                int backShadowX;
                int backShadowY;
                if (size == 24)
                {
                    backShadowX = bin.ReadInt32();
                    backShadowY = bin.ReadInt32();
                }
                else
                {   //best guess for how to handle this
                    backShadowX = shadowY;
                    backShadowY = shadowX;
                }

                if (newGame != MEGame.ME3)
                {
                    os.WriteInt32(backShadowX);
                    os.WriteInt32(backShadowY);
                }
            }

            os.WriteInt32(bin.ReadInt32());

            os.WriteInt32(count = bin.ReadInt32());
            os.WriteFromBuffer(bin.ReadToBuffer(count * 24));//NumZones

            os.WriteInt32(bin.ReadInt32());

            os.WriteInt32(bin.ReadInt32());
            os.WriteInt32(count = bin.ReadInt32());
            os.WriteFromBuffer(bin.ReadToBuffer(count * 4)); //LeafHulls
            os.WriteInt32(bin.ReadInt32());
            os.WriteInt32(count = bin.ReadInt32());
            os.WriteFromBuffer(bin.ReadToBuffer(count * 4)); //Leaves
            os.WriteInt32(bin.ReadInt32());
            os.WriteInt32(bin.ReadInt32());

            os.WriteInt32(bin.ReadInt32());
            os.WriteInt32(count = bin.ReadInt32());
            os.WriteFromBuffer(bin.ReadToBuffer(count * 4)); //PortalNodes
            os.WriteInt32(bin.ReadInt32());
            os.WriteInt32(count = bin.ReadInt32());
            os.WriteFromBuffer(bin.ReadToBuffer(count * 16)); //ShadowVolume

            os.WriteInt32(bin.ReadInt32());

            os.WriteInt32(bin.ReadInt32());
            os.WriteInt32(count = bin.ReadInt32());
            os.WriteFromBuffer(bin.ReadToBuffer(count * 36)); //VertexBuffer

            if (export.Game == MEGame.ME3)
            {
                bin.Skip(16);
                bin.Skip(bin.ReadInt32() * 36);
            }

            if (newGame == MEGame.ME3)
            {
                //this is probably important. user may have to fix manually
                os.WriteGuid(Guid.NewGuid());
                os.WriteInt32(1);//count
                os.WriteBoolInt(false);
                os.WriteBoolInt(false);
                os.WriteFloat(1);
                os.WriteBoolInt(false);
                os.WriteFloat(2);
                os.WriteFloat(0);
                os.WriteFloat(1);
                os.WriteFloat(1);
                os.WriteFloat(1);
            }

            return os.ToArray();
        }
    }

    public abstract class ObjectBinary
    {
        public ExportEntry Export { get; set; }
        public static T From<T>(ExportEntry export) where T : ObjectBinary, new()
        {
            var t = new T {Export = export};
            t.Serialize(new SerializingContainer2(new MemoryStream(export.getBinaryData()), export.FileRef, true, export.DataOffset + export.propsEnd()));
            return t;
        }

        public static ObjectBinary From(ExportEntry export)
        {
            switch (export.ClassName)
            {
                case "Level":
                    return From<Level>(export);
                case "World":
                    return From<World>(export);
                case "Polys":
                    return From<Polys>(export);
                case "DecalMaterial":
                case "Material":
                    return From<Material>(export);
                case "MaterialInstanceConstant":
                case "MaterialInstanceTimeVarying":
                    if (export.GetProperty<BoolProperty>("bHasStaticPermutationResource")?.Value == true)
                    {
                        return From<MaterialInstance>(export);
                    }
                    return new GenericObjectBinary(Array.Empty<byte>());
                case "StaticMesh":
                    return From<StaticMesh>(export);
                case "SkeletalMesh":
                    return From<SkeletalMesh>(export);
                case "StaticMeshComponent":
                    return From<StaticMeshComponent>(export);
                default:
                    return null;
            }
        }

        protected abstract void Serialize(SerializingContainer2 sc);

        public virtual List<(UIndex, string)> GetUIndexes(MEGame game) => new List<(UIndex, string)>();

        public virtual void WriteTo(Stream ms, IMEPackage pcc, int fileOffset)
        {
            Serialize(new SerializingContainer2(ms, pcc, false, fileOffset));
        }

        public virtual byte[] ToArray(IMEPackage pcc, int fileOffset = 0)
        {
            var ms = new MemoryStream();
            WriteTo(ms, pcc, fileOffset);
            return ms.ToArray();
        }
    }

    public sealed class GenericObjectBinary : ObjectBinary
    {
        private byte[] data;

        public GenericObjectBinary(byte[] buff)
        {
            data = buff;
        }

        //should never be called
        protected override void Serialize(SerializingContainer2 sc)
        {
            data = sc.ms.ReadFully();
        }

        public override void WriteTo(Stream ms, IMEPackage pcc, int fileOffset)
        {
            ms.WriteFromBuffer(data);
        }

        public override byte[] ToArray(IMEPackage pcc, int fileOffset = 0)
        {
            return data;
        }
    }

}
