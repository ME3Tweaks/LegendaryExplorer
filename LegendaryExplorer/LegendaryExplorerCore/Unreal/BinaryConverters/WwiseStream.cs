using System;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public partial class WwiseStream : ObjectBinary
    {
        public uint Unk1;//ME2/LE2
        public uint Unk2;//ME2/LE2
        public Guid UnkGuid;//ME2
        public uint Unk3;//ME2
        public uint Unk4;//ME2
        public uint BulkDataFlags;
        public int DataSize;
        public int DataOffset;
        public byte[] EmbeddedData;

        public int Id;
        public string Filename;

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.IsLoading)
            {
                Id = Export.GetProperty<IntProperty>("Id");
                Filename = Export.GetProperty<NameProperty>("Filename")?.Value;
            }

            if (!sc.Game.IsGame2() && !sc.Game.IsGame3())
            {
                throw new Exception($"WwiseStream is not a valid class for {sc.Game}!");
            }

            if (sc.Game.IsGame2())
            {
                sc.Serialize(ref Unk1);
                sc.Serialize(ref Unk2);
                if (sc.Game == MEGame.ME2 && sc.Pcc.Platform != MEPackage.GamePlatform.PS3)
                {
                    if (Unk1 == 0 && Unk2 == 0)
                    {
                        return; //not sure what's going on here
                    }
                    sc.Serialize(ref UnkGuid);
                    sc.Serialize(ref Unk3);
                    sc.Serialize(ref Unk4);
                }
            }
            sc.Serialize(ref BulkDataFlags);
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
                if (DataSize > 0)
                {
                    sc.Serialize(ref EmbeddedData, DataSize);
                }
                else
                {
                    // Some unused audio (such as female shepard tali romance in LE2) has blank external audio with no filename
                    // There is nothing to write back here - it is loading only
                    if (sc.IsLoading)
                    {
                        EmbeddedData = Array.Empty<byte>();
                    }
                }
            }
        }

        public static WwiseStream Create()
        {
            return new()
            {
                EmbeddedData = Array.Empty<byte>()
            };
        }
    }
}
