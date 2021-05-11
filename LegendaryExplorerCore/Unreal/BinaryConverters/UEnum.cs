using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class UEnum : UField
    {
        public NameReference[] Names;

        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref Names, SCExt.Serialize);
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            names.AddRange(Names.Select((name, i) => (name, $"Names[{i}]")));

            return names;
        }
    }
}
