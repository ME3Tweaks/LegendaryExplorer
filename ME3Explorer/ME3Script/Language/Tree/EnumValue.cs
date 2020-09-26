using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class EnumValue : ASTNode
    {
        public string Name;
        public byte IntVal;
        public Enumeration Enum;

        public EnumValue(string name, byte intVal, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.VariableIdentifier, start, end)
        {
            Name = name;
            IntVal = intVal;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
