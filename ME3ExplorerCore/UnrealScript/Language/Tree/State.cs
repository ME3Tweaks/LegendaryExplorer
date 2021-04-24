using System.Collections.Generic;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Language.Util;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
{
    public class State : ASTNode, IContainsByteCode, IHasFileReference
    {
        public StateFlags Flags;
        public string Name { get; }
        public CodeBody Body { get; set; }
        public State Parent;
        public List<Function> Functions;
        public List<Function> Ignores;
        public List<Label> Labels;

        public State(string name, CodeBody body, StateFlags flags,
            State parent, List<Function> funcs, List<Function> ignores,
            List<Label> labels, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.State, start, end)
        {
            Flags = flags;
            Name = name;
            Body = body;
            Parent = parent;
            Functions = funcs;
            Ignores = ignores;
            Labels = labels;
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

        public string FilePath { get; init; }
        public int UIndex { get; init; }
    }
}