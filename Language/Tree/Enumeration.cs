using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class Enumeration : VariableType
    {
        public List<Variable> Values;
        public Enumeration(String name, List<Variable> values)
            : base(name)
        {
            Type = ASTNodeType.Enumeration;
            Values = values;
        }
    }
}
