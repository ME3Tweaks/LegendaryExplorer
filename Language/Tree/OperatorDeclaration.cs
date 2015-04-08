using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public abstract class OperatorDeclaration : ASTNode
    {
        public String OperatorKeyword;
        public bool isDelimiter;
        public CodeBody Body;
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
        }

        public override bool VisitNode(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
