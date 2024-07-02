using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class DefaultPropertiesBlock : CodeBody
    {
        public bool IsNormalExport;

        public DefaultPropertiesBlock(List<Statement> contents = null, int start = -1, int end = -1)
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
