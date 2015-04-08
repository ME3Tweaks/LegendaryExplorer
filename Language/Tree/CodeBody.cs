using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class CodeBody : Statement
    {
        public List<Statement> Statements;

        public CodeBody(List<Statement> contents, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.CodeBody, start, end) 
        {
            Statements = contents;
        }

        public override void VisitNode(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
