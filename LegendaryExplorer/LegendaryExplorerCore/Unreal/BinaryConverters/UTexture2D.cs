using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class UTexture : ObjectBinary
    {
        public byte[] Thumbnail; // Not ME3 or LE3

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (!sc.Game.IsGame3())
            {
                int dummy = 0;
                sc.Serialize(ref dummy);
                sc.Serialize(ref dummy);
                sc.Serialize(ref Thumbnail);
                sc.SerializeFileOffset();
            }
        }
    }

    public class UTextureCube : UTexture
    {
        // This is here just to make sure it's different
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
        }
    }

    public class UTexture2D : UTexture
    {
        public List<Texture2DMipMap> Mips;
        public int Unk1;
        public Guid TextureGuid;

        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref Mips, SCExt.Serialize);
            if (sc.Game != MEGame.UDK)
            {
                sc.Serialize(ref Unk1);
            }

            if (sc.Game > MEGame.ME1)
            {
                sc.Serialize(ref TextureGuid);
            }

            if (sc.Game == MEGame.UDK)
            {
                var zeros = new byte[32];
                sc.Serialize(ref zeros, 32);
            }
            if (sc.Game == MEGame.ME3 || sc.Game.IsLEGame())
            {
                int dummy = 0;
                sc.Serialize(ref dummy);
            }
        }

        public static UTexture2D Create()
        {
            return new()
            {
                Mips = new List<Texture2DMipMap>()
            };
        }

        public class Texture2DMipMap
        {
            public StorageTypes StorageType;
            public int UncompressedSize;
            public int CompressedSize;
            public int DataOffset;
            public byte[] Mip;
            public int SizeX;
            public int SizeY;

            // Utility stuff
            /// <summary>
            /// The start of this mipmap 'struct' in the export, at the time it was read
            /// </summary>
            public int MipInfoOffsetFromBinStart;

            /// <summary>
            /// If the texture is locally stored. This will return true for empty mipmaps!
            /// </summary>
            public bool IsLocallyStored => ((int)StorageType & (int)StorageFlags.externalFile) == 0;
            public bool IsCompressed =>
                ((int)StorageType & (int)StorageFlags.compressedLZO) != 0 ||
                ((int)StorageType & (int)StorageFlags.compressedLZMA) != 0 ||
                ((int)StorageType & (int)StorageFlags.compressedZlib) != 0 ||
                ((int)StorageType & (int)StorageFlags.compressedOodle) != 0;

            public Texture2DMipMap() { }

            /// <summary>
            /// Creates an uncompressed mipmap object from the given data and size.
            /// </summary>
            /// <param name="src">Mipmap data, uncompressed.</param>
            /// <param name="w">The width of the mipmap</param>
            /// <param name="h">The height of the mipmap</param>
            public Texture2DMipMap(byte[] src, int w, int h)
            {
                StorageType = StorageTypes.pccUnc;
                UncompressedSize = CompressedSize = src.Length;
                Mip = src;
                SizeX = w;
                SizeY = h;
            }
        }
    }

    public class LightMapTexture2D : UTexture2D
    {
        public ELightMapFlags LightMapFlags;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            if (sc.Game >= MEGame.ME3)
            {
                int lmf = (int)LightMapFlags;
                sc.Serialize(ref lmf);
                LightMapFlags = (ELightMapFlags)lmf;
            }
        }

        public new static LightMapTexture2D Create()
        {
            return new()
            {
                Mips = new List<Texture2DMipMap>()
            };
        }
    }
    public enum ELightMapFlags
    {
        LMF_None,
        LMF_Streamed,
        LMF_SimpleLightmap
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref UTexture2D.Texture2DMipMap mip)
        {
            if (sc.IsLoading)
            {
                mip = new UTexture2D.Texture2DMipMap();
                mip.MipInfoOffsetFromBinStart = (int)sc.ms.Position; // this is used to update the DataOffset later
            }

            int mipStorageType = (int)mip.StorageType;
            sc.Serialize(ref mipStorageType);
            mip.StorageType = (StorageTypes)mipStorageType;
            sc.Serialize(ref mip.UncompressedSize);
            sc.Serialize(ref mip.CompressedSize);
            if (sc.IsSaving && mip.IsLocallyStored)
            {
                // This code is not accurate as the start offset may be 0 if the export is new and doesn't have a DataOffset yet.
                sc.SerializeFileOffset();
            }
            else
            {
                sc.Serialize(ref mip.DataOffset);
            }

            if (mip.IsLocallyStored)
            {
                sc.Serialize(ref mip.Mip, mip.CompressedSize);
            }
            sc.Serialize(ref mip.SizeX);
            sc.Serialize(ref mip.SizeY);
        }
    }
}