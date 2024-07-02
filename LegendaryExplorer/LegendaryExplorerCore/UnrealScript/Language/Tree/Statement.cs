using System;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public abstract class Statement(ASTNodeType type, int start, int end) : ASTNode(type, start, end)
    {
        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
