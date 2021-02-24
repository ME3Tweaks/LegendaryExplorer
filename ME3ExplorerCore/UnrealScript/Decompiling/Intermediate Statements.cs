
using ME3Script.Language.Tree;
using ME3Script.Utilities;

namespace ME3Script.Decompiling
{
    /*
     * These are transient classes for use during bytecode decompilation only.
     */

    public abstract class Jump : Statement
    {
        public ushort JumpLoc;

        protected Jump(ushort jumpLoc) : base(ASTNodeType.INVALID, null, null)
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

    public abstract class ConditionalJump : Jump
    {
        public Expression Condition;
        public int SizeOfExpression;

        protected ConditionalJump(ushort jumpLoc, Expression condition) : base(jumpLoc)
        {
            Condition = condition;
        }
    }

    public class IfNotJump : ConditionalJump
    {

        public IfNotJump(ushort jumpLoc, Expression condition, int sizeOfExpression) : base(jumpLoc, condition)
        {
            SizeOfExpression = sizeOfExpression;
        }
    }

    public class NullJump : ConditionalJump
    {
        public bool Not;

        public NullJump(ushort jumpLoc, Expression condition, bool not) : base(jumpLoc, condition)
        {
            Not = not;
            Condition = new InOpReference(new InOpDeclaration(Not ? "!=" : "==", 0, 0, null, null, null), Condition, new NoneLiteral());
        }
    }

    public class InEditorJump : ConditionalJump
    {
        public InEditorJump(ushort jumpLoc) : base(jumpLoc, new SymbolReference(null, "__IN_EDITOR"))
        {
        }
    }

    public class IteratorNext : Statement
    {
        public IteratorNext() : base(ASTNodeType.INVALID, null, null)
        {
        }
    }

    public class IteratorPop : Statement
    {
        public IteratorPop() : base(ASTNodeType.INVALID, null, null)
        {
        }
    }
}