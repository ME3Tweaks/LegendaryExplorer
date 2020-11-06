using ME3Script.Utilities;

namespace ME3Script.Lexing.Tokenizing
{
    public class Token<T> where T : class
    {
        #region Members
        public virtual T Value { get; private set; }

        public TokenType Type { get; private set; }

        public SourcePosition StartPos { get; private set; }
        public SourcePosition EndPos { get; private set; }
        #endregion

        #region Methods
        public Token(TokenType type)
        {
            Value = null;
            Type = type;
        }
        public Token(TokenType type, T value)
        {
            Value = value;
            Type = type;
        }

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
        WhiteSpace,     // 
        NewLine,        // \n
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


        SingleLineComment,
        MultiLineComment,
        StringLiteral,
        NameLiteral,
        StringRefLiteral,
        EOF,
        Word,
        IntegerNumber,
        FloatingNumber,

        //#region Keywords

        //#region Operators

        //VectorCross,    // Cross
        //VectorDot,      // Dot

        //IsClockwiseFrom,// ClockwiseFrom

        //#endregion

        //#region Variables
        //// Variables:
        //InstanceVariable,
        //LocalVariable,

        ////// Variable specifiers:
        ////ConfigSpecifier,
        ////GlobalConfigSpecifier,
        ////LocalizedSpecifier,
        ////PrivateSpecifier,
        ////ProtectedSpecifier,
        ////PrivateWriteSpecifier,
        ////ProtectedWriteSpecifier,
        ////RepNotifySpecifier,
        ////DeprecatedSpecifier,
        ////InstancedSpecifier,
        ////DatabindingSpecifier,
        ////EditorOnlySpecifier,
        ////NotForConsoleSpecifier,
        ////EditConstSpecifier,
        ////EditFixedSizeSpecifier,
        ////EditInlineSpecifier,
        ////EditInlineUseSpecifier,
        ////NoClearSpecifier,
        ////InterpSpecifier,
        ////InputSpecifier,
        ////TransientSpecifier,
        ////DuplicateTransientSpecifier,
        ////NoImportSpecifier,
        ////NativeSpecifier,
        ////NativeReplicationSpecifier,
        ////ExportSpecifier,
        ////NoExportSpecifier,
        ////NonTransactionalSpecifier,
        ////PointerSpecifier,
        ////RepRetrySpecifier,
        ////AllowAbstractSpecifier,
        ////// Function variables only:
        ////OutSpecifier,
        ////CoerceSpecifier,
        ////OptionalSpecifier,
        ////// Operator functions only:
        ////SkipSpecifier,

        ////// Class Specifiers:
        ////AbstractSpecifier,
        ////DependsOnSpecifier,
        ////ImplementsSpecifier,
        ////ParseConfigSpecifier,
        ////PerObjectConfigSpecifier,
        ////PerObjectLocalizedSpecifier,
        ////NonTransientSpecifier,
        ////PlaceableSpecifier,

        ////// Struct Specifiers:
        ////ImmutableSpecifier,
        ////ImmutableWhenCookedSpecifier,
        ////AtomicSpecifier,
        ////AtomicWhenCookedSpecifier,
        ////StrictConfigSpecifier,

        //// Variable types:
        ////Byte,
        ////Int,
        ////Bool,
        ////Float,
        //Enumeration,
        //// Aggregates:
        //Array,
        //Struct,
        //Class,
        //State,
        //Function,
        ////EventSpecifier,
        ////Delegate,
        ////Operator,
        //DefaultProperties,
        //// Unrealengine types:
        ////Object,
        ////Actor,
        ////Vector,
        ////Rotator,
        ////Plane,
        ////Coords,
        ////Color,
        ////Region,
        //// Constants:
        //Constant,
        //None,
        //Self,
        //EnumCount,
        //ArrayCount,

        //#endregion

        //#endregion

        //True,
        //False,
        //Scope,
        //StructMember,
        //Extends,
        //Within,
        //////Functions:
        ////PublicSpecifier,
        ////StaticSpecifier,
        ////FinalSpecifier,
        ////ExecSpecifier,
        ////K2CallSpecifier,
        ////K2OverrideSpecifier,
        ////K2PureSpecifier,
        ////SimulatedSpecifier,
        ////SingularSpecifier,
        ////ClientSpecifier,
        ////DemoRecordingSpecifier,
        ////ReliableSpecifier,
        ////ServerSpecifier,
        ////UnreliableSpecifier,
        ////IteratorSpecifier,
        ////LatentSpecifier,
        //////States
        ////AutoSpecifier,
        //Ignores,
        //////Operators
        ////PreOperator,
        ////PostOperator,

        ////Flow
        //If,
        //Else,
        //While,
        //Do,
        //Until,
        //For,
        //Continue,
        //Break,
        //ForEach,
        //Return,
        //Switch,
        //Case,
        //Default,
        //// State flow
        //GoTo,
        //GoToState,
        //Stop,

        ////comments

        INVALID
    }
}
