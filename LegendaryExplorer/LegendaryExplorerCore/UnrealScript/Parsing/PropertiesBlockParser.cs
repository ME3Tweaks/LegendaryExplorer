using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Utilities;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript.Parsing
{
    internal sealed class PropertiesBlockParser : StringParserBase
    {
        private readonly Stack<ObjectType> ExpressionScopes = [];
        private readonly Stack<Class> SubObjectClasses = [];
        private readonly IMEPackage Pcc;
        private readonly bool IsStructDefaults;
        private readonly bool IsInDefaultsTree;
        private readonly ObjectType Outer;
        private readonly UnrealScriptOptionsPackage usop;
        private bool InSubOject;
        private bool IsT3D;

        public static void Parse(DefaultPropertiesBlock propsBlock, IMEPackage pcc, SymbolTable symbols, MessageLog log, bool isInDefaultsTree, UnrealScriptOptionsPackage usop)
        {
            var parser = new PropertiesBlockParser(propsBlock, pcc, symbols, log, isInDefaultsTree, usop);
            var statements = parser.Parse(false);

            propsBlock.Statements = statements;
        }

        public static void ParseStructDefaults(Struct s, IMEPackage pcc, SymbolTable symbols, MessageLog log, UnrealScriptOptionsPackage usop)
        {
            symbols.PushScope(s.Name);
            foreach (Struct innerStruct in s.TypeDeclarations.OfType<Struct>())
            {
                ParseStructDefaults(innerStruct, pcc, symbols, log, usop);
            }
            var defaults = s.DefaultProperties;
            if (defaults.Tokens is not null)
            {
                Parse(defaults, pcc, symbols, log, false, usop);
            }
            symbols.PopScope();
        }

        public static List<Subobject> ParseBulkPropsFile(TokenStream tokens, IMEPackage pcc, SymbolTable symbols, MessageLog log, bool isInDefaultsTree, UnrealScriptOptionsPackage usop)
        {
            var parser = new PropertiesBlockParser(tokens, pcc, symbols, log, isInDefaultsTree, usop);
            return parser.ParseBulkProps();
        }

        public static Subobject ParseT3D(TokenStream tokens, IMEPackage pcc, SymbolTable symbols, MessageLog log, UnrealScriptOptionsPackage usop)
        {
            var parser = new PropertiesBlockParser(tokens, pcc, symbols, log, false, usop)
            {
                IsT3D = true
            };
            return parser.ParseObjectDeclaration();
        }

        private PropertiesBlockParser(IMEPackage pcc, SymbolTable symbols, MessageLog log, bool isInDefaultsTree, UnrealScriptOptionsPackage usop)
        {
            this.usop = usop;
            Symbols = symbols;
            Log = log;
            Pcc = pcc;
            IsInDefaultsTree = isInDefaultsTree;
        }

        private PropertiesBlockParser(DefaultPropertiesBlock propsBlock, IMEPackage pcc, SymbolTable symbols, MessageLog log, bool isInDefaultsTree, UnrealScriptOptionsPackage usop) : this(pcc, symbols, log, isInDefaultsTree, usop)
        {
            Tokens = propsBlock.Tokens;
            var outer = (ObjectType)propsBlock.Outer;
            IsStructDefaults = outer is Struct;

            ExpressionScopes.Push(outer);
        }

        private PropertiesBlockParser(TokenStream tokens, IMEPackage pcc, SymbolTable symbols, MessageLog log, bool isInDefaultsTree, UnrealScriptOptionsPackage usop) : this(pcc, symbols, log, isInDefaultsTree, usop)
        {
            Tokens = tokens;
            IsStructDefaults = false;
        }

        private List<Statement> Parse(bool requireBrackets = true)
        {
            if (requireBrackets && Consume(TokenType.LeftBracket) == null) throw ParseError("Expected '{'!", CurrentPosition);

            var statements = new List<Statement>();
            var subObjects = new List<Subobject>();
            Symbols.PushScope("DefaultProperties");
            try
            {
                var current = ParseTopLevelStatement();
                while (current != null)
                {
                    if (current is Subobject subObj)
                    {
                        subObjects.Add(subObj);
                    }
                    statements.Add(current);
                    current = ParseTopLevelStatement();
                }
                InSubOject = true;
                ParseSubObjectBodys(subObjects);
            }
            finally
            {
                Symbols.PopScope();
            }

            if (requireBrackets && Consume(TokenType.RightBracket) == null) throw ParseError("Expected '}'!", CurrentPosition);
            return statements;
        }

        private List<Subobject> ParseBulkProps()
        {
            var objects = new List<Subobject>();

            var current = ParseObjectDeclaration();
            while (current is not null)
            {
                objects.Add(current);
                current = ParseObjectDeclaration();
            }

            return objects;
        }

        private Statement ParseTopLevelStatement()
        {
            if (CurrentIs("BEGIN") && (NextIs("Object") || NextIs("Template")))
            {
                if (IsStructDefaults)
                {
                    throw ParseError($"SubObjects are not allowed in {STRUCTDEFAULTPROPERTIES}!", CurrentPosition);
                }
                if (!IsInDefaultsTree)
                {
                    TypeError("SubObjects are only allowed in Default objects", CurrentPosition);
                }
                return ParseSubobjectDeclaration();
            }

            return ParseNonStructAssignment();
        }
        private void ParseSubObjectBodys(List<Subobject> subObjects)
        {
            foreach (Subobject currentSubObj in subObjects)
            {
                var subSubObjects = new List<Subobject>();
                var statements = currentSubObj.Statements;
                var objectClass = currentSubObj.Class;
                var objectName = currentSubObj.NameDeclaration;
                ExpressionScopes.Push(objectClass);
                SubObjectClasses.Push(objectClass);
                Symbols.PushScope(objectName);
                Tokens = currentSubObj.Tokens;
                try
                {
                    var current = ParseTopLevelStatement();
                    while (current != null)
                    {
                        if (current is Subobject subObj)
                        {
                            subSubObjects.Add(subObj);
                        }
                        statements.Add(current);
                        current = ParseTopLevelStatement();
                    }
                    ParseSubObjectBodys(subSubObjects);
                }
                finally
                {
                    Symbols.PopScope();
                    ExpressionScopes.Pop();
                    SubObjectClasses.Pop();
                }
            }
        }

        private Subobject ParseSubobjectDeclaration()
        {
            var startPos = CurrentPosition;

            //BEGIN
            CurrentToken.SyntaxType = ST.Keyword;
            Tokens.Advance();
            var objOrTemplateToken = CurrentToken;
            objOrTemplateToken.SyntaxType = ST.Keyword;
            Tokens.Advance();
            bool isTemplate = objOrTemplateToken.Value.CaseInsensitiveEquals("Template");

            if (!Matches("Class", ST.Keyword) || !Matches(TokenType.Assign, ST.Operator))
            {
                throw ParseError("Expected 'Class=' after 'Begin Object'!", CurrentPosition);
            }

            var classNameToken = Consume(TokenType.Word);
            if (classNameToken is null)
            {
                throw ParseError("Expected name of class!", CurrentPosition);
            }
            classNameToken.SyntaxType = ST.Class;

            if (!Symbols.TryGetType(classNameToken.Value, out Class objectClass))
            {
                throw ParseError($"{classNameToken} is not the name of a class!", classNameToken);
            }

            if (objectClass.OuterClass is Class outerClass && SubObjectClasses.Any() && !SubObjectClasses.Peek().SameAsOrSubClassOf(outerClass))
            {
                TypeError($"A '{objectClass.Name}' must be declared within a '{outerClass.Name}', not a '{SubObjectClasses.Peek().Name}'!", classNameToken);
            }

            if (!Matches("Name", ST.Keyword) || !Matches(TokenType.Assign, ST.Operator))
            {
                throw ParseError("Expected 'Name=' after Class reference!", CurrentPosition);
            }

            var nameToken = Consume(TokenType.Word);
            if (nameToken is null)
            {
                throw ParseError($"Expected name of {objOrTemplateToken.Value}!", CurrentPosition);
            }
            string objectName = nameToken.Value;

            var bodyStartPos = CurrentToken.StartPos;
            int nestedLevel = 1;
            var tokens = new List<ScriptToken>();
            while (true)
            {
                if (CurrentTokenType == TokenType.EOF)
                {
                    throw ParseError("SubObject declaration has no end!", startPos);
                }
                if (CurrentIs("BEGIN") && (NextIs("Object") || NextIs("Template")))
                    nestedLevel++;
                else if (CurrentIs("END") && (NextIs("Object") || NextIs("Template")))
                    nestedLevel--;
                if (nestedLevel <= 0)
                {
                    break;
                }
                tokens.Add(CurrentToken);
                Tokens.Advance();
            }
            //END
            CurrentToken.SyntaxType = ST.Keyword;
            Tokens.Advance();
            //Object or Template
            CurrentToken.SyntaxType = ST.Keyword;
            Tokens.Advance();

            var subObj = new Subobject(objectName, objectClass, new List<Statement>(), isTemplate, startPos, PrevToken.EndPos)
            {
                Tokens = new TokenStream(tokens, Tokens)
            };
            if (!Symbols.TryAddSymbol(objectName, subObj))
            {
                throw ParseError($"'{objectName}' has already been defined in this scope!", nameToken);
            }
            return subObj;
        }

        private Subobject ParseObjectDeclaration()
        {
            var startPos = CurrentPosition;

            if (!Matches("BEGIN", ST.Keyword))
            {
                return null;
            }
            if (!Matches("OBJECT", ST.Keyword))
            {
                return null;
            }
            if (!Matches("Class", ST.Keyword) || !Matches(TokenType.Assign, ST.Operator))
            {
                throw ParseError("Expected 'Class=' after 'Begin Object'!", CurrentPosition);
            }

            var classNameToken = Consume(TokenType.Word) ?? throw ParseError("Expected name of class!", CurrentPosition);
            classNameToken.SyntaxType = ST.Class;

            if (!Symbols.TryGetType(classNameToken.Value, out Class objectClass))
            {
                throw ParseError($"{classNameToken} is not the name of a class!", classNameToken);
            }

            if (objectClass.OuterClass is Class outerClass && SubObjectClasses.Count > 0 && !SubObjectClasses.Peek().SameAsOrSubClassOf(outerClass))
            {
                TypeError($"A '{objectClass.Name}' must be declared within a '{outerClass.Name}', not a '{SubObjectClasses.Peek().Name}'!", classNameToken);
            }

            if (!Matches("Name", ST.Keyword) || !Matches(TokenType.Assign, ST.Operator))
            {
                throw ParseError("Expected 'Name=' after Class reference!", CurrentPosition);
            }

            string objectName;
            if (IsT3D)
            {
                objectName = "";
                //not really accurate, should only allow a subset of token types but this won't have false _negatives_ at least
                while (PrevToken.EndPos == CurrentToken.StartPos) //loop until whitespace
                {
                    objectName += CurrentToken.Value;
                    Tokens.Advance();
                }
            }
            else
            {
                objectName = Consume(TokenType.NameLiteral)?.Value ?? throw ParseError("Expected full path of Object!", CurrentPosition);
            }
            var statements = new List<Statement>();
            SubObjectClasses.Push(objectClass);
            ExpressionScopes.Push(objectClass);
            Symbols.PushScope(objectName);
            try
            {
                while (true)
                {
                    if (CurrentIs("BEGIN") && NextIs("Object"))
                    {
                        if (!IsT3D)
                        {
                            throw ParseError("SubObject declarations are not allowed in this context!", startPos);
                        }
                        statements.Add(ParseObjectDeclaration());
                    }
                    else if (CurrentIs("END") && NextIs("Object"))
                    {
                        break;
                    }
                    else
                    {
                        AssignStatement assignStatement = ParseNonStructAssignment() ?? throw ParseError("Object declaration has no end!", startPos);
                        if (IsT3D && assignStatement.Target.ResolveType() is ErrorType)
                        {
                            continue;
                        }
                        statements.Add(assignStatement);
                    }
                }
            }
            finally
            {

                Symbols.PopScope();
                ExpressionScopes.Pop();
                SubObjectClasses.Pop();
            }
            //END
            CurrentToken.SyntaxType = ST.Keyword;
            Tokens.Advance();
            //Object
            CurrentToken.SyntaxType = ST.Keyword;
            Tokens.Advance();

            //T3D support: condense dynamic array by-index assignments into a single DynamicArrayLiteral assignment
            if (IsT3D)
            {
                for (int i = 0; i < statements.Count; i++)
                {
                    Statement statement = statements[i];
                    if (statement is AssignStatement { Target: ArraySymbolRef { IsDynamic: true, Array: SymbolReference { Node: {} arrayDecl } target } } firstAssignStatement)
                    {
                        int startIndex = i;
                        var values = new List<Expression>();
                        long arrIndex = -1;
                        int dynArrayLitEndPos = firstAssignStatement.EndPos;
                        var dynamicArrayType = (DynamicArrayType)target.ResolveType();
                        var elementType = dynamicArrayType.ElementType;
                        while (statement is AssignStatement
                               {
                                   Target: ArraySymbolRef
                                   {
                                       IsDynamic: true,
                                       Index: IntegerLiteral { Value: long valIndex } intLit,
                                       Array: SymbolReference { Node: {} arrayRef } 
                                   }
                               } assignStatement &&
                               arrayRef == arrayDecl)
                        {
                            if (valIndex < arrIndex)
                            {
                                TypeError($"Dynamic array assignments must be unique and sequential.)", intLit);
                            }
                            for (; arrIndex + 1 < valIndex ; arrIndex++)
                            {
                                values.Add(elementType switch {
                                    ClassType => new NoneLiteral(),
                                    Class => new NoneLiteral(),
                                    DelegateType => new NoneLiteral(),
                                    Enumeration => new NoneLiteral(),
                                    Struct @struct => new StructLiteral(@struct, []),
                                    ObjectType => new NoneLiteral(),
                                    _ => elementType.PropertyType switch {
                                        EPropertyType.Byte => new IntegerLiteral(0),
                                        EPropertyType.Int => new IntegerLiteral(0),
                                        EPropertyType.Bool => new BooleanLiteral(false),
                                        EPropertyType.Float => new FloatLiteral(0f),
                                        EPropertyType.Name => new NameLiteral("None"),
                                        EPropertyType.String => new StringLiteral(""),
                                        EPropertyType.StringRef => new StringRefLiteral(0),
                                        _ => throw new ArgumentOutOfRangeException()
                                    }
                                });
                            }
                            arrIndex = valIndex;
                            values.Add(assignStatement.Value);
                            i++;
                            statement = statements[i];
                        }
                        statements[startIndex] = new AssignStatement(target,
                            new DynamicArrayLiteral(dynamicArrayType, values, firstAssignStatement.Value.StartPos, dynArrayLitEndPos),
                            firstAssignStatement.StartPos, dynArrayLitEndPos);
                        statements.RemoveRange(startIndex + 1, i - (startIndex + 1));
                        i = startIndex;
                    }
                }
            }

            var subObj = new Subobject(objectName, objectClass, statements, false, startPos, PrevToken.EndPos);
            foreach (Statement statement in statements)
            {
                statement.Outer = subObj;
            }
            return subObj;
        }

        private AssignStatement ParseNonStructAssignment()
        {
            if (CurrentIs(TokenType.RightBracket, TokenType.EOF))
            {
                return null;
            }
            var statement = ParseAssignment(false);
            if (statement is null)
            {
                return null;
            }

            Consume(TokenType.SemiColon); //semicolon's are optional
            return statement;
        }

        private AssignStatement ParseAssignment(bool inStruct)
        {
            if (Consume(TokenType.Word) is ScriptToken propName)
            {
                SymbolReference target = ParsePropName(propName, inStruct);
                if (InSubOject && target.Node is VariableDeclaration { IsTransient: true })
                {
                    TypeError("Cannot assign to a transient property in a SubObject!", propName);
                }
                VariableType targetType = target.ResolveType();
                if (Matches(TokenType.LeftSqrBracket) || (IsT3D && Matches(TokenType.LeftParenth)))
                {
                    var endTokenType = PrevToken.Type is TokenType.LeftSqrBracket ? TokenType.RightSqrBracket : TokenType.RightParenth;
                    Expression expression = ParseLiteral();
                    if (expression is not IntegerLiteral intLit)
                    {
                        throw ParseError("Expected an integer index!", expression?.StartPos ?? CurrentPosition, expression?.EndPos ?? -1);
                    }

                    if (targetType is StaticArrayType arrType)
                    {
                        if (intLit.Value >= arrType.Length)
                        {
                            TypeError($"'{propName}' only has {arrType.Length} elements!", intLit);
                        }
                        else if (intLit.Value < 0)
                        {
                            TypeError("Index cannot be a negative number!", intLit);
                        }
                        else
                        {
                            targetType = arrType.ElementType;
                        }
                    }
                    else if (IsT3D && targetType is DynamicArrayType dynArrType)
                    {
                        if (intLit.Value < 0)
                        {
                            TypeError("Index cannot be a negative number!", intLit);
                        }
                        else
                        {
                            targetType = dynArrType.ElementType;
                        }
                    }
                    else
                    {
                        TypeError($"Cannot index a property that is not {(IsT3D ? "an" : "a static")} array!", intLit);
                    }

                    if (Consume(endTokenType) is not { } closeBracket)
                    {
                        throw ParseError($"Expected a {(endTokenType is TokenType.RightSqrBracket ? ']' : ')')}!", CurrentPosition);
                    }
                    target = new ArraySymbolRef(target, intLit, target.StartPos, closeBracket.EndPos);
                }
                else if (targetType is StaticArrayType)
                {
                    throw ParseError($"Cannot assign directly to a static array! You must assign to each index individually, (eg. {propName.Value}[0] = ...)", propName);
                }
                if (Matches(TokenType.Assign, ST.Operator))
                {
                    Expression literal = ParseValue(targetType);
                    return new AssignStatement(target, literal, propName.StartPos, literal.EndPos);
                }

                throw ParseError("Expected '=' in assignment statement!", CurrentPosition);
            }

            throw ParseError("Expected name of property!", CurrentPosition);
        }

        private Expression ParseValue(VariableType targetType)
        {
            Expression literal;
            if (Matches(TokenType.LeftBracket))
            {
                if (targetType is not Struct targetStruct)
                {
                    throw ParseError($"A '{{' is used to start a struct. Expected a {targetType.DisplayName()} literal!", CurrentPosition);
                }
                if (IsT3D && !Matches(TokenType.LeftParenth))
                {
                    throw ParseError("In T3D format, a struct literal with a '{' must have a '(' after it.");
                }
                literal = FinishStructLiteral(targetStruct);
                if (IsT3D && !Matches(TokenType.RightBracket))
                {
                    throw ParseError("This struct literal was begun with '{(', so it must end with ')}'.");
                }
            }
            else if (Matches(TokenType.LeftParenth))
            {
                switch (targetType)
                {
                    case DynamicArrayType dynamicArrayType:
                        literal = FinishDynamicArrayLiteral(dynamicArrayType);
                        break;
                    case Struct targetStruct:
                        if (!IsT3D)
                        {
                            ParseError("Use '{' for struct literals, not '('.", CurrentPosition);
                            goto default;
                        }
                        literal = FinishStructLiteral(targetStruct);
                        break;
                    default:
                        if (IsT3D && targetType.PropertyType is EPropertyType.String && Matches(TokenType.RightParenth))
                        {
                            //I guess () is supposed to be empty string??? whyyyy Epic, whyyyyy
                            literal = new StringLiteral("");
                            break;
                        }
                        throw ParseError($"A '(' is used to start a {(IsT3D ? "struct or " : "")}dynamic array literal. Expected a {targetType.DisplayName()} literal!", CurrentPosition);
                }
            }
            else
            {
                var literalStart = CurrentPosition;
                bool isNegative = Matches(TokenType.MinusSign, ST.Operator);

                literal = ParseLiteral();
                if (literal is null)
                {
                    if (Consume(TokenType.Word) is { } token)
                    {
                        if (Consume(TokenType.NameLiteral) is { } objName)
                        {
                            literal = ParseObjectLiteral(token, objName, false);
                        }
                        else if (IsT3D && targetType.PropertyType is EPropertyType.Name)
                        {
                            literal = new NameLiteral(token.Value, token.StartPos, token.EndPos);
                        }
                        else if (IsT3D && targetType is Enumeration enm && enm.Values.FirstOrDefault(val => val.Name.CaseInsensitiveEquals(token.Value)) is EnumValue enumValue)
                        {
                            token.SyntaxType = ST.Enum;
                            Tokens.AddDefinitionLink(enm, token);
                            literal = NewSymbolReference(enumValue, token, false);
                        }
                        else
                        {
                            literal = ParseBasicRef(token);
                            if (literal is SymbolReference { Node: Const cnst })
                            {
                                literal = cnst.Literal;
                            }
                        }
                    }
                    else
                    {
                        throw ParseError("Expected a value!", CurrentPosition);
                    }
                }

                if (isNegative)
                {
                    //clone the literals so we don't modify Const values
                    switch (literal)
                    {
                        case FloatLiteral floatLiteral:
                            literal = new FloatLiteral(floatLiteral.Value * -1, literalStart, floatLiteral.EndPos);
                            break;
                        case IntegerLiteral integerLiteral:
                            literal = new IntegerLiteral(integerLiteral.Value * -1, literalStart, integerLiteral.EndPos);
                            break;
                        default:
                            throw ParseError("Unexpected '-' !", literalStart);
                    }
                }
            }
            if (IsT3D && targetType is ErrorType)
            {
                return literal;
            }
            VerifyLiteral(targetType, ref literal);
            return literal;
        }

        private StructLiteral FinishStructLiteral(Struct targetStruct)
        {
            ScriptToken openingBracket = PrevToken;
            var statements = new List<AssignStatement>();
            var endTokenType = IsT3D ? TokenType.RightParenth : TokenType.RightBracket;
            if (!Matches(endTokenType))
            {
                ExpressionScopes.Push(targetStruct);
                try
                {
                    var statement = ParseAssignment(true);
                    statements.Add(statement);
                    while (Matches(TokenType.Comma))
                    {
                        statement = ParseAssignment(true);
                        statements.Add(statement);
                    }
                    if (!Matches(endTokenType))
                    {
                        throw ParseError($"Expected struct literal to end with a '{(IsT3D ? ')' : '}')}'!", openingBracket.StartPos, CurrentPosition);
                    }
                }
                finally
                {
                    ExpressionScopes.Pop();
                }
            }

            return new StructLiteral(targetStruct, statements, PrevToken.StartPos, PrevToken.EndPos);
        }

        private DynamicArrayLiteral FinishDynamicArrayLiteral(DynamicArrayType arrayType)
        {
            ScriptToken openingParen = PrevToken;
            var values = new List<Expression>();

            if (!Matches(TokenType.RightParenth))
            {
                var targetType = arrayType.ElementType;
                var value = ParseValue(targetType);
                values.Add(value);
                while (Matches(TokenType.Comma))
                {
                    value = ParseValue(targetType);
                    values.Add(value);
                }

                if (!Matches(TokenType.RightParenth))
                {
                    throw ParseError("Expected array literal to end with a ')'!", openingParen.StartPos, CurrentPosition);
                }
            }

            return new DynamicArrayLiteral(arrayType, values, openingParen.StartPos, PrevToken.EndPos);
        }

        private void VerifyLiteral(VariableType targetType, ref Expression literal)
        {
            switch (targetType)
            {
                case Class targetClass:
                    if (literal is not NoneLiteral)
                    {
                        VariableType valueClass;
                        if (literal is ObjectLiteral objectLiteral)
                        {
                            VerifyObjectLiteral(objectLiteral);
                            valueClass = objectLiteral.Class;
                        }
                        else if (literal is SymbolReference { Node: Subobject { Class: Class subObjClass } })
                        {
                            valueClass = subObjClass;
                        }
                        else
                        {
                            TypeError($"Expected an {OBJECT} literal or sub-object name!", literal);
                            break;
                        }

                        if (valueClass is not (Class or ClassType)
                            || valueClass is Class literalClass && !literalClass.SameAsOrSubClassOf(targetClass)
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
                        else if (targetClassLimiter.ClassLimiter != literalClassType.ClassLimiter && !((Class)literalClassType.ClassLimiter).SameAsOrSubClassOf(targetClassLimiter.ClassLimiter.Name))
                        {
                            if (literalClassType.ClassLimiter.Name is "BioDeprecated")
                            {
                                LogWarning("Use of BioDeprecated! If this is pre-existing it's probably fine, but do not write new code like this.");
                            }
                            else
                            {
                                TypeError($"Cannot assign a value of type '{literalClassType.DisplayName()}' to a variable of type '{targetClassLimiter.DisplayName()}'.", literal);
                            }
                        }
                    }

                    break;
                case DelegateType delegateType:
                    if (literal is not NoneLiteral)
                    {
                        if (literal is not SymbolReference { Node: Function func })
                        {
                            if (literal is ObjectLiteral { Class: ClassType { ClassLimiter: Class containingclass } } && Matches(TokenType.Dot) && Consume(TokenType.Word) is ScriptToken funcNameToken
                                && Symbols.TryGetSymbolInScopeStack(funcNameToken.Value, out func, containingclass.GetScope()))
                            {
                                literal = new CompositeSymbolRef(literal, NewSymbolReference(func, funcNameToken, false), true, literal.StartPos, funcNameToken.EndPos);
                            }
                            else if (literal is ObjectLiteral {Class: Class cls} objLit && Matches(TokenType.Dot) && Consume(TokenType.Word) is ScriptToken funcNameTok 
                                     && Symbols.TryGetSymbolInScopeStack(funcNameTok.Value, out func, cls.GetScope()))
                            {
                                VerifyObjectLiteral(objLit);
                                literal = new CompositeSymbolRef(literal, NewSymbolReference(func, funcNameTok, false), false, literal.StartPos, funcNameTok.EndPos);
                            }
                            else
                            {
                                TypeError("Expected a function reference!", literal);
                                break;
                            }
                        }
                        if (!func.SignatureEquals(delegateType.DefaultFunction))
                        {
                            TypeError($"Expected a function with the same signature as {(delegateType.DefaultFunction.Outer as Class)?.Name}.{delegateType.DefaultFunction.Name}!", literal);
                        }
                    }
                    break;
                case DynamicArrayType arrayType:
                    if (literal is not DynamicArrayLiteral)
                    {
                        if (arrayType.ElementType != SymbolTable.ByteType
                            || literal is not StringLiteral stringLiteral
                            || !Base64.IsValid(stringLiteral.Value))
                        {
                            TypeError($"Expected a dynamic array literal!", literal);
                        }
                    }
                    break;
                case Enumeration enumeration:
                    if (literal is not NoneLiteral)
                    {
                        if (literal is not SymbolReference { Node: EnumValue enumVal })
                        {
                            var prevToken = PrevToken;
                            //this handles the case where a property has the same name as the Enumeration, and reparses it as an enumvalue
                            if (Symbols.TryGetType(prevToken.Value, out Enumeration enum2) && enum2 == enumeration
                                                                                           && Matches(TokenType.Dot) && Consume(TokenType.Word) is ScriptToken enumValueToken)
                            {
                                prevToken.SyntaxType = ST.Enum;
                                Tokens.AddDefinitionLink(enum2, prevToken);
                                if (enumeration.Values.FirstOrDefault(val => val.Name.CaseInsensitiveEquals(enumValueToken.Value)) is EnumValue enumValue)
                                {
                                    literal = NewSymbolReference(enumValue, enumValueToken, false);
                                    break;
                                }
                                throw ParseError("Expected valid enum value!", CurrentPosition);
                            }
                            TypeError($"Expected an enum value!", literal);
                        }
                        else if (enumeration != enumVal.Enum)
                        {
                            TypeError($"Expected an {enumeration.Name} value, not an {enumVal.Enum.Name} value!", literal);
                        }
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
                                TypeError($"Expected {TRUE} or {FALSE}!", literal);
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
                            if (IsT3D && literal is StringLiteral stringNameLiteral)
                            {
                                literal = new NameLiteral(stringNameLiteral.Value, stringNameLiteral.StartPos, stringNameLiteral.EndPos)
                                {
                                    Outer = stringNameLiteral.Outer
                                };
                            }
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
            return;

            void VerifyObjectLiteral(ObjectLiteral objectLiteral)
            {
                if (objectLiteral.Class is not ClassType && Pcc.FindEntry(objectLiteral.Name.Value, objectLiteral.Class.Name) is null)
                {
                    if (IsT3D)
                    {
                        return;
                    }
                    if (usop.MissingObjectResolver?.Invoke(Pcc, objectLiteral.Name.Value, objectLiteral.Class.Name) is null)
                    {
                        TypeError($"Could not find '{objectLiteral.Name.Value}' in this file!", objectLiteral);
                    }
                }
            }
        }

        private SymbolReference ParsePropName(ScriptToken token, bool inStruct)
        {
            ObjectType scopeObject = ExpressionScopes.Peek();
            ASTNode symbol;
            if (scopeObject.LookupVariable(token.Value) is { } decl)
            {
                symbol = decl;
            }
            else
            {
                if (!IsT3D)
                {
                    TypeError($"{scopeObject.GetScope()} has no member named '{token.Value}'!", token);
                }

                symbol = new ErrorType(scopeObject);
            }

            if (!inStruct && !IsT3D && (token.Value.CaseInsensitiveEquals("Name") || token.Value.CaseInsensitiveEquals("ObjectArchetype")))
            {
                TypeError($"Cannot set '{token.Value}' property!", token);
            }

            return NewSymbolReference(symbol, token, false);
        }

        private SymbolReference ParseBasicRef(ScriptToken token)
        {
            string specificScope = Symbols.CurrentScopeName;
            if (!Symbols.TryGetSymbolInScopeStack(token.Value, out ASTNode symbol, specificScope))
            {
                //const, or enum
                if (Symbols.TryGetType(token.Value, out VariableType destType))
                {
                    Tokens.AddDefinitionLink(destType, token);
                    if (destType is Enumeration enm && Matches(TokenType.Dot))
                    {
                        token.SyntaxType = ST.Enum;
                        if (Consume(TokenType.Word) is { } enumValName
                         && enm.Values.FirstOrDefault(val => val.Name.CaseInsensitiveEquals(enumValName.Value)) is EnumValue enumValue)
                        {
                            Tokens.AddDefinitionLink(enm, enumValName);
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
                Symbols.TryGetScopeSymbol(specificScope, out ASTNode scopeSymbol);
                symbol = new ErrorType(scopeSymbol);
            }

            return NewSymbolReference(symbol, token, false);
        }
    }
}
