using System;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class SFXMorphFaceFrontEndDataSource : ObjectBinary
    {
        public string[] DefaultSettingsNames;
        protected override void Serialize(SerializingContainer2 sc)
        {
            int i = 0;
            sc.Serialize(ref DefaultSettingsNames, (SerializingContainer2 sc2, ref string name) =>
            {
                sc2.Serialize(ref name);
                sc2.Serialize(ref i);
                ++i;
            });
        }

        public static SFXMorphFaceFrontEndDataSource Create()
        {
            return new()
            {
                DefaultSettingsNames = Array.Empty<string>()
            };
        }
    }
}
