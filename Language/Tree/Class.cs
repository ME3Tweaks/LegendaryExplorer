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

        public override bool VisitNode(IASTVisitor visitor)
        {
            bool status = true;
            status = this.VisitNode(visitor);
            foreach (VariableType type in TypeDeclarations)
                status = type.VisitNode(visitor);
            foreach (VariableDeclaration decl in VariableDeclarations)
                status = decl.VisitNode(visitor);
            foreach (VariableDeclaration decl in VariableDeclarations)
                status = decl.VisitNode(visitor);
            foreach (OperatorDeclaration op in Operators)
                status = op.VisitNode(visitor);
            foreach (Function func in Functions)
                status = func.VisitNode(visitor);
            foreach (State state in States)
                status = state.VisitNode(visitor);
            return status;
        }

        #region Helpers

        public bool Extends(String name)
        {
            Class current = this;
            while (current.Parent.Name != "Object")
            {
                if (current.Parent.Name == name)
                    return true;
                current = (Class)current.Parent;
            }
            return false;
        }

        #endregion
    }
}
