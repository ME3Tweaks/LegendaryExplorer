using ME3Data.DataTypes.ScriptTypes;
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
            // TODO: interfaces
            // TODO: components
            // TODO: constants
            // TODO: states

            var Types = new List<VariableType>();
            foreach (var member in Object.Structs)
                Types.Add(ConvertStruct(member));
            foreach (var member in Object.Enums)
                Types.Add(ConvertEnum(member));

            var Vars = new List<VariableDeclaration>();
            foreach (var member in Object.Variables)
                Vars.Add(ConvertVariable(member));

            var Funcs = new List<Function>();
            foreach (var member in Object.FunctionRefs)
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
            // TODO: fix
            var type = new VariableType(obj.ExportEntry.ClassName, null, null);
            return new Variable(new List<Specifier>(), 
                new VariableIdentifier(obj.Name, null, null), type, null, null);
        }

        public Function ConvertFunction(ME3Function obj)
        {
            VariableType returnType = null;
            if (obj.ReturnValue != null)
                returnType = new VariableType(obj.ReturnValue.Name, null, null);
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
