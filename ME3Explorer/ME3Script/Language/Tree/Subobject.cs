using System.Collections.Generic;
using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class Subobject : CodeBody
    {
        public VariableDeclaration Name;

        public string Class;

        public Subobject(VariableDeclaration name, string @class, List<Statement> contents, SourcePosition start = null, SourcePosition end = null) : base(contents, start, end)
        {
            Name = name;
            Class = @class;
            Type = ASTNodeType.SubObject;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
