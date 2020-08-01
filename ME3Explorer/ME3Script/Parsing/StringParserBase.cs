using ME3Script.Compiling.Errors;
using ME3Script.Language.Tree;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Parsing
{
    public abstract class StringParserBase
    {
        protected MessageLog Log;
        protected TokenStream<string> Tokens;
        protected TokenType CurrentTokenType => Tokens.CurrentItem.Type;

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

        protected ASTNode Error(string msg, SourcePosition start = null, SourcePosition end = null)
        {
            Log.LogError(msg, start, end);
            return null;
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
                        return Error("Expected an integer number for size!", CurrentPosition);
                    }

                    if (Tokens.ConsumeToken(TokenType.RightSqrBracket) == null)
                    {
                        return Error("Expected ']'!", CurrentPosition);
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
                if (Tokens.ConsumeToken(TokenType.Array) != null)
                {
                    if (Tokens.ConsumeToken(TokenType.LeftArrow) == null)
                    {
                        return Error("Expected '<' after 'array'!", CurrentPosition);
                    }
                    Token<string> arrayType = Tokens.ConsumeToken(TokenType.Word);
                    if (arrayType == null)
                    {
                        return Error("Expected type name for array!", CurrentPosition);
                    }
                    if (Tokens.ConsumeToken(TokenType.RightArrow) == null)
                    {
                        return Error("Expected '>' after array type!", CurrentPosition);
                    }
                    return new VariableType($"array<{arrayType.Value}>");//TODO: do this better. ArrayVariableType?
                }
                // TODO: word or basic datatype? (int float etc)
                Token<string> type = Tokens.ConsumeToken(TokenType.Word) ?? Tokens.ConsumeToken(TokenType.Class); //class is a valid type, in addition to being a keyword
                if (type == null)
                {
                    return Error("Expected type name!", CurrentPosition);
                }

                return new VariableType(type.Value, type.StartPosition, type.EndPosition);
            }

        }
    }
}
