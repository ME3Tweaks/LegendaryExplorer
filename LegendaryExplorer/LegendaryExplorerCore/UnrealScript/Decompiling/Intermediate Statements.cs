using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Decompiling
{
    /*
     * These are transient classes for use during bytecode decompilation only.
     */

    public abstract class Jump : Statement
    {
        public readonly ushort JumpLoc;

        protected Jump(ushort jumpLoc) : base(ASTNodeType.INVALID, -1, -1)
        {
            JumpLoc = jumpLoc;
        }
    }

    public class UnconditionalJump : Jump
    {
        public UnconditionalJump(ushort jumpLoc) : base(jumpLoc)
        {
        }
    }

    internal abstract class ConditionalJump : Jump
    {
        public Expression Condition;
        public int SizeOfExpression;

        protected ConditionalJump(ushort jumpLoc, Expression condition) : base(jumpLoc)
        {
            Condition = condition;
        }
    }

    internal class IfNotJump : ConditionalJump
    {
        public IfNotJump(ushort jumpLoc, Expression condition, int sizeOfExpression) : base(jumpLoc, condition)
        {
            SizeOfExpression = sizeOfExpression;
        }
    }

    internal class NullJump : ConditionalJump
    {
        public NullJump(ushort jumpLoc, Expression condition, bool not) : base(jumpLoc, condition)
        {
            Condition = new InOpReference(new InOpDeclaration(not ? TokenType.NotEquals : TokenType.Equals, 0, 0, null, null, null), Condition, new NoneLiteral());
        }
    }

    internal class InEditorJump : ConditionalJump
    {
        public InEditorJump(ushort jumpLoc) : base(jumpLoc, new SymbolReference(null, Keywords.__IN_EDITOR))
        {
        }
    }

    internal class IteratorNext : Statement
    {
        public IteratorNext() : base(ASTNodeType.INVALID, -1, -1)
        {
        }
    }

    internal class IteratorPop : Statement
    {
        public IteratorPop() : base(ASTNodeType.INVALID, -1, -1)
        {
        }
    }
}