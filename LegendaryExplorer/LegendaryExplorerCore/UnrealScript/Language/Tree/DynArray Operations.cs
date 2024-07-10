using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public abstract class DynArrayOperation : Expression
    {
        public Expression DynArrayExpression;

        protected DynArrayOperation(Expression dynArrayExpression, int start = -1, int end = -1) : base(ASTNodeType.CompositeReference, start, end)
        {
            DynArrayExpression = dynArrayExpression;
        }

        public override VariableType ResolveType()
        {
            return null;
        }
    }
    public class DynArrayLength : DynArrayOperation
    {
        public DynArrayLength(Expression dynArrayExpression, int start = -1, int end = -1) : base(dynArrayExpression, start, end){}

        public override VariableType ResolveType()
        {
            return SymbolTable.IntType;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
    public class DynArrayAdd : DynArrayOperation
    {
        public Expression CountArg;

        public DynArrayAdd(Expression dynArrayExpression, Expression countArg, int start = -1, int end = -1) : base(dynArrayExpression, start, end)
        {
            CountArg = countArg;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return SymbolTable.IntType;
        }
    }
    public class DynArrayAddItem : DynArrayOperation
    {
        public Expression ValueArg;

        public DynArrayAddItem(Expression dynArrayExpression, Expression valueArg, int start = -1, int end = -1) : base(dynArrayExpression, start, end)
        {
            ValueArg = valueArg;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return SymbolTable.IntType;
        }
    }
    public class DynArrayInsert : DynArrayOperation
    {
        public Expression IndexArg;
        public Expression CountArg;

        public DynArrayInsert(Expression dynArrayExpression, Expression indexArg, Expression countArg, int start = -1, int end = -1) : base(dynArrayExpression, start, end)
        {
            IndexArg = indexArg;
            CountArg = countArg;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
    public class DynArrayInsertItem : DynArrayOperation
    {
        public Expression IndexArg;
        public Expression ValueArg;

        public DynArrayInsertItem(Expression dynArrayExpression, Expression indexArg, Expression valueArg, int start = -1, int end = -1) : base(dynArrayExpression, start, end)
        {
            IndexArg = indexArg;
            ValueArg = valueArg;
        }

        public override VariableType ResolveType()
        {
            return SymbolTable.IntType;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
    public class DynArrayRemove : DynArrayOperation
    {
        public Expression IndexArg;
        public Expression CountArg;

        public DynArrayRemove(Expression dynArrayExpression, Expression indexArg, Expression countArg, int start = -1, int end = -1) : base(dynArrayExpression, start, end)
        {
            IndexArg = indexArg;
            CountArg = countArg;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
    public class DynArrayRemoveItem : DynArrayOperation
    {
        public Expression ValueArg;

        public DynArrayRemoveItem(Expression dynArrayExpression, Expression valueArg, int start = -1, int end = -1) : base(dynArrayExpression, start, end)
        {
            ValueArg = valueArg;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return SymbolTable.IntType;
        }
    }
    public class DynArrayFind : DynArrayOperation
    {
        public Expression ValueArg;

        public DynArrayFind(Expression dynArrayExpression, Expression valueArg, int start = -1, int end = -1) : base(dynArrayExpression, start, end)
        {
            ValueArg = valueArg;
        }

        public override VariableType ResolveType()
        {
            return SymbolTable.IntType;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
    public class DynArrayFindStructMember : DynArrayOperation
    {
        public Expression MemberNameArg;
        public Expression ValueArg;
        public VariableType MemberType;

        public DynArrayFindStructMember(Expression dynArrayExpression, Expression memberNameArg, Expression valueArg, int start = -1, int end = -1) : base(dynArrayExpression, start, end)
        {
            MemberNameArg = memberNameArg;
            ValueArg = valueArg;
        }

        public override VariableType ResolveType()
        {
            return SymbolTable.IntType;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
    public class DynArraySort : DynArrayOperation
    {
        public Expression CompareFuncArg;

        public DynArraySort(Expression dynArrayExpression, Expression comparefunctionArg, int start = -1, int end = -1) : base(dynArrayExpression, start, end)
        {
            CompareFuncArg = comparefunctionArg;
        }

        public override VariableType ResolveType()
        {
            return ((DynamicArrayType)DynArrayExpression.ResolveType()).ElementType;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }

    public class DynArrayIterator : DynArrayOperation
    {
        public Expression ValueArg;
        public Expression IndexArg;

        public DynArrayIterator(Expression dynArrayExpression, Expression valueArg, Expression indexArg, int start = -1, int end = -1) : base(dynArrayExpression, start, end)
        {
            ValueArg = valueArg;
            IndexArg = indexArg;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
