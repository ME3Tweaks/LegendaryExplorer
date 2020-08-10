using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer;
using ME3Script.Language.Util;
using static ME3Explorer.Unreal.UnrealFlags;
using static ME3Script.Utilities.Keywords;
using ASTNodeDict = ME3Explorer.CaseInsensitiveDictionary<ME3Script.Language.Tree.ASTNode>;

namespace ME3Script.Analysis.Symbols
{
    public class SymbolTable
    {
        private readonly CaseInsensitiveDictionary<ASTNodeDict> Cache;
        private readonly LinkedList<ASTNodeDict> Scopes;
        private readonly LinkedList<string> ScopeNames;
        private readonly CaseInsensitiveDictionary<OperatorDeclaration> Operators;
        private readonly CaseInsensitiveDictionary<VariableType> Types;

        public string CurrentScopeName => ScopeNames.Count == 0 ? "" : ScopeNames.Last();

        private SymbolTable()
        {
            ScopeNames = new LinkedList<string>();
            Scopes = new LinkedList<ASTNodeDict>();
            Cache = new CaseInsensitiveDictionary<ASTNodeDict>();
            Operators = new CaseInsensitiveDictionary<OperatorDeclaration>();
            Types = new CaseInsensitiveDictionary<VariableType>();
        }

        public static SymbolTable CreateIntrinsicTable(Class objectClass)
        {
            const EClassFlags intrinsicClassFlags = EClassFlags.Intrinsic;
            var table = new SymbolTable();

            #region CORE

            //setup root 'Object' scope
            objectClass.OuterClass = objectClass;
            objectClass.Parent = null;
            table.PushScope(objectClass.Name);
            table.AddType(objectClass);

            //primitives
            var intType = new VariableType(INT);
            table.AddType(intType);
            var floatType = new VariableType(FLOAT);
            table.AddType(floatType);
            var boolType = new VariableType(BOOL);
            table.AddType(boolType);
            var byteType = new VariableType(BYTE);
            table.AddType(byteType);
            var stringType = new VariableType(STRING);
            table.AddType(stringType);
            var stringrefType = new VariableType(STRINGREF);
            table.AddType(stringrefType);
            var biomask4Type = new VariableType(BIOMASK4);
            table.AddType(biomask4Type);
            var nameType = new VariableType(NAME);
            table.AddType(nameType);

            
            var packageType = new Class("Package", objectClass, objectClass, intrinsicClassFlags);
            table.AddType(packageType);

            //script type intrinsics
            var fieldType = new Class("Field", objectClass, objectClass, intrinsicClassFlags | EClassFlags.Abstract);
            table.AddType(fieldType);
            var structType = new Class(STRUCT, fieldType, objectClass, intrinsicClassFlags);
            table.AddType(structType);
            var scriptStructType = new Class("ScriptStruct", structType, objectClass, intrinsicClassFlags);
            table.AddType(scriptStructType);
            var stateType = new Class(STATE, structType, objectClass, intrinsicClassFlags);
            table.AddType(stateType);
            var functionType = new Class(FUNCTION, structType, stateType, intrinsicClassFlags);
            table.AddType(functionType);
            var enumType = new Class(ENUM, fieldType, structType, intrinsicClassFlags);
            table.AddType(enumType);
            var constType = new Class(CONST, fieldType, structType, intrinsicClassFlags);
            table.AddType(constType);
            var classType = new Class(CLASS, stateType, packageType, intrinsicClassFlags);
            table.AddType(classType);

            //property intrinsics
            var propertyType = new Class("Property", fieldType, fieldType, intrinsicClassFlags);
            table.AddType(propertyType);
            var bytePropertyType = new Class("ByteProperty", propertyType, objectClass, intrinsicClassFlags);
            table.AddType(bytePropertyType);
            var intPropertyType = new Class("IntProperty", propertyType, objectClass, intrinsicClassFlags);
            table.AddType(intPropertyType);
            var boolPropertyType = new Class("BoolProperty", propertyType, objectClass, intrinsicClassFlags);
            table.AddType(boolPropertyType);
            var floatPropertyType = new Class("FloatProperty", propertyType, objectClass, intrinsicClassFlags);
            table.AddType(floatPropertyType);
            var objectPropertyType = new Class("ObjectProperty", propertyType, objectClass, intrinsicClassFlags);
            table.AddType(objectPropertyType);
            var componentPropertyType = new Class("ComponentProperty", propertyType, objectClass, intrinsicClassFlags);
            table.AddType(componentPropertyType);
            var classPropertyType = new Class("ClassProperty", propertyType, objectClass, intrinsicClassFlags);
            table.AddType(classPropertyType);
            var interfacePropertyType = new Class("InterfaceProperty", propertyType, objectClass, intrinsicClassFlags);
            table.AddType(interfacePropertyType);
            var namePropertyType = new Class("NameProperty", propertyType, objectClass, intrinsicClassFlags);
            table.AddType(namePropertyType);
            var strPropertyType = new Class("StrProperty", propertyType, objectClass, intrinsicClassFlags);
            table.AddType(strPropertyType);
            var arrayPropertyType = new Class("ArrayProperty", propertyType, objectClass, intrinsicClassFlags);
            table.AddType(arrayPropertyType);
            var mapPropertyType = new Class("MapProperty", propertyType, objectClass, intrinsicClassFlags);
            table.AddType(mapPropertyType);
            var structPropertyType = new Class("StructProperty", propertyType, objectClass, intrinsicClassFlags);
            table.AddType(structPropertyType);
            var delegatePropertyType = new Class("DelegateProperty", propertyType, objectClass, intrinsicClassFlags);
            table.AddType(delegatePropertyType);
            var stringRefPropertyType = new Class("StringRefProperty", propertyType, objectClass, intrinsicClassFlags);
            table.AddType(stringRefPropertyType);


            #endregion

            #region ENGINE 
            //TODO: these classes have members accessed from script that need to be added here

            var clientType = new Class("Client", objectClass, objectClass, intrinsicClassFlags | EClassFlags.Abstract | EClassFlags.Config) { ConfigName = "Engine" };
            table.AddType(clientType);
            var staticMeshType = new Class("StaticMesh", objectClass, objectClass, intrinsicClassFlags | EClassFlags.SafeReplace | EClassFlags.CollapseCategories);
            table.AddType(staticMeshType);
            var fracturedStaticMeshType = new Class("FracturedStaticMesh", staticMeshType, objectClass, intrinsicClassFlags | EClassFlags.SafeReplace | EClassFlags.CollapseCategories);
            table.AddType(fracturedStaticMeshType);
            var shadowMap1DType = new Class("ShadowMap1D", objectClass, objectClass, intrinsicClassFlags);
            table.AddType(shadowMap1DType);
            var levelBase = new Class("LevelBase", objectClass, objectClass, intrinsicClassFlags | EClassFlags.Abstract);
            table.AddType(levelBase);
            var levelType = new Class("Level", levelBase, objectClass, intrinsicClassFlags);
            table.AddType(levelType);
            var pendingLevel = new Class("PendingLevel", levelBase, objectClass, intrinsicClassFlags | EClassFlags.Abstract);
            table.AddType(pendingLevel);
            var modelType = new Class("Model", objectClass, objectClass, intrinsicClassFlags);
            table.AddType(modelType);
            var worldType = new Class("World", objectClass, objectClass, intrinsicClassFlags);
            table.AddType(worldType);
            var polysType = new Class("Polys", objectClass, objectClass, intrinsicClassFlags);
            table.AddType(polysType);


            #endregion

            return table;
        }

        public void PushScope(string name)
        {
            string fullName = (CurrentScopeName == "" ? "" : $"{CurrentScopeName}.") + name;
            bool cached = Cache.TryGetValue(fullName, out ASTNodeDict scope);
            if (!cached)
                scope = new ASTNodeDict();

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

        public bool TryResolveType(ref VariableType stub, bool globalOnly = false)
        {
            switch (stub)
            {
                case DynamicArrayType dynArr:
                {
                    dynArr.ElementType.Outer = dynArr;
                    return TryResolveType(ref dynArr.ElementType, globalOnly);
                }
                case DelegateType delegateType:
                {
                    if (TryGetSymbol(delegateType.DefaultFunction.Name, out ASTNode funcNode, NodeUtils.GetOuterClassScope(stub.Outer))
                     && funcNode is Function func)
                    {
                        delegateType.DefaultFunction = func;
                        return true;
                    }
                    return false;
                }
            }

            VariableType temp = InternalResolveType(stub, globalOnly ? null : NodeUtils.GetContainingScopeObject(stub));
            if (temp != null)
            {
                stub = temp;
                return true;
            }

            return false;
        }

        private VariableType InternalResolveType(VariableType stub, IObjectType containingClass)
        {
            //first check the containing class (needed for structs that don't have globally unique names)
            if (containingClass?.TypeDeclarations.FirstOrDefault(decl => decl.Name.CaseInsensitiveEquals(stub.Name)) is VariableType typeDecl)
            {
                return typeDecl;
            }

            if (Types.TryGetValue(stub.Name, out VariableType temp))
            {
                return temp;
            }

            return null;
        }

        public bool SymbolExists(string symbol, string outerScope)
        {
            return TryGetSymbol(symbol, out _, outerScope);
        }

        public bool TypeExists(VariableType type, bool globalOnly = false) => TryResolveType(ref type, globalOnly);

        public bool TryGetSymbolInScopeStack(string symbol, out ASTNode node, string lowestScope)
        {
            node = null;
            if (string.Equals(lowestScope, "object", StringComparison.OrdinalIgnoreCase)) //As all classes inherit from object this is already checked.
                return false;

            return TryBuildSpecificScope(lowestScope, out LinkedList<ASTNodeDict> stack) && TryGetSymbolInternal(symbol, out node, stack);
        }

        private bool TryBuildSpecificScope(string lowestScope, out LinkedList<ASTNodeDict> stack)
        {
            string[] names = lowestScope.Split('.');
            stack = new LinkedList<ASTNodeDict>();
            foreach (string scopeName in names)
            {
                if (Cache.TryGetValue(scopeName, out ASTNodeDict currentScope))
                    stack.AddLast(currentScope);
                else
                    return false;
            }
            return stack.Count > 0;
        }

        private static bool TryGetSymbolInternal(string symbol, out ASTNode node, LinkedList<ASTNodeDict> stack)
        {
            LinkedListNode<ASTNodeDict> it;
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
            return Cache.TryGetValue(specificScope, out ASTNodeDict scope) &&
                   scope.TryGetValue(symbol, out node);
        }

        public void AddSymbol(string symbol, ASTNode node)
        {
            Scopes.Last().Add(symbol, node);
        }

        public void AddType(VariableType node)
        {
            Types.Add(node.Name, node);

            //hack for registering intrinsic classes that inherit from non-intrinsics
            switch (node.Name)
            {
                case "Player":
                {
                    var objClass = Types["Object"];
                    var netConType = new Class("NetConnection", node, objClass, EClassFlags.Intrinsic | EClassFlags.Abstract | EClassFlags.Transient | EClassFlags.Config) { ConfigName = "Engine" };
                    AddType(netConType);
                    AddType(new Class("ChildConnection", netConType, objClass, EClassFlags.Intrinsic | EClassFlags.Transient | EClassFlags.Config) { ConfigName = "Engine" });
                    break;
                }
            }
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

        public bool TryAddType(VariableType node)
        {
            if (TypeExists(node, true))
            {
                return false;
            }
            AddType(node);
            return true;
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
                if (!Cache.ContainsKey($"{CurrentScopeName}.{scopes[n]}"))
                    return false; // this should not happen? possibly load classes from ppc on demand?
                PushScope(scopes[n]);
            }

            return true;
        }

        public void RevertToObjectStack()
        {
            while (!string.Equals(CurrentScopeName, "object", StringComparison.OrdinalIgnoreCase))
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
