using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class Variable : VariableDeclaration
    {
        public VariableIdentifier Name
        {
            get
            {
                return Variables.First();
            }
        }

        public Variable(List<Specifier> specs, VariableIdentifier name,
            VariableType type, SourcePosition start, SourcePosition end)
            : base(type, specs, new List<VariableIdentifier> { name }, start, end)
        {
            Type = ASTNodeType.Variable;
        }
    }
}
