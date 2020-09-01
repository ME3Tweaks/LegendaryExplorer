using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace ME3Script.Language.Tree
{
    public enum ASTNodeType
    {
        PrefixOperator,
        PostfixOperator,
        InfixOperator,
        PreOpRef,
        PostOpRef,
        InOpRef,
        NewOp,

        AssignStatement,
        IfStatement,
        CodeBody,
        VariableType,
        Specifier,
        VariableDeclaration,
        VariableIdentifier,
        Variable,
        Struct,
        Enumeration,
        Const,
        Class,
        Function,
        State,
        StateLabel,
        FunctionParameter,
        WhileLoop,
        DoUntilLoop,
        ForLoop,
        ForEachLoop,
        BreakStatement,
        ContinueStatement,
        ReturnStatement,
        StopStatement,

        SwitchStatement,
        CaseStatement,
        DefaultStatement,

        FunctionCall,
        SymbolReference,
        ArrayReference,
        CompositeReference,

        IntegerLiteral,
        FloatLiteral,
        StringLiteral,
        NameLiteral,
        BooleanLiteral,
        StringRefLiteral,
        StructLiteral,
        DynamicArrayLiteral,
        ObjectLiteral,
        VectorLiteral,
        RotatorLiteral,
        NoneLiteral,

        ConditionalExpression,
        CastExpression,

        DefaultPropertiesBlock,
        SubObject,

        INVALID
    }

    public abstract class ASTNode : IAcceptASTVisitor
    {
        public ASTNodeType Type { get; protected set; }

        public ASTNode Outer;

        public SourcePosition StartPos { get; }
        public SourcePosition EndPos { get; }

        protected ASTNode(ASTNodeType type, SourcePosition start, SourcePosition end)
        {
            Type = type;
            StartPos = start; 
            EndPos = end;
        }

        public abstract bool AcceptVisitor(IASTVisitor visitor);
        public virtual IEnumerable<ASTNode> ChildNodes => Enumerable.Empty<ASTNode>();
    }
}
