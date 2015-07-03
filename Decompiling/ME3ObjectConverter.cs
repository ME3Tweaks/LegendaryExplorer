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
            var parent = new VariableType(Object.SuperField.Name, null, null);
            var outer = new VariableType(Object.OuterClass.Name, null, null);
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
                Vars.Add(ConvertFunction(member));

            AST = new Class(Object.Name, null, Vars, Types, Funcs, null, 
                parent, outer, null, null, null);
            return AST;
        }

        public Struct ConvertStruct(ME3Struct obj)
        {
            return null;
        }

        public Struct ConvertEnum(ME3Enum obj)
        {
            return null;
        }

        public Variable ConvertVariable(ME3Property obj)
        {
            return null;
        }

        public Variable ConvertFunction(ME3Function obj)
        {
            return null;
        }
    }
}
