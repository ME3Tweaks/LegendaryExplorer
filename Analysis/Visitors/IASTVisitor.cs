using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Script.Language.Tree;

namespace ME3Script.Analysis.Visitors
{
    public interface IASTVisitor
    {
        bool VisitNode(Class node);
        bool VisitNode(VariableDeclaration node);
        bool VisitNode(VariableType node);
        bool VisitNode(Struct node);
        bool VisitNode(Enumeration node);
        bool VisitNode(Function node);
        bool VisitNode(State node);
        bool VisitNode(OperatorDeclaration node);
        bool VisitNode(FunctionParameter node);

        bool VisitNode(CodeBody node);
        bool VisitNode(StateLabel node);

        bool VisitNode(Variable node);
        bool VisitNode(VariableIdentifier node);

        bool VisitNode(DoUntilLoop node);
        bool VisitNode(ForLoop node);
        bool VisitNode(WhileLoop node);

        bool VisitNode(AssignStatement node);
        bool VisitNode(BreakStatement node);
        bool VisitNode(ContinueStatement node);
        bool VisitNode(IfStatement node);
        bool VisitNode(ReturnStatement node);
        bool VisitNode(StopStatement node);

        bool VisitNode(InOpReference node);
        bool VisitNode(PreOpReference node);
        bool VisitNode(PostOpReference node);

        bool VisitNode(FunctionCall node);

        bool VisitNode(ArraySymbolRef node);
        bool VisitNode(CompositeSymbolRef node);
        bool VisitNode(SymbolReference node);

        bool VisitNode(BooleanLiteral node);
        bool VisitNode(FloatLiteral node);
        bool VisitNode(IntegerLiteral node);
        bool VisitNode(NameLiteral node);
        bool VisitNode(StringLiteral node);
    }
}
