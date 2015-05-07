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
    public class Function : ASTNode, IContainsLocals
    {
        public String Name;
        public CodeBody Body;
        public List<VariableDeclaration> Locals;
        public VariableType ReturnType;
        public List<Specifier> Specifiers;
        public List<FunctionParameter> Parameters;

        public Function(String name, VariableType returntype, CodeBody body,
            List<Specifier> specs, List<FunctionParameter> parameters,
            SourcePosition start, SourcePosition end)
            : base(ASTNodeType.Function, start, end)
        {
            Name = name;
            Body = body;
            ReturnType = returntype;
            Specifiers = specs;
            Parameters = parameters;
            Locals = new List<VariableDeclaration>();
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
