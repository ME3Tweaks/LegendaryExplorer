using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class VariableType : ASTNode
    {
        public String Name;
        public ASTNode Declaration;
        public ASTNodeType NodeType { get { return Declaration == null ? ASTNodeType.INVALID : Declaration.Type; } }

        public VariableType(String name, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.VariableType, start, end) 
        {
            Name = name;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return this.AcceptVisitor(visitor);
        }
    }
}
