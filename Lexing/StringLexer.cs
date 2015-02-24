using ME3Script.Lexing.Matching.StringMatchers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script
{
    public class StringLexer : LexerBase<String>
    {
        public StringLexer(String code) : base(new StringTokenizer(code))
        {
            TokenMatchers = new List<ITokenMatcher<String>>();

            var delimiters = new List<KeywordMatcher>
            {
                new KeywordMatcher("{", TokenType.LeftBracket, null),
                new KeywordMatcher("}", TokenType.RightBracket, null),
                new KeywordMatcher("[", TokenType.LeftBracket, null),
                new KeywordMatcher("]", TokenType.LeftBracket, null),
                new KeywordMatcher("==", TokenType.Equals, null),    
                new KeywordMatcher("+=", TokenType.AddAssign, null),   
                new KeywordMatcher("-=", TokenType.SubAssign, null),   
                new KeywordMatcher("*=", TokenType.MulAssign, null),   
                new KeywordMatcher("/=", TokenType.DivAssign, null),      
                new KeywordMatcher("!=", TokenType.NotEquals, null),  
                new KeywordMatcher("~=", TokenType.ApproxEquals, null), 
                new KeywordMatcher(">>", TokenType.RightShift, null),    
                new KeywordMatcher("<<", TokenType.LeftShift, null),
                new KeywordMatcher("<=", TokenType.LessOrEquals, null),
                new KeywordMatcher(">=", TokenType.GreaterOrEquals, null),
                new KeywordMatcher("**", TokenType.Power, null), 
                new KeywordMatcher("&&", TokenType.And, null),   
                new KeywordMatcher("||", TokenType.Or, null),         
                new KeywordMatcher("^^", TokenType.Xor, null),
                new KeywordMatcher("<", TokenType.LessThan, null),    
                new KeywordMatcher(">", TokenType.GreaterThan, null),         
                new KeywordMatcher("%", TokenType.Modulo, null),
                new KeywordMatcher("$=", TokenType.StrConcatAssign, null),
                new KeywordMatcher("$", TokenType.StrConcat, null),
                new KeywordMatcher("@=", TokenType.StrConcAssSpace, null),
                new KeywordMatcher("@", TokenType.StrConcatSpace, null),
                new KeywordMatcher("-", TokenType.Subract, null),      
                new KeywordMatcher("+", TokenType.Add, null),        
                new KeywordMatcher("*", TokenType.Multiply, null),   
                new KeywordMatcher("/", TokenType.Divide, null),  
                new KeywordMatcher("=", TokenType.Assign, null),  
                new KeywordMatcher("~", TokenType.BinaryNegate, null), 
                new KeywordMatcher("&", TokenType.BinaryAnd, null),    
                new KeywordMatcher("|", TokenType.BinaryOr, null),     
                new KeywordMatcher("^", TokenType.BinaryXor, null),     
                new KeywordMatcher("?", TokenType.Conditional, null),   
                new KeywordMatcher(":", TokenType.Colon, null)         
            };

            var keywords = new List<KeywordMatcher>
            {
                new KeywordMatcher("VectorCross", TokenType.VectorCross, delimiters, false),
                new KeywordMatcher("VectorDot", TokenType.VectorDot, delimiters, false),
                new KeywordMatcher("IsClockwiseFrom", TokenType.IsClockwiseFrom, delimiters, false),
                new KeywordMatcher("var", TokenType.InstanceVariable, delimiters, false),
                new KeywordMatcher("local", TokenType.LocalVariable, delimiters, false),
                new KeywordMatcher("GlobalConfig", TokenType.GlobalConfigSpecifier, delimiters, false),
                new KeywordMatcher("Config", TokenType.ConfigSpecifier, delimiters, false),
                new KeywordMatcher("Localized", TokenType.LocalizedSpecifier, delimiters, false),
                new KeywordMatcher("Const", TokenType.ConstSpecifier, delimiters, false),
                new KeywordMatcher("PrivateWrite", TokenType.PrivateWriteSpecifier, delimiters, false),
                new KeywordMatcher("ProtectedWrite", TokenType.ProtectedWriteSpecifier, delimiters, false),
                new KeywordMatcher("Private", TokenType.PrivateSpecifier, delimiters, false),
                new KeywordMatcher("Protected", TokenType.ProtectedSpecifier, delimiters, false),
                new KeywordMatcher("RepNotify", TokenType.RepNotifySpecifier, delimiters, false),
                new KeywordMatcher("Deprecated", TokenType.DeprecatedSpecifier, delimiters, false),
                new KeywordMatcher("Instanced", TokenType.InstancedSpecifier, delimiters, false),
                new KeywordMatcher("Databinding", TokenType.DatabindingSpecifier, delimiters, false),
                new KeywordMatcher("EditorOnly", TokenType.EditorOnlySpecifier, delimiters, false),
                new KeywordMatcher("NotForConsole", TokenType.NotForConsoleSpecifier, delimiters, false),
                new KeywordMatcher("EditConst", TokenType.EditConstSpecifier, delimiters, false),
                new KeywordMatcher("EditFixedSize", TokenType.EditFixedSizeSpecifier, delimiters, false),
                new KeywordMatcher("EditInline", TokenType.EditInlineSpecifier, delimiters, false),
                new KeywordMatcher("EditInlineUse", TokenType.EditInlineUseSpecifier, delimiters, false),
                new KeywordMatcher("NoClear", TokenType.NoClearSpecifier, delimiters, false),
                new KeywordMatcher("Interp", TokenType.InterpSpecifier, delimiters, false),
                new KeywordMatcher("Input", TokenType.InputSpecifier, delimiters, false),
                new KeywordMatcher("Transient", TokenType.TransientSpecifier, delimiters, false),
                new KeywordMatcher("DuplicateTransient", TokenType.DuplicateTransientSpecifier, delimiters, false),
                new KeywordMatcher("NoImport", TokenType.NoImportSpecifier, delimiters, false),
                new KeywordMatcher("Native", TokenType.NativeSpecifier, delimiters, false),
                new KeywordMatcher("Export", TokenType.ExportSpecifier, delimiters, false),
                new KeywordMatcher("NoExport", TokenType.NoExportSpecifier, delimiters, false),
                new KeywordMatcher("NonTransactional", TokenType.NonTransactionalSpecifier, delimiters, false),
                new KeywordMatcher("Pointer", TokenType.PointerSpecifier, delimiters, false),
                new KeywordMatcher("Init", TokenType.InitSpecifier, delimiters, false),
                new KeywordMatcher("RepRetry", TokenType.RepRetrySpecifier, delimiters, false),
                new KeywordMatcher("AllowAbstract", TokenType.AllowAbstractSpecifier, delimiters, false),
                new KeywordMatcher("Out", TokenType.OutSpecifier, delimiters, false),
                new KeywordMatcher("Coerce", TokenType.CoerceSpecifier, delimiters, false),
                new KeywordMatcher("Optional", TokenType.OptionalSpecifier, delimiters, false),
                new KeywordMatcher("Skip", TokenType.SkipSpecifier, delimiters, false),
                new KeywordMatcher("byte", TokenType.Byte, delimiters, false),
                new KeywordMatcher("int", TokenType.Int, delimiters, false),
                new KeywordMatcher("bool", TokenType.Bool, delimiters, false),
                new KeywordMatcher("float", TokenType.Float, delimiters, false),
                new KeywordMatcher("string", TokenType.String, delimiters, false),
                new KeywordMatcher("enumeration", TokenType.Enumeration, delimiters, false),
                new KeywordMatcher("array", TokenType.Array, delimiters, false),
                new KeywordMatcher("struct", TokenType.Struct, delimiters, false),
                new KeywordMatcher("class", TokenType.Class, delimiters, false),
                new KeywordMatcher("Name", TokenType.Name, delimiters, false),
                new KeywordMatcher("Object", TokenType.Object, delimiters, false),
                new KeywordMatcher("Actor", TokenType.Actor, delimiters, false),
                new KeywordMatcher("delegate", TokenType.Delegate, delimiters, false),
                new KeywordMatcher("Vector", TokenType.Vector, delimiters, false),
                new KeywordMatcher("Rotator", TokenType.Rotator, delimiters, false),
                new KeywordMatcher("constant", TokenType.Constant, delimiters, false),
                new KeywordMatcher("None", TokenType.None, delimiters, false),
                new KeywordMatcher("Self", TokenType.Self, delimiters, false),
                new KeywordMatcher("EnumCount", TokenType.EnumCount, delimiters, false),
                new KeywordMatcher("ArrayCount", TokenType.ArrayCount, delimiters, false)
            };

            TokenMatchers.AddRange(delimiters);
            TokenMatchers.AddRange(keywords);
            TokenMatchers.Add(new WhiteSpaceMatcher());
            TokenMatchers.Add(new NumberMatcher(delimiters));
            TokenMatchers.Add(new WordMatcher(delimiters));

            Console.WriteLine("End of Constructor");
        }

        public override Token<String> GetNextToken()
        {
            if (Data.AtEnd())
            {
                return new Token<String>(TokenType.EOF);
            }

            Token<String> result =
                (from matcher in TokenMatchers
                 let token = matcher.MatchNext(Data)
                 where token != null
                 select token).FirstOrDefault();

            if (result == null)
            {
                Data.Advance();
                return new Token<String>(TokenType.INVALID);
            }
            return result;
        }

        public override IEnumerable<Token<string>> LexData()
        {
            var token = GetNextToken();
            while (token != null && token.Type != TokenType.EOF)
            {
                if (token.Type != TokenType.WhiteSpace)
                    yield return token;

                token = GetNextToken();
            }
        }
    }
}
