using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class DefaultPropertiesBlock : CodeBody
    {
        public DefaultPropertiesBlock(List<Statement> contents = null, SourcePosition start = null, SourcePosition end = null)
            :base(contents, start, end)
        {
            Type = ASTNodeType.DefaultPropertiesBlock;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
