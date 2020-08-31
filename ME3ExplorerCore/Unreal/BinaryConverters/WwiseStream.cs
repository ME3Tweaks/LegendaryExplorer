using System;
using System.IO;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;

namespace ME3ExplorerCore.Unreal.BinaryConverters
{
    public class WwiseStream : ObjectBinary
    {
        public uint Unk1;//ME2
        public uint Unk2;//ME2
        public Guid UnkGuid;//ME2
        public uint Unk3;//ME2
        public uint Unk4;//ME2
        public uint Unk5;
        public int DataSize;
        public int DataOffset;
        public byte[] EmbeddedData;


        public int Id;
        public string Filename;
        public bool IsPCCStored => Filename == null;

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.IsLoading)
            {
                Id = Export.GetProperty<IntProperty>("Id");
                Filename = Export.GetProperty<NameProperty>("Filename")?.Value;
            }

            if (sc.Game != MEGame.ME3 && sc.Game != MEGame.ME2)
            {
                throw new Exception($"WwiseStream is not a valid class for {sc.Game}!");
            }
            if (sc.Game == MEGame.ME2)
            {
                sc.Serialize(ref Unk1);
                sc.Serialize(ref Unk2);
                if (Unk1 == 0 && Unk2 == 0)
                {
                    return; //not sure what's going on here
                }
                sc.Serialize(ref UnkGuid);
                sc.Serialize(ref Unk3);
                sc.Serialize(ref Unk4);
            }
            sc.Serialize(ref Unk5);
            if (sc.IsSaving && EmbeddedData != null)
            {
                DataOffset = sc.FileOffset + 12;
                DataSize = EmbeddedData.Length;
            }
            sc.Serialize(ref DataSize);
            sc.Serialize(ref DataSize);
            sc.Serialize(ref DataOffset);
            if (IsPCCStored)
            {
                sc.Serialize(ref EmbeddedData, DataSize);
            }
        }
    }
}
