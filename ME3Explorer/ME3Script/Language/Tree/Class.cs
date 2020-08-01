using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public sealed class Class : VariableType
    {
        public VariableType Parent;
        public VariableType OuterClass;
        public List<Specifier> Specifiers;
        public List<VariableDeclaration> VariableDeclarations;
        public List<VariableType> TypeDeclarations;
        public List<Function> Functions;
        public List<State> States;
        public List<OperatorDeclaration> Operators;
        public DefaultPropertiesBlock DefaultProperties;

        public Class(string name, List<Specifier> specs,
                     List<VariableDeclaration> vars, List<VariableType> types, List<Function> funcs,
                     List<State> states, VariableType parent, VariableType outer, List<OperatorDeclaration> ops,
                     DefaultPropertiesBlock defaultProperties,
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
            DefaultProperties = defaultProperties;
            Type = ASTNodeType.Class;

            foreach (ASTNode node in ChildNodes)
            {
                node.Outer = this;
            }
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        #region Helpers

        public bool SameOrSubClass(string name)
        {
            string nodeName = this.Name.ToLower();
            string inputName = name.ToLower();
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

        public string GetInheritanceString()
        {
            string str = this.Name;
            Class current = this;
            while (current.Parent != null)
            {
                current = current.Parent as Class;
                str = current.Name + "." + str; 
            }
            return str;
        }

        #endregion
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Parent;
                if (OuterClass != null) yield return OuterClass;
                foreach (Specifier specifier in Specifiers) yield return specifier;
                foreach (VariableType typeDeclaration in TypeDeclarations) yield return typeDeclaration;
                foreach (VariableDeclaration variableDeclaration in VariableDeclarations) yield return variableDeclaration;
                foreach (Function function in Functions) yield return function;
                foreach (State state in States) yield return state;
                foreach (OperatorDeclaration operatorDeclaration in Operators) yield return operatorDeclaration;
                yield return DefaultProperties;
            }
        }
    }
}
