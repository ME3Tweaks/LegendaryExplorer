using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class Variable : ASTNode
    {
        public String Name;
        public Variable(String name, SourcePosition start, SourcePosition end) 
            : base(ASTNodeType.Variable, start, end) 
        {
            Name = name;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
