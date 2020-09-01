using ME3Script.Analysis.Visitors;
using ME3Script.Language.Util;
using ME3Script.Utilities;
using System.Collections.Generic;
using ME3ExplorerCore.Unreal.BinaryConverters;

namespace ME3Script.Language.Tree
{
    public class Function : ASTNode, IContainsLocals
    {
        public string Name { get; }
        public CodeBody Body;
        public List<VariableDeclaration> Locals { get; set; }
        public VariableType ReturnType;
        public FunctionFlags Flags;
        public List<FunctionParameter> Parameters;

        public int NativeIndex;

        public DelegateType VarType;

        public bool IsNative => Flags.Has(FunctionFlags.Native);

        public bool IsDefined => Flags.Has(FunctionFlags.Defined);

        public Function(string name, FunctionFlags flags, 
                        VariableType returntype, CodeBody body,
                        List<FunctionParameter> parameters = null,
                        SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.Function, start, end)
        {
            Name = name;
            Body = body;
            ReturnType = returntype;
            Flags = flags;
            Parameters = parameters ?? new List<FunctionParameter>();
            Locals = new List<VariableDeclaration>();
            VarType = new DelegateType(this)
            {
                IsFunction = true,
                Declaration = this
            };
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
                if(ReturnType != null) yield return ReturnType;
                foreach (FunctionParameter functionParameter in Parameters) yield return functionParameter;
                foreach (VariableDeclaration variableDeclaration in Locals) yield return variableDeclaration;
                if (Body != null) yield return Body;
            }
        }
    }
}
