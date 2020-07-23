using ME3Script.Analysis.Visitors;
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
        public string Name => Variables.First().Name;

        public int Size => Variables.First().Size;

        public bool IsStaticArray => Variables.First().Size > 1;



        public Variable(List<Specifier> specs, VariableIdentifier name,
                        VariableType type, SourcePosition start, SourcePosition end)
            : base(type, specs, new List<VariableIdentifier> { name }, null, start, end)
        {
            Type = ASTNodeType.Variable;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
