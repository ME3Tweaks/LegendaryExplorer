using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Lexing
{
    public sealed class ScriptToken
    {
        public readonly string Value;

        public readonly TokenType Type;
        //TODO: remove this information from the token and store it seperately, as was done with the go-to-definition info
        public EF SyntaxType;
        public readonly int StartPos;
        public readonly int EndPos;

        public int Length => EndPos - StartPos;

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
    }

    public enum TokenType : byte
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
        Complement,     // ~
        BinaryAnd,      // &
        BinaryOr,       // |
        BinaryXor,      // ^
        RightShift,     // >>  also vector reverse rotate   //is matched manually in the parser. conflicts with arrays of delegates: array<delegate<somefunc>>
        LeftShift,      // <<  also vector rotate
        ExclamationMark,// !
        VectorTransform,// >>>
        DotProduct,     //Dot
        CrossProduct,   //Cross
        ClockwiseFrom,  //ClockwiseFrom

        QuestionMark,    // ?
        Colon,          // :
        SemiColon,      // ;
        Comma,          // ,
        Dot,            // .
        Hash,           // #

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
