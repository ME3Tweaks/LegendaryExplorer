namespace ME3ExplorerCore.Unreal.BinaryConverters
{
    public class SFXMorphFaceFrontEndDataSource : ObjectBinary
    {
        public string[] DefaultSettingsNames;
        protected override void Serialize(SerializingContainer2 sc)
        {
            int i = 0;
            sc.Serialize(ref DefaultSettingsNames, (SerializingContainer2 sc2, ref string name) =>
            {
                sc.Serialize(ref name);
                sc.Serialize(ref i);
                ++i;
            });
        }
    }
}
