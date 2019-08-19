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

            switch (export.ClassName)
            {
                case "World":
                    return From<World>(export);
                case "Polys":
                    return From<Polys>(export);
                case "DecalMaterial":
                case "Material":
                    return From<Material>(export);
                case "MaterialInstanceConstant":
                case "MaterialInstanceTimeVarying":
                    return From<MaterialInstance>(export);
                case "Level":
                    return new GenericObjectBinary(ConvertLevel(export, newGame));
                case "Model":
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

        public static byte[] ConvertTexture2D(ExportEntry export, MEGame newGame)
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
            for (int i = 0; i < mipCount; i++)
            {
                var storageType = (StorageTypes)bin.ReadInt32();
                int uncompressedSize = bin.ReadInt32();
                int compressedSize = bin.ReadInt32();
                bin.SkipInt32();
                byte[] texture;
                switch (storageType)
                {
                    case StorageTypes.pccUnc:
                        texture = bin.ReadToBuffer(uncompressedSize);
                        break;
                    case StorageTypes.pccLZO:
                    case StorageTypes.pccZlib:
                        texture = bin.ReadToBuffer(compressedSize);
                        break;
                    case StorageTypes.empty:
                        texture = new byte[0];
                        break;
                    default:
                        storageType = StorageTypes.pccUnc;
                        texture = EmbeddedTextureViewer.GetTextureData(mips[i]); //copy in external textures as pccUnc
                        uncompressedSize = compressedSize = texture.Length;
                        break;

                }

                int width = bin.ReadInt32();
                int height = bin.ReadInt32();

                os.WriteInt32((int)storageType);
                os.WriteInt32(uncompressedSize);
                os.WriteInt32(compressedSize);
                os.WriteInt32(0);//fileOffset will be fixed during save
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

        private static byte[] ConvertLevel(ExportEntry export, MEGame newGame)
        {
            MemoryStream bin = new MemoryStream(export.getBinaryData());
            var os = new MemoryStream();

            os.WriteInt32(bin.ReadInt32());//self
            int count = bin.ReadInt32();
            os.WriteInt32(count);
            os.WriteFromBuffer(bin.ReadToBuffer(count * 4));//Actors
            os.WriteUnrealString(bin.ReadUnrealString(), newGame);//URL
            os.WriteUnrealString(bin.ReadUnrealString(), newGame);
            os.WriteUnrealString(bin.ReadUnrealString(), newGame);
            os.WriteUnrealString(bin.ReadUnrealString(), newGame);
            os.WriteInt32(count = bin.ReadInt32());
            for (int i = 0; i < count; i--)
            {
                os.WriteUnrealString(bin.ReadUnrealString(), newGame);
            }

            os.WriteFromBuffer(bin.ReadToBuffer(12));
            os.WriteInt32(count = bin.ReadInt32());
            os.WriteFromBuffer(bin.ReadToBuffer(count * 4));//ModelComponents
            os.WriteInt32(count = bin.ReadInt32());
            os.WriteFromBuffer(bin.ReadToBuffer(count * 4));//GameSequences
            os.WriteInt32(count = bin.ReadInt32());
            for (int i = count; i > 0; i--)//TextureToInstancesMap
            {
                os.WriteInt32(bin.ReadInt32());
                os.WriteInt32(count = bin.ReadInt32());
                for (int j = count; j > 0; j--)
                {
                    os.WriteFromBuffer(bin.ReadToBuffer(20));
                }
            }

            //APEX
            if (export.Game == MEGame.ME3)
            {
                bin.Skip(bin.ReadInt32());
            }
            if (newGame == MEGame.ME3)
            {
                os.WriteInt32(0);//hopefully the Apex stuff isn't neccesary 
            }

            os.WriteInt32(bin.ReadInt32());//CachedPhysBSPData
            os.WriteInt32(count = bin.ReadInt32());
            os.WriteFromBuffer(bin.ReadToBuffer(count));
            os.WriteInt32(count = bin.ReadInt32());//CachedPhysSMDataMap
            os.WriteFromBuffer(bin.ReadToBuffer(count * 20));
            os.WriteInt32(count = bin.ReadInt32());
            for (int i = count; i > 0; i--)//CachedPhysSMDataStore
            {
                os.WriteInt32(count = bin.ReadInt32());
                for (int j = count; j > 0; j--)
                {
                    os.WriteInt32(bin.ReadInt32());
                    os.WriteInt32(count = bin.ReadInt32());
                    os.WriteFromBuffer(bin.ReadToBuffer(count));
                }
            }
            os.WriteInt32(count = bin.ReadInt32());//CachedPhysPerTriSMDataMap
            os.WriteFromBuffer(bin.ReadToBuffer(count * 20));
            os.WriteInt32(count = bin.ReadInt32());
            for (int i = count; i > 0; i--)//CachedPhysPerTriSMDataStore
            {
                os.WriteInt32(bin.ReadInt32());
                os.WriteInt32(count = bin.ReadInt32());
                os.WriteFromBuffer(bin.ReadToBuffer(count));
            }

            os.WriteFromBuffer(bin.ReadToBuffer(8)); //CachedPhysBSPDataVersion //CachedPhysSMDataVersion
            os.WriteInt32(count = bin.ReadInt32());
            os.WriteFromBuffer(bin.ReadToBuffer(count * 8));//ForceStreamTextures
            os.WriteFromBuffer(bin.ReadToBuffer(16)); //NavListStart
            //NavListEnd
            //CoverListStart
            //CoverListEnd
            if (export.Game == MEGame.ME3)
            {
                bin.SkipInt32(); //PylonListStart
                bin.SkipInt32(); //PylonListEnd
                bin.Skip(bin.ReadInt32() * 20);//guidToIntMap
                bin.Skip(bin.ReadInt32() * 4);//CoverLinks
                bin.Skip(bin.ReadInt32() * 5);//IntToByteMap
                bin.Skip(bin.ReadInt32() * 20);//guidToIntMap
                bin.Skip(bin.ReadInt32() * 4);//NavPoints
                bin.Skip(bin.ReadInt32() * 4);//Numbers
            }

            if (newGame == MEGame.ME3) //hope it can do without this info
            {
                os.WriteInt32(0);//PylonListStart
                os.WriteInt32(0);//PylonListEnd
                os.WriteInt32(0);//guidToIntMap
                os.WriteInt32(0);//CoverLinks
                os.WriteInt32(0);//IntToByteMap
                os.WriteInt32(0);//guidToIntMap
                os.WriteInt32(0);//NavPoints
                os.WriteInt32(0);//Numbers
            }
            os.WriteInt32(count = bin.ReadInt32());
            os.WriteFromBuffer(bin.ReadToBuffer(count * 4));//CrossLevelActors
            if (export.Game == MEGame.ME1)
            {
                bin.SkipInt32();
                bin.SkipInt32();
            }

            if (newGame == MEGame.ME1)
            {
                os.WriteInt32(0);
                os.WriteInt32(0);
            }

            if (newGame == MEGame.ME3)
            {
                os.WriteBoolInt(false);
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
        public static T From<T>(ExportEntry export) where T : ObjectBinary, new()
        {
            var t = new T();
            t.Serialize(new SerializingContainer2(new MemoryStream(export.getBinaryData()), export.FileRef, true));
            return t;
        }
        protected abstract void Serialize(SerializingContainer2 sc);

        public virtual byte[] Write(IMEPackage pcc)
        {
            var ms = new MemoryStream();
            Serialize(new SerializingContainer2(ms, pcc));

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

        protected override void Serialize(SerializingContainer2 sc)
        {
            data = sc.ms.ReadFully();
        }
        public override byte[] Write(IMEPackage pcc)
        {
            return data;
        }
    }

}
