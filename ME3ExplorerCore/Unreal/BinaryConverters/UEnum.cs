using System.Collections.Generic;
using System.Linq;
using ME3ExplorerCore.Packages;

namespace ME3ExplorerCore.Unreal.BinaryConverters
{
    public class UEnum : UField
    {
        public NameReference[] Names;

        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref Names, Unreal.SCExt.Serialize);
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            names.AddRange(Names.Select((name, i) => (name, $"Names[{i}]")));

            return names;
        }
    }
}
