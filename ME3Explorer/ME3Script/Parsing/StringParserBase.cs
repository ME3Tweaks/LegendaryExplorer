using ME3Script.Compiling.Errors;
using ME3Script.Language.Tree;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer;
using static ME3Script.Utilities.Keywords;

namespace ME3Script.Parsing
{
    public abstract class StringParserBase
    {
        protected MessageLog Log;
        protected TokenStream<string> Tokens;
        protected TokenType CurrentTokenType => Tokens.CurrentItem.Type;

        protected Token<string> CurrentToken => Tokens.CurrentItem;
        protected Token<string> PrevToken => Tokens.Prev();

        protected SourcePosition CurrentPosition => Tokens.CurrentItem.StartPos ?? new SourcePosition(-1, -1, -1);

        public static readonly List<ASTNodeType> SemiColonExceptions = new List<ASTNodeType>
        {
            ASTNodeType.WhileLoop,
            ASTNodeType.ForLoop,
            ASTNodeType.ForEachLoop,
            ASTNodeType.IfStatement,
            ASTNodeType.SwitchStatement,
            ASTNodeType.CaseStatement,
            ASTNodeType.DefaultStatement,
            ASTNodeType.StateLabel,
        };

        public static readonly List<ASTNodeType> CompositeTypes = new List<ASTNodeType>
        {
            ASTNodeType.Class,
            ASTNodeType.Struct,
            ASTNodeType.Enumeration,
            ASTNodeType.ObjectLiteral
        };

        protected ParseError Error(string msg, SourcePosition start = null, SourcePosition end = null)
        {
            Log.LogError(msg, start, end);
            return new ParseError(msg);
        }

        public VariableIdentifier ParseVariableName()
        {
            VariableIdentifier var = TryParseVariable();
            if (var == null)
            {
                Log.LogError("Expected a variable name!", CurrentPosition);
                return null;
            }
            return var;
        }

        public VariableIdentifier TryParseVariable()
        {
            return (VariableIdentifier)Tokens.TryGetTree(VariableParser);
            ASTNode VariableParser()
            {
                var name = Consume(TokenType.Word);
                if (name == null) return null;

                if (Consume(TokenType.LeftSqrBracket) != null)
                {
                    var size = Consume(TokenType.IntegerNumber);
                    if (size == null)
                    {
                        throw Error("Expected an integer number for size!", CurrentPosition);
                    }

                    if (Consume(TokenType.RightSqrBracket) == null)
                    {
                        throw Error("Expected ']'!", CurrentPosition);
                    }

                    return new VariableIdentifier(name.Value, name.StartPos, name.EndPos, int.Parse(size.Value));
                }

                return new VariableIdentifier(name.Value, name.StartPos, name.EndPos);
            }
        }

        public VariableType TryParseType()
        {
            return (VariableType)Tokens.TryGetTree(TypeParser);
            ASTNode TypeParser()
            {
                if (Matches(ARRAY))
                {
                    var arrayToken = Tokens.Prev();
                    if (Consume(TokenType.LeftArrow) is null)
                    {
                        throw Error("Expected '<' after 'array'!", CurrentPosition);
                    }
                    var elementType = TryParseType();
                    if (elementType == null)
                    {
                        throw Error("Expected element type for array!", CurrentPosition);
                    }

                    if (elementType is DynamicArrayType)
                    {
                        throw Error("Arrays of Arrays are not supported!", elementType.StartPos, elementType.EndPos);
                    }
                    if (Consume(TokenType.RightArrow) is null)
                    {
                        throw Error("Expected '>' after array type!", CurrentPosition);
                    }
                    return new DynamicArrayType(elementType, arrayToken.StartPos, CurrentPosition);
                }

                if (Matches(DELEGATE))
                {
                    var delegateToken = Tokens.Prev();
                    if (Consume(TokenType.LeftArrow) is null)
                    {
                        throw Error("Expected '<' after 'delegate'!", CurrentPosition);
                    }

                    string functionName = "";
                    do
                    {
                        if (Consume(TokenType.Word) is Token<string> identifier)
                        {
                            if (functionName.Length > 0)
                            {
                                functionName += ".";
                            }
                            functionName += identifier.Value;
                        }
                        else
                        {
                            throw Error("Expected function name for delegate!", CurrentPosition);
                        }
                    } while (Matches(TokenType.Dot));
                    if (Consume(TokenType.RightArrow) is null)
                    {
                        throw Error("Expected '>' after function name!", CurrentPosition);
                    }
                    return new DelegateType(new Function(functionName, default, null, null, null), delegateToken.StartPos, CurrentPosition);
                }

                if (Consume(CLASS) is {} classToken)
                {
                    if (Consume(TokenType.LeftArrow) is null)
                    {
                        return new ClassType(new VariableType(OBJECT));
                    }

                    if (!(Consume(TokenType.Word) is {} classNameToken))
                    {
                        throw Error("Expected class name!", CurrentPosition);
                    }

                    if (Consume(TokenType.RightArrow) is null)
                    {
                        throw Error("Expected '>' after class name!", CurrentPosition);
                    }
                    return new ClassType(new VariableType(classNameToken.Value), classToken.StartPos, PrevToken.EndPos);
                }

                Token<string> type = Consume(TokenType.Word);
                if (type == null)
                {
                    return null;
                }
                return new VariableType(type.Value, type.StartPos, type.EndPos);
            }

        }

        #region Helpers

        public bool Matches(string str)
        {
            bool matches = CurrentIs(str);
            if (matches)
            {
                Tokens.Advance();
            }
            return matches;
        }

        public bool Matches(params string[] strs)
        {
            bool matches = CurrentIs(strs);
            if (matches)
            {
                Tokens.Advance();
            }
            return matches;
        }

        public bool Matches(TokenType tokenType)
        {
            if (Tokens.CurrentItem.Type == tokenType)
            {
                Tokens.Advance();
                return true;
            }

            return false;
        }

        public bool Matches(params TokenType[] tokenTypes) => tokenTypes.Any(Matches);

        public bool CurrentIs(string str)
        {
            return CurrentToken.Type != TokenType.EOF && CurrentToken.Value.Equals(str, StringComparison.OrdinalIgnoreCase);
        }
        public bool CurrentIs(TokenType tokenType)
        {
            return CurrentToken.Type == tokenType;
        }

        public bool CurrentIs(params string[] strs) => strs.Any(CurrentIs);

        public bool CurrentIs(params TokenType[] tokenTypes) => tokenTypes.Any(CurrentIs);

        public Token<string> Consume(TokenType tokenType) => Tokens.ConsumeToken(tokenType);

        public Token<string> Consume(params TokenType[] tokenTypes) => tokenTypes.Select(Consume).NonNull().FirstOrDefault();

        public Token<string> Consume(string str)
        {

            Token<string> token = null;
            if (CurrentIs(str))
            {
                token = CurrentToken;
                Tokens.Advance();
            }
            return token;
        }

        public Token<string> Consume(params string[] strs) => strs.Select(Consume).NonNull().FirstOrDefault();

        #endregion

        public Expression ParseLiteral()
        {
            Token<string> token = CurrentToken;
            if (Matches(TokenType.IntegerNumber))
            {
                int val = int.Parse(token.Value, CultureInfo.InvariantCulture);
                return new IntegerLiteral(val, token.StartPos, token.EndPos) { NumType = val >= 0 && val <= byte.MaxValue ? BYTE : INT };
            }

            if (Matches(TokenType.FloatingNumber))
            {
                return new FloatLiteral(float.Parse(token.Value, CultureInfo.InvariantCulture), token.StartPos, token.EndPos);
            }

            if (Matches(TokenType.StringLiteral))
            {
                return new StringLiteral(token.Value, token.StartPos, token.EndPos);
            }

            if (Matches(TokenType.NameLiteral))
            {
                return new NameLiteral(token.Value, token.StartPos, token.EndPos);
            }

            if (Matches(TokenType.StringRefLiteral))
            {
                return new StringRefLiteral(int.Parse(token.Value, CultureInfo.InvariantCulture), token.StartPos, token.EndPos);
            }

            if (Matches(TRUE, FALSE))
            {
                return new BooleanLiteral(bool.Parse(token.Value), token.StartPos, token.EndPos);
            }

            if (Matches(NONE))
            {
                return new NoneLiteral(token.StartPos, token.EndPos);
            }

            if (CurrentIs(VECT) && Tokens.LookAhead(1).Type == TokenType.LeftParenth)
            {
                Tokens.Advance();
                return ParseVectorLiteral();
            }

            if (CurrentIs(ROT) && Tokens.LookAhead(1).Type == TokenType.LeftParenth)
            {
                Tokens.Advance();
                return ParseRotatorLiteral();
            }

            return null;
        }

        private Expression ParseRotatorLiteral()
        {
            var start = CurrentPosition;
            if (!Matches(TokenType.LeftParenth))
            {
                throw Error($"Expected '(' after '{ROT}' in rotator literal!");
            }

            int pitch = ParseInt();

            if (!Matches(TokenType.Comma))
            {
                throw Error("Expected ',' after pitch component in rotator literal!");
            }

            int yaw = ParseInt();

            if (!Matches(TokenType.Comma))
            {
                throw Error("Expected ',' after yaw component in rotator literal!");
            }

            int roll = ParseInt();

            if (!Matches(TokenType.RightParenth))
            {
                throw Error("Expected ')' after roll component in rotator literal!");
            }

            return new RotatorLiteral(pitch, yaw, roll, start, Tokens.Prev().EndPos);
        }

        private Expression ParseVectorLiteral()
        {
            var start = CurrentPosition;
            if (!Matches(TokenType.LeftParenth))
            {
                throw Error($"Expected '(' after '{VECT}' in vector literal!");
            }

            float x = ParseFloat();

            if (!Matches(TokenType.Comma))
            {
                throw Error("Expected ',' after x component in vector literal!");
            }

            float y = ParseFloat();

            if (!Matches(TokenType.Comma))
            {
                throw Error("Expected ',' after y component in vector literal!");
            }

            float z = ParseFloat();

            if (!Matches(TokenType.RightParenth))
            {
                throw Error("Expected ')' after z component in vector literal!");
            }

            return new VectorLiteral(x, y, z, start, Tokens.Prev().EndPos);
        }

        float ParseFloat()
        {
            bool isNegative = Matches(TokenType.MinusSign);
            if (!Matches(TokenType.FloatingNumber, TokenType.IntegerNumber))
            {
                throw Error("Expected number literal!");
            }

            var val = float.Parse(Tokens.Prev().Value, CultureInfo.InvariantCulture);
            return isNegative ? -val : val;
        }

        int ParseInt()
        {
            bool isNegative = Matches(TokenType.MinusSign);
            if (!Matches(TokenType.IntegerNumber))
            {
                throw Error("Expected integer literal!");
            }

            var val = int.Parse(Tokens.Prev().Value, CultureInfo.InvariantCulture);
            return isNegative ? -val : val;
        }
    }

    public class ParseError : Exception
    {
        public ParseError(string msg) : base(msg){}
    }

    public static class ParserExtensions
    {

    }
}
