using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class Struct : VariableType
    {
        public List<Specifier> Specifiers;
        public Struct Parent;
        public List<VariableDeclaration> Members;

        public Struct(String name, List<Specifier> specs, 
            List<VariableDeclaration> members, Struct parent = null) : base(name)
        {
            Type = ASTNodeType.Struct;
            Specifiers = specs;
            Members = members;
            Parent = parent;
        }
    }
}
