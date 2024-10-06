using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class UEnum : UField
    {
        public NameReference[] Names;

        protected override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref Names, sc.Serialize);
        }

        public static UEnum Create()
        {
            return new()
            {
                SuperClass = 0,
                Next = 0,
                Names = []
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            names.AddRange(Names.Select((name, i) => (name, $"Names[{i}]")));

            return names;
        }
    }
}
