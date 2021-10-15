using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Language.Util;
using LegendaryExplorerCore.UnrealScript.Utilities;
using static LegendaryExplorerCore.Unreal.UnrealFlags;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript.Analysis.Symbols
{

    public class SymbolTable
    {
        private class OperatorDefinitions
        {
            public readonly CaseInsensitiveDictionary<List<PreOpDeclaration>> PrefixOperators = new();
            public readonly CaseInsensitiveDictionary<List<InOpDeclaration>> InfixOperators = new();
            public readonly CaseInsensitiveDictionary<List<PostOpDeclaration>> PostfixOperators = new();
            public readonly List<string> InFixOperatorSymbols = new();
        }

        #region Primitives

        public static readonly VariableType IntType = new(INT) { PropertyType = EPropertyType.Int};
        public static readonly VariableType FloatType = new(FLOAT) { PropertyType = EPropertyType.Float };
        public static readonly VariableType BoolType = new(BOOL) { PropertyType = EPropertyType.Bool };
        public static readonly VariableType ByteType = new(BYTE) { PropertyType = EPropertyType.Byte };
        public static readonly VariableType BioMask4Type = new(BIOMASK4) { PropertyType = EPropertyType.Byte };
        public static readonly VariableType StringType = new(STRING) { PropertyType = EPropertyType.String };
        public static readonly VariableType StringRefType = new(STRINGREF) { PropertyType = EPropertyType.StringRef };
        public static readonly VariableType NameType = new(NAME) { PropertyType = EPropertyType.Name };

        public static bool IsPrimitive(VariableType vt) =>
            vt.PropertyType is EPropertyType.Int or EPropertyType.Float or EPropertyType.Bool or EPropertyType.Byte or EPropertyType.String or EPropertyType.StringRef or EPropertyType.Name;
        #endregion

        private readonly CaseInsensitiveDictionary<ASTNodeDict> Cache;
        private readonly LinkedList<ASTNodeDict> Scopes;
        private readonly LinkedList<string> ScopeNames;
        private readonly CaseInsensitiveDictionary<VariableType> Types;

        public string CurrentScopeName => ScopeNames.Count == 0 ? "" : ScopeNames.Last();

        private readonly OperatorDefinitions Operators;
        public List<string> InFixOperatorSymbols => Operators.InFixOperatorSymbols;

        public readonly MEGame Game;

        private SymbolTable(MEGame game)
        {
            Operators = new OperatorDefinitions();
            ScopeNames = new LinkedList<string>();
            Scopes = new LinkedList<ASTNodeDict>();
            Cache = new CaseInsensitiveDictionary<ASTNodeDict>();
            Types = new CaseInsensitiveDictionary<VariableType>();
            Game = game;
        }

        private SymbolTable(LinkedList<string> scopeNames, LinkedList<ASTNodeDict> scopes, CaseInsensitiveDictionary<ASTNodeDict> cache, CaseInsensitiveDictionary<VariableType> types, OperatorDefinitions ops, MEGame game)
        {
            Operators = ops;
            ScopeNames = scopeNames;
            Scopes = scopes;
            Cache = cache;
            Types = types;
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

            
            Class packageType = null;
            if (game >= MEGame.ME3)
            {
                packageType = new Class("Package", objectClass, objectClass, intrinsicClassFlags);
                table.AddType(packageType);
            }

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
            var propertyType = new Class("Property", fieldType, objectClass, intrinsicClassFlags);
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
            var staticMeshType = new Class("StaticMesh", objectClass, objectClass, intrinsicClassFlags | EClassFlags.SafeReplace | EClassFlags.CollapseCategories);
            table.AddType(staticMeshType);
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

            //NetConnection and ChildConnection are also intrinsic, but are added in the AddType function because they subclass the non-instrinsic class 'Player'
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
            var objectType = Types["Object"];
            var interfaceType = Types["Interface"];
            var vectorType = Types["Vector"];
            var rotatorType = Types["Rotator"];
            var quatType = Types["Quat"];
            var matrixType = Types["Matrix"];
            var vector2DType = Types["Vector2D"];
            var colorType = Types["Color"];
            var linearColorType = Types["LinearColor"];


            const EPropertyFlags parm = EPropertyFlags.Parm;
            const EPropertyFlags outFlags = parm | EPropertyFlags.OutParm;
            const EPropertyFlags skip = parm | EPropertyFlags.SkipParm;
            const EPropertyFlags coerce = parm | EPropertyFlags.CoerceParm;

            ASTNodeDict objectScope = Scopes.First.Value;

            //primitive PostOperators
            AddOperator(new PostOpDeclaration("++", ByteType, 139, new FunctionParameter(ByteType, outFlags, "A")));
            AddOperator(new PostOpDeclaration("--", ByteType, 140, new FunctionParameter(ByteType, outFlags, "A")));

            AddOperator(new PostOpDeclaration("++", IntType, 165, new FunctionParameter(IntType, outFlags, "A")));
            AddOperator(new PostOpDeclaration("--", IntType, 166, new FunctionParameter(IntType, outFlags, "A")));

            //primitive PreOperators
            AddOperator(new PreOpDeclaration("!", BoolType, 129, new FunctionParameter(BoolType, parm, "A")));

            AddOperator(new PreOpDeclaration("++", ByteType, 137, new FunctionParameter(ByteType, outFlags, "A")));
            AddOperator(new PreOpDeclaration("--", ByteType, 138, new FunctionParameter(ByteType, outFlags, "A")));

            AddOperator(new PreOpDeclaration("~", IntType, 141, new FunctionParameter(IntType, parm, "A")));
            AddOperator(new PreOpDeclaration("-", IntType, 143, new FunctionParameter(IntType, parm, "A")));
            AddOperator(new PreOpDeclaration("++", IntType, 163, new FunctionParameter(IntType, outFlags, "A")));
            AddOperator(new PreOpDeclaration("--", IntType, 164, new FunctionParameter(IntType, outFlags, "A")));

            AddOperator(new PreOpDeclaration("-", FloatType, 169, new FunctionParameter(FloatType, parm, "A")));

            AddOperator(new PreOpDeclaration("-", vectorType, 211, new FunctionParameter(vectorType, parm, "A")));

            //primitive InfixOperators
            AddOperator(new InOpDeclaration("==", 24, 242, BoolType, new FunctionParameter(BoolType, parm, "A"), new FunctionParameter(BoolType, parm, "B")));
            AddOperator(new InOpDeclaration("!=", 26, 243, BoolType, new FunctionParameter(BoolType, parm, "A"), new FunctionParameter(BoolType, parm, "B")));
            AddOperator(new InOpDeclaration("&&", 30, 130, BoolType, new FunctionParameter(BoolType, parm, "A"), new FunctionParameter(BoolType, skip, "B")));
            AddOperator(new InOpDeclaration("^^", 30, 131, BoolType, new FunctionParameter(BoolType, parm, "A"), new FunctionParameter(BoolType, parm, "B")));
            AddOperator(new InOpDeclaration("||", 32, 132, BoolType, new FunctionParameter(BoolType, parm, "A"), new FunctionParameter(BoolType, skip, "B")));

            AddOperator(new InOpDeclaration("*=", 34, 133, ByteType, new FunctionParameter(ByteType, outFlags, "A"), new FunctionParameter(ByteType, parm, "B")));
            AddOperator(new InOpDeclaration("*=", 34, 198, ByteType, new FunctionParameter(ByteType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("/=", 34, 134, ByteType, new FunctionParameter(ByteType, outFlags, "A"), new FunctionParameter(ByteType, parm, "B")));
            AddOperator(new InOpDeclaration("+=", 34, 135, ByteType, new FunctionParameter(ByteType, outFlags, "A"), new FunctionParameter(ByteType, parm, "B")));
            AddOperator(new InOpDeclaration("-=", 34, 136, ByteType, new FunctionParameter(ByteType, outFlags, "A"), new FunctionParameter(ByteType, parm, "B")));

            AddOperator(new InOpDeclaration("*", 16, 144, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("/", 16, 145, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("%", 18, 253, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("+", 20, 146, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("-", 20, 147, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("<<", 22, 148, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(">>", 22, 149, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(">>>", 22, 196, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("<", 24, 150, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(">", 24, 151, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("<=", 24, 152, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration(">=", 24, 153, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("==", 24, 154, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("!=", 26, 155, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("&", 28, 156, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("^", 28, 157, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("|", 28, 158, IntType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("*=", 34, 159, IntType, new FunctionParameter(IntType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("/=", 34, 160, IntType, new FunctionParameter(IntType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("+=", 34, 161, IntType, new FunctionParameter(IntType, outFlags, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("-=", 34, 162, IntType, new FunctionParameter(IntType, outFlags, "A"), new FunctionParameter(IntType, parm, "B")));


            AddOperator(new InOpDeclaration("**", 12, 170, FloatType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("*", 16, 171, FloatType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("/", 16, 172, FloatType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("%", 18, 173, FloatType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("+", 20, 174, FloatType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("-", 20, 175, FloatType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("<", 24, 176, BoolType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(">", 24, 177, BoolType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("<=", 24, 178, BoolType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration(">=", 24, 179, BoolType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("==", 24, 180, BoolType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("~=", 24, 210, BoolType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("!=", 26, 181, BoolType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("*=", 34, 182, FloatType, new FunctionParameter(FloatType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("/=", 34, 183, FloatType, new FunctionParameter(FloatType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("+=", 34, 184, FloatType, new FunctionParameter(FloatType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("-=", 34, 185, FloatType, new FunctionParameter(FloatType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));

            AddOperator(new InOpDeclaration("$", 40, 600, StringType, new FunctionParameter(StringType, coerce, "A"), new FunctionParameter(StringType, coerce, "B"))
            {
                Implementer = (Function)objectScope["Concat_StrStr"]
            });
            AddOperator(new InOpDeclaration("@", 40, 168, StringType, new FunctionParameter(StringType, coerce, "A"), new FunctionParameter(StringType, coerce, "B"))
            {
                Implementer = (Function)objectScope["At_StrStr"]
            });
            AddOperator(new InOpDeclaration("<", 24, 601, BoolType, new FunctionParameter(StringType, parm, "A"), new FunctionParameter(StringType, parm, "B")));
            AddOperator(new InOpDeclaration(">", 24, 602, BoolType, new FunctionParameter(StringType, parm, "A"), new FunctionParameter(StringType, parm, "B")));
            AddOperator(new InOpDeclaration("<=", 24, 603, BoolType, new FunctionParameter(StringType, parm, "A"), new FunctionParameter(StringType, parm, "B")));
            AddOperator(new InOpDeclaration(">=", 24, 604, BoolType, new FunctionParameter(StringType, parm, "A"), new FunctionParameter(StringType, parm, "B")));
            AddOperator(new InOpDeclaration("==", 24, 605, BoolType, new FunctionParameter(StringType, parm, "A"), new FunctionParameter(StringType, parm, "B")));
            AddOperator(new InOpDeclaration("!=", 26, 606, BoolType, new FunctionParameter(StringType, parm, "A"), new FunctionParameter(StringType, parm, "B")));
            AddOperator(new InOpDeclaration("~=", 24, 607, BoolType, new FunctionParameter(StringType, parm, "A"), new FunctionParameter(StringType, parm, "B")));
            AddOperator(new InOpDeclaration("$=", 44, 322, StringType, new FunctionParameter(StringType, outFlags, "A"), new FunctionParameter(StringType, coerce, "B"))
            {
                Implementer = (Function)objectScope["ConcatEqual_StrStr"]
            });
            AddOperator(new InOpDeclaration("@=", 44, 323, StringType, new FunctionParameter(StringType, outFlags, "A"), new FunctionParameter(StringType, coerce, "B"))
            {
                Implementer = (Function)objectScope["AtEqual_StrStr"]
            });
            AddOperator(new InOpDeclaration("-=", 45, 324, StringType, new FunctionParameter(StringType, outFlags, "A"), new FunctionParameter(StringType, coerce, "B"))
            {
                Implementer = (Function)objectScope["SubtractEqual_StrStr"]
            });

            AddOperator(new InOpDeclaration("==", 24, 640, BoolType, new FunctionParameter(objectType, parm, "A"), new FunctionParameter(objectType, parm, "B")));
            AddOperator(new InOpDeclaration("!=", 26, 641, BoolType, new FunctionParameter(objectType, parm, "A"), new FunctionParameter(objectType, parm, "B")));

            AddOperator(new InOpDeclaration("==", 24, 254, BoolType, new FunctionParameter(NameType, parm, "A"), new FunctionParameter(NameType, parm, "B")));
            AddOperator(new InOpDeclaration("!=", 26, 255, BoolType, new FunctionParameter(NameType, parm, "A"), new FunctionParameter(NameType, parm, "B")));

            AddOperator(new InOpDeclaration("==", 24, 1000, BoolType, new FunctionParameter(StringRefType, parm, "A"), new FunctionParameter(StringRefType, parm, "B")));
            AddOperator(new InOpDeclaration("==", 24, 1001, BoolType, new FunctionParameter(StringRefType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("==", 24, 1002, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(StringRefType, parm, "B")));
            AddOperator(new InOpDeclaration("!=", 26, 1003, BoolType, new FunctionParameter(StringRefType, parm, "A"), new FunctionParameter(StringRefType, parm, "B")));
            AddOperator(new InOpDeclaration("!=", 26, 1004, BoolType, new FunctionParameter(StringRefType, parm, "A"), new FunctionParameter(IntType, parm, "B")));
            AddOperator(new InOpDeclaration("!=", 26, 1005, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(StringRefType, parm, "B")));

            AddOperator(new InOpDeclaration("*", 16, 212, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("*", 16, 213, vectorType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration("*", 16, 296, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration("/", 16, 214, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("+", 20, 215, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration("-", 20, 216, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration("<<", 22, 275, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration(">>", 22, 276, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration("==", 24, 217, BoolType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration("!=", 26, 218, BoolType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration("Dot", 16, 219, FloatType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration("Cross", 16, 220, vectorType, new FunctionParameter(vectorType, parm, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration("*=", 34, 221, vectorType, new FunctionParameter(vectorType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("*=", 34, 297, vectorType, new FunctionParameter(vectorType, outFlags, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration("/=", 34, 222, vectorType, new FunctionParameter(vectorType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("+=", 34, 223, vectorType, new FunctionParameter(vectorType, outFlags, "A"), new FunctionParameter(vectorType, parm, "B")));
            AddOperator(new InOpDeclaration("-=", 34, 224, vectorType, new FunctionParameter(vectorType, outFlags, "A"), new FunctionParameter(vectorType, parm, "B")));

            AddOperator(new InOpDeclaration("==", 24, 142, BoolType, new FunctionParameter(rotatorType, parm, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration("!=", 26, 203, BoolType, new FunctionParameter(rotatorType, parm, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration("*", 16, 287, rotatorType, new FunctionParameter(rotatorType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("*", 16, 288, rotatorType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration("/", 16, 289, rotatorType, new FunctionParameter(rotatorType, parm, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("*=", 34, 290, rotatorType, new FunctionParameter(rotatorType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("/=", 34, 291, rotatorType, new FunctionParameter(rotatorType, outFlags, "A"), new FunctionParameter(FloatType, parm, "B")));
            AddOperator(new InOpDeclaration("+", 20, 316, rotatorType, new FunctionParameter(rotatorType, parm, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration("-", 20, 317, rotatorType, new FunctionParameter(rotatorType, parm, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration("+=", 34, 318, rotatorType, new FunctionParameter(rotatorType, outFlags, "A"), new FunctionParameter(rotatorType, parm, "B")));
            AddOperator(new InOpDeclaration("-=", 34, 319, rotatorType, new FunctionParameter(rotatorType, outFlags, "A"), new FunctionParameter(rotatorType, parm, "B")));

            AddOperator(new InOpDeclaration("+", 16, 270, quatType, new FunctionParameter(quatType, parm, "A"), new FunctionParameter(quatType, parm, "B")));
            AddOperator(new InOpDeclaration("-", 16, 271, quatType, new FunctionParameter(quatType, parm, "A"), new FunctionParameter(quatType, parm, "B")));


            //operators without a nativeIndex. must be linked directly to their function representations
            AddOperator(new InOpDeclaration("ClockwiseFrom", 24, 0, BoolType, new FunctionParameter(IntType, parm, "A"), new FunctionParameter(IntType, parm, "B"))
            {
                Implementer = (Function)objectScope["ClockwiseFrom_IntInt"]
            });

            AddOperator(new InOpDeclaration("==", 24, 0, BoolType, new FunctionParameter(interfaceType, parm, "A"), new FunctionParameter(interfaceType, parm, "B"))
            {
                Implementer = (Function)objectScope["EqualEqual_InterfaceInterface"]
            });
            AddOperator(new InOpDeclaration("!=", 26, 0, BoolType, new FunctionParameter(interfaceType, parm, "A"), new FunctionParameter(interfaceType, parm, "B"))
            {
                Implementer = (Function)objectScope["NotEqual_InterfaceInterface"]
            });

            AddOperator(new InOpDeclaration("*", 34, 0, matrixType, new FunctionParameter(matrixType, parm, "A"), new FunctionParameter(matrixType, parm, "B"))
            {
                Implementer = (Function)objectScope["Multiply_MatrixMatrix"]
            });

            AddOperator(new InOpDeclaration("+", 16, 0, vector2DType, new FunctionParameter(vector2DType, parm, "A"), new FunctionParameter(vector2DType, parm, "B"))
            {
                Implementer = (Function)objectScope["Add_Vector2DVector2D"]
            });
            AddOperator(new InOpDeclaration("-", 16, 0, vector2DType, new FunctionParameter(vector2DType, parm, "A"), new FunctionParameter(vector2DType, parm, "B"))
            {
                Implementer = (Function)objectScope["Subtract_Vector2DVector2D"]
            });

            AddOperator(new InOpDeclaration("-", 20, 0, colorType, new FunctionParameter(colorType, parm, "A"), new FunctionParameter(colorType, parm, "B"))
            {
                Implementer = (Function)objectScope["Subtract_ColorColor"]
            });
            AddOperator(new InOpDeclaration("*", 16, 0, colorType, new FunctionParameter(FloatType, parm, "A"), new FunctionParameter(colorType, parm, "B"))
            {
                Implementer = (Function)objectScope["Multiply_FloatColor"]
            });
            AddOperator(new InOpDeclaration("*", 16, 0, colorType, new FunctionParameter(colorType, parm, "A"), new FunctionParameter(FloatType, parm, "B"))
            {
                Implementer = (Function)objectScope["Multiply_ColorFloat"]
            });
            AddOperator(new InOpDeclaration("+", 20, 0, colorType, new FunctionParameter(colorType, parm, "A"), new FunctionParameter(colorType, parm, "B"))
            {
                Implementer = (Function)objectScope["Add_ColorColor"]
            });

            AddOperator(new InOpDeclaration("-", 20, 0, linearColorType, new FunctionParameter(linearColorType, parm, "A"), new FunctionParameter(linearColorType, parm, "B"))
            {
                Implementer = (Function)objectScope["Subtract_LinearColorLinearColor"]
            });
            AddOperator(new InOpDeclaration("*", 16, 0, linearColorType, new FunctionParameter(linearColorType, parm, "A"), new FunctionParameter(FloatType, parm, "B"))
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
            string fullName = (CurrentScopeName == "" ? "" : $"{CurrentScopeName}.") + name;
            ASTNodeDict scope = null;
            bool cached = useCache && Cache.TryGetValue(fullName, out scope);
            if (!cached)
            {
                scope = new ASTNodeDict();
            }

            if (secondaryScope != null)
            {
                scope.SecondaryScope = secondaryScope;
            }
            Scopes.AddLast(scope);
            ScopeNames.AddLast(fullName);
            
            if (useCache && !cached)
                Cache.Add(fullName, scope);
        }

        public void PopScope()
        {
            if (Scopes.Count == 0)
                throw new InvalidOperationException();

            Scopes.RemoveLast();
            ScopeNames.RemoveLast();
        }

        public bool TryGetSymbol<T>(string symbol, out T node) where T : ASTNode
        {
            return TryGetSymbolInternal(symbol, out node, Scopes);
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
                    staticArrayType.ElementType.Outer = staticArrayType;
                    return TryResolveType(ref staticArrayType.ElementType, globalOnly);
                }
                case ClassType classType:
                {
                    return TryResolveType(ref classType.ClassLimiter, true);
                }
                case DynamicArrayType dynArr:
                {
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
                        if (parts.Length == 2 && Types.TryGetValue(parts[0], out VariableType type) && type is Class cls)
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
            return TryGetSymbol<ASTNode>(symbol, out _, outerScope);
        }

        public bool TypeExists(VariableType type, bool globalOnly = false) => TryResolveType(ref type, globalOnly);

        public bool TryGetSymbolInScopeStack<T>(string symbol, out T node, string lowestScope) where T : ASTNode
        {
            node = null;

            return TryBuildSpecificScope(lowestScope, out LinkedList<ASTNodeDict> stack) && TryGetSymbolInternal(symbol, out node, stack);
        }

        private bool TryBuildSpecificScope(string lowestScope, out LinkedList<ASTNodeDict> stack)
        {
            IEnumerable<string> names = lowestScope.Split('.');
            if (!names.FirstOrDefault().CaseInsensitiveEquals(OBJECT))
            {
                names = names.Prepend(OBJECT);
            }
            stack = new LinkedList<ASTNodeDict>();
            string scopeName = null;
            foreach (string name in names)
            {
                if (scopeName != null)
                {
                    scopeName += ".";
                }

                scopeName += name;
                if (Cache.TryGetValue(scopeName, out ASTNodeDict currentScope))
                    stack.AddLast(currentScope);
                else
                    return false;
            }
            return stack.Count > 0;
        }

        private bool TryGetSymbolInternal<T>(string symbol, out T outNode, LinkedList<ASTNodeDict> stack) where T : ASTNode
        {
            LinkedListNode<ASTNodeDict> it;
            for (it = stack.Last; it != null; it = it.Previous)
            {
                ASTNodeDict nodeDict = it.Value;
                if (nodeDict.TryGetValue(symbol, out ASTNode tempNode) && tempNode is T)
                {
                    outNode = (T)tempNode;
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
            return Scopes.Last().ContainsKey(symbol);
        }

        public bool TryGetSymbolFromCurrentScope(string symbol, out ASTNode node)
        {
            return Scopes.Last().TryGetValue(symbol, out node);
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
            Scopes.Last().Add(symbol, node);
        }

        public void ReplaceSymbol(string symbol, ASTNode node, bool clearAssociatedScope)
        {
            Scopes.Last()[symbol] = node;
            if (clearAssociatedScope)
            {
                ClearScope(symbol);
            }
        }

        private void ClearScope(string symbol)
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
            Scopes.Last().Remove(symbol);
        }

        public bool AddType(VariableType node)
        {
            //awful hack for dealing with the fact that ME2 has 2 different classes with the same name
            //Hopefully the one defined later is the one that actually gets used...
            if (node.Name == "SFXGameEffect_DamageBonus")
            {
                Types[node.Name] = node;
            }
            else if (Types.ContainsKey(node.Name))
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
                Types.Add(node.Name, node);
            }

            //hack for registering intrinsic classes that inherit from non-intrinsics
            switch (node.Name)
            {
                case "Player":
                {
                    var objClass = Types[OBJECT];
                    var netConType = new Class("NetConnection", node, objClass, EClassFlags.Intrinsic | EClassFlags.Abstract | EClassFlags.Transient | EClassFlags.Config) { ConfigName = "Engine" };
                    AddType(netConType);
                    var childConType = new Class("ChildConnection", netConType, objClass, EClassFlags.Intrinsic | EClassFlags.Transient | EClassFlags.Config, vars: new List<VariableDeclaration>
                    {
                        new(netConType, default, "Parent")
                    }) { ConfigName = "Engine" };
                    AddType(childConType);
                    netConType.VariableDeclarations.Add(new VariableDeclaration(new DynamicArrayType(childConType), default, "Children"));
                    break;
                }
            }

            if (node is Class c && c.Flags.Has(EClassFlags.Intrinsic))
            {
                intrinsicClasses.Add(c);
            }

            return true;
        }

        public void RemoveType(string name)
        {
            if (Types.ContainsKey(name))
            {
                Types.Remove(name);
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
            if (!string.Equals(CurrentScopeName, OBJECT, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Tried to go a scopestack while not at the top level scope!");
            if (string.Equals(scope, OBJECT, StringComparison.OrdinalIgnoreCase))
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
            while (!string.Equals(CurrentScopeName, OBJECT, StringComparison.OrdinalIgnoreCase))
                PopScope();
        }

        public void AddOperator(OperatorDeclaration op)
        {
            switch (op)
            {
                case PreOpDeclaration preOpDeclaration:
                    Operators.PrefixOperators.AddToListAt(preOpDeclaration.OperatorKeyword, preOpDeclaration);
                    break;
                case InOpDeclaration inOpDeclaration:
                    Operators.InfixOperators.AddToListAt(inOpDeclaration.OperatorKeyword, inOpDeclaration);
                    break;
                case PostOpDeclaration postOpDeclaration:
                    Operators.PostfixOperators.AddToListAt(postOpDeclaration.OperatorKeyword, postOpDeclaration);
                    break;
            }
        }

        public PreOpDeclaration GetPreOp(string name, VariableType type)
        {
            if (Operators.PrefixOperators.TryGetValue(name, out List<PreOpDeclaration> operators))
            {
                foreach (var preOpDeclaration in operators)
                {
                    if (preOpDeclaration.Operand.VarType == type)
                    {
                        return preOpDeclaration;
                    }
                }
            }

            return new PreOpDeclaration(name, null, 0, null);
        }

        public IEnumerable<InOpDeclaration> GetInfixOperators(string name)
        {
            if (Operators.InfixOperators.TryGetValue(name, out List<InOpDeclaration> operators))
            {
                foreach (InOpDeclaration inOpDeclaration in operators)
                {
                    yield return inOpDeclaration;
                }
            }
        }

        public InOpDeclaration GetInOp(string name, VariableType lhs, VariableType rhs)
        {
            foreach (var inOpDeclaration in GetInfixOperators(name))
            {
                //TODO: do smarter comparison. This only finds exact matches. needs to find best fit given possible implicit conversions
                if (inOpDeclaration.LeftOperand.VarType == lhs && inOpDeclaration.RightOperand.VarType == rhs)
                {
                    return inOpDeclaration;
                }
            }
            return null;
        }

        public PostOpDeclaration GetPostOp(string name, VariableType type)
        {
            if (Operators.PostfixOperators.TryGetValue(name, out List<PostOpDeclaration> operators))
            {
                foreach (var postOpDeclaration in operators)
                {
                    if (postOpDeclaration.Operand.VarType == type)
                    {
                        return postOpDeclaration;
                    }
                }
            }

            return new PostOpDeclaration(name, null, 0, null);
        }

        public bool TryGetType<T>(string nameValue, out T variableType) where T : VariableType
        {
            if (Types.TryGetValue(nameValue, out VariableType varType) && varType is T tType)
            {
                variableType = tType;
                return true;
            }

            variableType = null;
            return false;
        }

        public SymbolTable Clone()
        {
            return new(
               new LinkedList<string>(ScopeNames), 
                       new LinkedList<ASTNodeDict>(Scopes.Select(dict => new ASTNodeDict(dict))), 
                       new CaseInsensitiveDictionary<ASTNodeDict>(Cache.ToDictionary(kvp => kvp.Key, kvp => new ASTNodeDict(kvp.Value))),
                       new CaseInsensitiveDictionary<VariableType>(Types),
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
