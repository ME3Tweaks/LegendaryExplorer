using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class Subobject : CodeBody
    {
        public VariableDeclaration NameDeclaration;

        public Class Class;

        public bool IsTemplate;

        public Subobject(VariableDeclaration nameDeclaration, Class @class, List<Statement> contents, bool isTemplate = false, SourcePosition start = null, SourcePosition end = null) : base(contents, start, end)
        {
            NameDeclaration = nameDeclaration;
            Class = @class;
            IsTemplate = isTemplate;
            Type = ASTNodeType.SubObject;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
