using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class State : ASTNode
    {
        public String Name;
        public CodeBody Body;
        public List<Specifier> Specifiers;
        public VariableType Parent;
        public List<Function> Functions;
        public List<Variable> Ignores;
        public List<StateLabel> Labels;

        public State(String name, CodeBody body, List<Specifier> specs,
            VariableType parent, List<Function> funcs, List<Variable> ignores,
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
        }

        public override bool VisitNode(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}