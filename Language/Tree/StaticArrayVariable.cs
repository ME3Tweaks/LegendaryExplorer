using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class StaticArrayVariable : Variable
    {
        public int Size
        {
            get
            {
                return (Variables.First() as StaticArrayIdentifier).Size;
            }
        }

        public StaticArrayVariable(List<Specifier> specs, StaticArrayIdentifier name,
            VariableType type, SourcePosition start, SourcePosition end)
            : base(specs, name, type, start, end)
        {
            Type = ASTNodeType.StaticArrayVariable;
        }
    }
}
