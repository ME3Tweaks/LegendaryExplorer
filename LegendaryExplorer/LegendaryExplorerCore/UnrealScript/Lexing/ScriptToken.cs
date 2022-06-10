using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;

namespace LegendaryExplorerCore.UnrealScript.Lexing
{
    public sealed class ScriptToken
    {
        #region Members
        public string Value { get; }

        public readonly TokenType Type;

        public EF SyntaxType { get; set; }

        public ASTNode AssociatedNode { get; set; }

        public readonly int StartPos;
        public readonly int EndPos;
        #endregion

        #region Methods
        public ScriptToken(TokenType type, string value, int start, int end)
        {
            Value = value;
            Type = type;
            StartPos = start;
            EndPos = end;
        }

        public override string ToString()
        {
            return "[" + Type + "] " + Value;
        }
        #endregion
    }

    public enum TokenType
    {
        #region Special Characters
        LeftBracket,    // {
        RightBracket,   // }
        LeftParenth,    // (
        RightParenth,   // )
        LeftSqrBracket, // [
        RightSqrBracket,// ]
        Assign,         // =
        AddAssign,      // +=
        SubAssign,      // -=
        MulAssign,      // *=
        DivAssign,      // /=
        Equals,         // ==
        NotEquals,      // !=
        ApproxEquals,   // ~=
        LeftArrow,      // <
        LessOrEquals,   // <=
        RightArrow,     // >
        GreaterOrEquals,// >=
        Increment,      // ++
        Decrement,      // --
        MinusSign,      // -
        PlusSign,       // +
        StarSign,       // *
        Slash,          // /
        Power,          // **
        Modulo,         // %
        And,            // &&
        Or,             // ||
        Xor,            // ^^
        DollarSign,     // $
        StrConcatAssign,// $=
        AtSign,         // @
        StrConcAssSpace,// @=
        Complement,   // ~
        BinaryAnd,      // &
        BinaryOr,       // |
        BinaryXor,      // ^
        //RightShift,     // >>, also vector reverse rotate   //is matched manually in the parser. conflicts with arrays of delegates: array<delegate<somefunc>>
        LeftShift,      // <<, also vector rotate

        QuestionMark,    // ?
        Colon,          // :
        SemiColon,      // ;
        Comma,          // ,
        Dot,            // .
        ExclamationMark,// !
        Hash,           // #
        VectorTransform,  // >>>


        #endregion

        WhiteSpace,     // 
        NewLine,        // \n

        SingleLineComment,

        StringLiteral,
        NameLiteral,
        StringRefLiteral,
        EOF,
        Word,
        IntegerNumber,
        FloatingNumber,


        INVALID
    }
}
