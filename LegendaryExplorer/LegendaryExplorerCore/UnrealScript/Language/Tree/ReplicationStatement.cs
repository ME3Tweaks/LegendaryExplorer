using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class ReplicationStatement : Statement
    {
        public readonly Expression Condition;
        public readonly List<SymbolReference> ReplicatedVariables;

        public ReplicationStatement(Expression condition, List<SymbolReference> replicatedVariables, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.ReplicationStatement, start, end)
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
