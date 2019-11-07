using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal.BinaryConverters
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
