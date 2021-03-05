
using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3Script.Analysis.Symbols;
using ME3Script.Lexing;
using ME3Script.Parsing;
using ME3Script.Utilities;

namespace ME3Script.Decompiling
{
    public static class ScriptObjectToASTConverter
    {

        public static Class ConvertClass(UClass uClass, bool decompileBytecode, FileLib lib = null)
        {
            IMEPackage pcc = uClass.Export.FileRef;

            VariableType parent = new VariableType(pcc.GetEntry(uClass.SuperClass)?.ObjectName.Instanced ?? "object");

            VariableType outer = new VariableType(pcc.GetEntry(uClass.OuterClass)?.ObjectName.Instanced ?? parent.Name);

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

            var nextItem = uClass.Children;

            while (pcc.TryGetUExport(nextItem, out ExportEntry nextChild))
            {
                var objBin = ObjectBinary.From(nextChild);
                switch (objBin)
                {
                    case UConst uConst:
                        Types.Add(new Const(uConst.Export.ObjectName.Instanced, uConst.Value)
                        {
                            Literal = new ClassOutlineParser(new TokenStream<string>(new StringLexer(uConst.Value))).ParseConstValue()
                        });
                        nextItem = uConst.Next;
                        break;
                    case UEnum uEnum:
                        Types.Add(ConvertEnum(uEnum));
                        nextItem = uEnum.Next;
                        break;
                    case UFunction uFunction:
                        Funcs.Add(ConvertFunction(uFunction, uClass, decompileBytecode));
                        nextItem = uFunction.Next;
                        break;
                    case UProperty uProperty:
                        Vars.Add(ConvertVariable(uProperty));
                        nextItem = uProperty.Next;
                        break;
                    case UScriptStruct uScriptStruct:
                        Types.Add(ConvertStruct(uScriptStruct));
                        nextItem = uScriptStruct.Next;
                        break;
                    case UState uState:
                        nextItem = uState.Next;
                        States.Add(ConvertState(uState, uClass, decompileBytecode));
                        break;
                    default:
                        nextItem = 0;
                        break;
                }
            }
            var propEntry = pcc.GetEntry(uClass.Defaults);
            DefaultPropertiesBlock defaultProperties = null; ;
            if (propEntry is ExportEntry propExport)
            {
                defaultProperties = ConvertDefaultProperties(propExport);
            }

            Class AST = new Class(uClass.Export.ObjectName.Instanced, parent, outer, uClass.ClassFlags, interfaces, Types, Vars, Funcs, States, defaultProperties)
            {
                ConfigName = uClass.ClassConfigName,
                Package = uClass.Export.Parent is null ? Path.GetFileNameWithoutExtension(pcc.FilePath) : uClass.Export.ParentInstancedFullPath,
                IsFullyDefined = nextItem.value == 0 && defaultProperties != null
            };
            // Ugly quick fix:
            foreach (var member in Types)
                member.Outer = AST;
            foreach (var member in Vars)
                member.Outer = AST;
            foreach (var member in Funcs)
                member.Outer = AST;
            foreach (var member in States)
                member.Outer = AST;


            var virtFuncLookup = new CaseInsensitiveDictionary<ushort>();
            if (pcc.Game is MEGame.ME3)
            {
                for (ushort i = 0; i < uClass.FullFunctionsList.Length; i++)
                {
                    virtFuncLookup.Add(uClass.FullFunctionsList[i].GetEntry(pcc)?.ObjectName, i);
                }
            }
            AST.VirtualFunctionLookup = virtFuncLookup;

            return AST;
        }

        public static State ConvertState(UState obj, UClass containingClass = null, bool decompileBytecode = true, FileLib lib = null)
        {
            if (containingClass is null)
            {
                ExportEntry classExport = obj.Export.Parent as ExportEntry;
                while (classExport != null && !classExport.IsClass)
                {
                    classExport = classExport.Parent as ExportEntry;
                }

                if (classExport == null)
                {
                    throw new Exception($"Could not get containing class for state {obj.Export.ObjectName}");
                }

                containingClass = classExport.GetBinaryData<UClass>();
            }
            // TODO: labels

            State parent = null;
            //if the parent is not from the same class, then it's overriden, not extended
            if (obj.SuperClass != 0 && obj.SuperClass.GetEntry(obj.Export.FileRef).Parent == obj.Export.Parent)
                parent = new State(obj.SuperClass.GetEntry(obj.Export.FileRef).ObjectName.Instanced, null, default, null, null, null, null, null, null);

            var Funcs = new List<Function>();
            var Ignores = new List<Function>();
            var nextItem = obj.Children;
            while (obj.Export.FileRef.TryGetUExport(nextItem, out ExportEntry nextChild))
            {
                var objBin = ObjectBinary.From(nextChild);
                switch (objBin)
                {
                    case UFunction uFunction when uFunction.FunctionFlags.HasFlag(FunctionFlags.Defined):
                        Funcs.Add(ConvertFunction(uFunction, containingClass, decompileBytecode));
                        nextItem = uFunction.Next;
                        break;
                    case UFunction uFunction:
                        Ignores.Add(new Function(uFunction.Export.ObjectName.Instanced, default, null, null));
                        /* Ignored functions are not marked as defined, so we dont need to lookup the ignormask.
                         * They are defined though, each being its own proper object with simply a return nothing for bytecode.
                         * */
                        nextItem = uFunction.Next;
                        break; ;
                    default:
                        nextItem = 0;
                        break;
                }
            }

            var body = decompileBytecode ? new ByteCodeDecompiler(obj, containingClass, lib: lib).Decompile() : null;

            return new State(obj.Export.ObjectName.Instanced, body, obj.StateFlags, parent, Funcs, Ignores, new List<Label>(), null, null);
        }

        public static Struct ConvertStruct(UScriptStruct obj)
        {
            var Vars = new List<VariableDeclaration>();
            var Types = new List<VariableType>();
            var nextItem = obj.Children;

            while (obj.Export.FileRef.TryGetUExport(nextItem, out ExportEntry nextChild))
            {
                var objBin = ObjectBinary.From(nextChild);
                switch (objBin)
                {
                    case UProperty uProperty:
                        Vars.Add(ConvertVariable(uProperty));
                        nextItem = uProperty.Next;
                        break;
                    case UScriptStruct uStruct:
                        Types.Add(ConvertStruct(uStruct));
                        nextItem = uStruct.Next;
                        break;
                    default:
                        nextItem = 0;
                        break;
                }
            }

            VariableType parent = obj.SuperClass != 0 
                ? new VariableType(obj.SuperClass.GetEntry(obj.Export.FileRef).ObjectName.Instanced) : null;

            var defaults = new DefaultPropertiesBlock(ConvertProperties(RemoveDefaultValues(obj.Defaults), obj.Export));

            var node = new Struct(obj.Export.ObjectName.Instanced, parent, obj.StructFlags, Vars, Types, defaults);

            foreach (var member in Vars)
                member.Outer = node;

            return node;
        }

        private static PropertyCollection RemoveDefaultValues(PropertyCollection props)
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
                        if (enumProperty.Value != enumProperty.EnumValues.FirstOrDefault())
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
                        structProperty.Properties = RemoveDefaultValues(structProperty.Properties);
                        if (structProperty.Properties.Count > 0)
                        {
                            result.Add(structProperty);
                        }
                        break;
                    case NoneProperty _:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(prop));
                }
            }

            return result;
        }

        public static Enumeration ConvertEnum(UEnum obj)
        {
            var vals = new List<EnumValue>();
            for (byte i = 0; i < obj.Names.Length; i++)
            {
                var val = obj.Names[i];
                if (val.Name.EndsWith("_MAX"))
                {
                    continue;
                }

                vals.Add(new EnumValue(val.Instanced, i));
            }

            var node = new Enumeration(obj.Export.ObjectName.Instanced, vals, null, null);

            foreach (var member in vals)
                member.Outer = node;

            return node;
        }

        public static VariableDeclaration ConvertVariable(UProperty obj)
        {
            int size = obj.ArraySize;

            return new VariableDeclaration(GetPropertyType(obj), obj.PropertyFlags, obj.Export.ObjectName.Instanced, size, obj.Category != "None" ? obj.Category : null);
        }

        private static VariableType GetPropertyType(UProperty obj)
        {
            string typeStr = "UNKNOWN";
            switch (obj)
            {
                case UArrayProperty arrayProperty:
                    return new DynamicArrayType(GetPropertyType(ObjectBinary.From(obj.Export.FileRef.GetUExport(arrayProperty.ElementType)) as UProperty));
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
                            return ConvertEnum(enumExp.GetBinaryData<UEnum>());
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
                                    return new DelegateType(new Function(entry?.ObjectName.Instanced, default, null, null, null));
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

        public static Function ConvertFunction(UFunction obj, UClass containingClass = null, bool decompileBytecode = true, FileLib lib = null)
        {
            if (containingClass is null)
            {
                ExportEntry classExport = obj.Export.Parent as ExportEntry;
                while (classExport != null && !classExport.IsClass)
                {
                    classExport = classExport.Parent as ExportEntry;
                }

                if (classExport == null)
                {
                    throw new Exception($"Could not get containing class for function {obj.Export.ObjectName}");
                }

                containingClass = classExport.GetBinaryData<UClass>();
            }
            VariableType returnType = null;
            bool retValNeedsDestruction = false;
            var nextItem = obj.Children;

            var parameters = new List<FunctionParameter>();
            var locals = new List<VariableDeclaration>();
            var coerceReturn = false;
            while (obj.Export.FileRef.TryGetUExport(nextItem, out ExportEntry nextChild))
            {
                var objBin = ObjectBinary.From(nextChild);
                switch (objBin)
                {
                    case UProperty uProperty:
                        if (uProperty.PropertyFlags.HasFlag(UnrealFlags.EPropertyFlags.ReturnParm))
                        {
                            var returnVal = ConvertVariable(uProperty);
                            returnType = returnVal.VarType;
                            if (uProperty.PropertyFlags.Has(UnrealFlags.EPropertyFlags.CoerceParm))
                            {
                                coerceReturn = true;
                            }

                            retValNeedsDestruction = returnVal.Flags.Has(UnrealFlags.EPropertyFlags.NeedCtorLink);
                        }
                        else if (uProperty.PropertyFlags.HasFlag(UnrealFlags.EPropertyFlags.Parm))
                        {
                            var convert = ConvertVariable(uProperty);
                            parameters.Add(new FunctionParameter(convert.VarType, convert.Flags, convert.Name, convert.ArrayLength));
                        }
                        else
                        {
                            locals.Add(ConvertVariable(uProperty));
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
                body = new ByteCodeDecompiler(obj, containingClass, parameters, returnType, lib).Decompile();
            }


            var func = new Function(obj.Export.ObjectName.Instanced,
                                    obj.FunctionFlags, returnType, body, parameters)
            {
                NativeIndex = obj.NativeIndex,
                CoerceReturn = coerceReturn,
                RetValNeedsDestruction = retValNeedsDestruction
            };
            if (obj.Export.Game <= MEGame.ME2)
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

        public static DefaultPropertiesBlock ConvertDefaultProperties(ExportEntry defaultsExport)
        {
            List<Statement> defaults = ConvertProperties(defaultsExport.GetProperties(), defaultsExport);

            return new DefaultPropertiesBlock(new List<Statement>(defaults));
        }

        private static List<Statement> ConvertProperties(PropertyCollection properties, ExportEntry containingExport)
        {
            var statements = new List<Statement>();
            foreach (var prop in properties)
            {
                if (prop is NoneProperty)
                {
                    continue;
                }
                var name = new SymbolReference(null, prop.Name);
                var value = ConvertPropertyValue(prop);
                if (value is StructLiteral structLiteral)
                {
                    var subObjectsToAdd = new List<Subobject>();
                    foreach (Subobject subobject in structLiteral.Statements.OfType<Subobject>())
                    {
                        if (statements.All(stmnt => (stmnt as Subobject)?.Name != subobject.Name))
                        {
                            subObjectsToAdd.Add(subobject);
                        }
                    }
                    statements.AddRange(subObjectsToAdd);
                    structLiteral.Statements = structLiteral.Statements.Where(stmnt => stmnt is AssignStatement).ToList();
                }
                statements.Add(new AssignStatement(name, value));
            }



            return statements;

            Expression ConvertPropertyValue(Property prop)
            {
                switch (prop)
                {
                    case BoolProperty boolProperty:
                        return new BooleanLiteral(boolProperty.Value);
                    case ByteProperty byteProperty:
                        return new IntegerLiteral(byteProperty.Value) { NumType = Keywords.BYTE };
                    case BioMask4Property bioMask4Property:
                        return new IntegerLiteral(bioMask4Property.Value) { NumType = Keywords.BIOMASK4 };
                    case DelegateProperty delegateProperty:
                        return new SymbolReference(null, delegateProperty.Value.FunctionName);
                    case EnumProperty enumProperty:
                        return new CompositeSymbolRef(new SymbolReference(null, enumProperty.EnumType.Instanced), new SymbolReference(null, enumProperty.Value.Instanced));
                    case FloatProperty floatProperty:
                        return new FloatLiteral(floatProperty.Value);
                    case IntProperty intProperty:
                        return new IntegerLiteral(intProperty.Value);
                    case NameProperty nameProperty:
                        return new NameLiteral(nameProperty.Value);
                    case ObjectProperty objectProperty:
                        var objRef = objectProperty.Value;
                        if (objRef == 0)
                            return new NoneLiteral();
                        var objEntry = containingExport.FileRef.GetEntry(objRef);
                        if (objEntry is ExportEntry objExp && objExp.Parent == containingExport)
                        {
                            string name = objExp.ObjectName.Instanced;
                            if (!(statements.FirstOrDefault(stmnt => (stmnt as Subobject)?.Name.Name == name) is Subobject subObj))
                            {
                                var type = new VariableType(objExp.ClassName);
                                var decl = new VariableDeclaration(type, default, name);
                                subObj = new Subobject(decl, objExp.ClassName, ConvertProperties(objExp.GetProperties(), objExp));
                                statements.Add(subObj);
                            }
                            return new SymbolReference(subObj.Name, name);
                        }
                        else
                        {
                            return new ObjectLiteral(new NameLiteral(objEntry.InstancedFullPath), new VariableType(objEntry.ClassName));
                        }
                    case StringRefProperty stringRefProperty:
                        return new StringRefLiteral(stringRefProperty.Value);
                    case StrProperty strProperty:
                        return new StringLiteral(strProperty.Value);
                    case StructProperty structProperty:
                        return new StructLiteral(structProperty.StructType, ConvertProperties(structProperty.Properties, containingExport));
                    case ArrayPropertyBase arrayPropertyBase:
                        return new DynamicArrayLiteral(arrayPropertyBase.Reference, arrayPropertyBase.Properties.Select(ConvertPropertyValue).ToList());
                    default:
                        return new SymbolReference(null, "UNSUPPORTED:" + prop.PropType);

                }
            }
        }
    }
}
