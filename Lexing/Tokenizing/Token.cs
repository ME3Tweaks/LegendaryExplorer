using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Lexing.Tokenizing
{
    public class Token<T> where T : class
    {
        #region Members
        public virtual T Value { get; private set; }

        public TokenType Type { get; private set; }

        public SourcePosition StartPosition { get; private set; }
        public SourcePosition EndPosition { get; private set; }
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
            StartPosition = start;
            EndPosition = end;
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
        LessThan,       // <
        LessOrEquals,   // <=
        GreaterThan,    // >
        GreaterOrEquals,// >=
        Subract,        // -
        Add,            // +
        Multiply,       // *
        Divide,         // /
        Power,          // **
        Modulo,         // %
        And,            // &&
        Or,             // ||
        Xor,            // ^^
        StrConcat,      // $
        StrConcatAssign,// $=
        StrConcatSpace, // @
        StrConcAssSpace,// @=
        BinaryNegate,   // ~
        BinaryAnd,      // &
        BinaryOr,       // |
        BinaryXor,      // ^
        RightShift,     // >>, also vector reverse rotate
        LeftShift,      // <<, also vector rotate

        Conditional,    // ?
        Colon,          // :
        SemiColon,      // ;
        Comma,          // ,
        Dot,            // .


        #endregion

        #region Keywords

        #region Operators

        VectorCross,    // Cross
        VectorDot,      // Dot

        IsClockwiseFrom,// ClockwiseFrom

        #endregion

        #region Variables
        // Variables:
        InstanceVariable,
        LocalVariable,

        // Variable specifiers:
        ConfigSpecifier,
        GlobalConfigSpecifier,
        LocalizedSpecifier,
        ConstSpecifier,
        PrivateSpecifier,
        ProtectedSpecifier,
        PrivateWriteSpecifier,
        ProtectedWriteSpecifier,
        RepNotifySpecifier,
        DeprecatedSpecifier,
        InstancedSpecifier,
        DatabindingSpecifier,
        EditorOnlySpecifier,
        NotForConsoleSpecifier,
        EditConstSpecifier,
        EditFixedSizeSpecifier,
        EditInlineSpecifier,
        EditInlineUseSpecifier,
        NoClearSpecifier,
        InterpSpecifier,
        InputSpecifier,
        TransientSpecifier,
        DuplicateTransientSpecifier,
        NoImportSpecifier,
        NativeSpecifier,
        ExportSpecifier,
        NoExportSpecifier,
        NonTransactionalSpecifier,
        PointerSpecifier,
        InitSpecifier,
        RepRetrySpecifier,
        AllowAbstractSpecifier,
        // Function variables only:
        OutSpecifier,
        CoerceSpecifier,
        OptionalSpecifier,
        // Operator functions only:
        SkipSpecifier,

        // Class Specifiers:
        AbstractSpecifier,
        DependsOnSpecifier,
        ImplementsSpecifier,
        ParseConfigSpecifier,
        PerObjectConfigSpecifier,
        PerObjectLocalizedSpecifier,
        NonTransientSpecifier,

        // Struct Specifiers:
        ImmutableSpecifier,
        ImmutableWhenCookedSpecifier,
        AtomicSpecifier,
        AtomicWhenCookedSpecifier,
        StrictConfigSpecifier,

        // Variable types:
        //Byte,
        //Int,
        //Bool,
        //Float,
        String,
        Enumeration,
        // Aggregates:
        Array,
        Struct,
        Class,
        State,
        Function,
        Event,
        Delegate,
        Operator,
        // Unrealengine types:
        //Name,
        //Object,
        //Actor,
        //Vector,
        //Rotator,
        //Plane,
        //Coords,
        //Color,
        //Region,
        // Constants:
        Constant,
        None,
        Self,
        EnumCount,
        ArrayCount,

        #endregion

        #endregion

        EOF,
        Word,
        IntegerNumber,
        FloatingNumber,
        Scope,
        StructMember,
        Extends,
        Within,
        //Functions:
        PublicSpecifier,
        StaticSpecifier,
        FinalSpecifier,
        ExecSpecifier,
        K2CallSpecifier,
        K2OverrideSpecifier,
        K2PureSpecifier,
        SimulatedSpecifier,
        SingularSpecifier,
        ClientSpecifier,
        DemoRecordingSpecifier,
        ReliableSpecifier,
        ServerSpecifier,
        UnreliableSpecifier,
        IteratorSpecifier,
        LatentSpecifier,
        //States
        AutoSpecifier,
        Ignores,
        //Operators
        PreOperator,
        PostOperator,

        INVALID
    }
}
