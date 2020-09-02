using ME3Script.Analysis.Symbols;
using ME3Script.Compiling.Errors;
using ME3Script.Language.Tree;
using ME3Script.Language.Util;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3Script.Lexing;
using static ME3Script.Utilities.Keywords;

namespace ME3Script.Parsing
{
    public class CodeBodyParser : StringParserBase
    {
        private const int NOPRECEDENCE = int.MaxValue;
        private readonly SymbolTable Symbols;
        private readonly string OuterClassScope;
        private readonly IContainsLocals NodeVariables;
        private readonly ASTNode Node;
        private readonly CodeBody Body;

        private readonly Stack<string> ExpressionScopes;

        private bool IsFunction => Node.Type == ASTNodeType.Function;
        private bool IsState => Node.Type == ASTNodeType.State;

        private int _loopCount;
        private bool InLoop => _loopCount > 0;
        private int _switchCount;
        private bool _useDynamicResolution = false;
        private bool InSwitch => _switchCount > 0;

        public static void ParseFunction(Function func, string source, SymbolTable symbols, MessageLog log = null)
        {
            symbols.PushScope(func.Name);

            var tokenStream = new TokenStream<string>(new StringLexer(source, log), func.Body.StartPos, func.Body.EndPos);
            var bodyParser = new CodeBodyParser(tokenStream, func.Body, symbols, func, log);

            var body = bodyParser.ParseBody();

            //TODO: insert defaultParamvalue statements

            func.Body = body;

            symbols.PopScope();
        }

        public CodeBodyParser(TokenStream<string> tokens, CodeBody body, SymbolTable symbols, ASTNode containingNode, MessageLog log = null)
        {
            Log = log ?? new MessageLog();
            Symbols = symbols;
            Tokens = tokens;
            _loopCount = 0;
            _switchCount = 0;
            Node = containingNode;
            Body = body;
            OuterClassScope = NodeUtils.GetOuterClassScope(containingNode);
            // TODO: refactor a better solution to this mess
            if (IsState)
                NodeVariables = (containingNode as State);
            else if (IsFunction)
                NodeVariables = (containingNode as Function);

            ExpressionScopes = new Stack<string>();
            ExpressionScopes.Push(Symbols.CurrentScopeName);
        }

        public CodeBody ParseBody()
        {
            do
            {
                if (Tokens.CurrentItem.StartPos.Equals(Body.StartPos))
                    break;
                Tokens.Advance();
            } while (!Tokens.AtEnd());

            if (Tokens.AtEnd())
                throw Error("Could not find the code body for the current node, please contact the maintainers of this compiler!");

            var body = TryParseBody(false);
            if (body == null)
                return null;
            Body.Statements = body.Statements;

            if (Tokens.CurrentItem.Type != TokenType.EOF && !Tokens.CurrentItem.StartPos.Equals(Body.EndPos))
                throw Error("Could not parse a valid statement, even though the current code body has supposedly not ended yet.", 
                    CurrentPosition);

            return Body;
        }

        private static bool TypeCompatible(VariableType dest, VariableType src)
        {
            if (dest is DynamicArrayType destArr && src is DynamicArrayType srcArr)
            {
                return destArr.ElementType == srcArr.ElementType || CastHelper.GetConversion(destArr.ElementType, srcArr.ElementType).Has(ECast.AutoConvert);
            }

            if (dest is ClassType destClassType && src is ClassType srcClassType)
            {
                return destClassType.ClassLimiter == srcClassType.ClassLimiter || ((Class)srcClassType.ClassLimiter).SameAsOrSubClassOf(destClassType.ClassLimiter.Name);
            }

            if (dest is Enumeration enumType && src.PropertyType == EPropertyType.Byte)
            {
                return true;
            }

            if (dest is Class destClass && src is Class srcClass)
            {
                return srcClass.SameAsOrSubClassOf(destClass.Name);
            }
            return dest == src || CastHelper.GetConversion(dest, src).Has(ECast.AutoConvert);
        }

        public CodeBody TryParseBody(bool requireBrackets = true)
        {
            return (CodeBody)Tokens.TryGetTree(CodeParser);
            ASTNode CodeParser()
            {
                if (requireBrackets && Consume(TokenType.LeftBracket) == null) throw Error("Expected '{'!", CurrentPosition);

                var statements = new List<Statement>();
                var startPos = CurrentPosition;
                var current = TryParseInnerStatement();
                while (current != null)
                {
                    if (!SemiColonExceptions.Contains(current.Type) && Consume(TokenType.SemiColon) == null) throw Error("Expected semi-colon after statement!", CurrentPosition);
                    statements.Add(current);
                    if (CurrentToken.Type == TokenType.EOF)
                    {
                        break;
                    }
                    current = TryParseInnerStatement();
                }

                var endPos = CurrentPosition;
                if (requireBrackets && Consume(TokenType.RightBracket) == null) throw Error("Expected '}'!", CurrentPosition);

                return new CodeBody(statements, startPos, endPos);
            }
        }

        #region Statements

        public CodeBody TryParseBodyOrStatement(bool allowEmpty = false)
        {
            return (CodeBody)Tokens.TryGetTree(BodyParser);
            ASTNode BodyParser()
            {
                CodeBody body = null;
                var single = TryParseInnerStatement();
                if (single != null)
                {
                    var content = new List<Statement> {single};
                    body = new CodeBody(content, single.StartPos, single.EndPos);
                }
                else
                {
                    body = TryParseBody();
                }

                if (body == null)
                {
                    if (allowEmpty && Consume(TokenType.SemiColon) != null)
                    {
                        body = new CodeBody(null, CurrentPosition.GetModifiedPosition(0, -1, -1), CurrentPosition);
                    }
                    else
                        throw Error("Expected a code body or single statement!", CurrentPosition);
                }

                return body;
            }
        }

        public Statement TryParseInnerStatement(bool throwError = false)
        {
            return (Statement)Tokens.TryGetTree(StatementParser);
            ASTNode StatementParser()
            {
                if (CurrentIs(LOCAL))
                {
                    return ParseLocalVarDecl();
                }
                if (CurrentIs(RETURN))
                {
                    return ParseReturn();
                }
                if (CurrentIs(IF))
                {
                    return ParseIf();
                }
                if (CurrentIs(SWITCH))
                {
                    return ParseSwitch();
                }
                if (CurrentIs(WHILE))
                {
                    return ParseWhile();
                }
                if (CurrentIs(FOR))
                {
                    return ParseFor();
                }
                if (CurrentIs(FOREACH))
                {
                    throw new NotImplementedException();
                }
                if (CurrentIs(DO))
                {
                    return ParseDoUntil();
                }
                if (CurrentIs(CONTINUE))
                {
                    return ParseContinue();
                }
                if (CurrentIs(BREAK))
                {
                    return ParseBreak();
                }
                if (CurrentIs(STOP))
                {
                    return ParseStop();
                }
                if (CurrentIs(CASE))
                {
                    return ParseCase();
                }
                if (CurrentIs(DEFAULT))
                {
                    return ParseDefault();
                }

                Expression expr = ParseExpression();
                if (expr == null)
                {
                    if (throwError)
                    {
                        throw Error("Expected a valid statement!", CurrentPosition);
                    }
                    return null;
                }


                if (Consume(TokenType.Assign) is { } assign)
                {
                    if (!IsLValue(expr))
                    {
                        throw Error("Assignments require a variable target (LValue expected).", CurrentPosition);
                    }

                    var value = ParseExpression();
                    if (value == null) throw Error("Assignments require a resolvable expression as value! (RValue expected).", CurrentPosition);

                    // TODO: allow built-in type conversion here!
                    if (!TypeCompatible(expr.ResolveType(), value.ResolveType())) throw Error($"Cannot assign a value of type '{value.ResolveType()}' to a variable of type '{null}'.", assign.StartPos, assign.EndPos);

                    return new AssignStatement(expr, value, assign.StartPos, assign.EndPos);
                }

                return new ExpressionOnlyStatement(expr, expr.StartPos, expr.EndPos);
            };
        }

        public VariableDeclaration ParseLocalVarDecl()
        {
            var startPos = CurrentPosition;
            if (!Matches(LOCAL)) return null;

            VariableType type = TryParseType();
            if (type == null) throw Error("Expected variable type!", CurrentPosition);
            if (!Symbols.TryResolveType(ref type)) throw Error($"The type '{type.Name}' does not exist in the current scope!", type.StartPos, type.EndPos);

            var var = ParseVariableName();
            if (var == null) throw Error("Malformed variable name!", CurrentPosition);


            if (Symbols.SymbolExistsInCurrentScope(var.Name)) throw Error($"A variable named '{var.Name}' already exists in this scope!", var.StartPos, var.EndPos);

            VariableDeclaration varDecl = new VariableDeclaration(type, UnrealFlags.EPropertyFlags.None, var, null, startPos, var.EndPos);
            Symbols.AddSymbol(varDecl.Name, varDecl);
            NodeVariables.Locals.Add(varDecl);
            varDecl.Outer = Node;

            return varDecl;
        }

        public IfStatement ParseIf()
        {
            var token = Consume(IF);
            if (token == null) return null;

            if (Consume(TokenType.LeftParenth) == null) throw Error("Expected '('!", CurrentPosition);

            var condition = ParseExpression();
            if (condition == null) throw Error("Expected an expression as the if-condition!", CurrentPosition);
            bool isNullCheckIf;
            VariableType conditionType = condition.ResolveType();
            if (conditionType == SymbolTable.BoolType)
            {
                isNullCheckIf = false;
            }
            else if (conditionType?.PropertyType == EPropertyType.Object)
            {
                isNullCheckIf = true;
            }
            else
            {
                throw Error("Expected a boolean result from the condition!", condition.StartPos, condition.EndPos);
            }

            if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition);

            CodeBody thenBody = TryParseBodyOrStatement();
            if (thenBody == null) throw Error("Expected a statement or code block!", CurrentPosition);

            CodeBody elseBody = null;
            var elsetoken = Consume(ELSE);
            if (elsetoken != null)
            {
                elseBody = TryParseBodyOrStatement();
                if (elseBody == null) throw Error("Expected a statement or code block!", CurrentPosition);
            }

            return new IfStatement(condition, thenBody, token.StartPos, token.EndPos, elseBody)
            {
                IsNullCheck = isNullCheckIf
            };
        }

        public ReturnStatement ParseReturn()
        {
            var token = Consume(RETURN);
            if (token == null) return null;

            if (!IsFunction) throw Error("Return statements can only exist in functions!", CurrentPosition);

            if (CurrentTokenType == TokenType.SemiColon) return new ReturnStatement(token.StartPos, token.EndPos);

            var value = ParseExpression();
            if (value == null) throw Error("Expected a return value or a semi-colon!", CurrentPosition);

            var type = value.ResolveType();
            if (IsFunction)
            {
                var func = (Function)Node;
                if (func.ReturnType == null) throw Error("Function should not return a value!", token.StartPos, token.EndPos);

                if (!TypeCompatible(func.ReturnType, type)) throw Error($"Cannot return a value of type '{type.Name}', function should return '{func.ReturnType.Name}'.", token.StartPos, token.EndPos);
            }

            return new ReturnStatement(token.StartPos, token.EndPos, value);
        }

        public SwitchStatement ParseSwitch()
        {
            var token = Consume(SWITCH);
            if (token == null) return null;

            if (Consume(TokenType.LeftParenth) == null) throw Error("Expected '('!", CurrentPosition);

            var expression = ParseExpression();
            if (expression == null) throw Error("Expected an expression as the switch value!", CurrentPosition);

            if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition);

            _switchCount++;
            CodeBody body = TryParseBodyOrStatement();
            _switchCount--;
            if (body == null) throw Error("Expected switch code block!", CurrentPosition);

            return new SwitchStatement(expression, body, token.StartPos, token.EndPos);
        }

        public WhileLoop ParseWhile()
        {
            var token = Consume(WHILE);
            if (token == null) return null;

            if (Consume(TokenType.LeftParenth) == null) throw Error("Expected '('!", CurrentPosition);

            var condition = ParseExpression();
            if (condition == null) throw Error("Expected an expression as the while condition!", CurrentPosition);
            if (condition.ResolveType().Name != BOOL) // TODO: check/fix!
                throw Error("Expected a boolean result from the condition!", condition.StartPos, condition.EndPos);

            if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition);

            _loopCount++;
            CodeBody body = TryParseBodyOrStatement(allowEmpty: true);
            _loopCount--;
            if (body == null) return null;

            return new WhileLoop(condition, body, token.StartPos, token.EndPos);
        }

        public ForLoop ParseFor()
        {
            var token = Consume(FOR);
            if (token == null) return null;

            if (Consume(TokenType.LeftParenth) == null) throw Error("Expected '('!", CurrentPosition);

            var initStatement = TryParseInnerStatement();
            if (initStatement.Type != ASTNodeType.AssignStatement && initStatement.Type != ASTNodeType.FunctionCall) throw Error("Init statement in a for-loop must be an assignment or a function call!", CurrentPosition);

            if (Consume(TokenType.SemiColon) == null) throw Error("Expected semi-colon after init statement!", CurrentPosition);

            var condition = ParseExpression();
            if (condition == null) throw Error("Expected an expression as the for condition!", CurrentPosition);
            if (condition.ResolveType().Name != BOOL) // TODO: check/fix!
                throw Error("Expected a boolean result from the condition!", condition.StartPos, condition.EndPos);

            if (Consume(TokenType.SemiColon) == null) throw Error("Expected semi-colon after condition!", CurrentPosition);

            var updateStatement = TryParseInnerStatement();
            if (updateStatement.Type != ASTNodeType.AssignStatement && initStatement.Type != ASTNodeType.Function && initStatement.Type != ASTNodeType.PrefixOperator && initStatement.Type != ASTNodeType.PostfixOperator) // TODO: what is actually supported?
                throw Error("Init statement in a for-loop must be an assignment, in/decrement or function call!", CurrentPosition);

            if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition);

            _loopCount++;
            CodeBody body = TryParseBodyOrStatement(allowEmpty: true);
            _loopCount--;
            if (body == null) return null;

            return new ForLoop(condition, body, initStatement, updateStatement, token.StartPos, token.EndPos);
        }

        public DoUntilLoop ParseDoUntil()
        {
            var doToken = Consume(DO);
            if (doToken == null) return null;

            _loopCount++;
            CodeBody body = TryParseBodyOrStatement();
            _loopCount--;
            if (body == null) return null;

            var untilToken = Consume(UNTIL);
            if (untilToken == null) throw Error("Expected 'until'!", CurrentPosition);

            if (Consume(TokenType.LeftParenth) == null) throw Error("Expected '('!", CurrentPosition);

            var condition = ParseExpression();
            if (condition == null) throw Error("Expected an expression as the until condition!", CurrentPosition);
            if (condition.ResolveType().Name != BOOL) // TODO: check/fix!
                throw Error("Expected a boolean result from the condition!", condition.StartPos, condition.EndPos);

            if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition);

            return new DoUntilLoop(condition, body, untilToken.StartPos, untilToken.EndPos);
        }

        public ContinueStatement ParseContinue()
        {
            var token = Consume(CONTINUE);
            if (token == null) return null;

            if (!InLoop) throw Error("The continue keyword is only valid inside loops!", token.StartPos, token.EndPos);

            return new ContinueStatement(token.StartPos, token.EndPos);
        }

        public BreakStatement ParseBreak()
        {
            var token = Consume(BREAK);
            if (token == null) return null;

            if (!InLoop && !InSwitch) throw Error("The break keyword is only valid inside loops and switch statements!", token.StartPos, token.EndPos);

            return new BreakStatement(token.StartPos, token.EndPos);
        }

        public StopStatement ParseStop()
        {
            var token = Consume(STOP);
            if (token == null) return null;

            if (!IsState) throw Error("The stop keyword is only valid inside state code!", token.StartPos, token.EndPos);

            return new StopStatement(token.StartPos, token.EndPos);
        }

        public CaseStatement ParseCase()
        {
            var token = Consume(CASE);
            if (token == null) return null;

            if (!InSwitch) throw Error("Case statements can only exist inside switch blocks!", CurrentPosition);

            var value = ParseExpression();
            if (value == null) throw Error("Expected an expression specifying the case value", CurrentPosition);

            if (Consume(TokenType.Colon) == null) throw Error("Expected colon after case expression!", CurrentPosition);

            /* TODO: advanced type checks here, intrinsic conversions should be allowed but other anomalies reported.
            var type = value.ResolveType();
            var parent = GetHashCode containing switch somehow;
            if (!TypeEquals(parent.Expression.ResolveType(), type))
                throw Error("Cannot use case: '" + type.Name + "', in switch of type '" + parent.Expression.ResolveType() + "'."
                        , token.StartPosition, token.EndPosition);
             * */

            return new CaseStatement(value, token.StartPos, token.EndPos);
        }

        public DefaultStatement ParseDefault()
        {
            var token = Consume(DEFAULT);
            if (token == null) return null;

            if (!InSwitch) throw Error("Default statements can only exist inside switch blocks!", CurrentPosition);

            if (Consume(TokenType.Colon) == null) throw Error("Expected colon after default statement!", CurrentPosition);

            return new DefaultStatement(token.StartPos, token.EndPos);
        }

        #endregion

        #region Expressions

        public Expression ParseExpression() => Ternary();

        public Expression Ternary()
        {
            var expr = BinaryExpression(NOPRECEDENCE);
            if (expr == null) return null;

            if (Matches(TokenType.QuestionMark))
            {
                if (!TypeCompatible(SymbolTable.BoolType, expr.ResolveType()))
                {
                    throw Error("Expected a boolean expression before a '?'!", CurrentPosition);
                }
                Expression trueExpr = Ternary();
                if (trueExpr is null)
                {
                    throw Error("Expected expression after '?'!", CurrentPosition);
                }

                if (!Matches(TokenType.Colon))
                {
                    throw Error("Expected ':' after true branch in conditional statement!", CurrentPosition);
                }
                Expression falseExpr = Ternary();
                if (trueExpr.ResolveType() != falseExpr.ResolveType()) //No type conversion, types must be an exact match
                {
                    throw Error("True and false results in conditional expression must match types!");
                }

                expr = new ConditionalExpression(expr, trueExpr, falseExpr, expr.StartPos, falseExpr.EndPos);
            }

            return expr;
        }

        public Expression BinaryExpression(int maxPrecedence)
        {
            Expression expr = Unary();

            while (IsOperator(out bool isRightShift))
            {
                string opKeyword = isRightShift ? ">>" : CurrentToken.Value;
                Expression lhs = expr;

                if (lhs is DynArrayLength && (opKeyword == "+=" || opKeyword == "-=" || opKeyword == "*=" || opKeyword == "/="))
                {
                    throw Error($"The {LENGTH} property of a dynamic array can only be changed by direct assignment!", CurrentPosition);
                }

                var possibleMatches = new List<InOpDeclaration>();
                int precedence = 0;
                foreach (InOpDeclaration opDecl in Symbols.GetInfixOperators(opKeyword))
                {
                    precedence = opDecl.Precedence;
                    possibleMatches.Add(opDecl);
                }

                if (possibleMatches.Count == 0 || precedence >= maxPrecedence)
                {
                    break; //don't handle at this precedence level
                }

                Token<string> opToken = Consume(CurrentTokenType);
                if (isRightShift)
                {
                    Consume(TokenType.RightArrow);
                }
                Expression rhs = BinaryExpression(precedence);
                if (rhs == null)
                {
                    throw Error($"Expected expression after '{opKeyword}' operator!", CurrentPosition);
                }

                var lType = lhs.ResolveType();
                var rType = rhs.ResolveType();
                int bestCost = 0;
                InOpDeclaration bestMatch = null;
                int matches = 0;
                foreach (InOpDeclaration opDecl in possibleMatches)
                {
                    int lCost = CastHelper.ConversionCost(opDecl.LeftOperand, lType);
                    int rCost = CastHelper.ConversionCost(opDecl.RightOperand, rType);
                    int cost = Math.Max(lCost, rCost);

                    if (bestMatch is null || cost < bestCost)
                    {
                        bestMatch = opDecl;
                        bestCost = cost;
                        matches = 1;
                    }
                    else if (cost == bestCost)
                    {
                        matches++;
                    }
                }

                if (bestCost == int.MaxValue)
                {
                    //Handle built-in comparison operators for delegates and structs
                    bool isEqualEqual = opKeyword == "==";
                    bool isComparison = isEqualEqual || opKeyword == "!=";
                    if (isComparison && lType.PropertyType == EPropertyType.Delegate && rType.PropertyType == EPropertyType.Delegate)
                    {
                        //TODO: check delegate types match, distinguish between deldel and delfunc 
                        expr = new DelegateComparison(isEqualEqual, lhs, rhs, lhs.StartPos, rhs.EndPos);
                    }
                    else if (isComparison && lType.PropertyType == EPropertyType.Struct && rType.PropertyType == EPropertyType.Struct)
                    {
                        if (lType == rType)
                        {
                            expr = new StructComparison(isEqualEqual, lhs, rhs, lhs.StartPos, rhs.EndPos);
                        }
                        else
                        {
                            throw Error("Cannot compare structs of different types!", opToken.StartPos);
                        }
                    }
                    else
                    {
                        throw Error($"No valid operator found for '{lType?.Name ?? "None"}' '{opKeyword}' '{rType?.Name ?? "None"}'!", opToken.StartPos);
                    }
                }
                else if (matches > 1)
                {
                    throw Error($"Ambiguous operator overload! {matches} equally valid possibilites for '{lType?.Name ?? "None"}' '{opKeyword}' '{rType?.Name ?? "None"}'!", opToken.StartPos);
                }
                else
                {
                    //TODO: add cast operators if neccesary (maybe do this during bytecode emission?)
                    expr = new InOpReference(bestMatch, lhs, rhs, lhs.StartPos, rhs.EndPos);
                }
            }

            return expr;

            bool IsOperator(out bool isRightShift)
            {
                //Lexer can't recognize >> as the right-shift operator, because of the conflicting array<delegate<delName>> syntax, so do it manually here
                isRightShift = false;
                if (CurrentToken.Type == TokenType.RightArrow && Tokens.LookAhead(1).Type == TokenType.RightArrow)
                {
                    //check to see if there is any whitespace between them. Otherwise > > would be recognized as the right shift operator! 
                    isRightShift = Tokens.LookAhead(1).StartPos.Equals(CurrentToken.EndPos);
                }
                return Symbols.InFixOperatorSymbols.Contains(CurrentToken.Value, StringComparer.OrdinalIgnoreCase);
            }
        }

        public Expression Unary()
        {
            var start = CurrentPosition;
            Expression expr;
            if (Consume(TokenType.Increment, TokenType.Decrement) is { } preFixToken)
            {
                expr = CompositeRef();
                if (expr is DynArrayLength)
                {
                    throw Error($"The {LENGTH} property of a dynamic array can only be changed by direct assignment!", CurrentPosition);
                }
                if (!IsLValue(expr))
                {
                    throw Error($"Cannot {(preFixToken.Type == TokenType.Increment ? "in" : "de")}crement an rvalue!");
                }
                VariableType exprType = expr.ResolveType();
                if (exprType == SymbolTable.IntType || exprType == SymbolTable.ByteType)
                {
                    PreOpDeclaration opDeclaration = Symbols.GetPreOp(preFixToken.Value, exprType);
                    return new PreOpReference(opDeclaration, expr, preFixToken.StartPos, expr.EndPos);
                }

                throw Error($"Only ints and bytes can be {(preFixToken.Type == TokenType.Increment ? "in" : "de")}cremented!");
            }
            if (Matches(TokenType.ExclamationMark))
            {
                expr = Unary();
                VariableType exprType = expr.ResolveType();
                if (exprType == SymbolTable.BoolType)
                {
                    PreOpDeclaration opDeclaration = Symbols.GetPreOp("!", exprType);
                    return new PreOpReference(opDeclaration, expr, start, expr.EndPos);
                }

                throw Error("'!' can only be used with expressions that evaluate to a boolean!");
            }
            if (Matches(TokenType.MinusSign))
            {
                //TODO: combine with literals?
                expr = Unary();
                VariableType exprType = expr.ResolveType();
                if (exprType == SymbolTable.ByteType)
                {
                    exprType = SymbolTable.IntType;
                }
                if (exprType == SymbolTable.FloatType || exprType == SymbolTable.IntType || exprType.Name.CaseInsensitiveEquals("Vector"))
                {
                    PreOpDeclaration opDeclaration = Symbols.GetPreOp("-", exprType);
                    return new PreOpReference(opDeclaration, expr, start, expr.EndPos);
                }

                throw Error("Unary '-' can only be used with expressions that evaluate to float, int, or Vector!");
            }
            if (Matches(TokenType.Complement))
            {
                expr = Unary();
                VariableType exprType = expr.ResolveType();
                if (exprType == SymbolTable.IntType)
                {
                    PreOpDeclaration opDeclaration = Symbols.GetPreOp("~", exprType);
                    return new PreOpReference(opDeclaration, expr, start, expr.EndPos);
                }

                throw Error("'~' can only be used with expressions that evaluate to int!");
            }

            expr = Cast();

            if (Consume(TokenType.Increment, TokenType.Decrement) is {} postFixToken)
            {
                if (expr is DynArrayLength)
                {
                    throw Error($"The {LENGTH} property of a dynamic array can only be changed by direct assignment!", CurrentPosition);
                }
                VariableType exprType = expr.ResolveType();
                if (!IsLValue(expr))
                {
                    throw Error($"Cannot {(postFixToken.Type == TokenType.Increment ? "in" : "de")}crement an rvalue!");
                }
                if (exprType == SymbolTable.IntType || exprType == SymbolTable.ByteType)
                {
                    PostOpDeclaration opDeclaration = Symbols.GetPostOp(postFixToken.Value, exprType);
                    expr = new PostOpReference(opDeclaration, expr, expr.StartPos, postFixToken.EndPos);
                }
                else
                {
                    throw Error($"Only ints and bytes can be {(postFixToken.Type == TokenType.Increment ? "in" : "de")}cremented!");
                }
            }

            return expr;
        }

        public Expression Cast()
        {
            if (CurrentIs(TokenType.Word) && Tokens.LookAhead(1).Type == TokenType.LeftParenth && Symbols.TryGetType(CurrentToken.Value, out VariableType destType))
            {
                //primitive or dynamic cast
                Token<string> castToken = Consume(TokenType.Word);
                Consume(TokenType.LeftParenth);
                Expression expr = ParseExpression();
                if (!Matches(TokenType.RightParenth))
                {
                    throw Error("Expected ')' at end of cast expression!", CurrentPosition);
                }
                var exprType = expr.ResolveType();
                if (destType.Equals(exprType))
                {
                    throw Error("Cannot cast to same type!", castToken.StartPos, PrevToken.EndPos);
                }
                if (destType is Class destClass && exprType is Class srcClass)
                {
                    //dynamic cast
                    if (!srcClass.SameAsOrSubClassOf(destClass.Name) && !destClass.SameAsOrSubClassOf(srcClass.Name))
                    {
                        throw Error($"Cannot cast between unrelated classes '{exprType}' and '{destType}'!", CurrentPosition);
                    }
                    //TODO: different AST class for dynamic class?
                    return new CastExpression(destType, expr, castToken.StartPos, CurrentPosition);
                }
                else
                {
                    //primitive cast
                    ECast cast = CastHelper.GetConversion(destType, exprType);
                    if (cast == ECast.Max)
                    {
                        throw Error($"Cannot cast from '{exprType}' to '{destType}'!", CurrentPosition);
                    }
                    return new CastExpression(destType, expr, castToken.StartPos, CurrentPosition);
                }
            }
            if (CurrentIs(CLASS) && Tokens.LookAhead(1).Type == TokenType.LeftArrow)
            {
                //metacast
                var castToken = Consume(CLASS);
                Consume(TokenType.LeftArrow);
                if (Consume(TokenType.Word) is {} limiter)
                {
                    if (!Matches(TokenType.RightArrow))
                    {
                        throw Error("Expected '>' after limiter class!", CurrentPosition);
                    }

                    if (Symbols.TryGetType(limiter.Value, out destType) && destType is Class limiterType)
                    {
                        if (!Matches(TokenType.LeftParenth))
                        {
                            throw Error("Expected '(' at start of cast!");
                        }
                        Expression expr = ParseExpression();
                        if (!Matches(TokenType.RightParenth))
                        {
                            throw Error("Expected ')' at end of cast expression!", CurrentPosition);
                        }
                        var exprType = expr.ResolveType();
                        if (exprType is ClassType exprClassType)
                        {
                            if (exprClassType.ClassLimiter == limiterType)
                            {
                                throw Error("Cannot cast to same type!", CurrentPosition);
                            }
                            if (!limiterType.SameAsOrSubClassOf(exprClassType.ClassLimiter.Name))
                            {
                                if (((Class)exprClassType.ClassLimiter).SameAsOrSubClassOf(limiterType.Name))
                                {
                                    throw Error("Casting to a less-derived type is pointless!", CurrentPosition);
                                }
                                throw Error("Cannot cast to an unrelated type!", CurrentPosition);
                            }
                        }
                        else if (!(exprType is Class classType) || !classType.Name.CaseInsensitiveEquals(OBJECT))
                        {
                            throw Error("Cannot cast to a class type from a non-class type!", CurrentPosition);
                        }
                        //TODO: different AST type for Metaclass?
                        return new CastExpression(new ClassType(destType), expr, castToken.StartPos, PrevToken.EndPos);
                    }

                    throw Error($"'{limiter.Value}' is not a Class!", CurrentPosition);
                }

                throw Error("Expecting class name in class limiter!", CurrentPosition);
            }

            return CompositeRef();
        }

        public Expression CompositeRef()
        {
            Expression result = InnerCompositeRef();

            //if this is a reference to a constant, we need to replace it with the constant's value
            if (result is SymbolReference symbolRef && symbolRef.Node is Const c)
            {
                result = c.Literal;
            }

            return result;

            Expression InnerCompositeRef()
            {
                Expression expr = CallOrAccess();
                if (expr is null)
                {
                    return null;
                }

                while (Matches(TokenType.Dot))
                {

                    var lhsType = expr.ResolveType();
                    if (lhsType is DynamicArrayType dynArrType)
                    {
                        //all the dynamic array properties and functions are built-ins
                        expr = ParseDynamicArrayOperation(expr, dynArrType.ElementType);
                        continue;
                    }

                    bool isConst = false;
                    bool isStatic = false;
                    if (Matches(CONST))
                    {
                        if (!Matches(TokenType.Dot))
                        {
                            throw Error($"Expected '.' after '{CONST}'!", CurrentPosition);
                        }
                        isConst = true;
                    }
                    else if (Matches(STATIC))
                    {
                        if (!Matches(TokenType.Dot))
                        {
                            throw Error($"Expected '.' after '{STATIC}'!", CurrentPosition);
                        }
                        isStatic = true;
                        if (lhsType?.PropertyType != EPropertyType.Object)
                        {
                            throw Error($"'{STATIC}' can only be used with class or object references!", CurrentPosition);
                        }
                        //else
                        //{
                        //    //disable type checking in this case
                        //    _useDynamicResolution = true;
                        //}
                    }

                    if (!isStatic && !CompositeTypes.Contains(lhsType?.NodeType ?? ASTNodeType.INVALID))
                    {
                        throw Error("Left side symbol is not of a composite type!", PrevToken.StartPos); //TODO: write a better error message
                    }
                    Class containingClass = NodeUtils.GetContainingClass(lhsType);
                    if (containingClass == null)
                    {
                        throw Error("Could not resolve type of expression!", expr.StartPos, expr.EndPos);
                    }
                    string specificScope = containingClass.GetInheritanceString();
                    if (!(lhsType is ClassType) && lhsType != containingClass)
                    {
                        specificScope += $".{lhsType.Name}";
                    }

                    ExpressionScopes.Push(specificScope);

                    Expression rhs = CallOrAccess();

                    ExpressionScopes.Pop();
                    if (rhs is null)
                    {
                        throw Error("Expected a valid member name to follow the dot!", CurrentPosition);
                    }

                    if (isStatic)
                    {
                        if (!_useDynamicResolution && (!(rhs is FunctionCall fc) || (fc.Function.Node as Function)?.Flags.Has(FunctionFlags.Static) != true))
                        {
                            throw Error("'static.' can only be used for calling a function with the 'static' modifier!");
                        }

                        _useDynamicResolution = false;
                    }
                    if (isConst)
                    {
                        if (!(rhs is SymbolReference symRef) || !(symRef.Node is Const))
                        {
                            throw Error("Expected property after 'const.' to be a Const!");
                        }
                    }

                    expr = new CompositeSymbolRef(expr, rhs, isStatic, expr.StartPos, rhs.EndPos);
                }

                return expr;
            }
        }

        private Expression ParseDynamicArrayOperation(Expression dynArrayRef, VariableType elementType)
        {
            if (Matches(LENGTH))
            {
                return new DynArrayLength(dynArrayRef, dynArrayRef.StartPos, PrevToken.EndPos);
            }
            if (Matches(ADD))
            {
                ExpectLeftParen(ADD);
                Expression countArg = ValidateArgument("count", ADD, SymbolTable.IntType);
                ExpectRightParen();
                return new DynArrayAdd(dynArrayRef, countArg, dynArrayRef.StartPos, PrevToken.EndPos);
            }
            if (Matches(ADDITEM))
            {
                ExpectLeftParen(ADDITEM);
                Expression valueArg = ValidateArgument("value", ADDITEM, elementType);
                ExpectRightParen();
                return new DynArrayAddItem(dynArrayRef, valueArg, dynArrayRef.StartPos, PrevToken.EndPos);
            }
            if (Matches(INSERT))
            {
                ExpectLeftParen(INSERT);
                Expression indexArg = ValidateArgument("index", INSERT, SymbolTable.IntType);
                ExpectComma();
                Expression countArg = ValidateArgument("count", INSERT, SymbolTable.IntType);
                ExpectRightParen();
                return new DynArrayInsert(dynArrayRef, indexArg, countArg, dynArrayRef.StartPos, PrevToken.EndPos);
            }
            if (Matches(INSERTITEM))
            {
                ExpectLeftParen(INSERTITEM);
                Expression indexArg = ValidateArgument("index", INSERTITEM, SymbolTable.IntType);
                ExpectComma();
                Expression valueArg = ValidateArgument("value", INSERTITEM, elementType);
                ExpectRightParen();
                return new DynArrayInsertItem(dynArrayRef, indexArg, valueArg, dynArrayRef.StartPos, PrevToken.EndPos);
            }
            if (Matches(REMOVE))
            {
                ExpectLeftParen(REMOVE);
                Expression indexArg = ValidateArgument("index", REMOVE, SymbolTable.IntType);
                ExpectComma();
                Expression countArg = ValidateArgument("count", REMOVE, SymbolTable.IntType);
                ExpectRightParen();
                return new DynArrayRemove(dynArrayRef, indexArg, countArg, dynArrayRef.StartPos, PrevToken.EndPos);
            }
            if (Matches(REMOVEITEM))
            {
                ExpectLeftParen(REMOVEITEM);
                Expression valueArg = ValidateArgument("value", REMOVEITEM, elementType);
                ExpectRightParen();
                return new DynArrayRemoveItem(dynArrayRef, valueArg, dynArrayRef.StartPos, PrevToken.EndPos);
            }
            if (Matches(FIND))
            {
                ExpectLeftParen(FIND);
                if (elementType is Struct s)
                {
                    Expression memberNameArg = ParseExpression();
                    if (memberNameArg == null)
                    {
                        throw Error("Expected function argument!", CurrentPosition);
                    }
                    if (memberNameArg is NameLiteral nameLiteral)
                    {
                        if (s.VariableDeclarations.FirstOrDefault(varDecl => varDecl.Name.CaseInsensitiveEquals(nameLiteral.Value)) is VariableDeclaration variableDeclaration)
                        {
                            ExpectComma();
                            Expression valueArg = ValidateArgument("value", FIND, variableDeclaration.VarType);
                            ExpectRightParen();
                            return new DynArrayFindStructMember(dynArrayRef, memberNameArg, valueArg, dynArrayRef.StartPos, PrevToken.EndPos);
                        }

                        throw Error($"Struct '{s.Name}' does not have a member named '{nameLiteral.Value}'!");
                    }

                    throw Error($"Expected 'membername' argument to '{FIND}' to be a name literal!");
                }
                else
                {
                    Expression valueArg = ValidateArgument("value", FIND, elementType);
                    ExpectRightParen();
                    return new DynArrayFind(dynArrayRef, valueArg, dynArrayRef.StartPos, PrevToken.EndPos);
                }
            }
            else if (Matches(SORT))
            {
                ExpectLeftParen(SORT);
                Expression comparefunctionArg = ParseExpression();
                if (comparefunctionArg == null)
                {
                    throw Error("Expected function argument!", CurrentPosition);
                }

                if (comparefunctionArg.ResolveType() is DelegateType delType)
                {
                    Function delFunc = delType.DefaultFunction;
                    if (delFunc.ReturnType == SymbolTable.IntType && delFunc.Parameters.Count == 2 && TypeCompatible(delFunc.Parameters[0].VarType, elementType)
                                                                                                   && TypeCompatible(delFunc.Parameters[1].VarType, elementType))
                    {
                        ExpectRightParen();
                        return new DynArraySort(dynArrayRef, comparefunctionArg, dynArrayRef.StartPos, PrevToken.EndPos);
                    }
                }

                throw Error($"Expected 'comparefunction' argument to '{SORT}' to be a delegate that takes two parameters of type '{elementType.Name}' and returns an int!");
            }
            else
            {
                throw Error($"Expected a dynamic array operation!", CurrentPosition);
            }

            void ExpectLeftParen(string funcName)
            {
                if (!Matches(TokenType.LeftParenth))
                {
                    throw Error($"Expected '(' after '{funcName}'!", CurrentPosition);
                }
            }
            void ExpectComma()
            {
                if (!Matches(TokenType.Comma))
                {
                    throw Error($"Expected ',' after argument!", CurrentPosition);
                }
            }
            void ExpectRightParen()
            {
                if (!Matches(TokenType.RightParenth))
                {
                    throw Error($"Expected ')' after argument list!", CurrentPosition);
                }
            }

            Expression ValidateArgument(string argumentName, string functionName, VariableType expectedType)
            {
                Expression arg = ParseExpression();
                if (arg == null)
                {
                    throw Error("Expected function argument!", CurrentPosition);
                }

                if (!TypeCompatible(expectedType, arg.ResolveType()))
                {
                    throw Error($"Expected '{argumentName}' argument to '{functionName}' to evaluate to '{expectedType.Name}'!");
                }
                return arg;
            }
        }

        public Expression CallOrAccess()
        {
            Expression expr = Primary();
            if (expr is null)
            {
                return null;
            }
            while (true)
            {
                if (Matches(TokenType.LeftParenth))
                {
                    expr = FinishCall(expr);
                }
                else if (Matches(TokenType.LeftSqrBracket))
                {
                    expr = FinishArrayAccess(expr);
                }
                else
                {
                    break;
                }
            }

            return expr;
        }

        private Expression FinishArrayAccess(Expression expr)
        {
            //TODO: check if expr evaluates to an array

            ExpressionScopes.Push(ExpressionScopes.Last());
            Expression arrIndex = ParseExpression();
            ExpressionScopes.Pop();
            if (arrIndex == null)
            {
                throw Error("Expected an array index!", CurrentPosition);
            }

            if (!TypeCompatible(SymbolTable.IntType, arrIndex.ResolveType()))
            {
                throw Error("Array index must be or evaluate to an integer!");
            }

            if (Consume(TokenType.RightSqrBracket) is {} endTok)
            {
                expr = new ArraySymbolRef(expr, arrIndex, expr.StartPos, endTok.EndPos);
            }
            else
            {
                throw Error("Expected ']'!", CurrentPosition);
            }

            return expr;
        }

        public FunctionCall FinishCall(Expression expr)
        {
            if (expr is SymbolReference funcRef && funcRef.Node.Type == ASTNodeType.Function)
            {
                Function func = (Function)funcRef.Node;
                var parameters = new List<Expression>();
                ExpressionScopes.Push(ExpressionScopes.Last());
                for (int i = 0; i < func.Parameters.Count; i++)
                {
                    FunctionParameter p = func.Parameters[i];
                    if (i == func.Parameters.Count - 1 ? CurrentIs(TokenType.RightParenth) : Matches(TokenType.Comma))
                    {
                        if (p.IsOptional)
                        {
                            parameters.Add(null);
                            continue;
                        }

                        throw Error("Missing non-optional parameter!", CurrentPosition);
                    }

                    Expression currentParam = ParseExpression();
                    // TODO: allow automatic type conversion for compatible basic types
                    if (currentParam == null || !TypeCompatible(p.VarType, currentParam.ResolveType())) throw Error($"Expected a parameter of type '{p.VarType.Name}'!", currentParam.StartPos, currentParam.EndPos);

                    parameters.Add(currentParam);
                    if (Consume(TokenType.Comma) == null) break;
                }

                ExpressionScopes.Pop();
                if (parameters.Count != func.Parameters.Count)
                {
                    if (func.Flags.Has(FunctionFlags.HasOptionalParms))
                    {
                        int numRequiredParams = func.Parameters.Count(param => !param.IsOptional);
                        if (parameters.Count > func.Parameters.Count || parameters.Count < numRequiredParams)
                        {
                            throw Error($"Expected between {numRequiredParams} and {func.Parameters.Count} parameters to function '{func.Name}'!", funcRef.StartPos, funcRef.EndPos);
                        }
                    }
                    else
                    {
                        throw Error($"Expected {func.Parameters.Count} parameters to function '{func.Name}'!", funcRef.StartPos, funcRef.EndPos);
                    }
                }

                if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition);

                return new FunctionCall(funcRef, parameters, funcRef.StartPos, CurrentPosition);
            }

            //TODO: casts


            throw Error("Can only call functions and delegates!", expr.StartPos, expr.EndPos);
        }

        public Expression Primary()
        {
            Expression literal = ParseLiteral();
            if (literal != null)
            {
                return literal;
            }

            Token<string> token = CurrentToken;
            if (Matches(SELF))
            {
                return new SymbolReference(NodeUtils.GetContainingClass(Body), SELF, token.StartPos, token.EndPos);
            }

            if (Matches(NEW))
            {
                Expression outerObj = null;
                Expression objName = null;
                Expression flags = null;

                if (Matches(TokenType.LeftParenth))
                {
                    outerObj = ParseExpression();
                    if (outerObj == null)
                    {
                        throw Error($"Expected 'outerobject' argument to '{NEW}' expression!", CurrentPosition);
                    }
                    if (Matches(TokenType.Comma))
                    {
                        objName = ParseExpression();
                        if (objName == null)
                        {
                            throw Error($"Expected 'name' argument to '{NEW}' expression!", CurrentPosition);
                        }
                        if (!TypeCompatible(SymbolTable.StringType, objName.ResolveType())) //TODO: should this really be a string, and not a name?
                        {
                            throw Error($"The 'name' argument to a '{NEW}' expression must be a string!", flags.StartPos, flags.EndPos);
                        }
                        if (Matches(TokenType.Comma))
                        {
                            flags = ParseExpression();
                            if (flags == null)
                            {
                                throw Error($"Expected 'flags' argument to '{NEW}' expression!", CurrentPosition);
                            }
                            if (!TypeCompatible(SymbolTable.IntType, flags.ResolveType()))
                            {
                                throw Error($"The 'flags' argument to a '{NEW}' expression must be an int!", flags.StartPos, flags.EndPos);
                            }
                        }
                    }

                    if (!Matches(TokenType.RightParenth))
                    {
                        throw Error($"Expected ')' at end of '{NEW}' expression's argument list!");
                    }
                }

                Expression objClass = ParseExpression();
                if (objClass == null)
                {
                    throw Error($"Expected '{NEW}' expression's class type!", CurrentPosition);
                }

                var newClass = (objClass.ResolveType() as ClassType)?.ClassLimiter as Class;
                if (newClass is null)
                {
                    throw Error($"'{NEW}' expression must specify a class type!", objClass.StartPos, objClass.EndPos); //TODO: write better error message
                }

                if (newClass.SameAsOrSubClassOf("Actor"))
                {
                    throw Error($"'{newClass.Name}' is a subclass of 'Actor'! Use the 'Spawn' function to create new 'Actor' instances.", objClass.StartPos, objClass.EndPos);
                }

                var outerObjType = outerObj?.ResolveType();
                if (outerObjType != null)
                {
                    if (!(outerObjType is Class outerClass) || !outerClass.SameAsOrSubClassOf(newClass.OuterClass.Name))
                    {
                        throw Error($"OuterObject argument for a '{NEW}' expression of type '{newClass.Name}' must be an object of class '{newClass.OuterClass.Name}'!");
                    }
                }

                Expression template = null;
                if (Matches(TokenType.LeftParenth))
                {
                    template = ParseExpression();
                    if (template == null)
                    {
                        throw Error($"Expected 'template' argument to '{NEW}' expression!", CurrentPosition);
                    }

                    var templateType = template.ResolveType();
                    if (!(templateType is Class templateClass) || !newClass.SameAsOrSubClassOf(templateClass.Name))
                    {
                        throw Error($"Template argument for a '{NEW}' expression of type '{newClass.Name}' must be an object of that class or a parent class!");
                    }
                    if (!Matches(TokenType.RightParenth))
                    {
                        throw Error($"Expected ')' after 'template' argument in '{NEW}' expression!", CurrentPosition);
                    }
                }
                return new NewOperator(outerObj, objName, flags, objClass, template, token.StartPos, PrevToken.EndPos);
            }

            if (Matches(SUPER))
            {
                throw new NotImplementedException();
            }

            if (Matches(GLOBAL))
            {
                throw new NotImplementedException();
            }

            bool isDefaultRef = false;
            if (Matches(DEFAULT))
            {
                isDefaultRef = true;
                if (!Matches(TokenType.Dot))
                {
                    throw Error("Expected '.' after 'default'!", CurrentPosition);
                }

                token = CurrentToken;
            }

            if (Matches(TokenType.Word))
            {
                if (Consume(TokenType.NameLiteral) is { } objName)
                {
                    return ParseObjectLiteral(token, objName);
                }

                return ParseBasicRef(token, isDefaultRef);
            }

            if (isDefaultRef)
            {
                throw Error("Expected property name after 'default.'!", CurrentPosition);
            }
            if (Matches(TokenType.LeftParenth))
            {
                Expression expr = Ternary();
                if (expr == null)
                {
                    throw Error("Expected expression after '('!", CurrentPosition);
                }
                if (!Matches(TokenType.RightParenth))
                {
                    throw Error("Expected closing ')' after expression!", token.StartPos, CurrentPosition);
                }

                return expr;
            }

            return null;
            throw Error("Expected Expression!");
        }

        private Expression ParseBasicRef(Token<string> token, bool isDefaultRef = false)
        {
            string specificScope = ExpressionScopes.Peek();
            if (!Symbols.TryGetSymbolInScopeStack(token.Value, out ASTNode symbol, specificScope))
            {
                if (!_useDynamicResolution)
                {
                    //TODO: better error message
                    throw Error($"{specificScope} has no member named '{token.Value}'!", token.StartPos, token.EndPos);
                }
            }

            if (isDefaultRef)
            {
                return new DefaultReference(symbol, token.Value, token.StartPos, token.EndPos);
            }
            return new SymbolReference(symbol, token.Value, token.StartPos, token.EndPos);
        }

        private Expression ParseObjectLiteral(Token<string> className, Token<string> objName)
        {
            bool isClassLiteral = className.Value.CaseInsensitiveEquals(CLASS);

            var classType = new VariableType((isClassLiteral ? objName : className).Value);
            if (!Symbols.TryResolveType(ref classType))
            {
                throw Error($"No type named '{classType.Name}' exists!", className.StartPos, className.EndPos);
            }

            if (!(classType is Class))
            {
                throw Error($"'{classType.Name}' is not a class!", className.StartPos, className.EndPos);
            }

            if (isClassLiteral)
            {
                classType = new ClassType(classType);
            }
            return new ObjectLiteral(new NameLiteral(objName.Value, objName.StartPos, objName.EndPos), classType, className.StartPos, objName.EndPos);
        }

        #endregion

        private bool IsPrimitiveType(VariableType type) =>
            type == SymbolTable.ByteType ||
            type == SymbolTable.BioMask4Type ||
            type == SymbolTable.BoolType ||
            type == SymbolTable.FloatType ||
            type == SymbolTable.IntType ||
            type == SymbolTable.NameType ||
            type == SymbolTable.StringRefType ||
            type == SymbolTable.StringType;

        private bool IsLValue(Expression expr)
        {
            //TODO: is this correct?
            return expr is SymbolReference || expr is DynArrayLength;
        }
    }
}
