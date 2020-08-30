namespace ME3ExplorerCore.Unreal.BinaryConverters
{
    public class SoundNodeWave : ObjectBinary
    {
        public byte[] RawData;
        public byte[] CompressedPCData;
        public byte[] CompressedXbox360Data;
        public byte[] CompressedPS3Data;

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.SerializeBulkData(ref RawData, Unreal.SCExt.Serialize);
            sc.SerializeBulkData(ref CompressedPCData, Unreal.SCExt.Serialize);
            sc.SerializeBulkData(ref CompressedXbox360Data, Unreal.SCExt.Serialize);
            sc.SerializeBulkData(ref CompressedPS3Data, Unreal.SCExt.Serialize);
        }
    }
}
