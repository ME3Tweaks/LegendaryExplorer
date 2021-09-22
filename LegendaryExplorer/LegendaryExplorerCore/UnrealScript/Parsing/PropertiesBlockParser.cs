using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript.Parsing
{
    //WIP
    public class PropertiesBlockParser : StringParserBase
    {
        private readonly Stack<string> ExpressionScopes;
        private readonly IMEPackage Pcc;
        public static void Parse(DefaultPropertiesBlock propsBlock, IMEPackage pcc, SymbolTable symbols, MessageLog log)
        {
            var parser = new PropertiesBlockParser(propsBlock, pcc, symbols, log);
            var statements = parser.Parse(false);

            propsBlock.Statements = statements;
        }

        private PropertiesBlockParser(DefaultPropertiesBlock propsBlock, IMEPackage pcc, SymbolTable symbols, MessageLog log)
        {
            Symbols = symbols;
            Log = log;
            Tokens = propsBlock.Tokens;
            Pcc = pcc;

            ExpressionScopes = new Stack<string>();
            ExpressionScopes.Push(Symbols.CurrentScopeName);
        }

        private List<Statement> Parse(bool requireBrackets = true)
        {
            if (requireBrackets && Consume(TokenType.LeftBracket) == null) throw ParseError("Expected '{'!", CurrentPosition);

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

            if (requireBrackets && Consume(TokenType.RightBracket) == null) throw ParseError("Expected '}'!", CurrentPosition);
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
                VariableType targetType = target.ResolveType();
                if (Matches(TokenType.Assign))
                {
                    if (CurrentIs(TokenType.LeftBracket))
                    {
                        throw new NotImplementedException("struct literal parsing is not implemented yet");
                    }

                    if (CurrentIs(TokenType.LeftParenth))
                    {
                        switch (targetType)
                        {
                            case DynamicArrayType dynamicArrayType:
                                throw new NotImplementedException("dynamic array literal parsing is not implemented yet");
                                break;
                            case StaticArrayType staticArrayType:
                                throw new NotImplementedException("static array literal parsing is not implemented yet");
                                break;
                            case Struct targetStructType:

                                break;
                            default:
                                throw ParseError($"Expected a {targetType.FullTypeName()} literal!", CurrentPosition);
                        }
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

                    switch (targetType)
                    {
                        case Class targetClass:
                            if (literal is not NoneLiteral)
                            {
                                VariableType valueClass;
                                if (literal is ObjectLiteral objectLiteral)
                                {
                                    valueClass = objectLiteral.Class;
                                }
                                else if (literal is SymbolReference {Node: Subobject {Class: SymbolReference {Node: VariableType subObjClass}}})
                                {
                                    valueClass = subObjClass;
                                }
                                else
                                {
                                    TypeError($"Expected an {OBJECT} literal or sub-object name!", literal);
                                    break;
                                }
                                if (valueClass is not (Class or ClassType)
                                    || valueClass is Class literalClass && !literalClass.SameAsOrSubClassOf(targetClass.Name)
                                    || valueClass is ClassType && targetClass.Name is not ("Class" or "Object"))
                                {
                                    TypeError($"Expected an object of class {targetClass.Name} or a subclass!", literal);
                                }
                            }
                            break;
                        case ClassType targetClassLimiter:
                            if (literal is not NoneLiteral)
                            {
                                if (literal is not ObjectLiteral { Class: ClassType literalClassType })
                                {
                                    TypeError($"Expected a class literal!", literal);
                                }
                                else if (targetClassLimiter.ClassLimiter != literalClassType.ClassLimiter && !((Class)targetClassLimiter.ClassLimiter).SameAsOrSubClassOf(literalClassType.ClassLimiter.Name))
                                {
                                    TypeError($"Cannot assign a value of type '{literalClassType.FullTypeName()}' to a variable of type '{literalClassType.FullTypeName()}'.", literal);
                                }
                            }
                            break;
                        case DelegateType delegateType:
                            if (literal is not NameLiteral nameLiteral)
                            {
                                TypeError("Expected a name literal!", literal);
                            }
                            else if (!Symbols.TryGetSymbol(nameLiteral.Value, out Function func))
                            {
                                TypeError($"No function named {nameLiteral.Value} found!", literal);
                            }
                            else if (!func.SignatureEquals(delegateType.DefaultFunction))
                            {
                                TypeError($"Expected a function with the same signature as {(delegateType.DefaultFunction.Outer as Class)?.Name}.{delegateType.DefaultFunction.Name}!", literal);
                            }
                            break;
                        case DynamicArrayType dynamicArrayType:
                            throw new NotImplementedException();
                            break;
                        case Enumeration enumeration:
                            if (literal is not SymbolReference {Node: EnumValue enumVal})
                            {
                                TypeError($"Expected an enum value!", literal);
                            }
                            break;
                        case Struct:
                            if (literal is not StructLiteral)
                            {
                                TypeError($"Expected a {STRUCT} literal!", literal);
                            }
                            break;
                        default:
                            switch (targetType.PropertyType)
                            {
                                case EPropertyType.Byte:
                                    if (literal is not IntegerLiteral byteLiteral)
                                    {
                                        TypeError($"Expected a {BYTE}!", literal);
                                    }
                                    else if (byteLiteral.Value is < 0 or > 255)
                                    {
                                        TypeError($"{byteLiteral.Value} is not in the range of valid byte values: [0, 255]", literal);
                                    }
                                    break;
                                case EPropertyType.Int:
                                    if (literal is not IntegerLiteral)
                                    {
                                        TypeError($"Expected an integer!", literal);
                                    }
                                    break;
                                case EPropertyType.Bool:
                                    if (literal is not BooleanLiteral)
                                    {
                                        TypeError($"Expected {TRUE} or {FALSE}!");
                                    }
                                    break;
                                case EPropertyType.Float:
                                    if (literal is IntegerLiteral intLit)
                                    {
                                        literal = new FloatLiteral(intLit.Value, intLit.StartPos, intLit.EndPos);
                                    }
                                    else if (literal is not FloatLiteral)
                                    {
                                        TypeError($"Expected a floating point number!", literal);
                                    }
                                    break;
                                case EPropertyType.Name:
                                    if (literal is not NameLiteral)
                                    {
                                        TypeError($"Expected a {NAME} literal!", literal);
                                    }
                                    break;
                                case EPropertyType.String:
                                    if (literal is not StringLiteral)
                                    {
                                        TypeError($"Expected a {STRING} literal!", literal);
                                    }
                                    break;
                                case EPropertyType.StringRef:
                                    if (literal is not StringRefLiteral)
                                    {
                                        TypeError($"Expected a {STRINGREF} literal!", literal);
                                    }
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                            break;
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
