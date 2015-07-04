using ME3Data.DataTypes.ScriptTypes;
using ME3Data.DataTypes.ScriptTypes.Properties;
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
        private Class AST;

        public ME3ObjectConverter(ME3Class classObject)
        {
            Object = classObject;
        }

        public Class ConvertClass()
        {
            // TODO: this is only for text decompiling, should extend to a full ast for modification.
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
            // TODO: states
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

            AST = new Class(Object.Name, new List<Specifier>(), Vars, Types, Funcs, 
                new List<State>(), parent, outer, new List<OperatorDeclaration>(), null, null);
            return AST;
        }

        public Struct ConvertStruct(ME3Struct obj)
        {
            var Vars = new List<VariableDeclaration>();
            foreach (var member in obj.Variables)
                Vars.Add(ConvertVariable(member));
            VariableType parent = obj.SuperField != null 
                ? new VariableType(obj.SuperField.Name, null, null) : null;
            return new Struct(obj.Name, null, Vars, null, null, parent);
        }

        public Enumeration ConvertEnum(ME3Enum obj)
        {
            var vals = new List<VariableIdentifier>();
            foreach (var val in obj.Names)
                vals.Add(new VariableIdentifier(val, null, null));

            return new Enumeration(obj.Name, vals, null, null);
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
                typeStr = "component"; // TODO: is this correct at all?
            else if (obj is ME3DelegateProperty)
                typeStr = "delegate< " + (obj as ME3DelegateProperty).Delegate.Name + " >";
            else if (obj is ME3FixedArrayProperty)
                typeStr = GetPropertyType((obj as ME3FixedArrayProperty).InnerProperty);
            else if (obj is ME3FloatProperty)
                typeStr = "float";
            else if (obj is ME3InterfaceProperty)
                typeStr = (obj as ME3InterfaceProperty).Interface.Name; // ?
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
            // TODO: resolve properties
            VariableType returnType = null;
            if (obj.ReturnValue != null)
                returnType = new VariableType(obj.ReturnValue.ExportEntry.ClassName, null, null);
            var parameters = new List<FunctionParameter>();
            foreach(var param in obj.Parameters)
            {
                var convert = ConvertVariable(param);
                parameters.Add(new FunctionParameter(convert.VarType, 
                    convert.Specifiers, convert.Variables.First(), 
                    null, null));
            }

            return new Function(obj.Name, returnType, 
                new CodeBody(new List<Statement>(), null, null),
                new List<Specifier>(), parameters, null, null);
        }
    }
}
