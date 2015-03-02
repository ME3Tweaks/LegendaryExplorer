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

        // Variable types:
        Byte,
        Int,
        Bool,
        Float,
        String,
        Enumeration,
        // Aggregates:
        Array,
        Struct,
        Class,
        // Unrealengine types:
        Name,
        Object,
        Actor,
        Delegate,
        Vector,
        Rotator,
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

        INVALID
    }
}
