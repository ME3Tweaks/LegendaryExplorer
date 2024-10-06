using System;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class SFXMorphFaceFrontEndDataSource : ObjectBinary
    {
        public string[] DefaultSettingsNames;
        protected override void Serialize(SerializingContainer sc)
        {
            int i = 0;
            sc.Serialize(ref DefaultSettingsNames, (ref string name) =>
            {
                sc.Serialize(ref name);
                sc.Serialize(ref i);
                ++i;
            });
        }

        public static SFXMorphFaceFrontEndDataSource Create()
        {
            return new()
            {
                DefaultSettingsNames = []
            };
        }
    }
}
