using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class Enumeration : VariableType
    {
        public List<VariableIdentifier> Values;
        public Enumeration(String name, List<VariableIdentifier> values,
            SourcePosition start, SourcePosition end)
            : base(name, start, end)
        {
            Type = ASTNodeType.Enumeration;
            Values = values;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
