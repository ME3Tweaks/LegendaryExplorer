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
            public readonly Dictionary<TokenType, List<PreOpDeclaration>> PrefixOperators = new();
            public readonly Dictionary<TokenType, List<InOpDeclaration>> InfixOperators = new();
            public readonly Dictionary<TokenType, List<PostOpDeclaration>> PostfixOperators = new();
            public readonly List<TokenType> InFixOperatorSymbols = new();
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
        public List<TokenType> InFixOperatorSymbols => Operators.InFixOperatorSymbols;

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
                    packageType = new Class("Package", objectClass, objectClass, intrinsicClassFlags);
                    table.AddType(packageType);
                    break;
                case MEGame.ME1:
                    table.AddType(new Class("ObjectRedirector", objectClass, objectClass, intrinsicClassFlags));
                    break;
            }

            //script type intrinsics
            var fieldType = new Class("Field", objectClass, objectClass, intrinsicClassFlags | EClassFlags.Abstract);
            table.AddType(fieldType);
            table.PushScope(fieldType.Name);
                var structType = new Class(STRUCT, fieldType, objectClass, intrinsicClassFlags);
                table.AddType(structType);
                table.PushScope(structType.Name);
                    var stateType = new Class(STATE, structType, objectClass, intrinsicClassFlags);
                    table.AddType(stateType);
                    table.PushScope(stateType.Name);
                        var classType = new Class(CLASS, stateType, packageType, intrinsicClassFlags);
                        table.AddType(classType);
                        table.PushScope(classType.Name); table.PopScope();
                    table.PopScope();
                    var scriptStructType = new Class("ScriptStruct", structType, objectClass, intrinsicClassFlags);
                    table.AddType(scriptStructType);
                    table.PushScope(scriptStructType.Name); table.PopScope();
                    var functionType = new Class(FUNCTION, structType, stateType, intrinsicClassFlags);
                    table.AddType(functionType);
                    table.PushScope(functionType.Name); table.PopScope();
                table.PopScope();
                var enumType = new Class(ENUM, fieldType, structType, intrinsicClassFlags);
                table.AddType(enumType);
                table.PushScope(enumType.Name); table.PopScope();
                var constType = new Class(CONST, fieldType, structType, intrinsicClassFlags);
                table.AddType(constType);
                table.PushScope(enumType.Name); table.PopScope();

                //property intrinsics
                var propertyType = new Class("Property", fieldType, objectClass, intrinsicClassFlags);
                table.AddType(propertyType);
                table.PushScope(propertyType.Name);
                    var bytePropertyType = new Class("ByteProperty", propertyType, objectClass, intrinsicClassFlags);
                    table.AddType(bytePropertyType);
                    table.PushScope(bytePropertyType.Name); table.PopScope();
                    var intPropertyType = new Class("IntProperty", propertyType, objectClass, intrinsicClassFlags);
                    table.AddType(intPropertyType);
                    table.PushScope(intPropertyType.Name); table.PopScope();
                    var boolPropertyType = new Class("BoolProperty", propertyType, objectClass, intrinsicClassFlags);
                    table.AddType(boolPropertyType);
                    table.PushScope(boolPropertyType.Name); table.PopScope();
                    var floatPropertyType = new Class("FloatProperty", propertyType, objectClass, intrinsicClassFlags);
                    table.AddType(floatPropertyType);
                    table.PushScope(floatPropertyType.Name); table.PopScope();
                    var objectPropertyType = new Class("ObjectProperty", propertyType, objectClass, intrinsicClassFlags);
                    table.AddType(objectPropertyType);
                    table.PushScope(objectPropertyType.Name); table.PopScope();
                    var componentPropertyType = new Class("ComponentProperty", propertyType, objectClass, intrinsicClassFlags);
                    table.AddType(componentPropertyType);
                    table.PushScope(componentPropertyType.Name); table.PopScope();
                    var classPropertyType = new Class("ClassProperty", propertyType, objectClass, intrinsicClassFlags);
                    table.AddType(classPropertyType);
                    table.PushScope(classPropertyType.Name); table.PopScope();
                    var interfacePropertyType = new Class("InterfaceProperty", propertyType, objectClass, intrinsicClassFlags);
                    table.AddType(interfacePropertyType);
                    table.PushScope(interfacePropertyType.Name); table.PopScope();
                    var namePropertyType = new Class("NameProperty", propertyType, objectClass, intrinsicClassFlags);
                    table.AddType(namePropertyType);
                    table.PushScope(namePropertyType.Name); table.PopScope();
                    var strPropertyType = new Class("StrProperty", propertyType, objectClass, intrinsicClassFlags);
                    table.AddType(strPropertyType);
                    table.PushScope(strPropertyType.Name); table.PopScope();
                    var arrayPropertyType = new Class("ArrayProperty", propertyType, objectClass, intrinsicClassFlags);
                    table.AddType(arrayPropertyType);
                    table.PushScope(arrayPropertyType.Name); table.PopScope();
                    var mapPropertyType = new Class("MapProperty", propertyType, objectClass, intrinsicClassFlags);
                    table.AddType(mapPropertyType);
                    table.PushScope(mapPropertyType.Name); table.PopScope();
                    var structPropertyType = new Class("StructProperty", propertyType, objectClass, intrinsicClassFlags);
                    table.AddType(structPropertyType);
                    table.PushScope(structPropertyType.Name); table.PopScope();
                    var delegatePropertyType = new Class("DelegateProperty", propertyType, objectClass, intrinsicClassFlags);
                    table.AddType(delegatePropertyType);
                    table.PushScope(delegatePropertyType.Name); table.PopScope();
                    var stringRefPropertyType = new Class("StringRefProperty", propertyType, objectClass, intrinsicClassFlags);
                    table.AddType(stringRefPropertyType);
                    table.PushScope(stringRefPropertyType.Name); table.PopScope();
                table.PopScope();
            table.PopScope();
            #endregion

            #region ENGINE 
            //TODO: these classes have members accessed from script that need to be added here

            var clientType = new Class("Client", objectClass, objectClass, intrinsicClassFlags | EClassFlags.Abstract | EClassFlags.Config)
            {
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
            table.PushScope(clientType.Name);
                foreach (VariableDeclaration clientClassVarDecl in clientType.VariableDeclarations)
                {
                    table.AddSymbol(clientClassVarDecl.Name, clientClassVarDecl);
                }
            table.PopScope();
            var staticMeshType = new Class("StaticMesh", objectClass, objectClass, intrinsicClassFlags | EClassFlags.SafeReplace | EClassFlags.CollapseCategories)
            {
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
                foreach (VariableDeclaration stmVarDecl in staticMeshType.VariableDeclarations)
                {
                    table.AddSymbol(stmVarDecl.Name, stmVarDecl);
                }
                var fracturedStaticMeshType = new Class("FracturedStaticMesh", staticMeshType, objectClass, intrinsicClassFlags | EClassFlags.SafeReplace | EClassFlags.CollapseCategories)
                {
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
                table.PushScope(fracturedStaticMeshType.Name); 
                    foreach (VariableDeclaration fracturedStmVarDecl in fracturedStaticMeshType.VariableDeclarations)
                    {
                        table.AddSymbol(fracturedStmVarDecl.Name, fracturedStmVarDecl);
                    }
                table.PopScope();
            table.PopScope();
            var shadowMap1DType = new Class("ShadowMap1D", objectClass, objectClass, intrinsicClassFlags);
            table.AddType(shadowMap1DType);
            table.PushScope(shadowMap1DType.Name); table.PopScope();
            var levelBase = new Class("LevelBase", objectClass, objectClass, intrinsicClassFlags | EClassFlags.Abstract);
            table.AddType(levelBase);
            table.PushScope(levelBase.Name);
                var levelType = new Class("Level", levelBase, objectClass, intrinsicClassFlags)
                {
                    VariableDeclarations =
                    {
                        new VariableDeclaration(FloatType, default, "LightmapTotalSize"),
                        new VariableDeclaration(FloatType, default, "ShadowmapTotalSize"),
                    }
                };
                table.AddType(levelType);
                table.PushScope(levelType.Name); 
                    foreach (VariableDeclaration levelVarDecl in levelType.VariableDeclarations)
                    {
                        table.AddSymbol(levelVarDecl.Name, levelVarDecl);
                    }
                table.PopScope();
                var pendingLevel = new Class("PendingLevel", levelBase, objectClass, intrinsicClassFlags | EClassFlags.Abstract);
                table.AddType(pendingLevel);
                table.PushScope(pendingLevel.Name); table.PopScope();
            table.PopScope();
            var modelType = new Class("Model", objectClass, objectClass, intrinsicClassFlags);
            table.AddType(modelType);
            table.PushScope(modelType.Name); table.PopScope();
            var worldType = new Class("World", objectClass, objectClass, intrinsicClassFlags);
            table.AddType(worldType);
            table.PushScope(worldType.Name); table.PopScope();
            var polysType = new Class("Polys", objectClass, objectClass, intrinsicClassFlags);
            table.AddType(polysType);
            table.PushScope(polysType.Name); table.PopScope();
            table.AddType(new Class("ShaderCache", objectClass, objectClass, intrinsicClassFlags));
            //NetConnection, ChildConnection, LightMapTexture2D, and CodecMovieBink are also intrinsic, but are added in the AddType function because they subclass the non-instrinsic class 'Player'
            #endregion

            return table;
        }

        private bool me3le3operatorsInitialized;
        //must be called AFTER Core.pcc has been parsed and validated, and BEFORE parsing any CodeBody!
        public void InitializeME3LE3Operators()
        {
            if (me3le3operatorsInitialized)
            {
                return;
            }
            //non-primitive types that have operators defined for
            var objectType = TypeDict["Object"];
            var interfaceType = TypeDict["Interface"];
            var vectorType = TypeDict["Vector"];
            var rotatorType = TypeDict["Rotator"];
            var quatType = TypeDict["Quat"];
            var matrixType = TypeDict["Matrix"];
            var vector2DType = TypeDict["Vector2D"];
            var colorType = TypeDict["Color"];
            var linearColorType = TypeDict["LinearColor"];

            const EPropertyFlags parm = EPropertyFlags.Parm;
            const EPropertyFlags outFlags = parm | EPropertyFlags.OutParm;
            const EPropertyFlags skip = parm | EPropertyFlags.SkipParm;
            const EPropertyFlags coerce = parm | EPropertyFlags.CoerceParm;

            ASTNodeDict objectScope = Scopes.Last();

            //primitive PostOperators
            AddOperator(new PostOpDeclaration(TokenType.Increment, ByteType, 139, new FunctionParameter(ByteType, outFlags, "A")));
            AddOperator(new PostOpDeclaration(TokenType.Decrement, ByteType, 140, new FunctionParameter(ByteType, outFlags, "A")));

            AddOperator(new PostOpDeclaration(TokenType.Increment, IntType, 165, new FunctionParameter(IntType, outFlags, "A")));
            AddOperator(new PostOpDeclaration(TokenType.Decrement, IntType, 166, new FunctionParameter(IntType, outFlags, "A")));

            //primitive PreOperators
            AddOperator(new PreOpDeclaration(TokenType.ExclamationMark, BoolType, 129, new FunctionParameter(BoolType, parm, "A")));

            AddOperator(new PreOpDeclaration(TokenType.Increment, ByteType, 137, new FunctionParameter(ByteType, outFlags, "A")));
            AddOperator(new PreOpDeclaration(TokenType.Decrement, ByteType, 138, new FunctionParameter(ByteType, outFlags, "A")));

            AddOperator(new PreOpDeclaration(TokenType.Complement, IntType, 141, new FunctionParameter(IntType, parm, "A")));
            AddOperator(new PreOpDeclaration(TokenType.MinusSign, IntType, 143, new FunctionParameter(IntType, parm, "A")));
            AddOperator(new PreOpDeclaration(TokenType.Increment, IntType, 163, new FunctionParameter(IntType, outFlags, "A")));
            AddOperator(new PreOpDeclaration(TokenType.Decrement, IntType, 164, new FunctionParameter(IntType, outFlags, "A")));

            AddOperator(new PreOpDeclaration(TokenType.MinusSign, FloatType, 169, new FunctionParameter(FloatType, parm, "A")));

            AddOperator(new PreOpDeclaration(TokenType.MinusSign, vectorType, 211, new FunctionParameter(vectorType, parm, "A")));

            //primitive InfixOperators
            AddOperator(new InOpDeclaration(TokenType.Equals, 24, 242, BoolType, new FunctionParameter(BoolType, parm, "A"), new FunctionParameter(BoolType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.NotEquals, 26, 243, BoolType, new FunctionParameter(BoolType, parm, "A"), new FunctionParameter(BoolType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.And, 30, 130, BoolType, new FunctionParameter(BoolType, parm, "A"), new FunctionParameter(BoolType, skip, "B")));
            AddOperator(new InOpDeclaration(TokenType.Xor, 30, 131, BoolType, new FunctionParameter(BoolType, parm, "A"), new FunctionParameter(BoolType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.Or, 32, 132, BoolType, new FunctionParameter(BoolType, parm, "A"), new FunctionParameter(BoolType, skip, "B")));

            AddOperator(new InOpDeclaration(TokenType.MulAssign, 34, 133, ByteType, new FunctionParameter(ByteType, outFlags, "A"), new FunctionParameter(ByteType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.MulAssign, 34, 198, ByteType, new FunctionParameter(ByteType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.DivAssign, 34, 134, ByteType, new FunctionParameter(ByteType, outFlags, "A"), new FunctionParameter(ByteType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.AddAssign, 34, 135, ByteType, new FunctionParameter(ByteType, outFlags, "A"), new FunctionParameter(ByteType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.SubAssign, 34, 136, ByteType, new FunctionParameter(ByteType, outFlags, "A"), new FunctionParameter(ByteType, parm, "B")));

            AddOperator(new InOpDeclaration(TokenType.StarSign, 16, 144, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.Slash, 16, 145, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.Modulo, 18, 253, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.PlusSign, 20, 146, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.MinusSign, 20, 147, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.LeftShift, 22, 148, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.RightShift, 22, 149, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.VectorTransform, 22, 196, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.LeftArrow, 24, 150, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.RightArrow, 24, 151, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.LessOrEquals, 24, 152, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.GreaterOrEquals, 24, 153, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.Equals, 24, 154, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.NotEquals, 26, 155, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.BinaryAnd, 28, 156, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.BinaryXor, 28, 157, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.BinaryOr, 28, 158, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.MulAssign, 34, 159, IntType, new FunctionParameter(IntType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.DivAssign, 34, 160, IntType, new FunctionParameter(IntType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.AddAssign, 34, 161, IntType, new FunctionParameter(IntType, outFlags, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.SubAssign, 34, 162, IntType, new FunctionParameter(IntType, outFlags, "A"), new FunctionParameter(IntType, parm, "B")));

            AddOperator(new InOpDeclaration(TokenType.Power, 12, 170, FloatType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.StarSign, 16, 171, FloatType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.Slash, 16, 172, FloatType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.Modulo, 18, 173, FloatType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.PlusSign, 20, 174, FloatType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.MinusSign, 20, 175, FloatType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.LeftArrow, 24, 176, BoolType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.RightArrow, 24, 177, BoolType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.LessOrEquals, 24, 178, BoolType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.GreaterOrEquals, 24, 179, BoolType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.Equals, 24, 180, BoolType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.ApproxEquals, 24, 210, BoolType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.NotEquals, 26, 181, BoolType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.MulAssign, 34, 182, FloatType, new FunctionParameter(FloatType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.DivAssign, 34, 183, FloatType, new FunctionParameter(FloatType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.AddAssign, 34, 184, FloatType, new FunctionParameter(FloatType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.SubAssign, 34, 185, FloatType, new FunctionParameter(FloatType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));

            AddOperator(new InOpDeclaration(TokenType.DollarSign, 40, 600, StringType, new FunctionParameter(StringType, coerce, "A"), new FunctionParameter(StringType, coerce, "B"))
            {
                Implementer = (Function)objectScope["Concat_StrStr"]
            });
            AddOperator(new InOpDeclaration(TokenType.AtSign, 40, 168, StringType, new FunctionParameter(StringType, coerce, "A"), new FunctionParameter(StringType, coerce, "B"))
            {
                Implementer = (Function)objectScope["At_StrStr"]
            });
            AddOperator(new InOpDeclaration(TokenType.LeftArrow, 24, 601, BoolType, new FunctionParameter(StringType, parm, "A"), new FunctionParameter(StringType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.RightArrow, 24, 602, BoolType, new FunctionParameter(StringType, parm, "A"), new FunctionParameter(StringType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.LessOrEquals, 24, 603, BoolType, new FunctionParameter(StringType, parm, "A"), new FunctionParameter(StringType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.GreaterOrEquals, 24, 604, BoolType, new FunctionParameter(StringType, parm, "A"), new FunctionParameter(StringType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.Equals, 24, 605, BoolType, new FunctionParameter(StringType, parm, "A"), new FunctionParameter(StringType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.NotEquals, 26, 606, BoolType, new FunctionParameter(StringType, parm, "A"), new FunctionParameter(StringType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.ApproxEquals, 24, 607, BoolType, new FunctionParameter(StringType, parm, "A"), new FunctionParameter(StringType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.StrConcatAssign, 44, 322, StringType, new FunctionParameter(StringType, outFlags, "A"), new FunctionParameter(StringType, coerce, "B"))
            {
                Implementer = (Function)objectScope["ConcatEqual_StrStr"]
            });
            AddOperator(new InOpDeclaration(TokenType.StrConcAssSpace, 44, 323, StringType, new FunctionParameter(StringType, outFlags, "A"), new FunctionParameter(StringType, coerce, "B"))
            {
                Implementer = (Function)objectScope["AtEqual_StrStr"]
            });
            AddOperator(new InOpDeclaration(TokenType.SubAssign, 45, 324, StringType, new FunctionParameter(StringType, outFlags, "A"), new FunctionParameter(StringType, coerce, "B"))
            {
                Implementer = (Function)objectScope["SubtractEqual_StrStr"]
            });

            AddOperator(new InOpDeclaration(TokenType.Equals, 24, 640, BoolType, new FunctionParameter(objectType, parm, "A"), new FunctionParameter(objectType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.NotEquals, 26, 641, BoolType, new FunctionParameter(objectType, parm, "A"), new FunctionParameter(objectType, parm, "B")));

            AddOperator(new InOpDeclaration(TokenType.Equals, 24, 254, BoolType, new FunctionParameter(NameType, parm, "A"), new FunctionParameter(NameType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.NotEquals, 26, 255, BoolType, new FunctionParameter(NameType, parm, "A"), new FunctionParameter(NameType, parm, "B")));

            AddOperator(new InOpDeclaration(TokenType.Equals, 24, 1000, BoolType, new FunctionParameter(StringRefType, parm, "A"), new FunctionParameter(StringRefType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.Equals, 24, 1001, BoolType, new FunctionParameter(StringRefType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.Equals, 24, 1002, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(StringRefType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.NotEquals, 26, 1003, BoolType, new FunctionParameter(StringRefType, parm, "A"), new FunctionParameter(StringRefType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.NotEquals, 26, 1004, BoolType, new FunctionParameter(StringRefType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.NotEquals, 26, 1005, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(StringRefType, parm, "B")));

            AddOperator(new InOpDeclaration(TokenType.StarSign, 16, 212, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.StarSign, 16, 213, vectorType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.StarSign, 16, 296, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.Slash, 16, 214, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.PlusSign, 20, 215, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.MinusSign, 20, 216, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.LeftShift, 22, 275, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.RightShift, 22, 276, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.Equals, 24, 217, BoolType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.NotEquals, 26, 218, BoolType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.DotProduct, 16, 219, FloatType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.CrossProduct, 16, 220, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.MulAssign, 34, 221, vectorType, new FunctionParameter(vectorType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.MulAssign, 34, 297, vectorType, new FunctionParameter(vectorType, outFlags, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.DivAssign, 34, 222, vectorType, new FunctionParameter(vectorType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.AddAssign, 34, 223, vectorType, new FunctionParameter(vectorType, outFlags, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.SubAssign, 34, 224, vectorType, new FunctionParameter(vectorType, outFlags, "A"), new FunctionParameter(vectorType, parm, "B")));

            AddOperator(new InOpDeclaration(TokenType.Equals, 24, 142, BoolType, new FunctionParameter(rotatorType, parm, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.NotEquals, 26, 203, BoolType, new FunctionParameter(rotatorType, parm, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.StarSign, 16, 287, rotatorType, new FunctionParameter(rotatorType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.StarSign, 16, 288, rotatorType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.Slash, 16, 289, rotatorType, new FunctionParameter(rotatorType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.MulAssign, 34, 290, rotatorType, new FunctionParameter(rotatorType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.DivAssign, 34, 291, rotatorType, new FunctionParameter(rotatorType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.PlusSign, 20, 316, rotatorType, new FunctionParameter(rotatorType, parm, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.MinusSign, 20, 317, rotatorType, new FunctionParameter(rotatorType, parm, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.AddAssign, 34, 318, rotatorType, new FunctionParameter(rotatorType, outFlags, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.SubAssign, 34, 319, rotatorType, new FunctionParameter(rotatorType, outFlags, "A"), new FunctionParameter(rotatorType, parm, "B")));

            AddOperator(new InOpDeclaration(TokenType.PlusSign, 16, 270, quatType, new FunctionParameter(quatType, parm, "A"), new FunctionParameter(quatType, parm, "B")));
            AddOperator(new InOpDeclaration(TokenType.MinusSign, 16, 271, quatType, new FunctionParameter(quatType, parm, "A"), new FunctionParameter(quatType, parm, "B")));

            //operators without a nativeIndex. must be linked directly to their function representations
            AddOperator(new InOpDeclaration(TokenType.ClockwiseFrom, 24, 0, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B"))
            {
                Implementer = (Function)objectScope["ClockwiseFrom_IntInt"]
            });

            AddOperator(new InOpDeclaration(TokenType.Equals, 24, 0, BoolType, new FunctionParameter(interfaceType, parm, "A"), new FunctionParameter(interfaceType, parm, "B"))
            {
                Implementer = (Function)objectScope["EqualEqual_InterfaceInterface"]
            });
            AddOperator(new InOpDeclaration(TokenType.NotEquals, 26, 0, BoolType, new FunctionParameter(interfaceType, parm, "A"), new FunctionParameter(interfaceType, parm, "B"))
            {
                Implementer = (Function)objectScope["NotEqual_InterfaceInterface"]
            });

            AddOperator(new InOpDeclaration(TokenType.StarSign, 34, 0, matrixType, new FunctionParameter(matrixType, parm, "A"), new FunctionParameter(matrixType, parm, "B"))
            {
                Implementer = (Function)objectScope["Multiply_MatrixMatrix"]
            });

            AddOperator(new InOpDeclaration(TokenType.PlusSign, 16, 0, vector2DType, new FunctionParameter(vector2DType, parm, "A"), new FunctionParameter(vector2DType, parm, "B"))
            {
                Implementer = (Function)objectScope["Add_Vector2DVector2D"]
            });
            AddOperator(new InOpDeclaration(TokenType.MinusSign, 16, 0, vector2DType, new FunctionParameter(vector2DType, parm, "A"), new FunctionParameter(vector2DType, parm, "B"))
            {
                Implementer = (Function)objectScope["Subtract_Vector2DVector2D"]
            });

            AddOperator(new InOpDeclaration(TokenType.MinusSign, 20, 0, colorType, new FunctionParameter(colorType, parm, "A"), new FunctionParameter(colorType, parm, "B"))
            {
                Implementer = (Function)objectScope["Subtract_ColorColor"]
            });
            AddOperator(new InOpDeclaration(TokenType.StarSign, 16, 0, colorType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(colorType, parm, "B"))
            {
                Implementer = (Function)objectScope["Multiply_FloatColor"]
            });
            AddOperator(new InOpDeclaration(TokenType.StarSign, 16, 0, colorType, new FunctionParameter(colorType, parm, "A"), new FunctionParameter(FloatType, parm, "B"))
            {
                Implementer = (Function)objectScope["Multiply_ColorFloat"]
            });
            AddOperator(new InOpDeclaration(TokenType.PlusSign, 20, 0, colorType, new FunctionParameter(colorType, parm, "A"), new FunctionParameter(colorType, parm, "B"))
            {
                Implementer = (Function)objectScope["Add_ColorColor"]
            });

            AddOperator(new InOpDeclaration(TokenType.MinusSign, 20, 0, linearColorType, new FunctionParameter(linearColorType, parm, "A"), new FunctionParameter(linearColorType, parm, "B"))
            {
                Implementer = (Function)objectScope["Subtract_LinearColorLinearColor"]
            });
            AddOperator(new InOpDeclaration(TokenType.StarSign, 16, 0, linearColorType, new FunctionParameter(linearColorType, parm, "A"), new FunctionParameter(FloatType, parm, "B"))
            {
                Implementer = (Function)objectScope["Multiply_LinearColorFloat"]
            });

            Operators.InFixOperatorSymbols.AddRange(Operators.InfixOperators.Keys);
            me3le3operatorsInitialized = true;
        }

        private readonly List<Class> intrinsicClasses = new();

        public void ValidateIntrinsics()
        {
            foreach (var validationPass in Enums.GetValues<ValidationPass>())
            {
                foreach (Class cls in intrinsicClasses)
                {
                    cls.AcceptVisitor(new ClassValidationVisitor(null, this, validationPass));
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
                    if (functionName.Contains("."))
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
                    var netConType = new Class("NetConnection", node, objClass, EClassFlags.Intrinsic | EClassFlags.Abstract | EClassFlags.Transient | EClassFlags.Config) { ConfigName = "Engine" };
                    AddType(netConType);
                    PushScope(netConType.Name);
                        var childConType = new Class("ChildConnection", netConType, objClass, EClassFlags.Intrinsic | EClassFlags.Transient | EClassFlags.Config, vars: new List<VariableDeclaration>
                        {
                            new(netConType, default, "Parent")
                        }) { ConfigName = "Engine" };
                        AddType(childConType);
                        PushScope(childConType.Name);
                            AddSymbol("Parent", childConType.VariableDeclarations[0]);
                        PopScope();
                        netConType.VariableDeclarations.Add(new VariableDeclaration(new DynamicArrayType(childConType), default, "Children"));
                        AddSymbol("Children", netConType.VariableDeclarations[0]);
                    PopScope();
                    break;
                }
                case "Texture2D":
                {
                    var objClass = TypeDict[OBJECT];
                    var lightmapTexture2DType = new Class("LightMapTexture2D", node, objClass, EClassFlags.Intrinsic | EClassFlags.Config) { ConfigName = "Engine" };
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
                    var codecBinkType = new Class("CodecMovieBink", node, TypeDict[OBJECT], EClassFlags.Intrinsic | EClassFlags.Transient);
                    AddType(codecBinkType);
                    PushScope(codecBinkType.Name); PopScope();
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

        public void AddOperator(OperatorDeclaration op)
        {
            switch (op)
            {
                case PreOpDeclaration preOpDeclaration:
                    Operators.PrefixOperators.AddToListAt(preOpDeclaration.OperatorType, preOpDeclaration);
                    break;
                case InOpDeclaration inOpDeclaration:
                    Operators.InfixOperators.AddToListAt(inOpDeclaration.OperatorType, inOpDeclaration);
                    break;
                case PostOpDeclaration postOpDeclaration:
                    Operators.PostfixOperators.AddToListAt(postOpDeclaration.OperatorType, postOpDeclaration);
                    break;
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

        public IEnumerable<InOpDeclaration> GetInfixOperators(TokenType opType)
        {
            if (Operators.InfixOperators.TryGetValue(opType, out List<InOpDeclaration> operators))
            {
                foreach (InOpDeclaration inOpDeclaration in operators)
                {
                    yield return inOpDeclaration;
                }
            }
        }

        public InOpDeclaration GetInOp(TokenType opType, VariableType lhs, VariableType rhs)
        {
            foreach (var inOpDeclaration in GetInfixOperators(opType))
            {
                //TODO: do smarter comparison. This only finds exact matches. needs to find best fit given possible implicit conversions
                if (inOpDeclaration.LeftOperand.VarType == lhs && inOpDeclaration.RightOperand.VarType == rhs)
                {
                    return inOpDeclaration;
                }
            }
            return null;
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
