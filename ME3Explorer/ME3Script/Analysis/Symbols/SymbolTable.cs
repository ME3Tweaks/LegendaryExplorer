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
        private Dictionary<string, Dictionary<string, ASTNode>> Cache;
        private LinkedList<Dictionary<string, ASTNode>> Scopes;
        private LinkedList<string> ScopeNames;
        private Dictionary<string, OperatorDeclaration> Operators;

        public string CurrentScopeName
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
            ScopeNames = new LinkedList<string>();
            Scopes = new LinkedList<Dictionary<string, ASTNode>>();
            Cache = new Dictionary<string, Dictionary<string, ASTNode>>();
            Operators = new Dictionary<string, OperatorDeclaration>();
        }

        public void PushScope(string name)
        {
            string fullName = (CurrentScopeName == "" ? "" : CurrentScopeName + ".") + name.ToLower();
            Dictionary<string, ASTNode> scope;
            bool cached = Cache.TryGetValue(fullName, out scope);
            if (!cached)
                scope = new Dictionary<string, ASTNode>();

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

        public bool TryGetSymbol(string symbol, out ASTNode node, string outerScope)
        {
            return TryGetSymbolInternal(symbol, out node, Scopes) ||
                TryGetSymbolInScopeStack(symbol, out node, outerScope);
        }

        public bool SymbolExists(string symbol, string outerScope)
        {   
            ASTNode dummy;
            return TryGetSymbol(symbol, out dummy, outerScope);
        }

        public bool TryGetSymbolInScopeStack(string symbol, out ASTNode node, string lowestScope)
        {
            node = null;
            string scope = lowestScope.ToLower();
            if (scope == "object") //As all classes inherit from object this is already checked.
                return false;

            LinkedList<Dictionary<string, ASTNode>> stack;
            if (!TryBuildSpecificScope(scope, out stack))
                return false;

            return TryGetSymbolInternal(symbol, out node, stack);
        }

        private bool TryBuildSpecificScope(string lowestScope, out LinkedList<Dictionary<string, ASTNode>> stack)
        {
            var names = lowestScope.Split('.');
            stack = new LinkedList<Dictionary<string, ASTNode>>();
            Dictionary<string, ASTNode> currentScope;
            foreach (string scopeName in names)
            {
                if (Cache.TryGetValue(scopeName, out currentScope))
                    stack.AddLast(currentScope);
                else
                    return false;
            }
            return stack.Count > 0;
        }

        private bool TryGetSymbolInternal(string symbol, out ASTNode node, LinkedList<Dictionary<string, ASTNode>> stack)
        {
            string name = symbol.ToLower();
            LinkedListNode<Dictionary<string, ASTNode>> it;
            for (it = stack.Last; it != null; it = it.Previous)
            {
                if (it.Value.TryGetValue(name, out node))
                    return true;
            }
            node = null;
            return false;
        }

        public bool SymbolExistsInCurrentScope(string symbol)
        {
            return Scopes.Last().ContainsKey(symbol.ToLower());
        }

        public bool TryGetSymbolFromCurrentScope(string symbol, out ASTNode node)
        {
            return Scopes.Last().TryGetValue(symbol.ToLower(), out node);
        }

        public bool TryGetSymbolFromSpecificScope(string symbol, out ASTNode node, string specificScope)
        {
            node = null;
            Dictionary<string, ASTNode> scope;
            return Cache.TryGetValue(specificScope.ToLower(), out scope) &&
                scope.TryGetValue(symbol.ToLower(), out node);
        }

        public void AddSymbol(string symbol, ASTNode node)
        {
            Scopes.Last().Add(symbol.ToLower(), node);
        }

        public bool TryAddSymbol(string symbol, ASTNode node)
        {
            if (!SymbolExistsInCurrentScope(symbol.ToLower()))
            {
                AddSymbol(symbol.ToLower(), node);
                return true;
            }
            return false;
        }

        public bool GoDirectlyToStack(string lowestScope)
        {
            string scope = lowestScope.ToLower();
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

        public bool GetInOperator(out InOpDeclaration op, string name, VariableType lhs, VariableType rhs)
        {
            op = null;
            var lookup = Operators.FirstOrDefault(opdecl => opdecl.Value.Type == ASTNodeType.InfixOperator && opdecl.Value.OperatorKeyword == name
                && (opdecl.Value as InOpDeclaration).LeftOperand.VarType.Name.ToLower() == lhs.Name.ToLower()
                && (opdecl.Value as InOpDeclaration).RightOperand.VarType.Name.ToLower() == rhs.Name.ToLower());
            if (lookup.Equals(new KeyValuePair<string, OperatorDeclaration>()))
                return false;

            op = lookup.Value as InOpDeclaration;
            return true;
        }
    }
}
