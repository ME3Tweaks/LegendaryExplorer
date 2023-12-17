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

        public Subobject(VariableDeclaration nameDeclaration, Class @class, List<Statement> contents, bool isTemplate = false, int start = -1, int end = -1) : base(contents, start, end)
        {
            NameDeclaration = nameDeclaration;
            Class = @class;
            IsTemplate = isTemplate;
            Type = ASTNodeType.SubObject;
        }

        public override bool AcceptVisitor(IASTVisitor visitor, UnrealScriptOptionsPackage usop)
        {
            return visitor.VisitNode(this, usop);
        }
    }
}
