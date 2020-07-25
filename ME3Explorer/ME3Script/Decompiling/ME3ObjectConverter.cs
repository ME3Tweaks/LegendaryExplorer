
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

            VariableType parent = new VariableType(pcc.GetEntry(uClass.SuperClass)?.ObjectName ?? "object", null, null);

            VariableType outer = new VariableType(pcc.GetEntry(uClass.OuterClass)?.ObjectName ?? parent.Name, null, null);
            // TODO: specifiers
            // TODO: operators
            // TODO: components
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
                        Types.Add(new Const(uConst.Export.ObjectName, uConst.Value, null, null));
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

            var specifiers = new List<Specifier>();
            foreach (UnrealFlags.EClassFlags classFlag in uClass.ClassFlags.MaskToList())
            {
                switch (classFlag)
                {
                    case UnrealFlags.EClassFlags.Abstract:
                        specifiers.Add(new Specifier("abstract"));
                        break;
                    case UnrealFlags.EClassFlags.Config:
                        specifiers.Add(new ConfigSpecifier(uClass.ClassConfigName));
                        break;
                    case UnrealFlags.EClassFlags.NoExport:
                        specifiers.Add(new Specifier("noexport"));
                        break;
                    case UnrealFlags.EClassFlags.Placeable:
                        specifiers.Add(new Specifier("placeable"));
                        break;
                    case UnrealFlags.EClassFlags.NativeReplication:
                        specifiers.Add(new Specifier("nativereplication"));
                        break;
                }
            }
            var propObject = pcc.GetUExport(uClass.Defaults);
            var defaultProperties = ConvertDefaultProperties(propObject);

            Class AST = new Class(uClass.Export.ObjectName, specifiers, Vars, Types, Funcs, 
                                  States, parent, outer, new List<OperatorDeclaration>(), defaultProperties, null, null);

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
                        Ignores.Add(new Function(uFunction.Export.ObjectName, null, null, null, null, false, null, null));
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

            var specifiers = new List<Specifier>();
            foreach (StateFlags flag in obj.StateFlags.MaskToList())
            {
                switch (flag)
                {
                    case StateFlags.Auto:
                        specifiers.Add(new Specifier("auto"));
                        break;
                    case StateFlags.Simulated:
                        specifiers.Add(new Specifier("simulated"));
                        break;
                }
            }
            return new State(obj.Export.ObjectName, body, specifiers, parent, Funcs, Ignores, new List<StateLabel>(), null, null);
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

            var specifiers = new List<Specifier>();
            foreach (ScriptStructFlags flag in obj.StructFlags.MaskToList())
            {
                switch (flag)
                {
                    case ScriptStructFlags.Immutable:
                        specifiers.Add(new Specifier("immutable"));
                        break;
                    case ScriptStructFlags.ImmutableWhenCooked:
                        specifiers.Add(new Specifier("immutablewhencooked"));
                        break;
                    case ScriptStructFlags.Native:
                        specifiers.Add(new Specifier("native"));
                        break;
                    case ScriptStructFlags.Transient:
                        specifiers.Add(new Specifier("transient"));
                        break;
                }
            }
            var node = new Struct(obj.Export.ObjectName, specifiers, Vars, null, null, parent);

            foreach (var member in Vars)
                member.Outer = node;

            return node;
        }

        public static Enumeration ConvertEnum(UEnum obj)
        {
            var vals = new List<VariableIdentifier>();
            foreach (var val in obj.Names)
            {
                vals.Add(new VariableIdentifier(val, null, null));
            }

            var node = new Enumeration(obj.Export.ObjectName, vals, null, null);

            foreach (var member in vals)
                member.Outer = node;

            return node;
        }

        public static VariableDeclaration ConvertVariable(UProperty obj)
        {
            int size = obj.ArraySize;
            var specifiers = new List<Specifier>();
            foreach (UnrealFlags.EPropertyFlags propFlag in obj.PropertyFlags.MaskToList())
            {
                switch (propFlag)
                {
                    case UnrealFlags.EPropertyFlags.Native:
                        specifiers.Add(new Specifier("native"));
                        break;
                    case UnrealFlags.EPropertyFlags.Transient:
                        specifiers.Add(new Specifier("transient"));
                        break;
                    case UnrealFlags.EPropertyFlags.EditConst:
                        specifiers.Add(new Specifier("editconst"));
                        break;
                    case UnrealFlags.EPropertyFlags.Const:
                        specifiers.Add(new Specifier("const"));
                        break;
                    case UnrealFlags.EPropertyFlags.Interp:
                        specifiers.Add(new Specifier("interp"));
                        break;
                    case UnrealFlags.EPropertyFlags.EditorOnly:
                        specifiers.Add(new Specifier("editoronly"));
                        break;
                    //parm flags
                    case UnrealFlags.EPropertyFlags.OptionalParm:
                        specifiers.Add(new Specifier("optional"));
                        break;
                    case UnrealFlags.EPropertyFlags.OutParm:
                        specifiers.Add(new Specifier("out"));
                        break;
                    case UnrealFlags.EPropertyFlags.CoerceParm:
                        specifiers.Add(new Specifier("coerce"));
                        break;
                }
            }

            return new VariableDeclaration(GetPropertyType(obj), specifiers,
                                           new VariableIdentifier(obj.Export.ObjectName, null, null, size),
                                           obj.Category != "None" ? obj.Category : null, null, null);
        }

        private static VariableType GetPropertyType(UProperty obj)
        {
            string typeStr = "UNKNOWN";
            switch (obj)
            {
                case UArrayProperty arrayProperty: //TODO: create ArrayVariableType?
                    typeStr = "array< " + GetPropertyType(ObjectBinary.From(obj.Export.FileRef.GetUExport(arrayProperty.ElementType)) as UProperty).Name + " >";
                    break;
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
                case UStringRefProperty _:
                    typeStr = "stringref";
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
                            parameters.Add(new FunctionParameter(convert.VarType, convert.Specifiers, convert.Variable, null, null)
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

            var specifiers = new List<Specifier>();
            bool isEvent = false;
            foreach (FunctionFlags funcFlag in obj.FunctionFlags.MaskToList())
            {
                switch (funcFlag)
                {
                    case FunctionFlags.Native when obj.NativeIndex > 0:
                        specifiers.Add(new Specifier($"native({obj.NativeIndex})"));
                        break;
                    case FunctionFlags.Native:
                        specifiers.Add(new Specifier("native"));
                        break;
                    case FunctionFlags.Static:
                        specifiers.Add(new Specifier("static"));
                        break;
                    case FunctionFlags.Simulated:
                        specifiers.Add(new Specifier("simulated"));
                        break;
                    case FunctionFlags.Net when !obj.FunctionFlags.Has(FunctionFlags.NetReliable):
                        specifiers.Add(new Specifier("unreliable"));
                        break;
                    case FunctionFlags.NetReliable:
                        specifiers.Add(new Specifier("reliable"));
                        break;
                    case FunctionFlags.NetServer:
                        specifiers.Add(new Specifier("server"));
                        break;
                    case FunctionFlags.NetClient:
                        specifiers.Add(new Specifier("client"));
                        break;
                    case FunctionFlags.Final:
                        specifiers.Add(new Specifier("final"));
                        break;
                    case FunctionFlags.PreOperator:
                        specifiers.Add(new Specifier("preoperator"));
                        break;
                    case FunctionFlags.Operator:
                        specifiers.Add(new Specifier("operator"));
                        break;
                    case FunctionFlags.Iterator:
                        specifiers.Add(new Specifier("iterator"));
                        break;
                    case FunctionFlags.Latent:
                        specifiers.Add(new Specifier("latent"));
                        break;
                    case FunctionFlags.Exec:
                        specifiers.Add(new Specifier("exec"));
                        break;
                    case FunctionFlags.Event:
                        isEvent = true;
                        break;
                    case FunctionFlags.Const:
                        specifiers.Add(new Specifier("const"));
                        break;
                }
            }
            var func = new Function(obj.Export.ObjectName, returnType, body,
                                    specifiers, parameters, isEvent, null, null);

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

            return new DefaultPropertiesBlock(new List<Statement>(defaults), null, null);
        }

        private static List<Statement> ConvertProperties(PropertyCollection properties, ExportEntry containingExport)
        {
            var statements = new List<Statement>();
            foreach (var prop in properties)
            {
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
