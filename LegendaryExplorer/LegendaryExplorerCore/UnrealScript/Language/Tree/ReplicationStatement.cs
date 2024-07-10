using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class ReplicationStatement : Statement
    {
        public readonly Expression Condition;
        public readonly List<SymbolReference> ReplicatedVariables;

        public ReplicationStatement(Expression condition, List<SymbolReference> replicatedVariables, int start = -1, int end = -1) : base(ASTNodeType.ReplicationStatement, start, end)
        {
            Condition = condition;
            ReplicatedVariables = replicatedVariables;
            foreach (ASTNode childNode in ChildNodes)
            {
                childNode.Outer = this;
            }
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Condition;
                foreach (SymbolReference replicatedVariable in ReplicatedVariables)
                {
                    yield return replicatedVariable;
                }
            }
        }
    }
}
