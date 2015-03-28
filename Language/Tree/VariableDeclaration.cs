using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class VariableDeclaration : Statement
    {
        public List<Specifier> Specifiers;
        // Can contain StaticArrayVariables as well
        public List<Variable> Variables;
        // Can reference an existing type, or declare a new struct/enum type
        public VariableType Type;

        public VariableDeclaration(VariableType type, List<Specifier> specs, 
            List<Variable> names) : base(ASTNodeType.VariableDeclaration)
        {
            Specifiers = specs;
            Type = type;
            Variables = names;
        }
    }
}
