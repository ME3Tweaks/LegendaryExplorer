using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing
{
    public class Token<T> where T : class
    {
        #region Members
        public virtual T Value { get; }

        public TokenType Type { get; }

        public EF SyntaxType { get; set; } 

        public ASTNode AssociatedNode { get; set; }

        public SourcePosition StartPos { get; }
        public SourcePosition EndPos { get; }
        #endregion

        #region Methods
        public Token(TokenType type, T value, SourcePosition start, SourcePosition end)
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
        //RightShift,     // >>, also vector reverse rotate   //will have to be matched manually in the parser. conflicts with arrays of delegates: array<delegate<somefunc>>
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
        MultiLineComment,

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
