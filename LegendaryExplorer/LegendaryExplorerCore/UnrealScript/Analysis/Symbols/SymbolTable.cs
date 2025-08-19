using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Language.Util;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Utilities;
using static LegendaryExplorerCore.Unreal.UnrealFlags;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript.Analysis.Symbols
{
    internal class SymbolTable
    {
        private class OperatorDefinitions
        {
            public readonly Dictionary<TokenType, List<PreOpDeclaration>> PrefixOperators = [];
            public readonly Dictionary<TokenType, List<InOpDeclaration>> InfixOperators = [];
            public readonly Dictionary<TokenType, List<PostOpDeclaration>> PostfixOperators = [];
            public readonly List<TokenType> InFixOperatorSymbols = [];
            public readonly Dictionary<string, OperatorDeclaration> VerboseNameOperatorLookup = [];
            public readonly HashSet<string> FriendlyNames = [];
        }

        #region Primitives

        public static readonly PrimitiveType IntType = new(INT, EPropertyType.Int);
        public static readonly PrimitiveType FloatType = new(FLOAT, EPropertyType.Float);
        public static readonly PrimitiveType BoolType = new(BOOL, EPropertyType.Bool);
        public static readonly PrimitiveType ByteType = new(BYTE, EPropertyType.Byte);
        public static readonly PrimitiveType BioMask4Type = new(BIOMASK4, EPropertyType.Byte);
        public static readonly PrimitiveType StringType = new(STRING, EPropertyType.String);
        public static readonly PrimitiveType StringRefType = new(STRINGREF, EPropertyType.StringRef);
        public static readonly PrimitiveType NameType = new(NAME, EPropertyType.Name);

        public static bool IsPrimitive(VariableType vt) => vt is PrimitiveType;
        #endregion

        private readonly CaseInsensitiveDictionary<ASTNodeDict> Cache;
        private readonly Stack<ASTNodeDict> Scopes;
        private readonly Stack<string> ScopeNames;
        private readonly CaseInsensitiveDictionary<VariableType> TypeDict;

        internal IReadOnlyCollection<VariableType> Types => TypeDict.Values;

        public string CurrentScopeName => ScopeNames.Count == 0 ? "" : ScopeNames.Peek();

        private readonly OperatorDefinitions Operators;

        public readonly MEGame Game;

        private SymbolTable(MEGame game)
        {
            Operators = new OperatorDefinitions();
            ScopeNames = new Stack<string>();
            Scopes = new Stack<ASTNodeDict>();
            Cache = new CaseInsensitiveDictionary<ASTNodeDict>();
            TypeDict = new CaseInsensitiveDictionary<VariableType>();
            Game = game;
        }

        private SymbolTable(Stack<string> scopeNames, Stack<ASTNodeDict> scopes, CaseInsensitiveDictionary<ASTNodeDict> cache, CaseInsensitiveDictionary<VariableType> typeDict, OperatorDefinitions ops, MEGame game)
        {
            Operators = ops;
            ScopeNames = scopeNames;
            Scopes = scopes;
            Cache = cache;
            TypeDict = typeDict;
            Game = game;
        }

        public static SymbolTable CreateIntrinsicTable(Class objectClass, MEGame game)
        {
            const EClassFlags intrinsicClassFlags = EClassFlags.Intrinsic;
            var table = new SymbolTable(game);

            #region CORE

            //setup root 'Object' scope
            objectClass.OuterClass = objectClass;
            objectClass.Parent = null;
            table.PushScope(objectClass.Name);
            table.AddType(objectClass);

            //primitives
            table.AddType(IntType);
            table.AddType(FloatType);
            table.AddType(BoolType);
            table.AddType(ByteType);
            table.AddType(StringType);
            table.AddType(StringRefType);
            if (game >= MEGame.ME3)
            {
                table.AddType(BioMask4Type);
            }
            table.AddType(NameType);

            //Add fake constants
            float unrealNaN = BitConverter.UInt32BitsToSingle(uint.MaxValue);//specific NaN value to reserialize existing NaNs identically
            objectClass.TypeDeclarations.Add(new Const("NaN", "NaN"){Literal = new FloatLiteral(unrealNaN)});
            objectClass.TypeDeclarations.Add(new Const("Infinity", "Infinity"){Literal = new FloatLiteral(float.PositiveInfinity)});

            Class packageType = null;
            switch (game)
            {
                case >= MEGame.ME3:
                    packageType = new Class("Package", objectClass, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(packageType);
                    break;
                case MEGame.ME1:
                    table.AddType(new Class("ObjectRedirector", objectClass, objectClass, intrinsicClassFlags) { Package = "Core" });
                    break;
            }

            //script type intrinsics
            var fieldType = new Class("Field", objectClass, objectClass, intrinsicClassFlags | EClassFlags.Abstract) { Package = "Core" };
            table.AddType(fieldType);
            table.PushScope(fieldType.Name);
                var structType = new Class(STRUCT, fieldType, objectClass, intrinsicClassFlags) { Package = "Core" };
                table.AddType(structType);
                table.PushScope(structType.Name);
                    var stateType = new Class(STATE, structType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(stateType);
                    table.PushScope(stateType.Name);
                        var classType = new Class(CLASS, stateType, packageType, intrinsicClassFlags) { Package = "Core" };
                        table.AddType(classType);
                        table.PushScope(classType.Name); table.PopScope();
                    table.PopScope();
                    var scriptStructType = new Class("ScriptStruct", structType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(scriptStructType);
                    table.PushScope(scriptStructType.Name); table.PopScope();
                    var functionType = new Class(FUNCTION, structType, stateType, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(functionType);
                    table.PushScope(functionType.Name); table.PopScope();
                table.PopScope();
                var enumType = new Class(ENUM, fieldType, structType, intrinsicClassFlags) { Package = "Core" };
                table.AddType(enumType);
                table.PushScope(enumType.Name); table.PopScope();
                var constType = new Class(CONST, fieldType, structType, intrinsicClassFlags) { Package = "Core" };
                table.AddType(constType);
                table.PushScope(enumType.Name); table.PopScope();

                //property intrinsics
                var propertyType = new Class("Property", fieldType, objectClass, intrinsicClassFlags) { Package = "Core" };
                table.AddType(propertyType);
                table.PushScope(propertyType.Name);
                    var bytePropertyType = new Class("ByteProperty", propertyType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(bytePropertyType);
                    table.PushScope(bytePropertyType.Name); table.PopScope();
                    var intPropertyType = new Class("IntProperty", propertyType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(intPropertyType);
                    table.PushScope(intPropertyType.Name); table.PopScope();
                    var boolPropertyType = new Class("BoolProperty", propertyType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(boolPropertyType);
                    table.PushScope(boolPropertyType.Name); table.PopScope();
                    var floatPropertyType = new Class("FloatProperty", propertyType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(floatPropertyType);
                    table.PushScope(floatPropertyType.Name); table.PopScope();
                    var objectPropertyType = new Class("ObjectProperty", propertyType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(objectPropertyType);
                    table.PushScope(objectPropertyType.Name); table.PopScope();
                    var componentPropertyType = new Class("ComponentProperty", propertyType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(componentPropertyType);
                    table.PushScope(componentPropertyType.Name); table.PopScope();
                    var classPropertyType = new Class("ClassProperty", propertyType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(classPropertyType);
                    table.PushScope(classPropertyType.Name); table.PopScope();
                    var interfacePropertyType = new Class("InterfaceProperty", propertyType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(interfacePropertyType);
                    table.PushScope(interfacePropertyType.Name); table.PopScope();
                    var namePropertyType = new Class("NameProperty", propertyType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(namePropertyType);
                    table.PushScope(namePropertyType.Name); table.PopScope();
                    var strPropertyType = new Class("StrProperty", propertyType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(strPropertyType);
                    table.PushScope(strPropertyType.Name); table.PopScope();
                    var arrayPropertyType = new Class("ArrayProperty", propertyType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(arrayPropertyType);
                    table.PushScope(arrayPropertyType.Name); table.PopScope();
                    var mapPropertyType = new Class("MapProperty", propertyType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(mapPropertyType);
                    table.PushScope(mapPropertyType.Name); table.PopScope();
                    var structPropertyType = new Class("StructProperty", propertyType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(structPropertyType);
                    table.PushScope(structPropertyType.Name); table.PopScope();
                    var delegatePropertyType = new Class("DelegateProperty", propertyType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(delegatePropertyType);
                    table.PushScope(delegatePropertyType.Name); table.PopScope();
                    var stringRefPropertyType = new Class("StringRefProperty", propertyType, objectClass, intrinsicClassFlags) { Package = "Core" };
                    table.AddType(stringRefPropertyType);
                    table.PushScope(stringRefPropertyType.Name); table.PopScope();
                table.PopScope();
            table.PopScope();
            #endregion

            #region ENGINE 
            //TODO: these classes have members accessed from script that need to be added here

            var clientType = new Class("Client", objectClass, objectClass, intrinsicClassFlags | EClassFlags.Abstract | EClassFlags.Config)
            {
                Package = "Engine",
                ConfigName = "Engine",
                VariableDeclarations =
                {
                    new VariableDeclaration(IntType, default, "StartupResolutionX"),
                    new VariableDeclaration(IntType, default, "StartupResolutionY"),
                    new VariableDeclaration(BoolType, default, "StartupFullscreen"),
                    new VariableDeclaration(BoolType, default, "UseHardwareCursor"),
                }
            };
            table.AddType(clientType);
            var staticMeshType = new Class("StaticMesh", objectClass, objectClass, intrinsicClassFlags | EClassFlags.SafeReplace | EClassFlags.CollapseCategories)
            {
                Package = "Engine",
                VariableDeclarations =
                {
                    new VariableDeclaration(BoolType, default, "UseSimpleRigidBodyCollision"),
                    new VariableDeclaration(BoolType, default, "UseSimpleLineCollision"),
                    new VariableDeclaration(BoolType, default, "UseSimpleBoxCollision"),
                    new VariableDeclaration(BoolType, default, "bUsedForInstancing"),
                    new VariableDeclaration(BoolType, default, "ForceDoubleSidedShadowVolumes"),
                    new VariableDeclaration(BoolType, default, "UseFullPrecisionUVs"),
                    //"BodySetup" added in the AddType function
                    new VariableDeclaration(FloatType, default, "LODDistanceRatio"),
                    new VariableDeclaration(IntType, default, "LightMapCoordinateIndex"),
                    new VariableDeclaration(IntType, default, "LightMapResolution"),
                }
            };
            table.AddType(staticMeshType);
            table.PushScope(staticMeshType.Name);
                var fracturedStaticMeshType = new Class("FracturedStaticMesh", staticMeshType, objectClass, intrinsicClassFlags | EClassFlags.SafeReplace | EClassFlags.CollapseCategories)
                {
                    Package = "Engine",
                    VariableDeclarations =
                    {
                        new VariableDeclaration(staticMeshType, default, "SourceStaticMesh"),
                        new VariableDeclaration(staticMeshType, default, "SourceCoreMesh"),
                        new VariableDeclaration(FloatType, default, "CoreMeshScale"),
                        new VariableDeclaration(new VariableType("Vector"), default, "CoreMeshScale3D"),
                        new VariableDeclaration(new VariableType("Vector"), default, "CoreMeshOffset"),
                        new VariableDeclaration(new VariableType("Rotator"), default, "CoreMeshRotation"),
                        new VariableDeclaration(new VariableType("Vector"), default, "PlaneBias"),
                        new VariableDeclaration(BoolType, default, "bSliceUsingCoreCollision"),
                        new VariableDeclaration(new VariableType("ParticleSystem"), default, "FragmentDestroyEffect"),
                        new VariableDeclaration(new DynamicArrayType(new VariableType("ParticleSystem")), default, "FragmentDestroyEffects"),
                        new VariableDeclaration(FloatType, default, "FragmentDestroyEffectScale"),
                        new VariableDeclaration(FloatType, default, "FragmentHealthScale"),
                        new VariableDeclaration(FloatType, default, "FragmentMinHealth"),
                        new VariableDeclaration(FloatType, default, "FragmentMaxHealth"),
                        new VariableDeclaration(BoolType, default, "bUniformFragmentHealth"),
                        new VariableDeclaration(FloatType, default, "ChunkLinVel"),
                        new VariableDeclaration(FloatType, default, "ChunkAngVel"),
                        new VariableDeclaration(FloatType, default, "ChunkLinHorizontalScale"),
                        new VariableDeclaration(FloatType, default, "ExplosionVelScale"),
                        new VariableDeclaration(BoolType, default, "bCompositeChunksExplodeOnImpact"),
                        new VariableDeclaration(BoolType, default, "bFixIsolatedChunks"),
                        new VariableDeclaration(BoolType, default, "bAlwaysBreakOffIsolatedIslands"),
                        new VariableDeclaration(BoolType, default, "bSpawnPhysicsChunks"),
                        new VariableDeclaration(FloatType, default, "ChanceOfPhysicsChunk"),
                        new VariableDeclaration(FloatType, default, "ExplosionChanceOfPhysicsChunk"),
                        new VariableDeclaration(FloatType, default, "NormalPhysicsChunkScaleMin"),
                        new VariableDeclaration(FloatType, default, "NormalPhysicsChunkScaleMax"),
                        new VariableDeclaration(FloatType, default, "ExplosionPhysicsChunkScaleMin"),
                        new VariableDeclaration(FloatType, default, "ExplosionPhysicsChunkScaleMax"),
                        new VariableDeclaration(FloatType, default, "MinConnectionSupportArea"),
                        new VariableDeclaration(new VariableType("MaterialInterface"), default, "DynamicOutsideMaterial"),
                        new VariableDeclaration(new VariableType("MaterialInterface"), default, "LoseChunkOutsideMaterial"),
                        new VariableDeclaration(IntType, default, "OutsideMaterialIndex"),
                    }
                };
                table.AddType(fracturedStaticMeshType);
            table.PopScope();
            var shadowMap1DType = new Class("ShadowMap1D", objectClass, objectClass, intrinsicClassFlags) { Package = "Engine" };
            table.AddType(shadowMap1DType);
            table.PushScope(shadowMap1DType.Name); table.PopScope();
            var levelBase = new Class("LevelBase", objectClass, objectClass, intrinsicClassFlags | EClassFlags.Abstract) { Package = "Engine" };
            table.AddType(levelBase);
            table.PushScope(levelBase.Name);
                var levelType = new Class("Level", levelBase, objectClass, intrinsicClassFlags)
                {
                    Package = "Engine",
                    VariableDeclarations =
                    {
                        new VariableDeclaration(FloatType, default, "LightmapTotalSize"),
                        new VariableDeclaration(FloatType, default, "ShadowmapTotalSize"),
                    }
                };
                table.AddType(levelType);
                var pendingLevel = new Class("PendingLevel", levelBase, objectClass, intrinsicClassFlags | EClassFlags.Abstract) { Package = "Engine" };
                table.AddType(pendingLevel);
                table.PushScope(pendingLevel.Name); table.PopScope();
            table.PopScope();
            var modelType = new Class("Model", objectClass, objectClass, intrinsicClassFlags) { Package = "Engine" };
            table.AddType(modelType);
            table.PushScope(modelType.Name); table.PopScope();
            var worldType = new Class("World", objectClass, objectClass, intrinsicClassFlags) { Package = "Engine" };
            table.AddType(worldType);
            table.PushScope(worldType.Name); table.PopScope();
            var polysType = new Class("Polys", objectClass, objectClass, intrinsicClassFlags) { Package = "Engine" };
            table.AddType(polysType);
            table.PushScope(polysType.Name); table.PopScope();
            table.AddType(new Class("ShaderCache", objectClass, objectClass, intrinsicClassFlags) { Package = "Engine" });
            //NetConnection, ChildConnection, LightMapTexture2D, and CodecMovieBink are also intrinsic, but are added in the AddType function because they subclass the non-instrinsic class 'Player'
            #endregion

            return table;
        }

        private readonly List<Class> intrinsicClasses = new();

        public void ValidateIntrinsics(UnrealScriptOptionsPackage usop)
        {
            foreach (var validationPass in Enums.GetValues<ValidationPass>())
            {
                foreach (Class cls in intrinsicClasses)
                {
                    cls.AcceptVisitor(new ClassValidationVisitor(null, this, validationPass, usop));
                }
            }
        }

        public void PushScope(string name, string secondaryScope = null, bool useCache = true)
        {
            string fullName = (ScopeNames.Count is 0 ? "" : (CurrentScopeName + ".")) + name;
            ASTNodeDict scope = null;
            bool cached = useCache && Cache.TryGetValue(fullName, out scope);
            if (!cached)
            {
                scope = new ASTNodeDict();
            }

            if (secondaryScope != null && secondaryScope != fullName)
            {
                scope.SecondaryScope = secondaryScope;
            }
            Scopes.Push(scope);
            ScopeNames.Push(fullName);
            
            if (useCache && !cached)
                Cache.Add(fullName, scope);
        }

        public void PopScope()
        {
            if (Scopes.Count <= 1)
                throw new InvalidOperationException();

            Scopes.Pop();
            ScopeNames.Pop();
        }

        public bool TryGetSymbol<T>(string symbol, out T node, string outerScope) where T : ASTNode
        {
            return TryGetSymbolInternal(symbol, out node, Scopes) ||
                TryGetSymbolInScopeStack(symbol, out node, outerScope);
        }

        public bool TryResolveType(ref VariableType stub, bool globalOnly = false)
        {
            switch (stub)
            {
                case StaticArrayType staticArrayType:
                {
                    if (staticArrayType.ElementType is PrimitiveType)
                    {
                        return true;
                    }
                    staticArrayType.ElementType.Outer = staticArrayType;
                    return TryResolveType(ref staticArrayType.ElementType, globalOnly);
                }
                case ClassType classType:
                {
                    return TryResolveType(ref classType.ClassLimiter, true);
                }
                case DynamicArrayType dynArr:
                {
                    if (dynArr.ElementType is PrimitiveType)
                    {
                        return true;
                    }
                    dynArr.ElementType.Outer = dynArr;
                    return TryResolveType(ref dynArr.ElementType, globalOnly);
                }
                case DelegateType delegateType:
                {
                    string functionName = delegateType.DefaultFunction.Name;
                    string scope;
                    if (functionName.Contains('.'))
                    {
                        var parts = functionName.Split('.');
                        functionName = parts[^1];
                        if (parts.Length == 2 && TypeDict.TryGetValue(parts[0], out VariableType type) && type is Class cls)
                        {
                            scope = cls.GetInheritanceString();
                        }
                        else
                        {
                            scope = string.Join(".", parts.Take(parts.Length - 1));
                        }
                    }
                    else
                    {
                        scope = NodeUtils.GetOuterClassScope(stub.Outer);
                    }

                    if (TryGetSymbol(functionName, out Function func, scope))
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

        private VariableType InternalResolveType(VariableType stub, ObjectType containingClass)
        {
            //first check the containing class (needed for structs that don't have globally unique names)
            if (containingClass is not null)
            {
                foreach (VariableType decl in containingClass.TypeDeclarations)
                {
                    if (decl.Name.CaseInsensitiveEquals(stub.Name))
                    {
                        return decl;
                    }
                }
            }

            if (TypeDict.TryGetValue(stub.Name, out VariableType temp))
            {
                return temp;
            }

            return null;
        }
        
        public bool TypeExists(VariableType type, bool globalOnly = false) => TryResolveType(ref type, globalOnly);

        public bool TryGetSymbolInScopeStack<T>(string symbol, out T node, string lowestScope) where T : ASTNode
        {
            node = null;

            return TryBuildSpecificScope(lowestScope, out Stack<ASTNodeDict> stack) && TryGetSymbolInternal(symbol, out node, stack);
        }

        private bool TryBuildSpecificScope(string lowestScope, out Stack<ASTNodeDict> stack)
        {
            IEnumerable<string> names = lowestScope.Split('.');
            if (!names.FirstOrDefault().CaseInsensitiveEquals(OBJECT))
            {
                names = names.Prepend(OBJECT);
            }
            stack = new Stack<ASTNodeDict>();
            string scopeName = null;
            foreach (string name in names)
            {
                if (scopeName != null)
                {
                    scopeName += ".";
                }

                scopeName += name;
                if (Cache.TryGetValue(scopeName, out ASTNodeDict currentScope))
                    stack.Push(currentScope);
                else
                    return false;
            }
            return stack.Count > 0;
        }

        private bool TryGetSymbolInternal<T>(string symbol, out T outNode, Stack<ASTNodeDict> stack) where T : ASTNode
        {
            foreach (ASTNodeDict node in stack)
            {
                ASTNodeDict nodeDict = node;
                if (nodeDict.TryGetValue(symbol, out ASTNode tempNode) && tempNode is T typedTempNode)
                {
                    outNode = typedTempNode;
                    return true;
                }

                /*
                SecondaryScope is an alternate chain of parents that needs to be fully searched before the standard parent chain. 
                SecondaryScope is used for State inheritance and Struct inheritance. 
                For Example, given: 

                class A extends Object;
                {
                    function F();
                     
                    state X
                    {
                      function F();
                    }
                }

                class B extends A;
                {
                    function F();

                    state X
                    {
                    }

                    state Y extends X
                    {
                    }
                }


                B.Y's parent scope chain is B.Y -> B -> A -> Object , but its SecondaryScope chain is B.Y -> B.X -> A.X
                The SecondaryScope must be searched first becuase if F() is called from within B.Y, it must resolve to A.X.F, not B.F 
                */
                while (nodeDict.SecondaryScope != null && Cache.TryGetValue(nodeDict.SecondaryScope, out nodeDict))
                {
                    if (nodeDict.TryGetValue(symbol, out tempNode) && tempNode is T)
                    {
                        outNode = (T)tempNode;
                        return true;
                    }
                }
            }
            outNode = null;
            return false;
        }

        public bool SymbolExistsInCurrentScope(string symbol)
        {
            return Scopes.Peek().ContainsKey(symbol);
        }

        public bool SymbolExistsInParentScopes(string symbol)
        {
            if (Scopes.Count < 2)
            {
                return false;
            }
            ASTNodeDict temp = Scopes.Pop();
            bool result = TryGetSymbolInternal<ASTNode>(symbol, out _, Scopes);
            Scopes.Push(temp);
            return result;
        }

        public bool TryGetSymbol<T>(string symbol, out T outNode) where T : ASTNode
        {
            return TryGetSymbolInternal(symbol, out outNode, Scopes);
        }

        public bool TryGetSymbolFromCurrentScope(string symbol, out ASTNode node)
        {
            return Scopes.Peek().TryGetValue(symbol, out node);
        }

        public bool TryGetSymbolFromSpecificScope<T>(string symbol, out T node, string specificScope) where T : ASTNode
        {
            if (Cache.TryGetValue(specificScope, out ASTNodeDict scope) &&
                scope.TryGetValue(symbol, out ASTNode astNode) && astNode is T tNode)
            {
                node = tNode;
                return true;
            }
            node = null;
            return false;
        }

        public void AddSymbol(string symbol, ASTNode node)
        {
            Scopes.Peek().Add(symbol, node);
        }

        public void ReplaceSymbol(string symbol, ASTNode node, bool clearAssociatedScope)
        {
            Scopes.Peek()[symbol] = node;
            if (clearAssociatedScope)
            {
                ClearScope(symbol);
            }
        }

        public void ClearScope(string symbol)
        {
            PushScope(symbol);

            string scopeName = CurrentScopeName;
            Cache.Remove(scopeName);
            scopeName += '.';
            foreach (string s in Cache.Keys.Where(k => k.StartsWith(scopeName, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                Cache.Remove(s);
            }

            PopScope();
        }

        public void RemoveSymbol(string symbol)
        {
            ClearScope(symbol);
            Scopes.Peek().Remove(symbol);
        }

        public bool AddType(VariableType node)
        {
            //awful hack for dealing with the fact that ME2 has 2 different classes with the same name
            //Hopefully the one defined later is the one that actually gets used...
            if (node.Name == "SFXGameEffect_DamageBonus")
            {
                TypeDict[node.Name] = node;
            }
            else if (TypeDict.ContainsKey(node.Name))
            {
                if (node is Class)
                {
                    //encountering multiple definitions of the same class is a somewhat unavoidable consequence of how ME games are compiled, so a more graceful handling than an exception is warranted.
                    return false;
                }

                throw new Exception($"Type '{node.Name}' has already been defined!");
            }
            else
            {
                TypeDict.Add(node.Name, node);
            }

            //hack for registering intrinsic classes that inherit from non-intrinsics
            switch (node.Name)
            {
                case "Player":
                    {
                        var objClass = TypeDict[OBJECT];
                        var netConType = new Class("NetConnection", node, objClass, EClassFlags.Intrinsic | EClassFlags.Abstract | EClassFlags.Transient | EClassFlags.Config)
                        {
                            ConfigName = "Engine",
                            Package = "Engine"
                        };
                        AddType(netConType);
                        PushScope(netConType.Name);
                        var childConType = new Class("ChildConnection", netConType, objClass, EClassFlags.Intrinsic | EClassFlags.Transient | EClassFlags.Config, vars: [new(netConType, default, "Parent")])
                        {
                            ConfigName = "Engine", Package = "Engine"
                        };
                        AddType(childConType);
                        PushScope(childConType.Name);
                        AddSymbol("Parent", childConType.VariableDeclarations[0]);
                        PopScope();
                        netConType.VariableDeclarations.Add(new VariableDeclaration(new DynamicArrayType(new VariableType("ChildConnection")), default, "Children"));
                        AddSymbol("Children", netConType.VariableDeclarations[0]);
                        PopScope();
                        break;
                    }
                case "Texture2D":
                    {
                        var objClass = TypeDict[OBJECT];
                        var lightmapTexture2DType = new Class("LightMapTexture2D", node, objClass, EClassFlags.Intrinsic | EClassFlags.Config)
                        {
                            ConfigName = "Engine",
                            Package = "Engine"
                        };
                        AddType(lightmapTexture2DType);
                        PushScope(lightmapTexture2DType.Name);
                        PopScope();
                        break;
                    }
                case "RB_BodySetup":
                    {
                        PushScope("StaticMesh");
                        var bodySetup = new VariableDeclaration(node, default, "BodySetup");
                        ((Class)TypeDict["StaticMesh"]).VariableDeclarations.Add(bodySetup);
                        AddSymbol(bodySetup.Name, bodySetup);
                        PopScope();
                        break;
                    }
                case "CodecMovie":
                    {
                        var codecBinkType = new Class("CodecMovieBink", node, TypeDict[OBJECT], EClassFlags.Intrinsic | EClassFlags.Transient) { Package = "Engine" };
                        AddType(codecBinkType);
                        PushScope(codecBinkType.Name); PopScope();
                        break;
                    }
                case "Material":
                {
                    //for t3d parsing
                    var matClass = (Class)node;
                    if (matClass.VariableDeclarations.All(varDecl => varDecl.Name != "ReferencedTextureGuids"))
                    {
                        //MUST USE STUB FOR GUID TYPE! The linker (ClassValidationVisitor) expects a DynamicArrayType to have either a primitive type or a stub.
                        //Using the real guid type will cause it to become corrupted 
                        matClass.VariableDeclarations.Add(new VariableDeclaration(new DynamicArrayType(new VariableType("Guid")), EPropertyFlags.Transient | EPropertyFlags.BioNonShip | EPropertyFlags.EditorOnly, "ReferencedTextureGuids"));
                    }
                    if (matClass.VariableDeclarations.All(varDecl => varDecl.Name != "EditorComments"))
                    {
                        matClass.VariableDeclarations.Add(new VariableDeclaration(new DynamicArrayType(StringType), EPropertyFlags.BioNonShip | EPropertyFlags.EditorOnly, "EditorComments"));
                    }
                    break;
                }
                case "MaterialExpression":
                {
                    // for t3d parsing
                    var exprClass = (Class)node;
                    if (exprClass.VariableDeclarations.All(varDecl => varDecl.Name != "MaterialExpressionEditorX"))
                    {
                        exprClass.VariableDeclarations.Add(new VariableDeclaration(IntType, EPropertyFlags.BioNonShip | EPropertyFlags.EditorOnly, "MaterialExpressionEditorX"));
                    }
                    if (exprClass.VariableDeclarations.All(varDecl => varDecl.Name != "MaterialExpressionEditorY"))
                    {
                        exprClass.VariableDeclarations.Add(new VariableDeclaration(IntType, EPropertyFlags.BioNonShip | EPropertyFlags.EditorOnly, "MaterialExpressionEditorY"));
                    }
                    break;
                }
            }

            if (node is Class c && c.Flags.Has(EClassFlags.Intrinsic))
            {
                intrinsicClasses.Add(c);
            }

            return true;
        }

        public void RemoveTypeAndChildTypes(VariableType type)
        {
            TypeDict.Remove(type.Name);
            if (type is ObjectType objectType)
            {
                foreach (VariableType innerType in objectType.TypeDeclarations)
                {
                    RemoveTypeAndChildTypes(innerType);
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

        public bool GoDirectlyToStack(string lowestScope, bool createScopesIfNeccesary = false)
        {
            string scope = lowestScope;
            if (!string.Equals(CurrentScopeName, OBJECT, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Tried to go a scopestack while not at the top level scope!");
            if (string.Equals(scope, OBJECT, StringComparison.OrdinalIgnoreCase))
                return true;

            string[] scopes = scope.Split('.');
            if (!scopes[0].CaseInsensitiveEquals(OBJECT))
            {
                //all scopes must start with Object
                return false;
            }
            for (int n = 1; n < scopes.Length; n++) // Start after "Object."
            {
                if (!createScopesIfNeccesary && !Cache.ContainsKey($"{CurrentScopeName}.{scopes[n]}"))
                {
                    throw new InvalidOperationException($"Could not go to scope \"{lowestScope}\" because scope \"{CurrentScopeName}.{scopes[n]}\" does not exist! Please file a detailed bug report if you see this.");
                }
                PushScope(scopes[n]);
            }

            return true;
        }

        public void RevertToObjectStack()
        {
            while (!string.Equals(CurrentScopeName, OBJECT, StringComparison.OrdinalIgnoreCase))
                PopScope();
        }

        //can't have more than one operator of the same type with identical parameters 
        public bool TryAddOperator(OperatorDeclaration op)
        {
            switch (op)
            {
                case PreOpDeclaration preOpDeclaration:
                    if (Operators.PrefixOperators.TryGetValue(op.OperatorType, out List<PreOpDeclaration> preOperators))
                    {
                        if (preOperators.Any(opDecl => preOpDeclaration.Operand.VarType == opDecl.Operand.VarType))
                        {
                            return false;
                        }
                        preOperators.Add(preOpDeclaration);
                    }
                    else
                    {
                        Operators.PrefixOperators.Add(op.OperatorType, [preOpDeclaration]);
                    }
                    break;
                case InOpDeclaration inOpDeclaration:
                    if (Operators.InfixOperators.TryGetValue(op.OperatorType, out List<InOpDeclaration> infixOperators))
                    {
                        if (op.OperatorType is TokenType.Word)
                        {
                            if (infixOperators.Any(opDecl => inOpDeclaration.LeftOperand.VarType == opDecl.LeftOperand.VarType
                                                       && inOpDeclaration.RightOperand.VarType == opDecl.RightOperand.VarType
                                                       && inOpDeclaration.Implementer.FriendlyName.CaseInsensitiveEquals(opDecl.Implementer.FriendlyName)))
                            {
                                return false;
                            }
                        }
                        else if (infixOperators.Any(opDecl => inOpDeclaration.LeftOperand.VarType == opDecl.LeftOperand.VarType 
                                                       && inOpDeclaration.RightOperand.VarType == opDecl.RightOperand.VarType))
                        {
                            return false;
                        }
                        infixOperators.Add(inOpDeclaration);
                    }
                    else
                    {
                        Operators.InfixOperators.Add(op.OperatorType, [inOpDeclaration]);
                    }
                    break;
                case PostOpDeclaration postOpDeclaration:
                    if (Operators.PostfixOperators.TryGetValue(op.OperatorType, out List<PostOpDeclaration> postOperators))
                    {
                        if (postOperators.Any(opDecl => postOpDeclaration.Operand.VarType == opDecl.Operand.VarType))
                        {
                            return false;
                        }
                        postOperators.Add(postOpDeclaration);
                    }
                    else
                    {
                        Operators.PostfixOperators.Add(op.OperatorType, [postOpDeclaration]);
                    }
                    break;
            }
            Operators.VerboseNameOperatorLookup[op.Implementer.Name] = op;
            if (op.Implementer.FriendlyName is not null)
            {
                Operators.FriendlyNames.Add(op.Implementer.FriendlyName);
            }
            return true;
        }

        //used when compiling a single function
        public void RemoveOperator(Function implementer)
        {
            TokenType tokenType = OperatorHelper.FriendlyNameToTokenType(implementer.FriendlyName);
            if (tokenType is TokenType.INVALID)
            {
                tokenType = TokenType.Word;
            }
            if (implementer.Parameters.Count is 1)
            {
                if (implementer.Flags.Has(EFunctionFlags.PreOperator))
                {
                    RemoveOpFrom(Operators.PrefixOperators);
                }
                else
                {
                    RemoveOpFrom(Operators.PostfixOperators);
                }
            }
            else
            {
                RemoveOpFrom(Operators.InfixOperators);
            }

            void RemoveOpFrom<T>(Dictionary<TokenType, List<T>> opDict) where T : OperatorDeclaration
            {
                if (opDict.TryGetValue(tokenType, out List<T> ops))
                {
                    ops.TryRemove(opDecl => opDecl.Implementer == implementer, out _);
                }
            }
        }

        public PreOpDeclaration GetPreOp(TokenType opType, VariableType type)
        {
            if (Operators.PrefixOperators.TryGetValue(opType, out List<PreOpDeclaration> operators))
            {
                foreach (var preOpDeclaration in operators)
                {
                    if (preOpDeclaration.Operand.VarType == type)
                    {
                        return preOpDeclaration;
                    }
                }
            }

            return new PreOpDeclaration(opType, null, 0, null);
        }

        public IEnumerable<InOpDeclaration> GetInfixOperators(TokenType opType, string friendlyName = null)
        {
            if (Operators.InfixOperators.TryGetValue(opType, out List<InOpDeclaration> operators))
            {
                if (opType is TokenType.Word && friendlyName is not null)
                {
                    foreach (InOpDeclaration inOpDeclaration in operators)
                    {
                        if (inOpDeclaration.FriendlyName.CaseInsensitiveEquals(friendlyName))
                        {
                            yield return inOpDeclaration;
                        }
                    }
                }
                else
                {
                    foreach (InOpDeclaration inOpDeclaration in operators)
                    {
                        yield return inOpDeclaration;
                    }
                }
            }
        }

        public PostOpDeclaration GetPostOp(TokenType opType, VariableType type)
        {
            if (Operators.PostfixOperators.TryGetValue(opType, out List<PostOpDeclaration> operators))
            {
                foreach (var postOpDeclaration in operators)
                {
                    if (postOpDeclaration.Operand.VarType == type)
                    {
                        return postOpDeclaration;
                    }
                }
            }

            return new PostOpDeclaration(opType, null, 0, null);
        }

        public bool TryGetOperatorFromVerboseName(string verboseName, out OperatorDeclaration operatorDeclaration) =>
            Operators.VerboseNameOperatorLookup.TryGetValue(verboseName, out operatorDeclaration);

        public bool TryGetType<T>(string nameValue, out T variableType) where T : VariableType
        {
            if (TypeDict.TryGetValue(nameValue, out VariableType varType) && varType is T tType)
            {
                variableType = tType;
                return true;
            }

            variableType = null;
            return false;
        }

        public SymbolTable Clone()
        {
            var newScopeNames = new Stack<string>(ScopeNames.Count);
            var newScopes = new Stack<ASTNodeDict>(Scopes.Count);
            var newCache = new CaseInsensitiveDictionary<ASTNodeDict>(Cache.Count);
            foreach ((string key, ASTNodeDict value) in Cache)
            {
                newCache.Add(key, new ASTNodeDict(value));
            }
            foreach (string scopeName in ScopeNames.Reverse())
            {
                newScopeNames.Push(scopeName);
                newScopes.Push(Cache[scopeName]);
            }
            return new(
                       newScopeNames, 
                       newScopes, 
                       newCache,
                       new CaseInsensitiveDictionary<VariableType>(TypeDict),
                       Operators,
                       Game);
        }

        internal bool IsInfixOperator(ScriptToken currentToken, out TokenType opType)
        {
            opType = currentToken.Type;
            if (currentToken.Type is TokenType.Word)
            {
                if (currentToken.Value.CaseInsensitiveEquals("Dot"))
                {
                    opType = TokenType.DotProduct;
                    return true;
                }
                if (currentToken.Value.CaseInsensitiveEquals("Cross"))
                {
                    opType = TokenType.CrossProduct;
                    return true;
                }
                if (currentToken.Value.CaseInsensitiveEquals("ClockwiseFrom"))
                {
                    opType = TokenType.ClockwiseFrom;
                    return true;
                }

                return Operators.FriendlyNames.Contains(currentToken.Value);
            }
            return opType is
                TokenType.Assign or
                TokenType.AddAssign or
                TokenType.SubAssign or
                TokenType.MulAssign or
                TokenType.DivAssign or
                TokenType.Equals or
                TokenType.NotEquals or
                TokenType.ApproxEquals or
                TokenType.LeftArrow or
                TokenType.LessOrEquals or
                TokenType.RightArrow or
                TokenType.GreaterOrEquals or
                TokenType.Increment or
                TokenType.Decrement or
                TokenType.MinusSign or
                TokenType.PlusSign or
                TokenType.StarSign or
                TokenType.Slash or
                TokenType.Power or
                TokenType.Modulo or
                TokenType.And or
                TokenType.Or or
                TokenType.Xor or
                TokenType.DollarSign or
                TokenType.StrConcatAssign or
                TokenType.AtSign or
                TokenType.StrConcAssSpace or
                TokenType.Complement or
                TokenType.BinaryAnd or
                TokenType.BinaryOr or
                TokenType.BinaryXor or
                TokenType.RightShift or
                TokenType.LeftShift or
                TokenType.ExclamationMark or
                TokenType.TripleRightShift or
                TokenType.DotProduct or
                TokenType.CrossProduct or
                TokenType.ClockwiseFrom;
        }
    }

    public class ASTNodeDict : CaseInsensitiveDictionary<ASTNode>
    {
        public string SecondaryScope;
        public ASTNodeDict()
        {
        }

        public ASTNodeDict(ASTNodeDict dictionary) : base(dictionary)
        {
            SecondaryScope = dictionary.SecondaryScope;
        }
    }
}
