using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Utilities;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript.Parsing
{
    internal abstract class StringParserBase
    {
        protected MessageLog Log;
        protected TokenStream Tokens;
        protected TokenType CurrentTokenType => Tokens.CurrentItem.Type;

        protected ScriptToken CurrentToken => Tokens.CurrentItem;
        protected ScriptToken PrevToken => Tokens.Prev();

        protected int CurrentPosition => Tokens.CurrentItem.StartPos;

        public static readonly List<ASTNodeType> SemiColonExceptions = new()
        {
            ASTNodeType.WhileLoop,
            ASTNodeType.ForLoop,
            ASTNodeType.ForEachLoop,
            ASTNodeType.IfStatement,
            ASTNodeType.SwitchStatement,
            ASTNodeType.CaseStatement,
            ASTNodeType.DefaultStatement,
            ASTNodeType.StateLabel
        };

        public static readonly List<ASTNodeType> CompositeTypes = new()
        {
            ASTNodeType.Class,
            ASTNodeType.Struct,
            ASTNodeType.Enumeration,
            ASTNodeType.ObjectLiteral
        };

        protected SymbolTable Symbols;

        protected ParseException ParseError(string msg, ScriptToken token)
        {
            token.SyntaxType = EF.ERROR;
            return ParseError(msg, token.StartPos, token.EndPos);
        }

        protected ParseException ParseError(string msg, ASTNode node) => ParseError(msg, node.StartPos, node.EndPos);

        protected ParseException ParseError(string msg, int start = -1, int end = -1)
        {
            Log.LogError(msg, start, end);
            return new ParseException(msg);
        }

        protected void TypeError(string msg, ScriptToken token)
        {
            token.SyntaxType = EF.ERROR;
            TypeError(msg, token.StartPos, token.EndPos);
        }

        protected void TypeError(string msg, ASTNode node) => TypeError(msg, node.StartPos, node.EndPos);

        protected void TypeError(string msg, int start = -1, int end = -1)
        {
            Log.LogError(msg, start, end);
        }

        protected void LogWarning(string msg, ScriptToken token)
        {
            token.SyntaxType = EF.ERROR;
            LogWarning(msg, token.StartPos, token.EndPos);
        }

        protected void LogWarning(string msg, ASTNode node) => LogWarning(msg, node.StartPos, node.EndPos);

        protected void LogWarning(string msg, int start = -1, int end = -1)
        {
            Log.LogWarning(msg, start, end);
        }

        public VariableIdentifier ParseVariableName()
        {
            VariableIdentifier var = ParseVariableIdentifier();
            if (var == null)
            {
                Log.LogError("Expected a variable name!", CurrentPosition);
                return null;
            }
            return var;
        }

        public VariableIdentifier ParseVariableIdentifier()
        {
            var name = Consume(TokenType.Word);
            if (name == null) return null;
            if (name.Value.CaseInsensitiveEquals("None"))
            {
                TypeError("'None' cannot be used as a variable name!", name);
            }

            if (Consume(TokenType.LeftSqrBracket) != null)
            {
                var size = Consume(TokenType.IntegerNumber);
                if (size == null)
                {
                    throw ParseError("Expected an integer number for size!", CurrentPosition);
                }

                if (Consume(TokenType.RightSqrBracket) == null)
                {
                    throw ParseError("Expected ']'!", CurrentPosition);
                }

                return new VariableIdentifier(name.Value, name.StartPos, name.EndPos, int.Parse(size.Value));
            }

            return new VariableIdentifier(name.Value, name.StartPos, name.EndPos);
        }

        public VariableType ParseTypeRef()
        {
            if (Matches(ARRAY, EF.Keyword))
            {
                var arrayToken = Tokens.Prev();
                if (Consume(TokenType.LeftArrow) is null)
                {
                    throw ParseError("Expected '<' after 'array'!", CurrentPosition);
                }
                var elementType = ParseTypeRef();
                if (elementType == null)
                {
                    throw ParseError("Expected element type for array!", CurrentPosition);
                }

                if (elementType is DynamicArrayType)
                {
                    throw ParseError("Arrays of Arrays are not supported!", elementType.StartPos, elementType.EndPos);
                }
                if (Consume(TokenType.RightArrow) is null)
                {
                    throw ParseError("Expected '>' after array type!", CurrentPosition);
                }
                return new DynamicArrayType(elementType, arrayToken.StartPos, CurrentPosition);
            }

            if (Matches(DELEGATE, EF.Keyword))
            {
                var delegateToken = Tokens.Prev();
                if (Consume(TokenType.LeftArrow) is null)
                {
                    throw ParseError("Expected '<' after 'delegate'!", CurrentPosition);
                }

                string functionName = "";
                do
                {
                    if (Consume(TokenType.Word) is ScriptToken identifier)
                    {
                        identifier.SyntaxType = EF.Function;
                        if (functionName.Length > 0)
                        {
                            functionName += ".";
                        }
                        functionName += identifier.Value;
                    }
                    else
                    {
                        throw ParseError("Expected function name for delegate!", CurrentPosition);
                    }
                } while (Matches(TokenType.Dot, EF.Function));
                if (Consume(TokenType.RightArrow) is null)
                {
                    throw ParseError("Expected '>' after function name!", CurrentPosition);
                }
                return new DelegateType(new Function(functionName, default, null, null, null), delegateToken.StartPos, CurrentPosition);
            }

            if (Consume(CLASS) is { } classToken)
            {
                classToken.SyntaxType = EF.Keyword;
                if (Consume(TokenType.LeftArrow) is null)
                {
                    return new ClassType(new VariableType(OBJECT), classToken.StartPos, classToken.EndPos);
                }

                if (Consume(TokenType.Word) is not { } classNameToken)
                {
                    throw ParseError("Expected class name!", CurrentPosition);
                }

                classNameToken.SyntaxType = EF.Class;

                if (Consume(TokenType.RightArrow) is null)
                {
                    throw ParseError("Expected '>' after class name!", CurrentPosition);
                }
                return new ClassType(new VariableType(classNameToken.Value), classToken.StartPos, PrevToken.EndPos);
            }

            ScriptToken type = Consume(TokenType.Word);
            if (type == null)
            {
                return null;
            }

            type.SyntaxType = type.Value is INT or FLOAT or BOOL or BYTE or BIOMASK4 or STRING or STRINGREF or NAME ? EF.Keyword : EF.Class;
            return new VariableType(type.Value, type.StartPos, type.EndPos);
        }

        #region Helpers

        public bool Matches(string str, EF syntaxType = EF.None)
        {
            bool matches = CurrentIs(str);
            if (matches)
            {
                if (syntaxType != EF.None)
                {
                    CurrentToken.SyntaxType = syntaxType;
                }
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

        public bool Matches(TokenType tokenType, EF syntaxType = EF.None)
        {
            if (Tokens.CurrentItem.Type == tokenType)
            {
                if (syntaxType != EF.None)
                {
                    CurrentToken.SyntaxType = syntaxType;
                }
                Tokens.Advance();
                return true;
            }

            return false;
        }

        public bool NextIs(string str)
        {
            return Tokens.LookAhead(1) is {} nextToken && nextToken.Type != TokenType.EOF && nextToken.Value.Equals(str, StringComparison.OrdinalIgnoreCase);
        }

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

        public ScriptToken Consume(TokenType tokenType) => Tokens.ConsumeToken(tokenType);

        public ScriptToken Consume(params TokenType[] tokenTypes) => tokenTypes.Select(Consume).NonNull().FirstOrDefault();

        public ScriptToken Consume(string str)
        {

            ScriptToken token = null;
            if (CurrentIs(str))
            {
                token = CurrentToken;
                Tokens.Advance();
            }
            return token;
        }

        public ScriptToken Consume(params string[] strs) => strs.Select(Consume).NonNull().FirstOrDefault();


        protected bool TypeCompatible(VariableType dest, VariableType src, int errorPosition, bool coerce = false)
        {
            if (dest is DynamicArrayType destDynArr && src is DynamicArrayType srcDynArr)
            {
                return TypeCompatible(destDynArr.ElementType, srcDynArr.ElementType, errorPosition);
            }
            if (dest is DynamicArrayType || src is DynamicArrayType)
            {
                return false;
            }
            if (dest is StaticArrayType || src is StaticArrayType)
            {
                if (dest is StaticArrayType destArr && src is StaticArrayType srcArr && destArr.Length == srcArr.Length)
                {
                    return TypeCompatible(destArr.ElementType, srcArr.ElementType, errorPosition);
                }
                return false;
            }

            if (dest is ClassType destClassType && src is ClassType srcClassType)
            {
                return destClassType.ClassLimiter == srcClassType.ClassLimiter || ((Class)srcClassType.ClassLimiter).SameAsOrSubClassOf(destClassType.ClassLimiter.Name);
            }

            if (dest.PropertyType == EPropertyType.Byte && src.PropertyType == EPropertyType.Byte)
            {
                return true;
            }

            if (dest is DelegateType destDel && src is DelegateType srcDel)
            {
                if (!destDel.DefaultFunction.SignatureEquals(srcDel.DefaultFunction))
                {
                    //should be en error, but there is bioware code that would have only compiled if all delegates are considered the same type
                    Log.LogWarning("Incompatible delegate types! This will likely cause a runtime error.", errorPosition);
                }
                return true;
            }

            if (dest is Class destClass)
            {
                if (src is Class srcClass)
                {
                    if (srcClass.SameAsOrSubClassOf(destClass))
                    {
                        return true;
                    }
                    if (srcClass.IsInterface)
                    {
                        return destClass.Implements(srcClass);
                    }

                    if (destClass.IsInterface)
                    {
                        return srcClass.Implements(destClass);
                    }
                    //allow implicit downcasts. This seems super wrong obviously. A sane type system would require an explicit downcast.
                    //But to make this work with existing bioware code, it's this, or write a control-flow analyzer that implicitly downcasts based on typecheck conditional gates
                    //I have chosen the lazy path
                    if (destClass.SameAsOrSubClassOf(srcClass))
                    {
                        Log.LogWarning("Dangerous implicit downcast! Use an explicit downcast.", errorPosition);
                        return true;
                    }
                    return false;
                }

                if (destClass.Name.CaseInsensitiveEquals("Object") && src is ClassType)
                {
                    return true;
                }

                if (src is null)
                {
                    return true;
                }
            }

            if (dest.Name.CaseInsensitiveEquals(src?.Name)) return true;
            ECast cast = CastHelper.GetConversion(dest, src);
            if (coerce)
            {
                return cast != ECast.Max;
            }
            return cast.Has(ECast.AutoConvert);
        }

        #endregion

        public Expression ParseLiteral()
        {
            ScriptToken token = CurrentToken;
            if (Matches(TokenType.IntegerNumber))
            {
                int val = int.Parse(token.Value, CultureInfo.InvariantCulture);
                return new IntegerLiteral(val, token.StartPos, token.EndPos) { NumType = INT };
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
                if (token.Value.Length > 63)
                {
                    Log.LogWarning($"Names should be less than 64 characters! (This name is {token.Value.Length})", token.StartPos, token.EndPos);
                }
                return new NameLiteral(token.Value, token.StartPos, token.EndPos);
            }

            if (Matches(TokenType.StringRefLiteral))
            {
                return new StringRefLiteral(int.Parse(token.Value, CultureInfo.InvariantCulture), token.StartPos, token.EndPos);
            }

            if (Matches(TRUE, FALSE))
            {
                token.SyntaxType = EF.Keyword;
                return new BooleanLiteral(bool.Parse(token.Value), token.StartPos, token.EndPos);
            }

            if (Matches(NONE, EF.Keyword))
            {
                return new NoneLiteral(token.StartPos, token.EndPos);
            }

            if (CurrentIs(VECT) && Tokens.LookAhead(1).Type == TokenType.LeftParenth)
            {
                token.SyntaxType = EF.Keyword;
                Tokens.Advance();
                return ParseVectorLiteral();
            }

            if (CurrentIs(ROT) && Tokens.LookAhead(1).Type == TokenType.LeftParenth)
            {
                token.SyntaxType = EF.Keyword;
                Tokens.Advance();
                return ParseRotatorLiteral();
            }

            return null;
        }

        private RotatorLiteral ParseRotatorLiteral()
        {
            var start = CurrentPosition;
            if (!Matches(TokenType.LeftParenth))
            {
                throw ParseError($"Expected '(' after '{ROT}' in rotator literal!", CurrentPosition);
            }

            int pitch = ParseInt();

            if (!Matches(TokenType.Comma))
            {
                throw ParseError("Expected ',' after pitch component in rotator literal!", CurrentPosition);
            }

            int yaw = ParseInt();

            if (!Matches(TokenType.Comma))
            {
                throw ParseError("Expected ',' after yaw component in rotator literal!", CurrentPosition);
            }

            int roll = ParseInt();

            if (!Matches(TokenType.RightParenth))
            {
                throw ParseError("Expected ')' after roll component in rotator literal!", CurrentPosition);
            }

            return new RotatorLiteral(pitch, yaw, roll, start, Tokens.Prev().EndPos);
        }

        private VectorLiteral ParseVectorLiteral()
        {
            var start = CurrentPosition;
            if (!Matches(TokenType.LeftParenth))
            {
                throw ParseError($"Expected '(' after '{VECT}' in vector literal!", CurrentPosition);
            }

            float x = ParseFloat();

            if (!Matches(TokenType.Comma))
            {
                throw ParseError("Expected ',' after x component in vector literal!", CurrentPosition);
            }

            float y = ParseFloat();

            if (!Matches(TokenType.Comma))
            {
                throw ParseError("Expected ',' after y component in vector literal!", CurrentPosition);
            }

            float z = ParseFloat();

            if (!Matches(TokenType.RightParenth))
            {
                throw ParseError("Expected ')' after z component in vector literal!", CurrentPosition);
            }

            return new VectorLiteral(x, y, z, start, Tokens.Prev().EndPos);
        }

        float ParseFloat()
        {
            bool isNegative = Matches(TokenType.MinusSign);
            if (!Matches(TokenType.FloatingNumber) && !Matches(TokenType.IntegerNumber))
            {
                throw ParseError("Expected number literal!", CurrentPosition);
            }

            var val = float.Parse(Tokens.Prev().Value, CultureInfo.InvariantCulture);
            return isNegative ? -val : val;
        }

        int ParseInt()
        {
            bool isNegative = Matches(TokenType.MinusSign);
            if (!Matches(TokenType.IntegerNumber))
            {
                throw ParseError("Expected integer literal!", CurrentPosition);
            }

            var val = int.Parse(Tokens.Prev().Value, CultureInfo.InvariantCulture);
            return isNegative ? -val : val;
        }

        protected ObjectLiteral ParseObjectLiteral(ScriptToken className, ScriptToken objName, bool noActors = true)
        {
            className.SyntaxType = EF.Class;
            bool isClassLiteral = className.Value.CaseInsensitiveEquals(CLASS);

            var classType = new VariableType((isClassLiteral ? objName : className).Value);
            if (!Symbols.TryResolveType(ref classType))
            {
                throw ParseError($"No type named '{classType.Name}' exists!", className);
            }

            if (classType is Class cls)
            {
                if (isClassLiteral)
                {
                    Tokens.AddDefinitionLink(classType, objName);
                    classType = new ClassType(classType);
                }
                else
                {
                    if (noActors && cls.SameAsOrSubClassOf("Actor"))
                    {
                        TypeError("Object constants must not be Actors!", className);
                    }

                    Tokens.AddDefinitionLink(classType, className);
                }
                

                return new ObjectLiteral(new NameLiteral(objName.Value, objName.StartPos, objName.EndPos), classType, className.StartPos, objName.EndPos);
            }

            throw ParseError($"'{classType.Name}' is not a class!", className);
        }

        protected SymbolReference NewSymbolReference(ASTNode symbol, ScriptToken token, bool isDefaultRef)
        {
            SymbolReference symRef;
            if (isDefaultRef)
            {
                symRef = new DefaultReference(symbol, token.Value, token.StartPos, token.EndPos);
            }
            else
            {
                symRef = new SymbolReference(symbol, token.Value, token.StartPos, token.EndPos);
            }

            if (token.Value.CaseInsensitiveEquals("Outer") && symbol is VariableDeclaration fakeOuterVarDecl)
            {
                Tokens.AddDefinitionLink(fakeOuterVarDecl.VarType, token);
            }
            else
            {
                Tokens.AddDefinitionLink(symbol, token);
            }
            if (symRef.Node is Function)
            {
                token.SyntaxType = EF.Function;
                if (isDefaultRef)
                {
                    TypeError("Expected property name!", token);
                }
            }

            return symRef;

        }

        public Expression ParseConstValue()
        {
            //minus sign is not parsed as part of a literal, so do it manually
            bool isNegative = Matches(TokenType.MinusSign);

            Expression literal = ParseLiteral();
            if (literal is null)
            {
                TypeError("Expected a literal value for the constant!", CurrentPosition);
            }

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
                        TypeError("Malformed constant value!", CurrentPosition);
                        break;
                }
            }

            return literal;
        }
    }

    public class ParseException : Exception
    {
        public ParseException(string msg) : base(msg){}
    }
}
