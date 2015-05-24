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
        protected TokenStream<String> Tokens;
        protected TokenType CurrentTokenType
        { get { return Tokens.CurrentItem.Type; } }
        protected SourcePosition CurrentPosition
        { get { return Tokens.CurrentItem.StartPosition; } }

        protected List<ASTNodeType> SemiColonExceptions = new List<ASTNodeType>
        {
            ASTNodeType.WhileLoop,
            ASTNodeType.ForLoop,
            ASTNodeType.IfStatement
        };

        protected List<ASTNodeType> CompositeTypes = new List<ASTNodeType>
        {
            ASTNodeType.Class,
            ASTNodeType.Struct,
            ASTNodeType.Enumeration
        };


        public List<VariableIdentifier> ParseVariableNames()
        {
            List<VariableIdentifier> vars = new List<VariableIdentifier>();
            do
            {
                VariableIdentifier variable = TryParseVariable();
                if (variable == null)
                {
                    Log.LogError("Expected at least one variable name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }
                vars.Add(variable);
            } while (Tokens.ConsumeToken(TokenType.Comma) != null);
            // TODO: This allows a trailing comma before semicolon, intended?
            return vars;
        }

        public VariableIdentifier TryParseVariable()
        {
            Func<ASTNode> variableParser = () =>
            {
                var name = Tokens.ConsumeToken(TokenType.Word);
                if (name == null)
                    return null;

                if (Tokens.ConsumeToken(TokenType.LeftSqrBracket) != null)
                {
                    var size = Tokens.ConsumeToken(TokenType.IntegerNumber);
                    if (size == null)
                    {
                        Log.LogError("Expected an integer number for size!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                    if (Tokens.ConsumeToken(TokenType.RightSqrBracket) == null)
                    {
                        Log.LogError("Expected ']'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }

                    return new VariableIdentifier(name.Value,
                        name.StartPosition, name.EndPosition, Int32.Parse(size.Value));
                }

                return new VariableIdentifier(name.Value, name.StartPosition, name.EndPosition);
            };
            return (VariableIdentifier)Tokens.TryGetTree(variableParser);
        }

        public VariableType TryParseType()
        {
            Func<ASTNode> typeParser = () =>
            {
                // TODO: word or basic datatype? (int float etc)
                var type = Tokens.ConsumeToken(TokenType.Word);
                if (type == null)
                {
                    Log.LogError("Expected type name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }
                return new VariableType(type.Value, type.StartPosition, type.EndPosition);
            };
            return (VariableType)Tokens.TryGetTree(typeParser);
        }
    }
}
