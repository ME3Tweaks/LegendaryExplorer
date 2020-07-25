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
        public string Name { get; }
        public CodeBody Body;
        public List<VariableDeclaration> Locals { get; set; }
        public VariableType ReturnType;
        public List<Specifier> Specifiers;
        public List<FunctionParameter> Parameters;

        public bool IsEvent;

        public Function(string name, VariableType returntype, CodeBody body,
                        List<Specifier> specs, List<FunctionParameter> parameters,
                        bool isEvent,
                        SourcePosition start, SourcePosition end)
            : base(ASTNodeType.Function, start, end)
        {
            Name = name;
            Body = body;
            ReturnType = returntype;
            Specifiers = specs;
            Parameters = parameters;
            Locals = new List<VariableDeclaration>();
            IsEvent = isEvent;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                foreach (Specifier specifier in Specifiers) yield return specifier;
                yield return ReturnType;
                foreach (FunctionParameter functionParameter in Parameters) yield return functionParameter;
                foreach (VariableDeclaration variableDeclaration in Locals) yield return variableDeclaration;
                yield return Body;
            }
        }
    }
}
