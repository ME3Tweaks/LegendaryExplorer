using ME3Script.Analysis.Symbols;
using ME3Script.Compiling.Errors;
using ME3Script.Language.Tree;
using ME3Script.Language.Util;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Unreal;
using static ME3Script.Utilities.Keywords;

namespace ME3Script.Parsing
{
    public class CodeBodyParser : StringParserBase
    {
        private readonly SymbolTable Symbols;
        private readonly string OuterClassScope;
        private readonly IContainsLocals NodeVariables;
        private readonly ASTNode Node;
        private readonly CodeBody Body;

        private bool IsFunction => Node.Type == ASTNodeType.Function;
        private bool IsState => Node.Type == ASTNodeType.State;

        private bool IsOperator =>
            Node.Type == ASTNodeType.PrefixOperator
         || Node.Type == ASTNodeType.PostfixOperator
         || Node.Type == ASTNodeType.InfixOperator;

        private int _loopCount;
        private bool InLoop => _loopCount > 0;
        private int _switchCount;
        private bool InSwitch => _switchCount > 0;

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
            else if (IsOperator)
                NodeVariables = (containingNode as OperatorDeclaration);
        }

        public ASTNode ParseBody()
        {
            do
            {
                if (Tokens.CurrentItem.StartPosition.Equals(Body.StartPos))
                    break;
                Tokens.Advance();
            } while (!Tokens.AtEnd());

            if (Tokens.AtEnd())
                throw Error("Could not find the code body for the current node, please contact the maintainers of this compiler!");

            var body = TryParseBody(false);
            if (body == null)
                return null;
            Body.Statements = body.Statements;

            if (!Tokens.CurrentItem.StartPosition.Equals(Body.EndPos))
                throw Error("Could not parse a valid statement, even though the current code body has supposedly not ended yet.", 
                    CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

            return Body;
        }

        private static bool TypeEquals(VariableType a, VariableType b)
        {
            return string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }

        public CodeBody TryParseBody(bool requireBrackets = true)
        {
            return (CodeBody)Tokens.TryGetTree(CodeParser);
            ASTNode CodeParser()
            {
                if (requireBrackets && Consume(TokenType.LeftBracket) == null) throw Error("Expected '{'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var statements = new List<Statement>();
                var startPos = CurrentPosition;
                var current = TryParseInnerStatement();
                while (current != null)
                {
                    if (!SemiColonExceptions.Contains(current.Type) && Consume(TokenType.SemiColon) == null) throw Error("Expected semi-colon after statement!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    statements.Add(current);
                    current = TryParseInnerStatement();
                }

                var endPos = CurrentPosition;
                if (requireBrackets && Consume(TokenType.RightBracket) == null) throw Error("Expected '}'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

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
                    var content = new List<Statement>();
                    content.Add(single);
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
                        throw Error("Expected a code body or single statement!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                }

                return body;
            }
        }

        public Statement TryParseInnerStatement(bool throwError = false)
        {
            return (Statement)Tokens.TryGetTree(StatementParser);
            ASTNode StatementParser()
            {
                var statement = TryParseLocalVarDecl() ??
                                TryParseAssignStatement() ??
                                TryParseIf() ??
                                TryParseSwitch() ??
                                TryParseWhile() ??
                                TryParseFor() ??
                                TryParseDoUntil() ??
                                TryParseContinue() ??
                                TryParseBreak() ??
                                TryParseStop() ??
                                TryParseReturn() ??
                                TryParseCase() ??
                                TryParseDefault() ??
                                TryParseExpressionOnlyStatement() ??
                                (Statement)null;

                if (statement == null && throwError)
                    throw Error("Expected a valid statement!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                return statement;
            };
        }

        public ExpressionOnlyStatement TryParseExpressionOnlyStatement()
        {
            return (ExpressionOnlyStatement)Tokens.TryGetTree(ExpressionParser);
            ASTNode ExpressionParser()
            {
                var expr = TryParseExpression();
                if (expr == null) return null;

                return new ExpressionOnlyStatement(expr.StartPos, expr.EndPos, expr);
            }
        }

        public VariableDeclaration TryParseLocalVarDecl()
        {
            ASTNode DeclarationParser()
            {
                var startPos = CurrentPosition;
                if (!Matches(LOCAL)) return null;

                ASTNode type = TryParseType();
                if (type == null) throw Error("Expected variable type!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                if (!Symbols.TryGetSymbol(((VariableType)type).Name, out type, OuterClassScope)) throw Error($"The type '{((VariableType)type).Name}' does not exist in the current scope!", type.StartPos, type.EndPos);

                var var = ParseVariableName();
                if (var == null) throw Error("Malformed variable name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));


                if (Symbols.SymbolExistsInCurrentScope(var.Name)) throw Error($"A variable named '{var.Name}' already exists in this scope!", var.StartPos, var.EndPos);
                Symbols.AddSymbol(var.Name, var);

                VariableDeclaration varDecl = new VariableDeclaration(type as VariableType, UnrealFlags.EPropertyFlags.None, var, null, startPos, var.EndPos);
                NodeVariables.Locals.Add(varDecl);
                varDecl.Outer = Node;

                return varDecl;
            }

            return (VariableDeclaration)Tokens.TryGetTree(DeclarationParser);
        }

        public AssignStatement TryParseAssignStatement()
        {
            ASTNode AssignParser()
            {
                var target = TryParseReference();
                var assign = Consume(TokenType.Assign);
                if (assign == null)
                    return null;
                else if (target == null) throw Error("Assignments require a variable target (LValue expected).", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var value = TryParseExpression();
                if (value == null) throw Error("Assignments require a resolvable expression as value! (RValue expected).", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                // TODO: allow built-in type convertion here!
                if (!TypeEquals(target.ResolveType(), value.ResolveType())) throw Error($"Cannot assign a value of type '{value.ResolveType()}' to a variable of type '{null}'.", assign.StartPosition, assign.EndPosition);

                return new AssignStatement(target, value, assign.StartPosition, assign.EndPosition);
            }

            return (AssignStatement)Tokens.TryGetTree(AssignParser);
        }

        public IfStatement TryParseIf()
        {
            return (IfStatement)Tokens.TryGetTree(IfParser);
            ASTNode IfParser()
            {
                var token = Consume(IF);
                if (token == null) return null;

                if (Consume(TokenType.LeftParenth) == null) throw Error("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var condition = TryParseExpression();
                if (condition == null) throw Error("Expected an expression as the if-condition!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                if (condition.ResolveType().Name != "bool") // TODO: check/fix!
                    throw Error("Expected a boolean result from the condition!", condition.StartPos, condition.EndPos);

                if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                CodeBody thenBody = TryParseBodyOrStatement();
                if (thenBody == null) throw Error("Expected a statement or code block!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                CodeBody elseBody = null;
                var elsetoken = Consume(ELSE);
                if (elsetoken != null)
                {
                    elseBody = TryParseBodyOrStatement();
                    if (elseBody == null) throw Error("Expected a statement or code block!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                }

                return new IfStatement(condition, thenBody, token.StartPosition, token.EndPosition, elseBody);
            }
        }

        public SwitchStatement TryParseSwitch()
        {
            return (SwitchStatement)Tokens.TryGetTree(SwitchParser);
            ASTNode SwitchParser()
            {
                var token = Consume(SWITCH);
                if (token == null) return null;

                if (Consume(TokenType.LeftParenth) == null) throw Error("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var expression = TryParseExpression();
                if (expression == null) throw Error("Expected an expression as the switch value!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                _switchCount++;
                CodeBody body = TryParseBodyOrStatement();
                _switchCount--;
                if (body == null) throw Error("Expected switch code block!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                return new SwitchStatement(expression, body, token.StartPosition, token.EndPosition);
            }
        }

        public WhileLoop TryParseWhile()
        {
            return (WhileLoop)Tokens.TryGetTree(WhileParser);
            ASTNode WhileParser()
            {
                var token = Consume(WHILE);
                if (token == null) return null;

                if (Consume(TokenType.LeftParenth) == null) throw Error("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var condition = TryParseExpression();
                if (condition == null) throw Error("Expected an expression as the while condition!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                if (condition.ResolveType().Name != "bool") // TODO: check/fix!
                    throw Error("Expected a boolean result from the condition!", condition.StartPos, condition.EndPos);

                if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                _loopCount++;
                CodeBody body = TryParseBodyOrStatement(allowEmpty: true);
                _loopCount--;
                if (body == null) return null;

                return new WhileLoop(condition, body, token.StartPosition, token.EndPosition);
            }
        }

        public DoUntilLoop TryParseDoUntil()
        {
            return (DoUntilLoop)Tokens.TryGetTree(UntilParser);
            ASTNode UntilParser()
            {
                var doToken = Consume(DO);
                if (doToken == null) return null;

                _loopCount++;
                CodeBody body = TryParseBodyOrStatement();
                _loopCount--;
                if (body == null) return null;

                var untilToken = Consume(UNTIL);
                if (untilToken == null) throw Error("Expected 'until'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (Consume(TokenType.LeftParenth) == null) throw Error("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var condition = TryParseExpression();
                if (condition == null) throw Error("Expected an expression as the until condition!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                if (condition.ResolveType().Name != "bool") // TODO: check/fix!
                    throw Error("Expected a boolean result from the condition!", condition.StartPos, condition.EndPos);

                if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                return new DoUntilLoop(condition, body, untilToken.StartPosition, untilToken.EndPosition);
            }
        }

        public ForLoop TryParseFor()
        {
            return (ForLoop)Tokens.TryGetTree(ForParser);
            ASTNode ForParser()
            {
                var token = Consume(FOR);
                if (token == null) return null;

                if (Consume(TokenType.LeftParenth) == null) throw Error("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var initStatement = TryParseInnerStatement();
                if (initStatement.Type != ASTNodeType.AssignStatement && initStatement.Type != ASTNodeType.FunctionCall) throw Error("Init statement in a for-loop must be an assignment or a function call!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (Consume(TokenType.SemiColon) == null) throw Error("Expected semi-colon after init statement!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var condition = TryParseExpression();
                if (condition == null) throw Error("Expected an expression as the for condition!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                if (condition.ResolveType().Name != "bool") // TODO: check/fix!
                    throw Error("Expected a boolean result from the condition!", condition.StartPos, condition.EndPos);

                if (Consume(TokenType.SemiColon) == null) throw Error("Expected semi-colon after condition!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var updateStatement = TryParseInnerStatement();
                if (updateStatement.Type != ASTNodeType.AssignStatement && initStatement.Type != ASTNodeType.Function && initStatement.Type != ASTNodeType.PrefixOperator && initStatement.Type != ASTNodeType.PostfixOperator) // TODO: what is actually supported?
                    throw Error("Init statement in a for-loop must be an assignment, in/decrement or function call!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                _loopCount++;
                CodeBody body = TryParseBodyOrStatement(allowEmpty: true);
                _loopCount--;
                if (body == null) return null;

                return new ForLoop(condition, body, initStatement, updateStatement, token.StartPosition, token.EndPosition);
            }
        }

        public BreakStatement TryParseBreak()
        {
            return (BreakStatement)Tokens.TryGetTree(StatementParser);
            ASTNode StatementParser()
            {
                var token = Consume(BREAK);
                if (token == null) return null;

                if (!InLoop && !InSwitch) throw Error("The break keyword is only valid inside loops and switch statements!", token.StartPosition, token.EndPosition);

                return new BreakStatement(token.StartPosition, token.EndPosition);
            }
        }

        public ContinueStatement TryParseContinue()
        {
            return (ContinueStatement)Tokens.TryGetTree(StatementParser);
            ASTNode StatementParser()
            {
                var token = Consume(CONTINUE);
                if (token == null) return null;

                if (!InLoop) throw Error("The continue keyword is only valid inside loops!", token.StartPosition, token.EndPosition);

                return new ContinueStatement(token.StartPosition, token.EndPosition);
            }
        }

        public StopStatement TryParseStop()
        {
            return (StopStatement)Tokens.TryGetTree(StatementParser);
            ASTNode StatementParser()
            {
                var token = Consume(STOP);
                if (token == null) return null;

                if (!IsState) throw Error("The stop keyword is only valid inside state code!", token.StartPosition, token.EndPosition);

                return new StopStatement(token.StartPosition, token.EndPosition);
            }
        }

        public ReturnStatement TryParseReturn()
        {
            return (ReturnStatement)Tokens.TryGetTree(StatementParser);
            ASTNode StatementParser()
            {
                var token = Consume(RETURN);
                if (token == null) return null;

                if (!IsFunction && !IsOperator) throw Error("Return statements can only exist in functions and operators!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (CurrentTokenType == TokenType.SemiColon) return new ReturnStatement(token.StartPosition, token.EndPosition);

                var value = TryParseExpression();
                if (value == null) throw Error("Expected a return value or a semi-colon!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var type = value.ResolveType();
                if (IsFunction)
                {
                    var func = (Function)Node;
                    if (func.ReturnType == null) throw Error("Function should not return any value!", token.StartPosition, token.EndPosition);

                    if (!TypeEquals(func.ReturnType, type)) throw Error($"Cannot return a value of type '{type.Name}', function should return '{func.ReturnType.Name}'.", token.StartPosition, token.EndPosition);
                }
                else if (IsOperator)
                {
                    var op = (OperatorDeclaration)Node;
                    if (op.ReturnType == null) throw Error("Operator should not return any value!", token.StartPosition, token.EndPosition);

                    if (!TypeEquals(op.ReturnType, type)) throw Error($"Cannot return a value of type '{type.Name}', operator should return '{op.ReturnType.Name}'.", token.StartPosition, token.EndPosition);
                }

                return new ReturnStatement(token.StartPosition, token.EndPosition, value);
            }
        }

        public CaseStatement TryParseCase()
        {
            return (CaseStatement)Tokens.TryGetTree(StatementParser);
            ASTNode StatementParser()
            {
                var token = Consume(CASE);
                if (token == null) return null;

                if (!InSwitch) throw Error("Case statements can only exist inside switch blocks!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var value = TryParseExpression();
                if (value == null) throw Error("Expected an expression specifying the case value", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (Consume(TokenType.Colon) == null) throw Error("Expected colon after case expression!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                /* TODO: advanced type checks here, intrinsic conversions should be allowed but other anomalies reported.
                var type = value.ResolveType();
                var parent = GetHashCode containing switch somehow;
                if (!TypeEquals(parent.Expression.ResolveType(), type))
                    throw Error("Cannot use case: '" + type.Name + "', in switch of type '" + parent.Expression.ResolveType() + "'."
                            , token.StartPosition, token.EndPosition);
                 * */

                return new CaseStatement(value, token.StartPosition, token.EndPosition);
            }
        }

        public DefaultStatement TryParseDefault()
        {
            return (DefaultStatement)Tokens.TryGetTree(StatementParser);
            ASTNode StatementParser()
            {
                var token = Consume(DEFAULT);
                if (token == null) return null;

                if (!InSwitch) throw Error("Default statements can only exist inside switch blocks!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (Consume(TokenType.Colon) == null) throw Error("Expected colon after default statement!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                return new DefaultStatement(token.StartPosition, token.EndPosition);
            }
        }

        #endregion

        #region Expressions

        public Expression TryParseExpression()
        {
            return (Expression)Tokens.TryGetTree(ExprParser);
            ASTNode ExprParser()
            {
                var expr = TryParseAtomicExpression();
                if (expr == null) return null;

                while (GlobalLists.ValidOperatorSymbols.Contains(CurrentTokenType))
                {
                    expr = TryParseIfExpression(expr) ?? TryParseInOperator(expr) ?? (Expression)null;
                    if (expr == null) throw Error("Could not parse expression/operator!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                }

                return expr;
            }
        }

        public Expression TryParseScopedExpression()
        {
            return (Expression)Tokens.TryGetTree(ExprParser);
            ASTNode ExprParser()
            {
                if (Consume(TokenType.LeftParenth) == null) return null;

                var expr = TryParseExpression();
                if (expr == null) return null;

                if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                return expr;
            }
        }

        public Expression TryParseIfExpression(Expression expr)
        {
            return (Expression)Tokens.TryGetTree(IfexprParser);
            ASTNode IfexprParser()
            {
                return null; // TODO
            }
        }

        public Expression TryParseAtomicExpression()
        {
            return (Expression)Tokens.TryGetTree(AtomParser);
            ASTNode AtomParser()
            {
                return TryParsePreOperator() ?? TryParsePostOperator() ?? TryParseScopedExpression() ?? TryParseExpressionLeaf() ?? (Expression)null;
            }
        }

        public Expression TryParseInOperator(Expression expr)
        {
            return (Expression)Tokens.TryGetTree(InopParser);
            ASTNode InopParser()
            {
                Expression lhs, rhs, rhs2;
                VariableType lhsType, rhsType, rhs2Type;
                InOpDeclaration opA, opB;
                lhs = expr;
                lhsType = lhs.ResolveType();

                var opA_tok = Consume(CurrentTokenType);
                rhs = TryParseAtomicExpression();
                if (rhs == null) return null; // error?
                rhsType = rhs.ResolveType();

                if (!Symbols.GetInOperator(out opA, opA_tok.Value, lhsType, rhsType)) throw Error($"No operator '{opA_tok}' with operands of types '{lhsType.Name}' and '{rhsType.Name}' was found!", opA_tok.StartPosition, opA_tok.EndPosition);

                while (GlobalLists.ValidOperatorSymbols.Contains(CurrentTokenType))
                {
                    Tokens.PushSnapshot();

                    var opB_tok = Consume(CurrentTokenType);
                    rhs2 = TryParseAtomicExpression();
                    if (rhs == null) throw Error("Expected a valid expression as the right hand side of the operator!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    rhs2Type = rhs2.ResolveType();

                    Tokens.PopSnapshot();

                    if (!Symbols.GetInOperator(out opB, opB_tok.Value, rhsType, rhs2Type)) throw Error($"No operator '{opB_tok}' with operands of types '{rhsType.Name}' and '{rhs2Type.Name}' was found!", opB_tok.StartPosition, opB_tok.EndPosition);

                    if (opA.Precedence <= opB.Precedence) break;

                    rhs = TryParseInOperator(rhs);
                }

                return new InOpReference(opA, lhs, rhs, lhs.StartPos, rhs.EndPos);
            }
        }

        public Expression TryParsePreOperator()
        {
            return (Expression)Tokens.TryGetTree(PreopParser);
            ASTNode PreopParser()
            {
                return null;
            }
        }

        public Expression TryParsePostOperator()
        {
            return (Expression)Tokens.TryGetTree(PostopParser);
            ASTNode PostopParser()
            {
                return null;
            }
        }

        public Expression TryParseExpressionLeaf()
        {
            return (Expression)Tokens.TryGetTree(ExprParser);
            ASTNode ExprParser()
            {
                //TODO: add 'new' here
                return TryParseFunctionCall() ?? TryParseReference() ?? TryParseLiteral() ?? (Expression)null;
            }
        }

        public Expression TryParseLiteral()
        {
            return (Expression)Tokens.TryGetTree(LiteralParser);
            ASTNode LiteralParser()
            { //TODO: object/class literals?
                return TryParseInteger() ?? TryParseFloat() ?? TryParseString() ?? TryParseName() ?? TryParseBoolean() ?? (Expression)null;
            }
        }

        public Expression TryParseFunctionCall()
        {
            return (Expression)Tokens.TryGetTree(CallParser);
            ASTNode CallParser()
            {
                // TODO: special parsing for call specifiers (Super/Global)
                var funcRef = TryParseBasicRef();
                if (funcRef == null) return null;

                if (Consume(TokenType.LeftParenth) == null) return null;

                if (funcRef.Node.Type != ASTNodeType.Function) throw Error($"'{funcRef.Name}' is not a function!", funcRef.StartPos, funcRef.EndPos);

                Function func = funcRef.Node as Function;
                var parameters = new List<Expression>();
                var currentParam = TryParseExpression();
                foreach (FunctionParameter p in func.Parameters)
                {
                    // TODO: allow automatic type conversion for compatible basic types
                    // TODO: allow optional parameters to be left out.
                    if (currentParam == null || !TypeEquals(currentParam.ResolveType(), p.VarType)) throw Error($"Expected a parameter of type '{p.VarType.Name}'!", currentParam.StartPos, currentParam.EndPos);

                    parameters.Add(currentParam);
                    if (Consume(TokenType.Comma) == null) break;
                    currentParam = TryParseExpression();
                }

                if (parameters.Count != func.Parameters.Count) throw Error($"Expected {func.Parameters.Count} parameters to function '{func.Name}'!", funcRef.StartPos, funcRef.EndPos);

                if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (CurrentTokenType == TokenType.Dot)
                {
                    return TryParseCompositeRecursive(new FunctionCall(funcRef, parameters, funcRef.StartPos, CurrentPosition));
                }

                return new FunctionCall(funcRef, parameters, funcRef.StartPos, CurrentPosition);
            }
        }

        public SymbolReference TryParseReference()
        {
            return (SymbolReference)Tokens.TryGetTree(RefParser);
            ASTNode RefParser()
            {
                // TODO: handle expression results?
                return TryParseArrayRef() ?? TryParseCompositeRef() ?? TryParseBasicRef() ?? (SymbolReference)null;
            }
        }

        public SymbolReference TryParseBasicRef(Expression compositeOuter = null)
        {
            return (SymbolReference)Tokens.TryGetTree(RefParser);
            ASTNode RefParser()
            {
                var token = Consume(TokenType.Word);
                if (token == null) return null;

                ASTNode symbol = null;
                FunctionCall func = compositeOuter as FunctionCall;
                SymbolReference outer = compositeOuter as SymbolReference;

                if (func != null)
                {
                    var containingClass = NodeUtils.GetContainingClass(func.ResolveType().Declaration);
                    if (!Symbols.TryGetSymbolFromSpecificScope(token.Value, out symbol, $"{containingClass.GetInheritanceString()}.{func.Function.Name}")) throw Error($"Left side has no member named '{func.Function.Name}'!", token.StartPosition, token.EndPosition);
                }
                else if (outer != null)
                {
                    var containingClass = NodeUtils.GetContainingClass(outer.ResolveType().Declaration);
                    if (!Symbols.TryGetSymbolFromSpecificScope(token.Value, out symbol, $"{containingClass.GetInheritanceString()}.{outer.ResolveType().Name}")) throw Error($"Left side has no member named '{outer.Name}'!", token.StartPosition, token.EndPosition);
                }
                else
                {
                    if (!Symbols.TryGetSymbol(token.Value, out symbol, NodeUtils.GetOuterClassScope(Node))) throw Error($"No symbol named '{token.Value}' exists in the current scope!", token.StartPosition, token.EndPosition);
                }

                return new SymbolReference(symbol, token.StartPosition, token.EndPosition, token.Value);
            }
        }

        public Expression TryParseArrayRef()
        {
            return (Expression)Tokens.TryGetTree(RefParser);
            ASTNode RefParser()
            {
                Expression arrayExpr = TryParseCompositeRef() ?? TryParseFunctionCall() ?? TryParseBasicRef() ?? (Expression)null;
                if (arrayExpr == null) return null;

                if (Consume(TokenType.LeftSqrBracket) == null) return null;

                // TODO: possibly check for number type?
                Expression index = TryParseExpression();
                if (index == null)
                {
                    Log.LogError("Expected a valid expression or number as array index!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                if (Consume(TokenType.RightSqrBracket) == null)
                {
                    Log.LogError("Expected ']'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                //TODO: check that the type is actually an array type.

                if (CurrentTokenType == TokenType.Dot)
                {
                    return TryParseCompositeRecursive(new ArraySymbolRef(arrayExpr, index, arrayExpr.StartPos, CurrentPosition));
                }

                return new ArraySymbolRef(arrayExpr, index, arrayExpr.StartPos, CurrentPosition);
            }
        }

        public CompositeSymbolRef TryParseCompositeRef()
        {
            return (CompositeSymbolRef)Tokens.TryGetTree(RefParser);
            ASTNode RefParser()
            {
                // TODO: possibly atomic / leaf?
                Expression left = TryParseBasicRef() ?? TryParseFunctionCall() ?? (Expression)null;
                if (left == null) return null;

                CompositeSymbolRef right = TryParseCompositeRecursive(left);
                if (right == null) return null; //Error?

                return right;
            }
        }

        private CompositeSymbolRef TryParseCompositeRecursive(Expression expr)
        {
            return (CompositeSymbolRef)Tokens.TryGetTree(CompositeParser);
            ASTNode CompositeParser()
            {
                Expression lhs = expr;
                VariableType lhsType = lhs.ResolveType();

                var token = Consume(TokenType.Dot);
                if (token == null) return null;

                Expression rhs = TryParseBasicRef(lhs) ?? TryParseFunctionCall() ?? (Expression)null;
                if (rhs == null) throw Error("Expected a valid member name to follow the dot!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (!CompositeTypes.Contains(lhsType.NodeType)) throw Error("Left side symbol is not of a composite type!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                while (CurrentTokenType == TokenType.Dot)
                {
                    return TryParseCompositeRecursive(new CompositeSymbolRef(lhs, rhs, lhs.StartPos, rhs.EndPos));
                }

                return new CompositeSymbolRef(lhs, rhs, lhs.StartPos, rhs.EndPos);
            }
        }

        public IntegerLiteral TryParseInteger()
        {
            return (IntegerLiteral)Tokens.TryGetTree(IntParser);
            ASTNode IntParser()
            {
                var token = Consume(TokenType.IntegerNumber);
                if (token == null) return null;

                return new IntegerLiteral(int.Parse(token.Value, CultureInfo.InvariantCulture), token.StartPosition, token.EndPosition);
            }
        }

        public BooleanLiteral TryParseBoolean()
        {
            return (BooleanLiteral)Tokens.TryGetTree(BoolParser);
            ASTNode BoolParser()
            {
                if (CurrentIs(TRUE, FALSE))
                {
                    var token = Consume(CurrentTokenType);
                    return new BooleanLiteral(bool.Parse(token.Value), token.StartPosition, token.EndPosition);
                }
                return null;
            }
        }

        public FloatLiteral TryParseFloat()
        {
            return (FloatLiteral)Tokens.TryGetTree(FloatParser);
            ASTNode FloatParser()
            {
                var token = Consume(TokenType.FloatingNumber);
                if (token == null) return null;

                return new FloatLiteral(float.Parse(token.Value, CultureInfo.InvariantCulture), token.StartPosition, token.EndPosition);
            }
        }

        public NameLiteral TryParseName()
        {
            return (NameLiteral)Tokens.TryGetTree(NameParser);
            ASTNode NameParser()
            {
                var token = Consume(TokenType.NameLiteral);
                if (token == null) return null;

                return new NameLiteral(token.Value, token.StartPosition, token.EndPosition);
            }
        }

        public StringLiteral TryParseString()
        {
            return (StringLiteral)Tokens.TryGetTree(StringParser);
            ASTNode StringParser()
            {
                var token = Consume(TokenType.StringLiteral);
                if (token == null) return null;

                return new StringLiteral(token.Value, token.StartPosition, token.EndPosition);
            }
        }

        #endregion
    }
}
