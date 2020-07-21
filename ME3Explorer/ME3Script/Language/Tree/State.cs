using ME3Script.Analysis.Visitors;
using ME3Script.Language.Util;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class State : ASTNode, IContainsLocals
    {
        public String Name;
        public CodeBody Body;
        public List<Specifier> Specifiers;
        public State Parent;
        public List<Function> Functions;
        public List<Function> Ignores;
        public List<VariableDeclaration> Locals { get; set; }
        public List<StateLabel> Labels;

        public State(String name, CodeBody body, List<Specifier> specs,
            State parent, List<Function> funcs, List<Function> ignores,
            List<StateLabel> labels, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.State, start, end)
        {
            Name = name;
            Body = body;
            Specifiers = specs;
            Parent = parent;
            Functions = funcs;
            Ignores = ignores;
            Labels = labels;
            Locals = new List<VariableDeclaration>();
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}