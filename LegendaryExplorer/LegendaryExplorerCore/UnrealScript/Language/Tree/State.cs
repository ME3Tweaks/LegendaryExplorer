using System.Collections.Generic;
using System.Diagnostics;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Language.Util;
using LegendaryExplorerCore.UnrealScript.Parsing;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    [DebuggerDisplay("State | {Name}")]
    public class State : ASTNode, IContainsByteCode, IHasFileReference, IContainsFunctions
    {
        public TokenStream Tokens { get; init; }

        public readonly EStateFlags Flags;
        public string Name { get; }
        public CodeBody Body { get; set; }
        public State Parent;
        public List<Function> Functions { get; }
        public EProbeFunctions IgnoreMask = (EProbeFunctions)ulong.MaxValue;
        public List<Label> Labels;

        public State(string name, CodeBody body, EStateFlags flags, State parent, List<Function> funcs, List<Label> labels, int start, int end)
            : base(ASTNodeType.State, start, end)
        {
            Flags = flags;
            Name = name;
            Body = body;
            Parent = parent;
            Functions = funcs;
            Labels = labels;
            if (Body != null) Body.Outer = this;
        }

        public override bool AcceptVisitor(IASTVisitor visitor, UnrealScriptOptionsPackage usop)
        {
            return visitor.VisitNode(this, usop);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Parent;
                if (Functions != null) foreach (Function function in Functions) yield return function;
                yield return Body;
            }
        }

        public string GetScope()
        {
            return $"{((Class)Outer).GetScope()}.{Name}";
        }

        public string FilePath { get; init; }
        public int UIndex { get; init; }
    }
}