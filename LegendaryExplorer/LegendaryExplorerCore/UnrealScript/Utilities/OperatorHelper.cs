using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.UnrealScript.Lexing;

namespace LegendaryExplorerCore.UnrealScript.Utilities
{
    internal static class OperatorHelper
    {
        internal static TokenType FriendlyNameToTokenType(string friendlyName)
        {
            return friendlyName switch
            {
                "=" => TokenType.Assign,
                "+=" => TokenType.AddAssign,
                "-=" => TokenType.SubAssign,
                "*=" => TokenType.MulAssign,
                "/=" => TokenType.DivAssign,
                "==" => TokenType.Equals,
                "!=" => TokenType.NotEquals,
                "~=" => TokenType.ApproxEquals,
                "<" => TokenType.LeftArrow,
                "<=" => TokenType.LessOrEquals,
                ">" => TokenType.RightArrow,
                ">=" => TokenType.GreaterOrEquals,
                "++" => TokenType.Increment,
                "--" => TokenType.Decrement,
                "-" => TokenType.MinusSign,
                "+" => TokenType.PlusSign,
                "*" => TokenType.StarSign,
                "/" => TokenType.Slash,
                "**" => TokenType.Power,
                "%" => TokenType.Modulo,
                "&&" => TokenType.And,
                "||" => TokenType.Or,
                "^^" => TokenType.Xor,
                "$" => TokenType.DollarSign,
                "$=" => TokenType.StrConcatAssign,
                "@" => TokenType.AtSign,
                "@=" => TokenType.StrConcAssSpace,
                "~" => TokenType.Complement,
                "&" => TokenType.BinaryAnd,
                "|" => TokenType.BinaryOr,
                "^" => TokenType.BinaryXor,
                ">>" => TokenType.RightShift,
                "<<" => TokenType.LeftShift,
                "!" => TokenType.ExclamationMark,
                ">>>" => TokenType.VectorTransform,
                _ => friendlyName.CaseInsensitiveEquals("Dot") ? TokenType.DotProduct :
                     friendlyName.CaseInsensitiveEquals("Cross") ? TokenType.CrossProduct :
                     friendlyName.CaseInsensitiveEquals("ClockwiseFrom") ? TokenType.ClockwiseFrom :
                     TokenType.INVALID
            };
        }

        internal static string OperatorTypeToString(TokenType opType) =>
            opType switch
            {
                TokenType.Assign => "=",
                TokenType.AddAssign => "+=",
                TokenType.SubAssign => "-=",
                TokenType.MulAssign => "*=",
                TokenType.DivAssign => "/=",
                TokenType.Equals => "==",
                TokenType.NotEquals => "!=",
                TokenType.ApproxEquals => "~=",
                TokenType.LeftArrow => "<",
                TokenType.LessOrEquals => "<=",
                TokenType.RightArrow => ">",
                TokenType.GreaterOrEquals => ">=",
                TokenType.Increment => "++",
                TokenType.Decrement => "--",
                TokenType.MinusSign => "-",
                TokenType.PlusSign => "+",
                TokenType.StarSign => "*",
                TokenType.Slash => "/",
                TokenType.Power => "**",
                TokenType.Modulo => "% ",
                TokenType.And => "&&",
                TokenType.Or => "||",
                TokenType.Xor => "^^",
                TokenType.DollarSign => "$",
                TokenType.StrConcatAssign => "$=",
                TokenType.AtSign => "@",
                TokenType.StrConcAssSpace => "@=",
                TokenType.Complement => "~",
                TokenType.BinaryAnd => "&",
                TokenType.BinaryOr => "|",
                TokenType.BinaryXor => "^",
                TokenType.RightShift => ">>",
                TokenType.LeftShift => "<<",
                TokenType.ExclamationMark => "!",
                TokenType.VectorTransform => ">>>",
                TokenType.DotProduct => "Dot",
                TokenType.CrossProduct => "Cross",
                TokenType.ClockwiseFrom => "ClockwiseFrom",
                _ => "__INVALID_OPERATOR"
            };
    }
}
