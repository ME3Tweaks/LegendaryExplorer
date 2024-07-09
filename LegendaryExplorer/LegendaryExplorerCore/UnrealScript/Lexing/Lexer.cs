using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Parsing;

namespace LegendaryExplorerCore.UnrealScript.Lexing
{
    public class Lexer
    {
        private const int ASCII_TABLE_LENGTH = 128;
        private readonly MessageLog Log;
        private readonly StringBuilder Builder;
        private readonly List<int> Lines;

        private readonly string Text;
        private int CurrentIndex;
        
        private readonly List<(int, ScriptToken)> Comments = [];

        private Lexer(string code, MessageLog log = null)
        {
            CurrentIndex = 0;
            Text = code;
            Log = log ?? new MessageLog();
            Builder = new StringBuilder();
            uint lineGuess = BitOperations.RoundUpToPowerOf2((uint)code.Length / 100);
            Lines = new List<int>((int)Math.Min(lineGuess, 524_288));
        }
        
        public static TokenStream Lex(string code, MessageLog log = null)
        {
            var lexer = new Lexer(code, log);
            var lineLookup = new LineLookup(lexer.Lines);
            if (log != null)
            {
                log.LineLookup = lineLookup;
            }

            List<ScriptToken> tokens = lexer.Lex();

            var tokenStream = new TokenStream(tokens, lineLookup)
            {
                Comments =  lexer.Comments
            };
            return tokenStream;
        }

        private List<ScriptToken> Lex()
        {
            uint tokenGuess = BitOperations.RoundUpToPowerOf2((uint)Text.Length / 20);
            var tokens = new List<ScriptToken>((int)Math.Min(tokenGuess, 2_097_152));

            Lines.Add(CurrentIndex);

            while (CurrentIndex < Text.Length)
            {
                char peek = Text[CurrentIndex];
                if (char.IsWhiteSpace(peek))
                {
                    ++CurrentIndex;
                    if (peek == '\n')
                    {
                        Lines.Add(CurrentIndex);

                        //due to indentation, there will often be large numbers of spaces after a newline
                        CurrentIndex = SkipSpaces(Text, CurrentIndex);
                    }
                    continue;
                }

                ScriptToken token = GetNextToken(peek);

                if (token.Type == TokenType.SingleLineComment)
                {
                    Comments.Add((Lines.Count, token));
                }
                else
                {
                    tokens.Add(token);
                }
            }
            return tokens;
        }

        private ScriptToken GetNextToken(char peek)
        {
            int startPos = CurrentIndex;
            char nextPeek = CurrentIndex + 1 >= Text.Length ? '\0' : Text[CurrentIndex + 1];

            ScriptToken result = peek switch
            {
                '"' => MatchString(),
                '\'' => MatchName(),
                '0' => MatchNumber(),
                '1' => MatchNumber(),
                '2' => MatchNumber(),
                '3' => MatchNumber(),
                '4' => MatchNumber(),
                '5' => MatchNumber(),
                '6' => MatchNumber(),
                '7' => MatchNumber(),
                '8' => MatchNumber(),
                '9' => MatchNumber(),
                ',' => MakeSymbolToken(TokenType.Comma, ","),
                '{' => MakeSymbolToken(TokenType.LeftBracket, "{"),
                '}' => MakeSymbolToken(TokenType.RightBracket, "}"),
                '[' => MakeSymbolToken(TokenType.LeftSqrBracket, "["),
                ']' => MakeSymbolToken(TokenType.RightSqrBracket, "]"),
                '(' => MakeSymbolToken(TokenType.LeftParenth, "("),
                ')' => MakeSymbolToken(TokenType.RightParenth, ")"),
                '=' => nextPeek switch
                {
                    '=' => MakeSymbolToken(TokenType.Equals, "=="),
                    _ => MakeSymbolToken(TokenType.Assign, "=")
                },
                '-' => nextPeek switch
                {
                    '=' => MakeSymbolToken(TokenType.SubAssign, "-="),
                    '-' => MakeSymbolToken(TokenType.Decrement, "--"),
                    _ => MakeSymbolToken(TokenType.MinusSign, "-")
                },
                ';' => MakeSymbolToken(TokenType.SemiColon, ";"),
                '.' => MakeSymbolToken(TokenType.Dot, "."),
                '<' => nextPeek switch
                {
                    '=' => MakeSymbolToken(TokenType.LessOrEquals, "<="),
                    '<' => MakeSymbolToken(TokenType.LeftShift, "<<"),
                    _ => MakeSymbolToken(TokenType.LeftArrow, "<")
                },
                '+' => nextPeek switch
                {
                    '=' => MakeSymbolToken(TokenType.AddAssign, "+="),
                    '+' => MakeSymbolToken(TokenType.Increment, "++"),
                    _ => MakeSymbolToken(TokenType.PlusSign, "+")
                },
                '~' => nextPeek switch
                {
                    '=' => MakeSymbolToken(TokenType.ApproxEquals, "~="),
                    _ => MakeSymbolToken(TokenType.Complement, "~")
                },
                '|' => nextPeek switch
                {
                    '|' => MakeSymbolToken(TokenType.Or, "||"),
                    _ => MakeSymbolToken(TokenType.BinaryOr, "|")
                },
                '^' => nextPeek switch
                {
                    '^' => MakeSymbolToken(TokenType.Xor, "^^"),
                    _ => MakeSymbolToken(TokenType.BinaryXor, "^")
                },
                '@' => nextPeek switch
                {
                    '=' => MakeSymbolToken(TokenType.StrConcAssSpace, "@="),
                    _ => MakeSymbolToken(TokenType.AtSign, "@")
                },
                '?' => MakeSymbolToken(TokenType.QuestionMark, "?"),
                ':' => MakeSymbolToken(TokenType.Colon, ":"),
                '/' => nextPeek switch
                {
                    '=' => MakeSymbolToken(TokenType.DivAssign, "/="),
                    '/' => MatchSingleLineComment(),
                    _ => MakeSymbolToken(TokenType.Slash, "/")
                },
                '*' => nextPeek switch
                {
                    '=' => MakeSymbolToken(TokenType.MulAssign, "*="),
                    '*' => MakeSymbolToken(TokenType.Power, "**"),
                    _ => MakeSymbolToken(TokenType.StarSign, "*")
                },
                '&' => nextPeek switch
                {
                    '&' => MakeSymbolToken(TokenType.And, "&&"),
                    _ => MakeSymbolToken(TokenType.BinaryAnd, "&")
                },
                '%' => MakeSymbolToken(TokenType.Modulo, "%"),
                '$' => nextPeek switch
                {
                    '=' => MakeSymbolToken(TokenType.StrConcatAssign, "$="),
                    >= '0' and <= '9' => MatchStringRef(),
                    '-' when LookAhead(2).IsDigit() => MatchStringRef(),
                    _ => MakeSymbolToken(TokenType.DollarSign, "$")
                },
                '#' => MakeSymbolToken(TokenType.Hash, "#"),
                '!' => nextPeek switch
                {
                    '=' => MakeSymbolToken(TokenType.NotEquals, "!="),
                    _ => MakeSymbolToken(TokenType.ExclamationMark, "!")
                },
                '>' => nextPeek switch
                {
                    '=' => MakeSymbolToken(TokenType.GreaterOrEquals, ">="),
                    // >> is matched manually in the parser, as it conflicts with arrays of delegates: array<delegate<somefunc>>
                    '>' when LookAhead(2) == '>' => MakeSymbolToken(TokenType.VectorTransform, ">>>"),
                    _ => MakeSymbolToken(TokenType.RightArrow, ">")
                },
                _ => MatchWord(peek)
            };

            if (result == null)
            {
                int endPos = startPos != CurrentIndex ? CurrentIndex : CurrentIndex + 1;
                Log.LogLexError($"Could not lex '{GetCurrentChar()}'", startPos, endPos);
                Advance();
                return new ScriptToken(TokenType.INVALID, GetCurrentChar().ToString(), startPos, endPos) { SyntaxType = EF.ERROR };
            }
            return result;
        }

        #region MatchTokenMethods

        private ScriptToken MatchNumber()
        {
            int startPos = CurrentIndex;
            ReadOnlySpan<char> first = SubNumberDec();
            if (first.IsEmpty)
                return null;

            TokenType type;
            string value;
            char peek = GetCurrentChar();
            if (peek == 'x')
            {
                if (!first.Equals("0", StringComparison.Ordinal))
                    return null;

                Advance();
                string hex = SubNumberHex();
                peek = GetCurrentChar();
                if (string.IsNullOrEmpty(hex) || peek is '.' or 'x')
                    return null;

                type = TokenType.IntegerNumber;
                value = Convert.ToInt32(hex, 16).ToString("D", CultureInfo.InvariantCulture);
            }
            else if (peek == '.' || peek.AsciiCaseInsensitiveEquals('e') || peek.AsciiCaseInsensitiveEquals('d'))
            {
                type = TokenType.FloatingNumber;
                ReadOnlySpan<char> second = default;
                if (peek == '.')
                {
                    ++CurrentIndex;
                    second = SubNumberDec();
                    peek = CurrentIndex >= Text.Length ? '\0' : Text[CurrentIndex];
                }
                if (peek.AsciiCaseInsensitiveEquals('e') || peek.AsciiCaseInsensitiveEquals('d'))
                {
                    Advance();
                    ReadOnlySpan<char> exponent = SubNumberDec();
                    peek = GetCurrentChar();
                    if (exponent.IsEmpty || peek is '.' or 'x')
                        return null;
                    if (second.IsEmpty)
                    {
                        value = string.Concat(first, ".0e", exponent);
                    }
                    else
                    {
                        value = string.Concat(first, ".", second, "e") + exponent.ToString();
                    }
                }
                else if (second.IsEmpty)
                {
                    if (peek is 'f')
                    {
                        ++CurrentIndex;
                        value = string.Concat(first, ".0");
                    }
                    else
                    {
                        Log.LogLexError("Incomplete number! Expected digit after '.'", CurrentIndex);
                        return new ScriptToken(type, first.ToString(), startPos, CurrentIndex) { SyntaxType = EF.Number };
                    }
                }
                else
                {
                    value = string.Concat(first, ".", second);
                }

                if (GetCurrentChar() == 'f')
                {
                    ++CurrentIndex;
                }
            }
            else
            {
                type = TokenType.IntegerNumber;
                value = first.ToString();
            }
            
            return new ScriptToken(type, value, startPos, CurrentIndex) { SyntaxType = EF.Number };
        }

        private string SubNumberHex()
        {
            int startIndex = CurrentIndex;
            
            while (CurrentIndex < Text.Length && Uri.IsHexDigit(Text[CurrentIndex]))
            {
                ++CurrentIndex;
            }

            return Text.Substring(startIndex, CurrentIndex - startIndex);
        }

        private ReadOnlySpan<char> SubNumberDec()
        {
            int startIndex = CurrentIndex;
            ReadOnlySpan<char> text = Text.AsSpan();
            
            while (CurrentIndex < text.Length && text[CurrentIndex] is >= '0' and <= '9')
            {
                ++CurrentIndex;
            }

            return text.Slice(startIndex, CurrentIndex - startIndex);
        }

        private ScriptToken MatchStringRef()
        {
            int tokenStart = CurrentIndex;
            ++CurrentIndex;
            int numStart = CurrentIndex;
            char peek = GetCurrentChar();
            if (peek == '-')
            {
                ++CurrentIndex;
                peek = GetCurrentChar();
            }
            while (peek is >= '0' and <= '9')
            {
                ++CurrentIndex;
                peek = GetCurrentChar();
            }

            int numLength = CurrentIndex - numStart;
            return new ScriptToken(TokenType.StringRefLiteral, Text.Substring(numStart, numLength), tokenStart, CurrentIndex) { SyntaxType = EF.Number };
        }

        private ScriptToken MatchWord(char peek)
        {
            int startIndex = CurrentIndex;

        loopStart:
            while (CurrentIndex < Text.Length)
            {
                peek = Text[CurrentIndex];
                if (peek >= ASCII_TABLE_LENGTH || !IdentifierCharLookup[peek])
                {
                    break;
                }
                ++CurrentIndex;
            }

            //HACK: there are variable names that include the c++ scope operator '::' for some godforsaken reason
            if (peek == ':' && LookAhead(1) == ':')
            {
                Advance(2);
                peek = GetCurrentChar();
                goto loopStart;
            }
            int length = CurrentIndex - startIndex;
            if (length > 0)
            {
                return new ScriptToken(TokenType.Word, Text.Substring(startIndex, length), startIndex, CurrentIndex);
            }
            return null;
        }

        private ScriptToken MatchName()
        {
            int startPos = CurrentIndex;
            Builder.Clear();
            ++CurrentIndex;
            bool inEscape = false;
            for (; CurrentIndex < Text.Length; ++CurrentIndex)
            {
                char peek = Text[CurrentIndex];
                if (inEscape)
                {
                    inEscape = false;
                    switch (peek)
                    {
                        case '\\':
                        case '\'':
                            Builder.Append(peek);
                            continue;
                        default:
                            Log.LogLexError(@$"Unrecognized escape sequence: '\{peek}'", CurrentIndex);
                            goto end;
                    }
                }

                switch (peek)
                {
                    case '\\':
                        inEscape = true;
                        continue;
                    case '\'':
                    {
                        ++CurrentIndex;
                        if (Builder.Length is 0)
                        {
                            Builder.Append("None"); //empty name literals should be interpreted as 'None'
                        }
                        goto end;
                    }
                    case '\n':
                        Log.LogLexError("Name Literals can not contain line breaks!", startPos, CurrentIndex);
                        goto end;
                    default:
                        Builder.Append(peek);
                        continue;
                }
            }

            Log.LogLexError("Name Literal was not terminated properly!", startPos, CurrentIndex);
        end:
            return new ScriptToken(TokenType.NameLiteral, Builder.ToString(), startPos, CurrentIndex) { SyntaxType = EF.Name };
        }

        private ScriptToken MatchString()
        {
            int startPos = CurrentIndex;
            Builder.Clear();
            ++CurrentIndex;
            bool inEscape = false;
            for (; CurrentIndex < Text.Length; ++CurrentIndex)
            {
                char peek = Text[CurrentIndex];
                if (inEscape)
                {
                    inEscape = false;
                    switch (peek)
                    {
                        case '\\':
                        case '"':
                            Builder.Append(peek);
                            continue;
                        case 'n':
                            Builder.Append('\n');
                            continue;
                        case 'r':
                            Builder.Append('\r');
                            continue;
                        case 't':
                            Builder.Append('\t');
                            continue;
                        default:
                            Log.LogLexError(@$"Unrecognized escape sequence: '\{peek}'", CurrentIndex);
                            goto end;
                    }
                }

                switch (peek)
                {
                    case '\\':
                        inEscape = true;
                        continue;
                    case '"':
                        ++CurrentIndex;
                        goto end;
                    case '\n':
                        Log.LogLexError("String Literals can not contain line breaks!", startPos, CurrentIndex);
                        goto end;
                    default:
                        Builder.Append(peek);
                        continue;
                }
            }
            
            Log.LogLexError("String Literal was not terminated properly!", startPos, CurrentIndex);
        end:
            return new ScriptToken(TokenType.StringLiteral, Builder.ToString(), startPos, CurrentIndex) { SyntaxType = EF.String };
        }

        private ScriptToken MatchSingleLineComment()
        {
            int startPos = CurrentIndex;
            int commentStart = CurrentIndex += 2;
            while (CurrentIndex < Text.Length && Text[CurrentIndex] is not ('\n' or '\r'))
            {
                ++CurrentIndex;
            }
            
            return new ScriptToken(TokenType.SingleLineComment, Text.Substring(commentStart, CurrentIndex - commentStart), startPos, CurrentIndex) { SyntaxType = EF.Comment };
        }
        
        private ScriptToken MakeSymbolToken(TokenType type, string symbol)
        {
            return new ScriptToken(type, symbol, CurrentIndex, CurrentIndex += symbol.Length);
        }

        #endregion

        //adapted from .NET bcl IndexOf
        private static unsafe int SkipSpaces(string str, int startIndex)
        {
            nint offset = startIndex;
            if (startIndex < 0 || startIndex >= str.Length)
            {
                goto Found;
            }
            nint lengthToExamine = str.Length - startIndex;

            fixed (char* strPointer = str)
            {
                ref char searchSpace = ref *strPointer;
                while (lengthToExamine >= 4)
                {
                    ref char current = ref Unsafe.Add(ref searchSpace, offset);

                    if (' ' != current)
                        goto Found;
                    if (' ' != Unsafe.Add(ref current, 1))
                        goto Found1;
                    if (' ' != Unsafe.Add(ref current, 2))
                        goto Found2;
                    if (' ' != Unsafe.Add(ref current, 3))
                        goto Found3;

                    offset += 4;
                    lengthToExamine -= 4;
                }

                while (lengthToExamine > 0)
                {
                    if (' ' != Unsafe.Add(ref searchSpace, offset))
                        goto Found;

                    offset++;
                    lengthToExamine--;
                }
            }

        Found:
            return (int)(offset);
        Found3:
            return (int)(offset + 3);
        Found2:
            return (int)(offset + 2);
        Found1:
            return (int)(offset + 1);
        }

        #region CharStream

        private char GetCurrentChar() => CurrentIndex >= Text.Length ? '\0' : Text[CurrentIndex];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char LookAhead(int reach)
        {
            return EndOfStream(reach) ? '\0' : Text[CurrentIndex + reach];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Advance()
        {
            ++CurrentIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Advance(int num)
        {
            CurrentIndex += num;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool AtEnd()
        {
            return CurrentIndex >= Text.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EndOfStream(int ahead = 0)
        {
            return CurrentIndex + ahead >= Text.Length;
        }

        #endregion

        private static readonly bool[] IdentifierCharLookup;

        static Lexer()
        {
            IdentifierCharLookup = new bool[ASCII_TABLE_LENGTH];
            for (int i = 0; i < ASCII_TABLE_LENGTH; i++)
            {
                IdentifierCharLookup[i] = (char)i is '_' or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9');
            }
        }
    }
}
