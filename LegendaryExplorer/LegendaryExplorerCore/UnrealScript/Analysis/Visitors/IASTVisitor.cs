using LegendaryExplorerCore.UnrealScript.Language.Tree;

namespace LegendaryExplorerCore.UnrealScript.Analysis.Visitors
{
    public interface IASTVisitor
    {
        bool VisitNode(Class node);
        bool VisitNode(VariableDeclaration node);
        bool VisitNode(VariableType node);
        bool VisitNode(StaticArrayType node);
        bool VisitNode(DynamicArrayType node);
        bool VisitNode(DelegateType node);
        bool VisitNode(ClassType node);
        bool VisitNode(Struct node);
        bool VisitNode(Enumeration node);
        bool VisitNode(Const node);
        bool VisitNode(Function node);
        bool VisitNode(State node);
        bool VisitNode(FunctionParameter node);

        bool VisitNode(CodeBody node);
        bool VisitNode(Label node);
        bool VisitNode(VariableIdentifier node);
        bool VisitNode(EnumValue node);

        bool VisitNode(DoUntilLoop node);
        bool VisitNode(ForLoop node);
        bool VisitNode(ForEachLoop node);
        bool VisitNode(WhileLoop node);

        bool VisitNode(SwitchStatement node);
        bool VisitNode(CaseStatement node);
        bool VisitNode(DefaultCaseStatement node);

        bool VisitNode(AssignStatement node);
        bool VisitNode(AssertStatement node);
        bool VisitNode(BreakStatement node);
        bool VisitNode(ContinueStatement node);
        bool VisitNode(IfStatement node);
        bool VisitNode(ReturnStatement node);
        bool VisitNode(ReturnNothingStatement node);
        bool VisitNode(StopStatement node);
        bool VisitNode(StateGoto node);
        bool VisitNode(Goto node);

        bool VisitNode(ExpressionOnlyStatement node);
        bool VisitNode(ReplicationStatement node);
        bool VisitNode(ErrorStatement node);
        bool VisitNode(ErrorExpression node);

        bool VisitNode(InOpReference node);
        bool VisitNode(PreOpReference node);
        bool VisitNode(PostOpReference node);
        bool VisitNode(StructComparison node);
        bool VisitNode(DelegateComparison node);
        bool VisitNode(NewOperator node);

        bool VisitNode(FunctionCall node);
        bool VisitNode(DelegateCall node);

        bool VisitNode(ArraySymbolRef node);
        bool VisitNode(CompositeSymbolRef node);
        bool VisitNode(SymbolReference node);
        bool VisitNode(DefaultReference node);

        bool VisitNode(BooleanLiteral node);
        bool VisitNode(FloatLiteral node);
        bool VisitNode(IntegerLiteral node);
        bool VisitNode(NameLiteral node);
        bool VisitNode(StringLiteral node);
        bool VisitNode(StringRefLiteral node);
        bool VisitNode(StructLiteral node);
        bool VisitNode(DynamicArrayLiteral node);
        bool VisitNode(ObjectLiteral node);
        bool VisitNode(VectorLiteral node);
        bool VisitNode(RotatorLiteral node);
        bool VisitNode(NoneLiteral node);

        bool VisitNode(ConditionalExpression node);
        bool VisitNode(CastExpression node);

        bool VisitNode(DefaultPropertiesBlock node);

        bool VisitNode(Subobject node);

        bool VisitNode(DynArrayLength node);
        bool VisitNode(DynArrayAdd node);
        bool VisitNode(DynArrayAddItem node);
        bool VisitNode(DynArrayInsert node);
        bool VisitNode(DynArrayInsertItem node);
        bool VisitNode(DynArrayRemove node);
        bool VisitNode(DynArrayRemoveItem node);
        bool VisitNode(DynArrayFind node);
        bool VisitNode(DynArrayFindStructMember node);
        bool VisitNode(DynArraySort node);
        bool VisitNode(DynArrayIterator node);
        bool VisitNode(CommentStatement node);
    }
}
