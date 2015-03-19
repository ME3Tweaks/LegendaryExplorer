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
        Body
    }

    public abstract class ASTNode
    {
        public ASTNodeType Type;

        public ASTNode(ASTNodeType type)
        {
            Type = type;
        }
    }
}
