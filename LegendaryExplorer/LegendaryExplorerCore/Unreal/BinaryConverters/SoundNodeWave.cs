using System;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class SoundNodeWave : ObjectBinary
    {
        public byte[] RawData;
        public byte[] CompressedPCData;
        public byte[] CompressedXbox360Data;
        public byte[] CompressedPS3Data;

        protected override void Serialize(SerializingContainer sc)
        {
            if (sc.Game.IsLEGame() || sc.Pcc.Platform == MEPackage.GamePlatform.PS3)
            {
                // Unknown items
                sc.SerializeConstInt(0);
                sc.SerializeConstInt(0);
            }
            sc.SerializeBulkData(ref RawData, sc.Serialize);
            if (sc.Game.IsLEGame())
            {
                sc.SerializeConstInt(0);
            }
            sc.SerializeBulkData(ref CompressedPCData, sc.Serialize);
            sc.SerializeBulkData(ref CompressedXbox360Data, sc.Serialize);
            sc.SerializeBulkData(ref CompressedPS3Data, sc.Serialize);
        }

        public static SoundNodeWave Create()
        {
            return new()
            {
                RawData = [],
                CompressedPCData = [],
                CompressedXbox360Data = [],
                CompressedPS3Data = [],
            };
        }
    }
}
