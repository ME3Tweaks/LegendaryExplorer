using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class SoundNodeWave : ObjectBinary
    {
        public byte[] RawData;
        public byte[] CompressedPCData;
        public byte[] CompressedXbox360Data;
        public byte[] CompressedPS3Data;

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Pcc.Platform == MEPackage.GamePlatform.PS3)
            {
                // Unknown items
                sc.SerializeConstInt(0);
                sc.SerializeConstInt(0);
            }
            sc.SerializeBulkData(ref RawData, SCExt.Serialize);
            sc.SerializeBulkData(ref CompressedPCData, SCExt.Serialize);

            if(!sc.Game.IsLEGame())
            {
                sc.SerializeBulkData(ref CompressedXbox360Data, SCExt.Serialize);
                sc.SerializeBulkData(ref CompressedPS3Data, SCExt.Serialize);
            }
        }
    }
}
