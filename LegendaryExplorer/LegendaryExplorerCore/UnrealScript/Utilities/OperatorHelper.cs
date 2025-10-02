using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.UnrealScript.Lexing;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace LegendaryExplorerCore.UnrealScript.Utilities;

internal static partial class OperatorHelper
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
            ">>>" => TokenType.TripleRightShift,
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
            TokenType.Modulo => "%",
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
            TokenType.TripleRightShift => ">>>",
            TokenType.DotProduct => "Dot",
            TokenType.CrossProduct => "Cross",
            TokenType.ClockwiseFrom => "ClockwiseFrom",
            _ => "__INVALID_OPERATOR"
        };

    public static Dictionary<TokenType, string> OperatorTypeToVerboseName = new()
    {
        [TokenType.AddAssign] = "AddEqual",
        [TokenType.SubAssign] = "SubtractEqual",
        [TokenType.MulAssign] = "MultiplyEqual",
        [TokenType.DivAssign] = "DivideEqual",
        [TokenType.Equals] = "EqualEqual",
        [TokenType.NotEquals] = "NotEqual",
        [TokenType.ApproxEquals] = "ComplementEqual",
        [TokenType.LeftArrow] = "Less",
        [TokenType.LessOrEquals] = "LessEqual",
        [TokenType.RightArrow] = "Greater",
        [TokenType.GreaterOrEquals] = "GreaterEqual",
        [TokenType.Increment] = "AddAdd",
        [TokenType.Decrement] = "SubtractSubtract",
        [TokenType.MinusSign] = "Subtract",
        [TokenType.PlusSign] = "Add",
        [TokenType.StarSign] = "Multiply",
        [TokenType.Slash] = "Divide",
        [TokenType.Power] = "MultiplyMultiply",
        [TokenType.Modulo] = "Percent",
        [TokenType.And] = "AndAnd",
        [TokenType.Or] = "OrOr",
        [TokenType.Xor] = "XorXor",
        [TokenType.DollarSign] = "Concat",
        [TokenType.StrConcatAssign] = "ConcatEqual",
        [TokenType.AtSign] = "At",
        [TokenType.StrConcAssSpace] = "AtEqual",
        [TokenType.Complement] = "Complement",
        [TokenType.BinaryAnd] = "And",
        [TokenType.BinaryOr] = "Or",
        [TokenType.BinaryXor] = "Xor",
        [TokenType.RightShift] = "GreaterGreater",
        [TokenType.LeftShift] = "LessLess",
        [TokenType.ExclamationMark] = "Not",
        [TokenType.TripleRightShift] = "GreaterGreaterGreater",
        [TokenType.DotProduct] = "Dot",
        [TokenType.CrossProduct] = "Cross",
        [TokenType.ClockwiseFrom] = "ClockwiseFrom",
    };

    public static Dictionary<string, TokenType> VerboseNameToOperatorType = OperatorTypeToVerboseName.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    public static bool IsOperatorToken(TokenType tokenType) =>
        tokenType is
                TokenType.Assign or
                TokenType.AddAssign or
                TokenType.SubAssign or
                TokenType.MulAssign or
                TokenType.DivAssign or
                TokenType.Equals or
                TokenType.NotEquals or
                TokenType.ApproxEquals or
                TokenType.LeftArrow or
                TokenType.LessOrEquals or
                TokenType.RightArrow or
                TokenType.GreaterOrEquals or
                TokenType.Increment or
                TokenType.Decrement or
                TokenType.MinusSign or
                TokenType.PlusSign or
                TokenType.StarSign or
                TokenType.Slash or
                TokenType.Power or
                TokenType.Modulo or
                TokenType.And or
                TokenType.Or or
                TokenType.Xor or
                TokenType.DollarSign or
                TokenType.StrConcatAssign or
                TokenType.AtSign or
                TokenType.StrConcAssSpace or
                TokenType.Complement or
                TokenType.BinaryAnd or
                TokenType.BinaryOr or
                TokenType.BinaryXor or
                TokenType.RightShift or
                TokenType.LeftShift or
                TokenType.ExclamationMark or
                TokenType.TripleRightShift or
                TokenType.DotProduct or
                TokenType.CrossProduct or
                TokenType.ClockwiseFrom;

    internal static byte DefaultPrecedence(TokenType opType) =>
        opType switch
        {
            TokenType.AddAssign => 34,
            TokenType.SubAssign => 34,
            TokenType.MulAssign => 34,
            TokenType.DivAssign => 34,
            TokenType.Equals => 24,
            TokenType.NotEquals => 26,
            TokenType.ApproxEquals => 24,
            TokenType.LeftArrow => 24,
            TokenType.LessOrEquals => 24,
            TokenType.RightArrow => 24,
            TokenType.GreaterOrEquals => 24,
            TokenType.MinusSign => 20,
            TokenType.PlusSign => 20,
            TokenType.StarSign => 16,
            TokenType.Slash => 16,
            TokenType.Power => 12,
            TokenType.Modulo => 18,
            TokenType.And => 30,
            TokenType.Or => 32,
            TokenType.Xor => 30,
            TokenType.DollarSign => 40,
            TokenType.StrConcatAssign => 44,
            TokenType.AtSign => 40,
            TokenType.StrConcAssSpace => 44,
            TokenType.BinaryAnd => 28,
            TokenType.BinaryOr => 28,
            TokenType.BinaryXor => 28,
            TokenType.RightShift => 22,
            TokenType.LeftShift => 22,
            TokenType.TripleRightShift => 22,
            TokenType.DotProduct => 16,
            TokenType.CrossProduct => 16,
            TokenType.ClockwiseFrom => 24,
            _ => 20
        };


    [GeneratedRegex("^[a-zA-Z][a-zA-Z0-9]*$")]
    private static partial Regex ValidWordOperator();

    internal static bool IsValidWordOperator(string friendlyName) => ValidWordOperator().IsMatch(friendlyName);
}
