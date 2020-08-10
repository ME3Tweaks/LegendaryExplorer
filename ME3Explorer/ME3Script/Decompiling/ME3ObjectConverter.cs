
using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gammtek.Conduit.Extensions;
using ME3Explorer;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;

namespace ME3Script.Decompiling
{
    public static class ME3ObjectToASTConverter
    {

        public static Class ConvertClass(UClass uClass) // TODO: this is only for text decompiling, should extend to a full ast for modification.
        {
            IMEPackage pcc = uClass.Export.FileRef;

            VariableType parent = new VariableType(pcc.GetEntry(uClass.SuperClass)?.ObjectName.Instanced ?? "object");

            VariableType outer = new VariableType(pcc.GetEntry(uClass.OuterClass)?.ObjectName.Instanced ?? parent.Name);
            // TODO: operators
            // TODO: components

            var interfaces = new List<VariableType>();
            foreach ((UIndex interfaceUIndex, UIndex vftablePointerProperty) in uClass.Interfaces)
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
                        Types.Add(new Const(uConst.Export.ObjectName.Instanced, uConst.Value, null, null));
                        nextItem = uConst.Next;
                        break;
                    case UEnum uEnum:
                        Types.Add(ConvertEnum(uEnum));
                        nextItem = uEnum.Next;
                        break;
                    case UFunction uFunction:
                        Funcs.Add(ConvertFunction(uFunction));
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
                        States.Add(ConvertState(uState));
                        break;
                    default:
                        nextItem = 0;
                        break;
                }
            }
            var propObject = pcc.GetUExport(uClass.Defaults);
            var defaultProperties = ConvertDefaultProperties(propObject);

            Class AST = new Class(uClass.Export.ObjectName.Instanced, parent, outer, uClass.ClassFlags, interfaces, Types, Vars, Funcs, States, new List<OperatorDeclaration>(), defaultProperties)
            {
                ConfigName = uClass.ClassConfigName
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


            return AST;
        }

        public static State ConvertState(UState obj)
        {
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
                        Funcs.Add(ConvertFunction(uFunction));
                        nextItem = uFunction.Next;
                        break;
                    case UFunction uFunction:
                        Ignores.Add(new Function(uFunction.Export.ObjectName.Instanced, default, null, null, null, null, null));
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

            var ByteCode = new ME3ByteCodeDecompiler(obj, new List<FunctionParameter>());
            var body = ByteCode.Decompile();

            return new State(obj.Export.ObjectName.Instanced, body, obj.StateFlags, parent, Funcs, Ignores, new List<StateLabel>(), null, null);
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
            var vals = new List<VariableIdentifier>();
            foreach (var val in obj.Names)
            {
                vals.Add(new VariableIdentifier(val.Instanced, null, null));
            }

            var node = new Enumeration(obj.Export.ObjectName.Instanced, vals, null, null);

            foreach (var member in vals)
                member.Outer = node;

            return node;
        }

        public static VariableDeclaration ConvertVariable(UProperty obj)
        {
            int size = obj.ArraySize;

            return new VariableDeclaration(GetPropertyType(obj), obj.PropertyFlags,
                                           new VariableIdentifier(obj.Export.ObjectName.Instanced, null, null, size),
                                           obj.Category != "None" ? obj.Category : null, null, null);
        }

        private static VariableType GetPropertyType(UProperty obj)
        {
            string typeStr = "UNKNOWN";
            switch (obj)
            {
                case UArrayProperty arrayProperty:
                    return new DynamicArrayType(GetPropertyType(ObjectBinary.From(obj.Export.FileRef.GetUExport(arrayProperty.ElementType)) as UProperty));
                case UBioMask4Property _:
                    typeStr = "biomask4";
                    break;
                case UBoolProperty _:
                    typeStr = "bool";
                    break;
                case UByteProperty byteProperty:
                    typeStr = byteProperty.IsEnum ? byteProperty.Enum.GetEntry(obj.Export.FileRef).ObjectName.Instanced : "byte";
                    break;
                case UClassProperty _:
                    typeStr = "class";
                    break;
                case UDelegateProperty delegateProperty:
                {
                    IEntry function = obj.Export.FileRef.GetEntry(delegateProperty.Function);
                    IEntry functionClass = function.Parent;
                    for (IEntry delPropClass = delegateProperty.Export; delPropClass != null; delPropClass = delPropClass.Parent)
                    {
                        if (delPropClass.ClassName == "Class")
                        {
                            while (delPropClass != null)
                            {
                                if (delPropClass == functionClass)
                                {
                                    return new DelegateType(new Function(function?.ObjectName.Instanced, default, null, null, null));
                                }

                                delPropClass = (delPropClass as ExportEntry)?.SuperClass;
                            }
                            break;
                        }
                    }
                    //function is not in scope, fully qualify it
                    return new DelegateType(new Function(function?.InstancedFullPath, default, null, null, null));
                }
                case UFloatProperty _:
                    typeStr = "float";
                    break;
                case UIntProperty _:
                    typeStr = "int";
                    break;
                case UNameProperty _:
                    typeStr = "Name"; // ?
                    break;
                case UStringRefProperty _:
                    typeStr = "stringref";
                    break;
                case UStrProperty _:
                    typeStr = "string";
                    break;
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

        public static Function ConvertFunction(UFunction obj)
        {
            VariableType returnType = null;
            var nextItem = obj.Children;

            var parameters = new List<FunctionParameter>();
            var locals = new List<VariableDeclaration>();
            while (obj.Export.FileRef.TryGetUExport(nextItem, out ExportEntry nextChild))
            {
                var objBin = ObjectBinary.From(nextChild);
                switch (objBin)
                {
                    case UProperty uProperty:
                        if (uProperty.PropertyFlags.HasFlag(UnrealFlags.EPropertyFlags.ReturnParm))
                        {
                            returnType = ConvertVariable(uProperty).VarType;
                        }
                        else if (uProperty.PropertyFlags.HasFlag(UnrealFlags.EPropertyFlags.Parm))
                        {
                            var convert = ConvertVariable(uProperty);
                            parameters.Add(new FunctionParameter(convert.VarType, convert.Flags, convert.Variable, null, null));
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

            var ByteCode = new ME3ByteCodeDecompiler(obj, parameters);
            var body = ByteCode.Decompile();

            
            var func = new Function(obj.Export.ObjectName.Instanced,
                                    obj.FunctionFlags, returnType, body, parameters, null, null)
            {
                NativeIndex = obj.NativeIndex
            };

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
                var name = new SymbolReference(null, null, null, prop.Name);
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
                statements.Add(new AssignStatement(name, value, null, null));
            }



            return statements;

            Expression ConvertPropertyValue(Property prop)
            {
                switch (prop)
                {
                    case BoolProperty boolProperty:
                        return new BooleanLiteral(boolProperty.Value, null, null);
                    case ByteProperty byteProperty:
                        return new IntegerLiteral(byteProperty.Value, null, null);
                    case BioMask4Property bioMask4Property:
                        return new IntegerLiteral(bioMask4Property.Value, null, null);
                    case DelegateProperty delegateProperty:
                        return new SymbolReference(null, null, null, delegateProperty.Value.FunctionName);
                    case EnumProperty enumProperty:
                        return new SymbolReference(null, null, null, $"{enumProperty.EnumType.Instanced}.{enumProperty.Value.Instanced}");
                    case FloatProperty floatProperty:
                        return new FloatLiteral(floatProperty.Value, null, null);
                    case IntProperty intProperty:
                        return new IntegerLiteral(intProperty.Value, null, null);
                    case NameProperty nameProperty:
                        return new NameLiteral(nameProperty.Value, null, null);
                    case ObjectProperty objectProperty:
                        var objRef = objectProperty.Value;
                        if (objRef == 0)
                            return new SymbolReference(null, null, null, "None");
                        var objEntry = containingExport.FileRef.GetEntry(objRef);
                        string objStr;
                        if (objEntry is ExportEntry objExp && objExp.Parent == containingExport)
                        {
                            string name = objExp.ObjectName.Instanced;
                            objStr = $"{name}";
                            if (statements.All(stmnt => (stmnt as Subobject)?.Name != name))
                            {
                                statements.Add(new Subobject(name, objExp.ClassName, ConvertProperties(objExp.GetProperties(), objExp)));
                            }
                        }
                        else
                        {
                            objStr = $"{objEntry.ClassName}'{objEntry.InstancedFullPath}'";
                        }
                        return new SymbolReference(null, null, null, objStr);
                    case StringRefProperty stringRefProperty:
                        return new StringRefLiteral(stringRefProperty.Value, null, null);
                    case StrProperty strProperty:
                        return new StringLiteral(strProperty.Value, null, null);
                    case StructProperty structProperty:
                        return new StructLiteral(structProperty.StructType, ConvertProperties(structProperty.Properties, containingExport));
                    case ArrayPropertyBase arrayPropertyBase:
                        return new DynamicArrayLiteral(arrayPropertyBase.Reference, arrayPropertyBase.Properties.Select(ConvertPropertyValue).ToList());
                    default:
                        return new SymbolReference(null, null, null, "UNSUPPORTED:" + prop.PropType);

                }
            }
        }
    }
}
