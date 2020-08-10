using ME3Script.Compiling.Errors;
using ME3Script.Language.Tree;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer;

namespace ME3Script.Parsing
{
    public abstract class StringParserBase
    {
        protected MessageLog Log;
        protected TokenStream<string> Tokens;
        protected TokenType CurrentTokenType => Tokens.CurrentItem.Type;

        protected Token<string> CurrentToken => Tokens.CurrentItem;

        protected SourcePosition CurrentPosition => Tokens.CurrentItem.StartPosition ?? new SourcePosition(-1, -1, -1);

        protected List<ASTNodeType> SemiColonExceptions = new List<ASTNodeType>
        {
            ASTNodeType.WhileLoop,
            ASTNodeType.ForLoop,
            ASTNodeType.IfStatement,
            ASTNodeType.SwitchStatement,
            ASTNodeType.CaseStatement,
            ASTNodeType.DefaultStatement
        };

        protected List<ASTNodeType> CompositeTypes = new List<ASTNodeType>
        {
            ASTNodeType.Class,
            ASTNodeType.Struct,
            ASTNodeType.Enumeration
        };

        protected ParseError Error(string msg, SourcePosition start = null, SourcePosition end = null)
        {
            Log.LogError(msg, start, end);
            return new ParseError();
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
                var name = Tokens.ConsumeToken(TokenType.Word);
                if (name == null) return null;

                if (Tokens.ConsumeToken(TokenType.LeftSqrBracket) != null)
                {
                    var size = Tokens.ConsumeToken(TokenType.IntegerNumber);
                    if (size == null)
                    {
                        throw Error("Expected an integer number for size!", CurrentPosition);
                    }

                    if (Tokens.ConsumeToken(TokenType.RightSqrBracket) == null)
                    {
                        throw Error("Expected ']'!", CurrentPosition);
                    }

                    return new VariableIdentifier(name.Value, name.StartPosition, name.EndPosition, int.Parse(size.Value));
                }

                return new VariableIdentifier(name.Value, name.StartPosition, name.EndPosition);
            }
        }

        public VariableType TryParseType()
        {
            return (VariableType)Tokens.TryGetTree(TypeParser);
            ASTNode TypeParser()
            {
                if (Matches(Keywords.ARRAY))
                {
                    var arrayToken = Tokens.Prev();
                    if (Tokens.ConsumeToken(TokenType.LeftArrow) is null)
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
                    if (Tokens.ConsumeToken(TokenType.RightArrow) is null)
                    {
                        throw Error("Expected '>' after array type!", CurrentPosition);
                    }
                    return new DynamicArrayType(elementType, arrayToken.StartPosition, CurrentPosition);
                }

                if (Matches(Keywords.DELEGATE))
                {
                    var delegateToken = Tokens.Prev();
                    if (Tokens.ConsumeToken(TokenType.LeftArrow) is null)
                    {
                        throw Error("Expected '<' after 'delegate'!", CurrentPosition);
                    }
                    Token<string> delegateFunction = Tokens.ConsumeToken(TokenType.Word);
                    if (delegateFunction == null)
                    {
                        throw Error("Expected function name for delegate!", CurrentPosition);
                    }
                    if (Tokens.ConsumeToken(TokenType.RightArrow) is null)
                    {
                        throw Error("Expected '>' after function name!", CurrentPosition);
                    }
                    return new DelegateType(new Function(delegateFunction.Value, default, null, null, null), delegateToken.StartPosition, CurrentPosition);
                }
                // TODO: word or basic datatype? (int float etc)
                Token<string> type = Tokens.ConsumeToken(TokenType.Word);
                if (type == null)
                {
                    return null;
                }

                return new VariableType(type.Value, type.StartPosition, type.EndPosition);
            }

        }
        public bool Matches(string str)
        {
            bool matches = CurrentIs(str);
            if (matches)
            {
                Tokens.Advance();
            }
            return matches;
        }

        public bool CurrentIs(string str)
        {
            return CurrentToken.Value.Equals(str, StringComparison.OrdinalIgnoreCase);
        }

        public bool CurrentIs(params string[] strs)
        {
            return strs.Any(CurrentIs);
        }

        public Token<string> Consume(TokenType tokenType) => Tokens.ConsumeToken(tokenType);

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
    }

    public class ParseError : Exception
    {

    }

    public static class ParserExtensions
    {

    }
}
