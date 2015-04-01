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
        PreFixOperatior,
        PostFixOperatior,
        InFixOperatior,
        AssignStatement,
        IfStatement,
        CodeBody,
        VariableType,
        Specifier,
        VariableDeclaration,
        Variable,
        StaticArrayVariable,
        Struct,
        Enumeration,
        Class,
        Function,
        State,
        StateLabel,
        FunctionParameter,
        // Temporary types:
        FunctionStub,
        StateSkeleton
    }

    public abstract class ASTNode
    {
        public ASTNodeType Type;

        public ASTNode ParentNode;

        public SourcePosition StartPos { get; private set; }
        public SourcePosition EndPos { get; private set; }

        public ASTNode(ASTNodeType type, SourcePosition start, SourcePosition end)
        {
            Type = type;
            StartPos = start; EndPos = end;
        }
    }
}
