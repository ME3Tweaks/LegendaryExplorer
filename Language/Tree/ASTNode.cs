using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        Class,
        Function,
        State,
        StateLabel,
        FunctionParameter,
        WhileLoop,
        DoUntilLoop,
        ForLoop,
        BreakStatement,
        ContinueStatement,
        ReturnStatement,
        StopStatement,

        FunctionCall,
        SymbolReference,
        ArrayReference,
        CompositeReference,

        IntegerLiteral,
        FloatLiteral,
        StringLiteral,
        NameLiteral,
        BooleanLiteral,

        INVALID
    }

    public abstract class ASTNode : IAcceptASTVisitor
    {
        public ASTNodeType Type;

        public ASTNode Outer;

        public SourcePosition StartPos { get; private set; }
        public SourcePosition EndPos { get; private set; }

        public ASTNode(ASTNodeType type, SourcePosition start, SourcePosition end)
        {
            Type = type;
            StartPos = start; EndPos = end;
        }

        public abstract bool AcceptVisitor(IASTVisitor visitor);
    }
}
