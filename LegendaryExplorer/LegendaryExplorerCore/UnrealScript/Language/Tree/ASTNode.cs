using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
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
        AssertStatement,
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
        ExpressionStatement,
        ReplicationStatement,
        Goto,

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
        public ASTNodeType Type { get; protected init; }

        public ASTNode Outer;

        public int StartPos;
        public int EndPos;

        public int TextLength => EndPos - StartPos;

        protected ASTNode(ASTNodeType type, int start, int end)
        {
            Type = type;
            StartPos = start; 
            EndPos = end;
        }

        public abstract bool AcceptVisitor(IASTVisitor visitor);
        public virtual IEnumerable<ASTNode> ChildNodes => Enumerable.Empty<ASTNode>();
    }
}
