using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Unreal.BinaryConverters;

namespace ME3Script.Language.Tree
{
    public class InOpDeclaration : OperatorDeclaration
    {
        public FunctionParameter LeftOperand;
        public FunctionParameter RightOperand;
        public int Precedence;

        public InOpDeclaration(string keyword, int precedence,
        bool delim, CodeBody body, VariableType returnType,
        FunctionParameter leftOp, FunctionParameter rightOp,
        FunctionFlags flags, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.InfixOperator, keyword, delim, body, returnType, flags, start, end)
        {
            LeftOperand = leftOp;
            RightOperand = rightOp;
            Precedence = precedence;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public bool IdenticalSignature(InOpDeclaration other)
        {
            return base.IdenticalSignature(other)
                && this.LeftOperand.VarType.Name.ToLower() == other.LeftOperand.VarType.Name.ToLower()
                && this.RightOperand.VarType.Name.ToLower() == other.RightOperand.VarType.Name.ToLower();
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return LeftOperand;
                yield return RightOperand;
            }
        }
    }
}