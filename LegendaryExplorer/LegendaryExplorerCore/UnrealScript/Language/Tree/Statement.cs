using System;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public abstract class Statement : ASTNode
    {
        protected Statement(ASTNodeType type,int start, int end) 
            : base(type, start, end) { }

        public override bool AcceptVisitor(IASTVisitor visitor, UnrealScriptOptionsPackage usop)
        {
            throw new NotImplementedException();
        }
    }
}
