using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class VariableType : ASTNode
    {
        public String Name;
        public VariableType(String name, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.VariableType, start, end) 
        {
            Name = name;
        }
    }
}
