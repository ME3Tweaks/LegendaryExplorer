using ME3Data.DataTypes;
using ME3Data.DataTypes.ScriptTypes;
using ME3Data.DataTypes.ScriptTypes.DefaultProperties;
using ME3Data.DataTypes.ScriptTypes.Properties;
using ME3Data.FileFormats.PCC;
using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Decompiling
{
    public class ME3ObjectConverter
    {
        private ME3Class Object;
        private PCCFile PCC { get { return Object.ExportEntry.CurrentPCC; } }
        private Class AST;

        public ME3ObjectConverter(ME3Class classObject)
        {
            Object = classObject;
        }

        public Class ConvertClass() // TODO: this is only for text decompiling, should extend to a full ast for modification.
        {
            VariableType parent;
            if (Object.SuperField != null)
                parent = new VariableType(Object.SuperField.Name, null, null);
            else
                parent = new VariableType("object", null, null);

            VariableType outer;
            if (Object.OuterClass != null)
                outer = new VariableType(Object.OuterClass.Name, null, null);
            else
                outer = new VariableType(parent.Name, null, null);
            // TODO: specifiers
            // TODO: operators
            // TODO: components
            // TODO: constants
            // TODO: interfaces

            var Types = new List<VariableType>();
            foreach (var member in Object.Structs)
                Types.Add(ConvertStruct(member));
            foreach (var member in Object.Enums)
                Types.Add(ConvertEnum(member));

            var Vars = new List<VariableDeclaration>();
            foreach (var member in Object.Variables)
                Vars.Add(ConvertVariable(member));

            var Funcs = new List<Function>();
            foreach (var member in Object.DefinedFunctions)
                Funcs.Add(ConvertFunction(member));

            var States = new List<State>();
            foreach (var member in Object.States)
                States.Add(ConvertState(member));

            AST = new Class(Object.Name, new List<Specifier>(), Vars, Types, Funcs, 
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

            var propObject = PCC.GetExportObject(Object.DefaultPropertyIndex);
            if (propObject != null && propObject.DefaultProperties != null && propObject.DefaultProperties.Count != 0)
                AST.DefaultProperties = ConvertDefaultProperties(propObject.DefaultProperties);

            return AST;
        }

        public State ConvertState(ME3State obj)
        {
            // TODO: ignores and body/labels

            State parent = null;
            if (obj.SuperField != null)
                parent = new State(obj.SuperField.Name, null, null, null, null, null, null, null, null);

            var Funcs = new List<Function>();
            var Ignores = new List<Function>();
            foreach (var member in obj.DefinedFunctions)
            {
                if (member.FunctionFlags.HasFlag(FunctionFlags.Defined))
                    Funcs.Add(ConvertFunction(member));
                else
                    Ignores.Add(new Function(member.Name, null, null,
                         null, null, null, null));
                /* Ignored functions are not marked as defined, so we dont need to lookup the ignormask.
                 * They are defined though, each being its own proper object with simply a return nothing for bytecode.
                 * */
            }

            var ByteCode = new ME3ByteCodeDecompiler(obj, new List<FunctionParameter>());
            var body = ByteCode.Decompile();

            return new State(obj.Name, body, new List<Specifier>(), (State)parent, Funcs, new List<Function>(), new List<StateLabel>(), null, null);
        }

        public Struct ConvertStruct(ME3Struct obj)
        {
            var Vars = new List<VariableDeclaration>();
            foreach (var member in obj.Variables)
                Vars.Add(ConvertVariable(member));

            VariableType parent = obj.SuperField != null 
                ? new VariableType(obj.SuperField.Name, null, null) : null;

            var node = new Struct(obj.Name, new List<Specifier>(), Vars, null, null, parent);

            foreach (var member in Vars)
                member.Outer = node;

            return node;
        }

        public Enumeration ConvertEnum(ME3Enum obj)
        {
            var vals = new List<VariableIdentifier>();
            foreach (var val in obj.Names)
                vals.Add(new VariableIdentifier(val, null, null));

            var node = new Enumeration(obj.Name, vals, null, null);

            foreach (var member in vals)
                member.Outer = node;

            return node;
        }

        public Variable ConvertVariable(ME3Property obj)
        {
            int size = -1;
            if (obj is ME3FixedArrayProperty)
                size = (obj as ME3FixedArrayProperty).ArraySize;
            return new Variable(new List<Specifier>(), 
                new VariableIdentifier(obj.Name, null, null, size), 
                new VariableType(GetPropertyType(obj), null, null), null, null);
        }

        private String GetPropertyType(ME3Property obj)
        {
            String typeStr = "UNKNOWN";
            if (obj is ME3ArrayProperty)
                typeStr = "array< " + GetPropertyType((obj as ME3ArrayProperty).InnerProperty) + " >";
            else if (obj is ME3BoolProperty)
                typeStr = "bool";
            else if (obj is ME3ByteProperty)
                typeStr = (obj as ME3ByteProperty).IsEnum ? (obj as ME3ByteProperty).Enum.Name : "byte";
            else if (obj is ME3ClassProperty)
                typeStr = "class";
            else if (obj is ME3ComponentProperty)
                typeStr = "ActorComponent"; // TODO: is this correct at all?
            else if (obj is ME3DelegateProperty)
                typeStr = "delegate< " + (obj as ME3DelegateProperty).FunctionName + " >";
            else if (obj is ME3FixedArrayProperty)
                typeStr = GetPropertyType((obj as ME3FixedArrayProperty).InnerProperty);
            else if (obj is ME3FloatProperty)
                typeStr = "float";
            else if (obj is ME3InterfaceProperty)
                typeStr = (obj as ME3InterfaceProperty).InterfaceName; // ?
            else if (obj is ME3IntProperty)
                typeStr = "int";
            else if (obj is ME3NameProperty)
                typeStr = "Name"; // ?
            else if (obj is ME3Object)
                typeStr = "object";
            else if (obj is ME3StrProperty)
                typeStr = "string";
            else if (obj is ME3StructProperty)
                typeStr = (obj as ME3StructProperty).Struct.Name;

            return typeStr;
        }

        public Function ConvertFunction(ME3Function obj)
        {
            VariableType returnType = null;
            if (obj.ReturnValue != null)
                returnType = ConvertVariable(obj.ReturnValue).VarType;
            var parameters = new List<FunctionParameter>();
            foreach(var param in obj.Parameters)
            {
                var convert = ConvertVariable(param);
                parameters.Add(new FunctionParameter(convert.VarType, 
                    convert.Specifiers, convert.Variables.First(), 
                    null, null));
            }

            var ByteCode = new ME3ByteCodeDecompiler(obj, parameters);
            var body = ByteCode.Decompile();

            var func = new Function(obj.Name, returnType, body,
                new List<Specifier>(), parameters, null, null);

            var locals = new List<VariableDeclaration>();
            foreach (var local in obj.LocalVariables)
            {
                var convert = ConvertVariable(local);
                convert.Outer = func;
                locals.Add(convert);
            }
            func.Locals = locals;
            return func;
        }

        public DefaultPropertiesBlock ConvertDefaultProperties(List<ME3DefaultProperty> properties)
        {
            var defaults = new List<Statement>();
            foreach (var prop in properties)
            {
                        var name = new SymbolReference(null, null, null, prop.Name);
                        var value = ConvertPropertyValue(prop);
                        defaults.Add(new AssignStatement(name, value, null, null));
            }

            return new DefaultPropertiesBlock(defaults, null, null);
        }

        public Expression ConvertPropertyValue(ME3DefaultProperty prop)
        {
            switch (prop.Type)
            {
                case PropertyType.BoolProperty:
                    return new BooleanLiteral((prop.Value as BoolPropertyValue).Value, null, null);
                case PropertyType.ByteProperty:
                    if (prop.Value is EnumPropertyValue) // TODO:
                    {
                        var enumRef = prop.Value as EnumPropertyValue;
                        var enumStr = enumRef.EnumName + "." + enumRef.EnumValue;
                        return new SymbolReference(null, null, null, enumStr);
                    }
                    else
                        return new IntegerLiteral((prop.Value as BytePropertyValue).Value, null, null);
                case PropertyType.IntProperty:
                    return new IntegerLiteral((prop.Value as IntPropertyValue).Value, null, null);
                case PropertyType.FloatProperty:
                    return new FloatLiteral((prop.Value as FloatPropertyValue).Value, null, null);
                case PropertyType.StrProperty:
                    return new StringLiteral((prop.Value as StrPropertyValue).Value, null, null);
                case PropertyType.StringRefProperty:
                    return new IntegerLiteral((prop.Value as StringRefPropertyValue).Value, null, null); // TODO
                case PropertyType.NameProperty:
                    return new NameLiteral((prop.Value as NamePropertyValue).Name, null, null);
                case PropertyType.ObjectProperty:
                    var objRef = prop.Value as ObjectPropertyValue;
                    if (objRef.Index == 0)
                        return new SymbolReference(null, null, null, "None");
                    var objEntry = objRef.PCC.GetObjectEntry(objRef.Index); // TODO: maybe qualifying like this is unnecessary?
                    var outer = objEntry.GetOuterTreeString();
                    var objStr = objEntry.ClassName + "'" + (outer != "" ? (outer + ".") : outer) + objEntry.ObjectName + "'";
                    return new SymbolReference(null, null, null, objStr);
                case PropertyType.DelegateProperty:
                    return new SymbolReference(null, null, null, (prop.Value as DelegatePropertyValue).DelegateValue); // TODO: verify
                case PropertyType.StructProperty:
                    return new SymbolReference(null, null, null, "UNSUPPORTED:" + prop.Type);
                case PropertyType.ArrayProperty:
                    return new SymbolReference(null, null, null, "UNSUPPORTED:" + prop.Type);
                case PropertyType.InterfaceProperty:
                    return new SymbolReference(null, null, null, PCC.GetObjectEntry((prop.Value as InterfacePropertyValue).Index).ObjectName); // TODO: verify

                default:
                    return new SymbolReference(null, null, null, "UNSUPPORTED:" + prop.Type);

            }
        }
    }
}
