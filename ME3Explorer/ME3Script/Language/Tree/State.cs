using ME3Script.Analysis.Visitors;
using ME3Script.Language.Util;
using ME3Script.Utilities;
using System.Collections.Generic;
using ME3ExplorerCore.Unreal.BinaryConverters;

namespace ME3Script.Language.Tree
{
    public class State : ASTNode, IContainsLocals
    {
        public StateFlags Flags;
        public string Name;
        public CodeBody Body;
        public State Parent;
        public List<Function> Functions;
        public List<Function> Ignores;
        public List<VariableDeclaration> Locals { get; set; }
        public List<StateLabel> Labels;

        public State(string name, CodeBody body, StateFlags flags,
            State parent, List<Function> funcs, List<Function> ignores,
            List<StateLabel> labels, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.State, start, end)
        {
            Flags = flags;
            Name = name;
            Body = body;
            Parent = parent;
            Functions = funcs;
            Ignores = ignores;
            Labels = labels;
            Locals = new List<VariableDeclaration>();
            if (Body != null) Body.Outer = this;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Parent;
                if (Functions != null) foreach (Function function in Functions) yield return function;
                yield return Body;
                if (Ignores != null) foreach (Function function in Ignores) yield return function;
            }
        }
    }
}