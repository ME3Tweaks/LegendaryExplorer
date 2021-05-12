using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
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
