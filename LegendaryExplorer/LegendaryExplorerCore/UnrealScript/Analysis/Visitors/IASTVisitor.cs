using LegendaryExplorerCore.UnrealScript.Language.Tree;

namespace LegendaryExplorerCore.UnrealScript.Analysis.Visitors
{
    public interface IASTVisitor
    {
        bool VisitNode(Class node, UnrealScriptOptionsPackage usop);
        bool VisitNode(VariableDeclaration node, UnrealScriptOptionsPackage usop);
        bool VisitNode(VariableType node, UnrealScriptOptionsPackage usop);
        bool VisitNode(StaticArrayType node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DynamicArrayType node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DelegateType node, UnrealScriptOptionsPackage usop);
        bool VisitNode(ClassType node, UnrealScriptOptionsPackage usop);
        bool VisitNode(Struct node, UnrealScriptOptionsPackage usop);
        bool VisitNode(Enumeration node, UnrealScriptOptionsPackage usop);
        bool VisitNode(Const node, UnrealScriptOptionsPackage usop);
        bool VisitNode(Function node, UnrealScriptOptionsPackage usop);
        bool VisitNode(State node, UnrealScriptOptionsPackage usop);
        bool VisitNode(FunctionParameter node, UnrealScriptOptionsPackage usop);

        bool VisitNode(CodeBody node, UnrealScriptOptionsPackage usop);
        bool VisitNode(Label node, UnrealScriptOptionsPackage usop);
        bool VisitNode(VariableIdentifier node, UnrealScriptOptionsPackage usop);
        bool VisitNode(EnumValue node, UnrealScriptOptionsPackage usop);

        bool VisitNode(DoUntilLoop node, UnrealScriptOptionsPackage usop);
        bool VisitNode(ForLoop node, UnrealScriptOptionsPackage usop);
        bool VisitNode(ForEachLoop node, UnrealScriptOptionsPackage usop);
        bool VisitNode(WhileLoop node, UnrealScriptOptionsPackage usop);

        bool VisitNode(SwitchStatement node, UnrealScriptOptionsPackage usop);
        bool VisitNode(CaseStatement node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DefaultCaseStatement node, UnrealScriptOptionsPackage usop);

        bool VisitNode(AssignStatement node, UnrealScriptOptionsPackage usop);
        bool VisitNode(AssertStatement node, UnrealScriptOptionsPackage usop);
        bool VisitNode(BreakStatement node, UnrealScriptOptionsPackage usop);
        bool VisitNode(ContinueStatement node, UnrealScriptOptionsPackage usop);
        bool VisitNode(IfStatement node, UnrealScriptOptionsPackage usop);
        bool VisitNode(ReturnStatement node, UnrealScriptOptionsPackage usop);
        bool VisitNode(ReturnNothingStatement node, UnrealScriptOptionsPackage usop);
        bool VisitNode(StopStatement node, UnrealScriptOptionsPackage usop);
        bool VisitNode(StateGoto node, UnrealScriptOptionsPackage usop);
        bool VisitNode(Goto node, UnrealScriptOptionsPackage usop);

        bool VisitNode(ExpressionOnlyStatement node, UnrealScriptOptionsPackage usop);
        bool VisitNode(ReplicationStatement node, UnrealScriptOptionsPackage usop);
        bool VisitNode(ErrorStatement node, UnrealScriptOptionsPackage usop);
        bool VisitNode(ErrorExpression node, UnrealScriptOptionsPackage usop);

        bool VisitNode(InOpReference node, UnrealScriptOptionsPackage usop);
        bool VisitNode(PreOpReference node, UnrealScriptOptionsPackage usop);
        bool VisitNode(PostOpReference node, UnrealScriptOptionsPackage usop);
        bool VisitNode(StructComparison node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DelegateComparison node, UnrealScriptOptionsPackage usop);
        bool VisitNode(NewOperator node, UnrealScriptOptionsPackage usop);

        bool VisitNode(FunctionCall node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DelegateCall node, UnrealScriptOptionsPackage usop);

        bool VisitNode(ArraySymbolRef node, UnrealScriptOptionsPackage usop);
        bool VisitNode(CompositeSymbolRef node, UnrealScriptOptionsPackage usop);
        bool VisitNode(SymbolReference node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DefaultReference node, UnrealScriptOptionsPackage usop);

        bool VisitNode(BooleanLiteral node, UnrealScriptOptionsPackage usop);
        bool VisitNode(FloatLiteral node, UnrealScriptOptionsPackage usop);
        bool VisitNode(IntegerLiteral node, UnrealScriptOptionsPackage usop);
        bool VisitNode(NameLiteral node, UnrealScriptOptionsPackage usop);
        bool VisitNode(StringLiteral node, UnrealScriptOptionsPackage usop);
        bool VisitNode(StringRefLiteral node, UnrealScriptOptionsPackage usop);
        bool VisitNode(StructLiteral node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DynamicArrayLiteral node, UnrealScriptOptionsPackage usop);
        bool VisitNode(ObjectLiteral node, UnrealScriptOptionsPackage usop);
        bool VisitNode(VectorLiteral node, UnrealScriptOptionsPackage usop);
        bool VisitNode(RotatorLiteral node, UnrealScriptOptionsPackage usop);
        bool VisitNode(NoneLiteral node, UnrealScriptOptionsPackage usop);

        bool VisitNode(ConditionalExpression node, UnrealScriptOptionsPackage usop);
        bool VisitNode(CastExpression node, UnrealScriptOptionsPackage usop);

        bool VisitNode(DefaultPropertiesBlock node, UnrealScriptOptionsPackage usop);

        bool VisitNode(Subobject node, UnrealScriptOptionsPackage usop);

        bool VisitNode(DynArrayLength node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DynArrayAdd node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DynArrayAddItem node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DynArrayInsert node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DynArrayInsertItem node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DynArrayRemove node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DynArrayRemoveItem node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DynArrayFind node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DynArrayFindStructMember node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DynArraySort node, UnrealScriptOptionsPackage usop);
        bool VisitNode(DynArrayIterator node, UnrealScriptOptionsPackage usop);
    }
}
