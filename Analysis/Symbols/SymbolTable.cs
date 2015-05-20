using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Analysis.Symbols
{
    public class SymbolTable
    {
        private Dictionary<String, Dictionary<String, ASTNode>> Cache;
        private LinkedList<Dictionary<String, ASTNode>> Scopes;
        private LinkedList<String> ScopeNames;
        private Dictionary<String, OperatorDeclaration> Operators;

        public String CurrentScopeName
        {
            get
            {
                if (ScopeNames.Count == 0)
                    return "";
                return ScopeNames.Last();
            }
        }

        public SymbolTable()
        {
            ScopeNames = new LinkedList<String>();
            Scopes = new LinkedList<Dictionary<String, ASTNode>>();
            Cache = new Dictionary<String, Dictionary<String, ASTNode>>();
            Operators = new Dictionary<String, OperatorDeclaration>();
        }

        public void PushScope(String name)
        {
            String fullName = (CurrentScopeName == "" ? "" : CurrentScopeName + ".") + name.ToLower();
            Dictionary<String, ASTNode> scope;
            bool cached = Cache.TryGetValue(fullName, out scope);
            if (!cached)
                scope = new Dictionary<String, ASTNode>();

            Scopes.AddLast(scope);
            ScopeNames.AddLast(fullName);
            
            if (!cached)
                Cache.Add(fullName, scope);
        }

        public void PopScope()
        {
            if (Scopes.Count == 0)
                throw new InvalidOperationException();

            Scopes.RemoveLast();
            ScopeNames.RemoveLast();
        }

        public bool TryGetSymbol(String symbol, out ASTNode node, String outerScope)
        {
            return TryGetSymbolInternal(symbol, out node, Scopes) ||
                TryGetSymbolInScopeStack(symbol, out node, outerScope);
        }

        public bool SymbolExists(String symbol, String outerScope)
        {   
            ASTNode dummy;
            return TryGetSymbol(symbol, out dummy, outerScope);
        }

        public bool TryGetSymbolInScopeStack(String symbol, out ASTNode node, String lowestScope)
        {
            node = null;
            String scope = lowestScope.ToLower();
            if (scope == "object") //As all classes inherit from object this is already checked.
                return false;

            LinkedList<Dictionary<String, ASTNode>> stack;
            if (!TryBuildSpecificScope(scope, out stack))
                return false;

            return TryGetSymbolInternal(symbol, out node, stack);
        }

        private bool TryBuildSpecificScope(String lowestScope, out LinkedList<Dictionary<String, ASTNode>> stack)
        {
            var names = lowestScope.Split('.');
            stack = new LinkedList<Dictionary<String, ASTNode>>();
            Dictionary<String, ASTNode> currentScope;
            foreach (string scopeName in names)
            {
                if (Cache.TryGetValue(scopeName, out currentScope))
                    stack.AddLast(currentScope);
                else
                    return false;
            }
            return stack.Count > 0;
        }

        private bool TryGetSymbolInternal(String symbol, out ASTNode node, LinkedList<Dictionary<String, ASTNode>> stack)
        {
            String name = symbol.ToLower();
            LinkedListNode<Dictionary<String, ASTNode>> it;
            for (it = stack.Last; it != null; it = it.Previous)
            {
                if (it.Value.TryGetValue(name, out node))
                    return true;
            }
            node = null;
            return false;
        }

        public bool SymbolExistsInCurrentScope(String symbol)
        {
            return Scopes.Last().ContainsKey(symbol.ToLower());
        }

        public bool TryGetSymbolFromCurrentScope(String symbol, out ASTNode node)
        {
            return Scopes.Last().TryGetValue(symbol.ToLower(), out node);
        }

        public bool TryGetSymbolFromSpecificScope(String symbol, out ASTNode node, String specificScope)
        {
            node = null;
            Dictionary<String, ASTNode> scope;
            return Cache.TryGetValue(specificScope.ToLower(), out scope) &&
                scope.TryGetValue(symbol.ToLower(), out node);
        }

        public void AddSymbol(String symbol, ASTNode node)
        {
            Scopes.Last().Add(symbol.ToLower(), node);
        }

        public bool TryAddSymbol(String symbol, ASTNode node)
        {
            if (!SymbolExistsInCurrentScope(symbol.ToLower()))
            {
                AddSymbol(symbol.ToLower(), node);
                return true;
            }
            return false;
        }

        public bool GoDirectlyToStack(String lowestScope)
        {
            String scope = lowestScope.ToLower();
            // TODO: 5 AM coding.. REVISIT THIS!
            if (CurrentScopeName != "object")
                throw new InvalidOperationException("Tried to go a scopestack while not at the top level scope!");
            if (scope == "object")
                return true;

            var scopes = scope.Split('.');
            for (int n = 1; n < scopes.Length; n++) // Start after "Object."
            {
                if (!Cache.ContainsKey(CurrentScopeName + "." + scopes[n]))
                    return false; // this should not happen? possibly load classes from ppc on demand?
                PushScope(scopes[n]);
            }

            return true;
        }

        public void RevertToObjectStack()
        {
            while (CurrentScopeName != "object")
                PopScope();
        }

        public bool OperatorSignatureExists(OperatorDeclaration sig)
        {
            if (sig.Type == ASTNodeType.InfixOperator)
                return Operators.Any(opdecl => opdecl.Value.Type == ASTNodeType.InfixOperator && sig.IdenticalSignature((opdecl.Value as InOpDeclaration)));
            else if (sig.Type == ASTNodeType.PrefixOperator)
                return Operators.Any(opdecl => opdecl.Value.Type == ASTNodeType.PrefixOperator && sig.IdenticalSignature((opdecl.Value as PreOpDeclaration)));
            else if (sig.Type == ASTNodeType.PostfixOperator)
                return Operators.Any(opdecl => opdecl.Value.Type == ASTNodeType.PostfixOperator && sig.IdenticalSignature((opdecl.Value as PostOpDeclaration)));
            return false;
        }

        public void AddOperator(OperatorDeclaration op)
        {
            Operators.Add(op.OperatorKeyword.ToLower(), op);
        }

        public OperatorDeclaration GetOperator(OperatorDeclaration sig)
        {
            if (sig.Type == ASTNodeType.InfixOperator)
                return Operators.First(opdecl => opdecl.Value.Type == ASTNodeType.InfixOperator && sig.IdenticalSignature((opdecl.Value as InOpDeclaration))).Value;
            else if (sig.Type == ASTNodeType.PrefixOperator)
                return Operators.First(opdecl => opdecl.Value.Type == ASTNodeType.PrefixOperator && sig.IdenticalSignature((opdecl.Value as PreOpDeclaration))).Value;
            else if (sig.Type == ASTNodeType.PostfixOperator)
                return Operators.First(opdecl => opdecl.Value.Type == ASTNodeType.PostfixOperator && sig.IdenticalSignature((opdecl.Value as PostOpDeclaration))).Value;
            return null;
        }

        public bool GetInOperator(out InOpDeclaration op, String name, VariableType lhs, VariableType rhs)
        {
            op = null;
            var lookup = Operators.First(opdecl => opdecl.Value.Type == ASTNodeType.InfixOperator && opdecl.Value.OperatorKeyword == name
                && (opdecl.Value as InOpDeclaration).LeftOperand.VarType.Name.ToLower() == lhs.Name.ToLower()
                && (opdecl.Value as InOpDeclaration).RightOperand.VarType.Name.ToLower() == rhs.Name.ToLower());
            if (lookup.Equals(new KeyValuePair<String, OperatorDeclaration>()))
                return false;

            op = lookup.Value as InOpDeclaration;
            return true;
        }
    }
}
