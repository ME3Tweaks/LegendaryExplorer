using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class NewOperator : Expression
    {
        public Expression OuterObject; //Object
        public Expression ObjectName; //Name
        public Expression Flags; //int
        public Expression ObjectClass; //class
        public Expression Template; //Object

        public NewOperator(Expression outerObject,
                           Expression objectName,
                           Expression flags,
                           Expression objectClass,
                           Expression template,
                           int start = -1, int end = -1) : base(ASTNodeType.NewOp, start, end)
        {
            OuterObject = outerObject;
            ObjectName = objectName;
            Flags = flags;
            ObjectClass = objectClass;
            Template = template;
        }

        public override VariableType ResolveType()
        {
            return ((ClassType)ObjectClass.ResolveType()).ClassLimiter;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                if (OuterObject != null) yield return OuterObject;
                if (ObjectName != null) yield return ObjectName;
                if (Flags != null) yield return Flags;
                yield return ObjectClass;
                if (Template != null) yield return Template;
            }
        }
    }
}
