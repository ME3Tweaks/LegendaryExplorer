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
    }
}
