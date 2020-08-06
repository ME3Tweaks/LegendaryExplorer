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
    public abstract class OperatorDeclaration : ASTNode, IContainsLocals
    {
        public string OperatorKeyword;
        public bool isDelimiter;
        public FunctionFlags Flags;
        public int NativeIndex;
        public CodeBody Body;
        public List<VariableDeclaration> Locals { get; set; }
        public VariableType ReturnType;

        protected OperatorDeclaration(ASTNodeType type, string keyword, 
                                      bool delim, CodeBody body, VariableType returnType, FunctionFlags flags, SourcePosition start, SourcePosition end) 
            : base(type, start, end)
        {
            Flags = flags;
            OperatorKeyword = keyword;
            isDelimiter = delim;
            Body = body;
            ReturnType = returnType;
            Locals = new List<VariableDeclaration>();
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }

        public bool IdenticalSignature(OperatorDeclaration other)
        {
            if (this.ReturnType == null && other.ReturnType != null)
                return false;
            else if (other.ReturnType == null)
                return false;
                
            return this.OperatorKeyword == other.OperatorKeyword
                && this.ReturnType.Name.ToLower() == other.ReturnType.Name.ToLower();
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return ReturnType;
                foreach (VariableDeclaration variableDeclaration in Locals) yield return variableDeclaration;
                yield return Body;
            }
        }
    }
}
