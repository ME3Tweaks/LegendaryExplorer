using ME3Script.Analysis.Visitors;
using ME3Script.Language.Util;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Unreal.BinaryConverters;

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

        public Function(string name, VariableType returntype, CodeBody body,
                        FunctionFlags flags, List<FunctionParameter> parameters,
                        SourcePosition start, SourcePosition end)
            : base(ASTNodeType.Function, start, end)
        {
            Name = name;
            Body = body;
            ReturnType = returntype;
            Flags = flags;
            Parameters = parameters;
            Locals = new List<VariableDeclaration>();
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return ReturnType;
                foreach (FunctionParameter functionParameter in Parameters) yield return functionParameter;
                foreach (VariableDeclaration variableDeclaration in Locals) yield return variableDeclaration;
                yield return Body;
            }
        }
    }
}
