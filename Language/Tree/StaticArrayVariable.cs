using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class StaticArrayVariable : Variable
    {
        public int Size;
        public StaticArrayVariable(String name, int size) : base(name)
        {
            Size = size;
            Type = ASTNodeType.StaticArrayVariable;
        }
    }
}
