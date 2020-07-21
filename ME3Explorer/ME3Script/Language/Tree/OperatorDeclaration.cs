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
    public abstract class OperatorDeclaration : ASTNode, IContainsLocals
    {
        public String OperatorKeyword;
        public bool isDelimiter;
        public CodeBody Body;
        public List<VariableDeclaration> Locals { get; set; }
        public VariableType ReturnType;
        public List<Specifier> Specifiers;

        public OperatorDeclaration(ASTNodeType type, String keyword, 
            bool delim, CodeBody body, VariableType returnType,
            List<Specifier> specs, SourcePosition start, SourcePosition end) 
            : base(type, start, end)
        {
            OperatorKeyword = keyword;
            isDelimiter = delim;
            Body = body;
            ReturnType = returnType;
            Specifiers = specs;
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
    }
}
