using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public enum ASTNodeType
    {
        UnaryOperation,
        BinaryOperation,
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
        FunctionStub
    }

    public abstract class ASTNode
    {
        public ASTNodeType Type;

        public ASTNode Parent;

        public ASTNode(ASTNodeType type)
        {
            Type = type;
        }
    }
}
