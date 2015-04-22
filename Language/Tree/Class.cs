using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class Class : VariableType
    {
        public VariableType Parent;
        public VariableType OuterClass;
        public List<Specifier> Specifiers;
        public List<VariableDeclaration> VariableDeclarations;
        public List<VariableType> TypeDeclarations;
        public List<Function> Functions;
        public List<State> States;
        public List<OperatorDeclaration> Operators;

        public Class(String name, List<Specifier> specs, 
            List<VariableDeclaration> vars, List<VariableType> types, List<Function> funcs,
            List<State> states, VariableType parent, VariableType outer, List<OperatorDeclaration> ops,
            SourcePosition start, SourcePosition end)
            : base(name, start, end)
        {
            Parent = parent;
            OuterClass = outer;
            Specifiers = specs;
            VariableDeclarations = vars;
            TypeDeclarations = types;
            Functions = funcs;
            States = states;
            Operators = ops;
            Type = ASTNodeType.Class;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        #region Helpers

        public bool SameOrSubClass(String name)
        {
            String nodeName = this.Name.ToLower();
            String inputName = name.ToLower();
            if (nodeName == inputName)
                return true;
            Class current = this;
            while (current.Parent != null && current.Parent.Name.ToLower() != "object")
            {
                if (current.Parent.Name.ToLower() == inputName)
                    return true;
                current = (Class)current.Parent;
            }
            return false;
        }

        public String GetInheritanceString()
        {
            String str = this.Name;
            Class current = this;
            while (current.Parent != null)
            {
                current = current.Parent as Class;
                str = current.Name + "." + str; 
            }
            return str;
        }

        #endregion
    }
}
