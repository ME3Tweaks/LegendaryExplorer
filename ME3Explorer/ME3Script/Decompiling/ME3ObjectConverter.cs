
using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            VariableType parent = new VariableType(pcc.GetEntry(uClass.SuperClass)?.ObjectName ?? "object", null, null);

            VariableType outer = new VariableType(pcc.GetEntry(uClass.OuterClass)?.ObjectName ?? parent.Name, null, null);
            // TODO: specifiers
            // TODO: operators
            // TODO: components
            // TODO: constants
            // TODO: interfaces



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
                        //TODO
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

            Class AST = new Class(uClass.Export.ObjectName, new List<Specifier>(), Vars, Types, Funcs, 
                States, parent, outer, new List<OperatorDeclaration>(), null, null);

            // Ugly quick fix:
            foreach (var member in Types)
                member.Outer = AST;
            foreach (var member in Vars)
                member.Outer = AST;
            foreach (var member in Funcs)
                member.Outer = AST;
            foreach (var member in States)
                member.Outer = AST;

            var propObject = pcc.GetUExport(uClass.Defaults);
            if (propObject != null && propObject.GetProperties() is PropertyCollection props && props.Count != 0)
                AST.DefaultProperties = ConvertDefaultProperties(props, pcc);

            return AST;
        }

        public static State ConvertState(UState obj)
        {
            // TODO: ignores and body/labels

            State parent = null;
            if (obj.SuperClass != 0)
                parent = new State(obj.SuperClass.GetEntry(obj.Export.FileRef).ObjectName, null, null, null, null, null, null, null, null);

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
                        Ignores.Add(new Function(uFunction.Export.ObjectName, null, null, null, null, null, null));
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

            return new State(obj.Export.ObjectName, body, new List<Specifier>(), (State)parent, Funcs, new List<Function>(), new List<StateLabel>(), null, null);
        }

        public static Struct ConvertStruct(UScriptStruct obj)
        {
            var Vars = new List<VariableDeclaration>();
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
                    default:
                        nextItem = 0;
                        break;
                }
            }

            VariableType parent = obj.SuperClass != 0 
                ? new VariableType(obj.SuperClass.GetEntry(obj.Export.FileRef).ObjectName, null, null) : null;

            var node = new Struct(obj.Export.ObjectName, new List<Specifier>(), Vars, null, null, parent);

            foreach (var member in Vars)
                member.Outer = node;

            return node;
        }

        public static Enumeration ConvertEnum(UEnum obj)
        {
            var vals = new List<VariableIdentifier>();
            foreach (var val in obj.Names)
                vals.Add(new VariableIdentifier(val, null, null));

            var node = new Enumeration(obj.Export.ObjectName, vals, null, null);

            foreach (var member in vals)
                member.Outer = node;

            return node;
        }

        public static Variable ConvertVariable(UProperty obj)
        {
            int size = -1;
            return new Variable(new List<Specifier>(), 
                new VariableIdentifier(obj.Export.ObjectName, null, null, size), 
                new VariableType(GetPropertyType(obj), null, null), null, null);
        }

        private static string GetPropertyType(UProperty obj)
        {
            string typeStr = "UNKNOWN";
            switch (obj)
            {
                case UArrayProperty arrayProperty:
                    typeStr = "array< " + GetPropertyType(ObjectBinary.From(obj.Export.FileRef.GetUExport(arrayProperty.ElementType)) as UProperty) + " >";
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
                case UComponentProperty _:
                    typeStr = "ActorComponent"; // TODO: is this correct at all?
                    break;
                case UDelegateProperty delegateProperty:
                    typeStr = "delegate< " + delegateProperty.Function + " >";
                    break;
                case UFloatProperty _:
                    typeStr = "float";
                    break;
                case UInterfaceProperty interfaceProperty:
                    typeStr = interfaceProperty.ObjectRef.GetEntry(obj.Export.FileRef)?.ObjectName.Instanced ?? typeStr; // ?
                    break;
                case UIntProperty _:
                    typeStr = "int";
                    break;
                case UNameProperty _:
                    typeStr = "Name"; // ?
                    break;
                case UStrProperty _:
                    typeStr = "string";
                    break;
                case UStructProperty structProperty:
                    typeStr = structProperty.Struct.GetEntry(obj.Export.FileRef)?.ObjectName.Instanced ?? typeStr;
                    break;
                default:
                {
                    //if (obj is UObject)
                        typeStr = "object";
                    break;
                }
            }

            return typeStr;
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
                            parameters.Add(new FunctionParameter(convert.VarType, convert.Specifiers, convert.Variables.First(), null, null)
                            {
                                IsOptional = uProperty.PropertyFlags.HasFlag(UnrealFlags.EPropertyFlags.OptionalParm)
                            });
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

            var func = new Function(obj.Export.ObjectName, returnType, body,
                new List<Specifier>(), parameters, null, null);

            foreach (var local in locals)
            {
                local.Outer = func;
            }
            func.Locals = locals;
            return func;
        }

        public static DefaultPropertiesBlock ConvertDefaultProperties(PropertyCollection properties, IMEPackage pcc)
        {
            var defaults = new List<Statement>();
            foreach (var prop in properties)
            {
                var name = new SymbolReference(null, null, null, prop.Name);
                var value = ConvertPropertyValue(prop, pcc);
                defaults.Add(new AssignStatement(name, value, null, null));
            }

            return new DefaultPropertiesBlock(defaults, null, null);
        }

        public static Expression ConvertPropertyValue(Property prop, IMEPackage pcc)
        {
            switch (prop)
            {
                case BoolProperty boolProperty:
                    return new BooleanLiteral(boolProperty.Value, null, null);
                case ByteProperty byteProperty:
                    return new IntegerLiteral(byteProperty.Value, null, null);
                case DelegateProperty delegateProperty:
                    return new SymbolReference(null, null, null, delegateProperty.Value.FunctionName);
                case EnumProperty enumProperty:
                    return new SymbolReference(null, null, null, $"{enumProperty.EnumType.Instanced}.{enumProperty.Value.Instanced}");
                case FloatProperty floatProperty:
                    return new FloatLiteral(floatProperty.Value, null, null);
                case IntProperty intProperty:
                    return new IntegerLiteral(intProperty.Value, null, null);
                case NameProperty nameProperty:
                    return new NameLiteral(nameProperty.Name, null, null);
                case ObjectProperty objectProperty:
                    var objRef = objectProperty.Value;
                    if (objRef == 0)
                        return new SymbolReference(null, null, null, "None");
                    var objEntry = pcc.GetEntry(objRef);
                    var objStr = $"{objEntry.ClassName}'{objEntry.InstancedFullPath}'";
                    return new SymbolReference(null, null, null, objStr);
                case StringRefProperty stringRefProperty:
                    return new StringRefLiteral(stringRefProperty.Value, null, null);
                case StrProperty strProperty:
                    return new StringLiteral(strProperty.Value, null, null);
                case ArrayPropertyBase arrayPropertyBase:
                case StructProperty structProperty:
                default:
                    return new SymbolReference(null, null, null, "UNSUPPORTED:" + prop.PropType); //TODO

            }
        }
    }
}
