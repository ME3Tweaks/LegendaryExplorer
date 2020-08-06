using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer;

namespace ME3Script.Analysis.Symbols
{
    public class SymbolTable
    {
        private readonly CaseInsensitiveDictionary<CaseInsensitiveDictionary<ASTNode>> Cache;
        private readonly LinkedList<CaseInsensitiveDictionary<ASTNode>> Scopes;
        private readonly LinkedList<string> ScopeNames;
        private readonly CaseInsensitiveDictionary<OperatorDeclaration> Operators;

        public string CurrentScopeName => ScopeNames.Count == 0 ? "" : ScopeNames.Last();

        public SymbolTable()
        {
            ScopeNames = new LinkedList<string>();
            Scopes = new LinkedList<CaseInsensitiveDictionary<ASTNode>>();
            Cache = new CaseInsensitiveDictionary<CaseInsensitiveDictionary<ASTNode>>();
            Operators = new CaseInsensitiveDictionary<OperatorDeclaration>();
        }

        public void PushScope(string name)
        {
            string fullName = (CurrentScopeName == "" ? "" : CurrentScopeName + ".") + name;
            bool cached = Cache.TryGetValue(fullName, out CaseInsensitiveDictionary<ASTNode> scope);
            if (!cached)
                scope = new CaseInsensitiveDictionary<ASTNode>();

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
            return TryGetSymbol(symbol, out _, outerScope);
        }

        public bool TryGetSymbolInScopeStack(string symbol, out ASTNode node, string lowestScope)
        {
            node = null;
            if (string.Equals(lowestScope, "object", StringComparison.OrdinalIgnoreCase)) //As all classes inherit from object this is already checked.
                return false;

            return TryBuildSpecificScope(lowestScope, out LinkedList<CaseInsensitiveDictionary<ASTNode>> stack) && TryGetSymbolInternal(symbol, out node, stack);
        }

        private bool TryBuildSpecificScope(string lowestScope, out LinkedList<CaseInsensitiveDictionary<ASTNode>> stack)
        {
            string[] names = lowestScope.Split('.');
            stack = new LinkedList<CaseInsensitiveDictionary<ASTNode>>();
            foreach (string scopeName in names)
            {
                if (Cache.TryGetValue(scopeName, out CaseInsensitiveDictionary<ASTNode> currentScope))
                    stack.AddLast(currentScope);
                else
                    return false;
            }
            return stack.Count > 0;
        }

        private static bool TryGetSymbolInternal(string symbol, out ASTNode node, LinkedList<CaseInsensitiveDictionary<ASTNode>> stack)
        {
            LinkedListNode<CaseInsensitiveDictionary<ASTNode>> it;
            for (it = stack.Last; it != null; it = it.Previous)
            {
                if (it.Value.TryGetValue(symbol, out node))
                    return true;
            }
            node = null;
            return false;
        }

        public bool SymbolExistsInCurrentScope(string symbol)
        {
            return Scopes.Last().ContainsKey(symbol);
        }

        public bool TryGetSymbolFromCurrentScope(string symbol, out ASTNode node)
        {
            return Scopes.Last().TryGetValue(symbol, out node);
        }

        public bool TryGetSymbolFromSpecificScope(string symbol, out ASTNode node, string specificScope)
        {
            node = null;
            return Cache.TryGetValue(specificScope, out CaseInsensitiveDictionary<ASTNode> scope) &&
                   scope.TryGetValue(symbol, out node);
        }

        public void AddSymbol(string symbol, ASTNode node)
        {
            Scopes.Last().Add(symbol, node);
        }

        public bool TryAddSymbol(string symbol, ASTNode node)
        {
            if (!SymbolExistsInCurrentScope(symbol))
            {
                AddSymbol(symbol, node);
                return true;
            }
            return false;
        }

        public bool GoDirectlyToStack(string lowestScope)
        {
            string scope = lowestScope;
            // TODO: 5 AM coding.. REVISIT THIS!
            if (!string.Equals(CurrentScopeName, "object", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Tried to go a scopestack while not at the top level scope!");
            if (string.Equals(scope, "object", StringComparison.OrdinalIgnoreCase))
                return true;

            string[] scopes = scope.Split('.');
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

        public bool OperatorSignatureExists(OperatorDeclaration sig) =>
            sig.Type switch
            {
                ASTNodeType.InfixOperator => Operators.Any(opdecl => opdecl.Value.Type == ASTNodeType.InfixOperator && sig.IdenticalSignature(opdecl.Value as InOpDeclaration)),
                ASTNodeType.PrefixOperator => Operators.Any(opdecl => opdecl.Value.Type == ASTNodeType.PrefixOperator && sig.IdenticalSignature(opdecl.Value as PreOpDeclaration)),
                ASTNodeType.PostfixOperator => Operators.Any(opdecl => opdecl.Value.Type == ASTNodeType.PostfixOperator && sig.IdenticalSignature(opdecl.Value as PostOpDeclaration)),
                _ => false
            };

        public void AddOperator(OperatorDeclaration op)
        {
            Operators.Add(op.OperatorKeyword, op);
        }

        public OperatorDeclaration GetOperator(OperatorDeclaration sig)
        {
            return sig.Type switch
            {
                ASTNodeType.InfixOperator => Operators.First(opdecl => opdecl.Value.Type == ASTNodeType.InfixOperator && sig.IdenticalSignature(opdecl.Value as InOpDeclaration)).Value,
                ASTNodeType.PrefixOperator => Operators.First(opdecl => opdecl.Value.Type == ASTNodeType.PrefixOperator && sig.IdenticalSignature(opdecl.Value as PreOpDeclaration)).Value,
                ASTNodeType.PostfixOperator => Operators.First(opdecl => opdecl.Value.Type == ASTNodeType.PostfixOperator && sig.IdenticalSignature(opdecl.Value as PostOpDeclaration)).Value,
                _ => null
            };
        }

        public bool GetInOperator(out InOpDeclaration op, string name, VariableType lhs, VariableType rhs)
        {
            op = null;
            var lookup = Operators.FirstOrDefault(opdecl => opdecl.Value.Type == ASTNodeType.InfixOperator && opdecl.Value.OperatorKeyword == name
                && string.Equals(((InOpDeclaration)opdecl.Value).LeftOperand.VarType.Name, lhs.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(((InOpDeclaration)opdecl.Value).RightOperand.VarType.Name, rhs.Name, StringComparison.OrdinalIgnoreCase));
            if (lookup.Equals(new KeyValuePair<string, OperatorDeclaration>()))
                return false;

            op = lookup.Value as InOpDeclaration;
            return true;
        }
    }
}
