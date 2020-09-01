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
            sc.SerializeBulkData(ref RawData, SCExt.Serialize);
            sc.SerializeBulkData(ref CompressedPCData, SCExt.Serialize);
            sc.SerializeBulkData(ref CompressedXbox360Data, SCExt.Serialize);
            sc.SerializeBulkData(ref CompressedPS3Data, SCExt.Serialize);
        }
    }
}
