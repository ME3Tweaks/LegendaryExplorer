using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Parsing;
using LegendaryExplorerCore.UnrealScript.Utilities;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.UnrealScript.Decompiling
{
    public static class ScriptObjectToASTConverter
    {

        public static Class ConvertClass(UClass uClass, bool decompileBytecodeAndDefaults, FileLib lib = null, PackageCache packageCache = null)
        {
            ExportEntry uClassExport = uClass.Export;
            IMEPackage pcc = uClassExport.FileRef;

            var parent = new VariableType(pcc.GetEntry(uClass.SuperClass)?.ObjectName.Instanced ?? "object");

            var outer = new VariableType(pcc.GetEntry(uClass.OuterClass)?.ObjectName.Instanced ?? parent.Name);

            // TODO: components

            var interfaces = new List<VariableType>();
            foreach ((UIndex interfaceUIndex, UIndex _) in uClass.Interfaces)
            {
                interfaces.Add(new VariableType(pcc.GetEntry(interfaceUIndex)?.ObjectName.Instanced ?? "UNK_INTERFACE"));
            }

            var Types = new List<VariableType>();
            var Vars = new List<VariableDeclaration>();
            var Funcs = new List<Function>();
            var States = new List<State>();

            var replicatedProperties = new Dictionary<ushort, List<string>>();

            var nextItem = uClass.Children;
            while (pcc.TryGetUExport(nextItem, out ExportEntry nextChild))
            {
                var objBin = ObjectBinary.From(nextChild, packageCache);
                switch (objBin)
                {
                    case UConst uConst:
                        Types.Add(new Const(uConst.Export.ObjectName.Instanced, uConst.Value)
                        {
                            FilePath = pcc.FilePath,
                            UIndex = nextChild.UIndex,
                            game = pcc.Game
                        });
                        nextItem = uConst.Next;
                        break;
                    case UEnum uEnum:
                        Types.Add(ConvertEnum(uEnum));
                        nextItem = uEnum.Next;
                        break;
                    case UFunction uFunction:
                        //functions are added below by parsing the LocalFunctionMap 
                        nextItem = uFunction.Next;
                        break;
                    case UProperty uProperty:
                        if (decompileBytecodeAndDefaults && uProperty.PropertyFlags.Has(EPropertyFlags.Net))
                        {
                            replicatedProperties.AddToListAt(uProperty.ReplicationOffset, uProperty.Export.ObjectName.Instanced);
                        }
                        Vars.Add(ConvertVariable(uProperty, packageCache));
                        nextItem = uProperty.Next;
                        break;
                    case UScriptStruct uScriptStruct:
                        Types.Add(ConvertStruct(uScriptStruct, decompileBytecodeAndDefaults, packageCache, lib));
                        nextItem = uScriptStruct.Next;
                        break;
                    case UState uState:
                        nextItem = uState.Next;
                        States.Add(ConvertState(uState, uClass, decompileBytecodeAndDefaults, lib, packageCache));
                        break;
                    default:
                        nextItem = 0;
                        break;
                }
            }
            foreach (UIndex uIndex in uClass.LocalFunctionMap.Values())
            {
                if (uIndex.GetEntry(pcc) is ExportEntry funcExp && funcExp.GetBinaryData<UFunction>() is UFunction uFunction)
                {
                    Funcs.Add(ConvertFunction(uFunction, uClass, decompileBytecodeAndDefaults, lib, packageCache));
                }
            }
            DefaultPropertiesBlock defaultProperties = null;
            CodeBody replicationBlock = null;
            var propEntry = pcc.GetEntry(uClass.Defaults);
            if (decompileBytecodeAndDefaults && propEntry is ExportEntry propExport)
            {
                defaultProperties = ConvertDefaultProperties(propExport, lib, packageCache);
                if (uClass.ScriptBytecodeSize > 0)
                {
                    replicationBlock = new ByteCodeDecompiler(uClass, uClass, lib)
                    {
                        ReplicatedProperties = replicatedProperties
                    }.Decompile();
                }
            }

            var ast = new Class(uClassExport.ObjectName.Instanced, parent, outer, uClass.ClassFlags, interfaces, Types, Vars, Funcs, States, defaultProperties)
            {
                ConfigName = uClass.ClassConfigName,
                Package = uClassExport.Parent is null ? Path.GetFileNameWithoutExtension(pcc.FilePath) : uClassExport.ParentInstancedFullPath,
                IsFullyDefined = nextItem.value == 0 && propEntry is ExportEntry,
                FilePath = pcc.FilePath,
                UIndex = uClassExport.UIndex,
                ReplicationBlock = replicationBlock
            };
            // Ugly quick fix:
            foreach (var member in Types)
                member.Outer = ast;
            foreach (var member in Vars)
                member.Outer = ast;
            foreach (var member in Funcs)
                member.Outer = ast;
            foreach (var member in States)
                member.Outer = ast;


            var virtFuncLookup = new List<string>(uClass.VirtualFunctionTable?.Length ?? 0);
            if (pcc.Game.IsGame3())
            {
                foreach (UIndex uIdx in uClass.VirtualFunctionTable)
                {
                    virtFuncLookup.Add(uIdx.GetEntry(pcc)?.ObjectName.Instanced);
                }
            }
            ast.VirtualFunctionNames = virtFuncLookup;

            return ast;
        }

        public static State ConvertState(UState obj, UClass containingClass = null, bool decompileBytecode = true, FileLib lib = null, PackageCache packageCache = null)
        {
            if (containingClass is null)
            {
                var classExport = obj.Export.Parent as ExportEntry;
                while (classExport is {IsClass: false})
                {
                    classExport = classExport.Parent as ExportEntry;
                }

                if (classExport == null)
                {
                    throw new Exception($"Could not get containing class for state {obj.Export.ObjectName}");
                }

                containingClass = classExport.GetBinaryData<UClass>(packageCache);
            }
            // TODO: labels

            State parent = null;
            //if the parent has the same name, then it's overriden, not extended
            if (obj.SuperClass != 0 && obj.SuperClass.GetEntry(obj.Export.FileRef) is IEntry parentState &&
                !parentState.ObjectName.Instanced.CaseInsensitiveEquals(obj.Export.ObjectName.Instanced))
            {
                parent = new State(parentState.ObjectName.Instanced, null, default, null, null, null, null, null);
            }

            var funcs = new List<Function>();
            var nextItem = obj.Children;
            while (obj.Export.FileRef.TryGetUExport(nextItem, out ExportEntry nextChild))
            {
                var objBin = ObjectBinary.From(nextChild, packageCache);
                if (objBin is not UFunction uFunction)
                {
                    //todo: State should never have non-function children, so this is indicative of a broken state definition. is there some way to log this?
                    break;
                }
                funcs.Add(ConvertFunction(uFunction, containingClass, decompileBytecode, lib));
                nextItem = uFunction.Next;
            }

            var body = decompileBytecode ? new ByteCodeDecompiler(obj, containingClass, lib).Decompile() : null;

            return new State(obj.Export.ObjectName.Instanced, body, obj.StateFlags, parent, funcs, new List<Label>(), null, null)
            {
                FilePath = obj.Export.FileRef.FilePath,
                UIndex = obj.Export.UIndex,
                IgnoreMask = obj.IgnoreMask
            };
        }

        public static Struct ConvertStruct(UScriptStruct obj, bool decompileDefaults, PackageCache packageCache = null, FileLib fileLib = null)
        {
            var vars = new List<VariableDeclaration>();
            var types = new List<VariableType>();
            var nextItem = obj.Children;

            IMEPackage pcc = obj.Export.FileRef;
            while (pcc.TryGetUExport(nextItem, out ExportEntry nextChild))
            {
                var objBin = ObjectBinary.From(nextChild, packageCache);
                switch (objBin)
                {
                    case UProperty uProperty:
                        vars.Add(ConvertVariable(uProperty, packageCache));
                        nextItem = uProperty.Next;
                        break;
                    case UScriptStruct uStruct:
                        types.Add(ConvertStruct(uStruct, decompileDefaults, packageCache, fileLib));
                        nextItem = uStruct.Next;
                        break;
                    default:
                        nextItem = 0;
                        break;
                }
            }

            VariableType parent = obj.SuperClass != 0
                ? new VariableType(obj.SuperClass.GetEntry(pcc).ObjectName.Instanced) : null;

            DefaultPropertiesBlock defaults = null;
            string structName = obj.Export.ObjectName.Instanced;
            if (decompileDefaults)
            {
                try
                {
                    PropertyCollection properties = null;
                    if (parent is not null)
                    {
                        properties = obj.Defaults;
                    }
                    else if (fileLib?.ReadonlySymbolTable is SymbolTable symbols && symbols.TryGetType(structName, out Struct libStruct))
                    {
                        properties = obj.Defaults.Diff(libStruct.MakeBaseProps(pcc, packageCache));
                    }

                    properties ??= RemoveDefaultValues(obj.Defaults.DeepClone(), pcc.Game);
                    defaults = new DefaultPropertiesBlock(ConvertProperties(properties, obj.Export, structName, true, fileLib));
                }
                catch (Exception e)
                {
                    throw new Exception($"Exception while removing default properties in export {obj.Export.InstancedFullPath} {obj.Export.FileRef.FilePath}", e);
                }
            }

            var node = new Struct(structName, parent, obj.StructFlags, vars, types, defaults, obj.Defaults.DeepClone())
            {
                FilePath = pcc.FilePath,
                UIndex = obj.Export.UIndex
            };

            foreach (VariableDeclaration member in vars)
                member.Outer = node;

            return node;
        }

        private static PropertyCollection RemoveDefaultValues(PropertyCollection props, MEGame game)
        {
            var result = new PropertyCollection();

            foreach (Property prop in props)
            {
                switch (prop)
                {
                    case ArrayPropertyBase arrayPropertyBase:
                        if (arrayPropertyBase.Count > 0)
                        {
                            result.Add(prop);
                        }
                        break;
                    case BioMask4Property bioMask4Property:
                        if (bioMask4Property.Value != 0)
                        {
                            result.Add(prop);
                        }
                        break;
                    case BoolProperty boolProperty:
                        if (boolProperty.Value)
                        {
                            result.Add(prop);
                        }
                        break;
                    case ByteProperty byteProperty:
                        if (byteProperty.Value != 0)
                        {
                            result.Add(prop);
                        }
                        break;
                    case DelegateProperty delegateProperty:
                        if (delegateProperty.Value != ScriptDelegate.Empty)
                        {
                            result.Add(prop);
                        }
                        break;
                    case EnumProperty enumProperty:
                        if (enumProperty.Value != GlobalUnrealObjectInfo.GetEnumValues(game, enumProperty.EnumType).FirstOrDefault())
                        {
                            result.Add(prop);
                        }
                        break;
                    case FloatProperty floatProperty:
                        if (floatProperty.Value != 0f)
                        {
                            result.Add(prop);
                        }
                        break;
                    case IntProperty intProperty:
                        if (intProperty.Value != 0)
                        {
                            result.Add(prop);
                        }
                        break;
                    case NameProperty nameProperty:
                        if (nameProperty.Value != "None")
                        {
                            result.Add(prop);
                        }
                        break;
                    case ObjectProperty objectProperty:
                        if (objectProperty.Value != 0)
                        {
                            result.Add(prop);
                        }
                        break;
                    case StringRefProperty stringRefProperty:
                        if (stringRefProperty.Value != 0)
                        {
                            result.Add(prop);
                        }
                        break;
                    case StrProperty strProperty:
                        if (strProperty.Value != string.Empty)
                        {
                            result.Add(prop);
                        }
                        break;
                    case StructProperty structProperty:
                        structProperty.Properties = RemoveDefaultValues(structProperty.Properties, game);
                        if (structProperty.Properties.Count > 0)
                        {
                            result.Add(structProperty);
                        }
                        break;
                    case NoneProperty _:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"RemoveDefaultValue() invalid property type for {nameof(prop)}: {prop?.PropType}");
                }
            }

            return result;
        }

        public static Enumeration ConvertEnum(UEnum obj)
        {
            var vals = new List<EnumValue>();
            for (byte i = 0; i < obj.Names.Length - 1; i++) //- 1 to skip _MAX value
            {
                var val = obj.Names[i];

                vals.Add(new EnumValue(val.Instanced, i));
            }

            var node = new Enumeration(obj.Export.ObjectName.Instanced, vals, null, null)
            {
                FilePath = obj.Export.FileRef.FilePath,
                UIndex = obj.Export.UIndex
            };

            foreach (var member in vals)
                member.Outer = node;

            return node;
        }

        public static VariableDeclaration ConvertVariable(UProperty obj, PackageCache packageCache = null)
        {
            int size = obj.ArraySize;

            return new VariableDeclaration(GetPropertyType(obj, packageCache), obj.PropertyFlags, obj.Export.ObjectName.Instanced, size, obj.Category != "None" ? obj.Category : null)
            {
                FilePath = obj.Export.FileRef.FilePath,
                UIndex = obj.Export.UIndex
            };
        }

        private static VariableType GetPropertyType(UProperty obj, PackageCache packageCache = null)
        {
            string typeStr = "UNKNOWN";
            switch (obj)
            {
                case UArrayProperty arrayProperty:
                    return new DynamicArrayType(GetPropertyType(ObjectBinary.From(obj.Export.FileRef.GetUExport(arrayProperty.ElementType), packageCache) as UProperty, packageCache));
                case UBioMask4Property _:
                    return SymbolTable.BioMask4Type;
                case UBoolProperty _:
                    return SymbolTable.BoolType;
                case UByteProperty byteProperty:
                    if (byteProperty.IsEnum)
                    {
                        IEntry enumDef = byteProperty.Enum.GetEntry(obj.Export.FileRef);
                        if (enumDef is ExportEntry enumExp)
                        {
                            return ConvertEnum(enumExp.GetBinaryData<UEnum>(packageCache));
                        }
                        typeStr = enumDef.ObjectName.Instanced;
                    }
                    else
                    {
                        return SymbolTable.ByteType;
                    }
                    break;
                case UClassProperty classProp:
                    return new ClassType(new VariableType(obj.Export.FileRef.GetEntry(classProp.ClassRef).ObjectName));
                case UDelegateProperty delegateProperty:
                    {
                        IEntry entry = obj.Export.FileRef.GetEntry(delegateProperty.Function);
                        IEntry functionClass = entry.Parent;
                        for (IEntry delPropClass = delegateProperty.Export; delPropClass != null; delPropClass = delPropClass.Parent)
                        {
                            if (delPropClass.ClassName == "Class")
                            {
                                while (delPropClass != null)
                                {
                                    if (delPropClass == functionClass)
                                    {
                                        return new DelegateType(new Function(entry.ObjectName.Instanced, default, null, null, null));
                                    }

                                    delPropClass = (delPropClass as ExportEntry)?.SuperClass;
                                }
                                break;
                            }
                        }
                        //function is not in scope, fully qualify it
                        string qualifiedFunctionName = entry.ObjectName;
                        while (entry.Parent != null && entry.Parent.ClassName != "Package")
                        {
                            entry = entry.Parent;
                            qualifiedFunctionName = $"{entry.ObjectName.Instanced}.{qualifiedFunctionName}";
                        }
                        return new DelegateType(new Function(qualifiedFunctionName, default, null, null, null));
                    }
                case UFloatProperty _:
                    return SymbolTable.FloatType;
                case UIntProperty _:
                    return SymbolTable.IntType;
                case UNameProperty _:
                    return SymbolTable.NameType;
                case UStringRefProperty _:
                    return SymbolTable.StringRefType;
                case UStrProperty _:
                    return SymbolTable.StringType;
                case UStructProperty structProperty:
                    typeStr = structProperty.Struct.GetEntry(obj.Export.FileRef)?.ObjectName.Instanced ?? typeStr;
                    break;
                //if we're just getting the name of the objectref, then Interface and Component are the same as Object
                //Leave these here in case we do something fancier
                //case UInterfaceProperty interfaceProperty:
                //    typeStr = interfaceProperty.ObjectRef.GetEntry(obj.Export.FileRef)?.ObjectName.Instanced ?? typeStr; // ?
                //    break;
                //case UComponentProperty componentProperty:
                //    typeStr = componentProperty.ObjectRef.GetEntry(obj.Export.FileRef)?.ObjectName.Instanced ?? typeStr; // ?
                //    break;
                case UObjectProperty objectProperty:
                    typeStr = objectProperty.ObjectRef.GetEntry(obj.Export.FileRef)?.ObjectName.Instanced ?? typeStr; // ?
                    break;
                default:
                    {
                        //if (obj is UObject)
                        typeStr = "Object";
                        break;
                    }
            }

            return new VariableType(typeStr);
        }

        public static Function ConvertFunction(UFunction obj, UClass containingClass = null, bool decompileBytecode = true, FileLib lib = null, PackageCache packageCache = null)
        {
            if (containingClass is null)
            {
                var classExport = obj.Export.Parent as ExportEntry;
                while (classExport is {IsClass: false})
                {
                    classExport = classExport.Parent as ExportEntry;
                }

                if (classExport == null)
                {
                    throw new Exception($"Could not get containing class for function {obj.Export.ObjectName}");
                }

                containingClass = classExport.GetBinaryData<UClass>(packageCache);
            }
            VariableDeclaration returnVal = null;
            var nextItem = obj.Children;

            var parameters = new List<FunctionParameter>();
            var locals = new List<VariableDeclaration>();
            IMEPackage pcc = obj.Export.FileRef;
            while (pcc.TryGetUExport(nextItem, out ExportEntry nextChild))
            {
                var objBin = ObjectBinary.From(nextChild, packageCache);
                switch (objBin)
                {
                    case UProperty uProperty:
                        if (uProperty.PropertyFlags.Has(EPropertyFlags.ReturnParm))
                        {
                            returnVal = ConvertVariable(uProperty, packageCache);
                        }
                        else if (uProperty.PropertyFlags.Has(EPropertyFlags.Parm))
                        {
                            var convert = ConvertVariable(uProperty, packageCache);
                            parameters.Add(new FunctionParameter(convert.VarType, convert.Flags, convert.Name, convert.ArrayLength));
                        }
                        else
                        {
                            locals.Add(ConvertVariable(uProperty, packageCache));
                        }
                        nextItem = uProperty.Next;
                        break;
                    default:
                        nextItem = 0;
                        break;
                }
            }

            CodeBody body = null;
            if (decompileBytecode)
            {
                body = new ByteCodeDecompiler(obj, containingClass, lib, parameters, returnVal?.VarType).Decompile();
            }


            var func = new Function(obj.Export.ObjectName.Instanced, obj.FunctionFlags, returnVal, body, parameters)
            {
                NativeIndex = obj.NativeIndex,
                FilePath = pcc.FilePath,
                UIndex = obj.Export.UIndex,
            };
            if (!obj.Export.Game.IsGame3())
            {
                func.OperatorPrecedence = obj.OperatorPrecedence;
                func.FriendlyName = obj.FriendlyName;
            }

            foreach (var local in locals)
            {
                local.Outer = func;
            }
            func.Locals = locals;
            return func;
        }

        public static DefaultPropertiesBlock ConvertDefaultProperties(ExportEntry defaultsExport, FileLib fileLib = null, PackageCache packageCache = null)
        {
            return new DefaultPropertiesBlock(GetStatements(defaultsExport));

            List<Statement> GetStatements(ExportEntry exportEntry)
            {
                var defaults = new List<Statement>();
                foreach (ExportEntry child in exportEntry.GetChildren<ExportEntry>())
                {
                    var type = new VariableType(child.ClassName);
                    var decl = new VariableDeclaration(type, default, child.ObjectName.Instanced);
                    defaults.Add(new Subobject(decl, new Class(child.ClassName, null, null, default), GetStatements(child), child.HasArchetype));
                }

                defaults.AddRange(ConvertProperties(exportEntry.GetProperties(packageCache: packageCache), defaultsExport, exportEntry.Class.ObjectName.Instanced, false, fileLib));
                return defaults;
            }
        }

        private static List<Statement> ConvertProperties(PropertyCollection properties, ExportEntry defaultsExport, string objectName, bool isStruct, FileLib fileLib = null)
        {
            var statements = new List<Statement>();
            foreach (var prop in properties)
            {
                if (prop is NoneProperty)
                {
                    continue;
                }

                Expression name = new SymbolReference(null, prop.Name.Instanced);

                if (fileLib?.ReadonlySymbolTable is SymbolTable symbols)
                {
                    string scope = null;
                    if (isStruct && !defaultsExport.ClassName.CaseInsensitiveEquals("ScriptStruct"))
                    {
                        if (symbols.TryGetType(objectName, out Struct strct))
                        {
                            scope = strct.GetScope();
                        }
                    }
                    else if (symbols.TryGetType(isStruct ? defaultsExport.Parent.ObjectName.Instanced : objectName, out Class containingClass))
                    {
                        if (isStruct)
                        {
                            foreach (Struct strct in containingClass.TypeDeclarations.OfType<Struct>())
                            {
                                if (strct.Name.CaseInsensitiveEquals(objectName))
                                {
                                    scope = strct.GetScope();
                                    break;
                                }
                            }
                        }
                        else
                        {
                            scope = containingClass.GetScope();
                        }
                    }

                    if (scope is not null && symbols.TryGetSymbolInScopeStack(prop.Name, out VariableDeclaration decl, scope) && decl.IsStaticArray)
                    {
                        name = new ArraySymbolRef(name, new IntegerLiteral(prop.StaticArrayIndex), null, null);
                    }
                }
                else if (prop.StaticArrayIndex > 0)
                {
                    name = new ArraySymbolRef(name, new IntegerLiteral(prop.StaticArrayIndex), null, null);
                }
                var value = ConvertPropertyValue(prop);
                statements.Add(new AssignStatement(name, value));
            }



            return statements;

            Expression ConvertPropertyValue(Property prop)
            {
                IMEPackage pcc = defaultsExport.FileRef;
                switch (prop)
                {
                    case BoolProperty boolProperty:
                        return new BooleanLiteral(boolProperty.Value);
                    case ByteProperty byteProperty:
                        return new IntegerLiteral(byteProperty.Value) { NumType = Keywords.BYTE };
                    case BioMask4Property bioMask4Property:
                        return new IntegerLiteral(bioMask4Property.Value) { NumType = Keywords.BIOMASK4 };
                    case DelegateProperty delegateProperty:
                        string funcName = delegateProperty.Value.FunctionName.Instanced;
                        var symRef = new SymbolReference(null, funcName);
                        if (pcc.TryGetEntry(delegateProperty.Value.Object, out IEntry containingObject))
                        {
                            symRef = new CompositeSymbolRef(new ObjectLiteral(new NameLiteral(containingObject.ClassName), new VariableType("Class")), symRef);
                        }
                        return symRef;
                    case EnumProperty enumProperty:
                        if (enumProperty.Value.Instanced.CaseInsensitiveEquals("None"))
                        {
                            return new NoneLiteral();
                        }
                        return new SymbolReference(new EnumValue(enumProperty.Value.Instanced, 0) {Enum = new Enumeration(enumProperty.EnumType.Instanced, new List<EnumValue>(), null, null)}, enumProperty.Value.Instanced);
                    case FloatProperty floatProperty:
                        return new FloatLiteral(floatProperty.Value);
                    case IntProperty intProperty:
                        return new IntegerLiteral(intProperty.Value);
                    case NameProperty nameProperty:
                        return new NameLiteral(nameProperty.Value.Instanced);
                    case ObjectProperty objectProperty:
                        var objRef = objectProperty.Value;
                        if (objRef == 0)
                            return new NoneLiteral();
                        var objEntry = pcc.GetEntry(objRef);
                        if (objEntry is ExportEntry objExp && objExp.InstancedFullPath.StartsWith(defaultsExport.InstancedFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            //subObject reference
                            return new SymbolReference(null, objExp.ObjectName.Instanced);
                        }
                        else
                        {
                            return new ObjectLiteral(new NameLiteral(objEntry.ClassName.CaseInsensitiveEquals("Class") ? objEntry.ObjectName.Instanced : objEntry.InstancedFullPath), new VariableType(objEntry.ClassName));
                        }
                    case StringRefProperty stringRefProperty:
                        return new StringRefLiteral(stringRefProperty.Value);
                    case StrProperty strProperty:
                        return new StringLiteral(strProperty.Value);
                    case StructProperty structProperty:
                        return new StructLiteral(null, ConvertProperties(structProperty.Properties, defaultsExport, structProperty.StructType, true, fileLib));
                    case ArrayPropertyBase arrayPropertyBase:
                        return new DynamicArrayLiteral(null, arrayPropertyBase.Properties.Select(ConvertPropertyValue).ToList());
                    default:
                        return new SymbolReference(null, "UNSUPPORTED:" + prop.PropType);

                }
            }
        }
    }
}
