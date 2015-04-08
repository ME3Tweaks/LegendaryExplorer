using ME3Script.Analysis.Symbols;
using ME3Script.Compiling.Errors;
using ME3Script.Language.Tree;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Analysis.Visitors
{
    public class ClassValidationVisitor : IASTVisitor
    {
        private SymbolTable Symbols;
        private MessageLog Log;
        private bool Success;

        public ClassValidationVisitor(MessageLog log, SymbolTable symbols)
        {
            Log = log;
            Symbols = symbols;
            Success = false;
        }

        private bool Error(String msg, SourcePosition start = null, SourcePosition end = null)
        {
            Log.LogError(msg, start, end);
            Success = false;
            return false;
        }

        public bool VisitNode(Class node)
        {
            if (Symbols.SymbolExists(node.Name))
                return Error("A class named '" + node.Name + "' already exists!", node.StartPos, node.EndPos);

            Symbols.AddSymbol(node.Name, node);
            Symbols.PushScope(node.Name);

            ASTNode parent;
            if (!Symbols.TryGetSymbol(node.Parent.Name, out parent))
                Error("No parent class named '" + node.Name + "' found!", node.StartPos, node.EndPos);
            if (parent != null && parent.Type != ASTNodeType.Class)
                Error("Parent named '" + node.Name + "' is not a class!", node.StartPos, node.EndPos);


            return Success;
        }


        public bool VisitNode(VariableDeclaration node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(VariableType node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(Struct node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(Enumeration node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(Function node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(State node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(OperatorDeclaration node)
        {
            throw new NotImplementedException();
        }
    }
}
