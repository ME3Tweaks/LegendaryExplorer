using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript.Parsing
{
    //WIP
    public class PropertiesBlockParser : StringParserBase
    {
        private readonly Stack<string> ExpressionScopes;
        public static void Parse(DefaultPropertiesBlock propsBlock, SymbolTable symbols, MessageLog log)
        {
            var parser = new PropertiesBlockParser(propsBlock, symbols, log);
        }

        private PropertiesBlockParser(DefaultPropertiesBlock propsBlock, SymbolTable symbols, MessageLog log)
        {
            Symbols = symbols;
            Log = log;
            Tokens = propsBlock.Tokens;
        }

        private List<Statement> Parse(bool requireBrackets = true)
        {
            if (Consume(TokenType.LeftBracket) == null) throw ParseError("Expected '{'!", CurrentPosition);

            var statements = new List<Statement>();
            try
            {
                Symbols.PushScope("DefaultProperties", useCache:false);
                var current = ParseTopLevelStatement();
                while (current != null)
                {
                    statements.Add(current);
                    current = ParseTopLevelStatement();
                }
            }
            finally
            {
                Symbols.PopScope();
            }

            if (Consume(TokenType.RightBracket) == null) throw ParseError("Expected '}'!", CurrentPosition);
            return statements;
        }

        private Statement ParseTopLevelStatement()
        {
            if (CurrentIs("BEGIN") && NextIs("Object"))
            {
                return ParseSubobject();
            }

            return ParseNonStructAssignment();
        }

        private Subobject ParseSubobject()
        {
            var startPos = CurrentPosition;
            Tokens.Advance(2);// Begin Object

            if (!Matches("Class") || !Matches(TokenType.Equals))
            {
                throw ParseError("Expected 'Class=' after 'Begin Object'!", CurrentPosition);
            }

            var classNameToken = Consume(TokenType.Word);
            if (classNameToken is null)
            {
                throw ParseError("Expected name of class!", CurrentPosition);
            }

            var classRef = ParseBasicRef(classNameToken);


            if (!Matches("Name") || !Matches(TokenType.Equals))
            {
                throw ParseError("Expected 'Name=' after Class reference!", CurrentPosition);
            }

            var nameToken = Consume(TokenType.Word);
            if (nameToken is null)
            {
                throw ParseError("Expected name of Object!", CurrentPosition);
            }

            var statements = new List<Statement>();

            while (true)
            {
                Statement current;
                if (CurrentIs("BEGIN") && NextIs("Object"))
                {
                     current = ParseSubobject();
                }
                else if (CurrentIs("END") && NextIs("Object"))
                {
                    var subObj = new Subobject(new VariableDeclaration(classRef.ResolveType(), default, nameToken.Value), classRef, statements, startPos, CurrentPosition);
                    Symbols.AddSymbol(nameToken.Value, subObj);
                    return subObj;
                }
                else
                {
                    current = ParseNonStructAssignment();
                }

                if (current is null)
                {
                    throw ParseError("Subobject declarations must be closed with 'End Object' !");
                }
                statements.Add(current);
            }

        }

        private AssignStatement ParseNonStructAssignment()
        {
            if (CurrentIs(TokenType.RightBracket))
            {
                return null;
            }
            var statement = ParseAssignment();
            if (statement is null)
            {
                return null;
            }

            Consume(TokenType.SemiColon); //semicolon's are optional
            return statement;
        }

        private AssignStatement ParseAssignment()
        {
            //todo: type checking
            if (Consume(TokenType.Word) is Token<string> propName)
            {
                var target = ParsePropName(propName);
                if (Matches(TokenType.Assign))
                {
                    if (CurrentIs(TokenType.LeftBracket))
                    {
                        //struct value
                    }

                    if (CurrentIs(TokenType.LeftParenth))
                    {
                        //array or struct
                    }

                    bool isNegative = Matches(TokenType.MinusSign);

                    Expression literal = ParseLiteral();
                    if (literal is not null)
                    {
                        if (isNegative)
                        {
                            switch (literal)
                            {
                                case FloatLiteral floatLiteral:
                                    floatLiteral.Value *= -1;
                                    break;
                                case IntegerLiteral integerLiteral:
                                    integerLiteral.Value *= -1;
                                    break;
                                default:
                                    throw ParseError("Malformed constant value!", CurrentPosition);
                            }
                        }
                    }
                    else
                    {

                        if (isNegative)
                        {
                            throw ParseError("Unexpected '-' !", CurrentPosition);
                        }

                        if (Consume(TokenType.Word) is { } token)
                        {
                            if (Consume(TokenType.NameLiteral) is { } objName)
                            {
                                literal = ParseObjectLiteral(token, objName);
                            }
                            else
                            {
                                literal = ParseBasicRef(token);
                            }
                        }
                        else
                        {
                            throw ParseError("Expected a value in assignment!", CurrentPosition);
                        }
                    }
                    return new AssignStatement(target, literal, propName.StartPos, literal.EndPos);
                }

                throw ParseError("Expected '=' in assignment statement!", CurrentPosition);
            }

            throw ParseError("Expected name of property!", CurrentPosition);
        }

        private SymbolReference ParsePropName(Token<string> token)
        {
            string specificScope = ExpressionScopes.Peek();
            if (!Symbols.TryGetSymbolInScopeStack(token.Value, out ASTNode symbol, specificScope))
            {
                //TODO: better error message
                TypeError($"{specificScope} has no member named '{token.Value}'!", token);
                symbol = new VariableType("ERROR");
            }

            return NewSymbolReference(symbol, token, false);
        }

        private SymbolReference ParseBasicRef(Token<string> token)
        {
            string specificScope = ExpressionScopes.Peek();
            if (!Symbols.TryGetSymbolInScopeStack(token.Value, out ASTNode symbol, specificScope))
            {
                //const, or enum
                if (Symbols.TryGetType(token.Value, out VariableType destType))
                {
                    token.AssociatedNode = destType;
                    if (destType is Enumeration enm && Matches(TokenType.Dot))
                    {
                        token.SyntaxType = EF.Enum;
                        if (Consume(TokenType.Word) is { } enumValName
                         && enm.Values.FirstOrDefault(val => val.Name.CaseInsensitiveEquals(enumValName.Value)) is EnumValue enumValue)
                        {
                            enumValName.AssociatedNode = enm;
                            return NewSymbolReference(enumValue, enumValName, false);
                        }
                        throw ParseError("Expected valid enum value!", CurrentPosition);
                    }
                    if (destType is Const cnst)
                    {
                        return NewSymbolReference(cnst, token, false);
                    }
                }
                //TODO: better error message
                TypeError($"{specificScope} has no member named '{token.Value}'!", token);
                symbol = new VariableType("ERROR");
            }
            
            return NewSymbolReference(symbol, token, false);
        }
    }
}
