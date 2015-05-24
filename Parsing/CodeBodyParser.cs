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

namespace ME3Script.Parsing
{
    public class CodeBodyParser : StringParserBase
    {
        private SymbolTable Symbols;
        private String OuterClassScope;
        private IContainsLocals NodeVariables;
        private ASTNode Node;
        private CodeBody Body;

        private bool IsFunction { get { return Node.Type == ASTNodeType.Function; } }
        private bool IsState { get { return Node.Type == ASTNodeType.State; } }
        private bool IsOperator
        {
            get
            {
                return Node.Type == ASTNodeType.PrefixOperator
                    || Node.Type == ASTNodeType.PostfixOperator
                    || Node.Type == ASTNodeType.InfixOperator;
            }
        }
        private int _loopCount;
        private bool InLoop { get { return _loopCount > 0; } }
        private int _switchCount;
        private bool InSwitch { get { return _switchCount > 0; } }

        public CodeBodyParser(TokenStream<String> tokens, CodeBody body, SymbolTable symbols, ASTNode containingNode, MessageLog log = null)
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
                return Error("Could not find the code body for the current node, please contact the maintainers of this compiler!");

            var body = TryParseBody(false);
            if (body == null)
                return null;
            Body.Statements = body.Statements;

            if (!Tokens.CurrentItem.StartPosition.Equals(Body.EndPos))
                return Error("Could not parse a valid statement, even though the current code body has supposedly not ended yet.", 
                    CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

            return Body;
        }

        private bool TypeEquals(VariableType a, VariableType b)
        {
            return a.Name.ToLower() == b.Name.ToLower();
        }

        public CodeBody TryParseBody(bool requireBrackets = true)
        {
            Func<ASTNode> codeParser = () =>
            {
                if (requireBrackets && Tokens.ConsumeToken(TokenType.LeftBracket) == null)
                    return Error("Expected '{'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var statements = new List<Statement>();
                var startPos = CurrentPosition;
                var current = TryParseInnerStatement();
                while (current != null)
                {
                    if (!SemiColonExceptions.Contains(current.Type) && Tokens.ConsumeToken(TokenType.SemiColon) == null)
                        return Error("Expected semi-colon after statement!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    statements.Add(current);
                    current = TryParseInnerStatement();
                }

                var endPos = CurrentPosition;
                if (requireBrackets && Tokens.ConsumeToken(TokenType.RightBracket) == null)
                    return Error("Expected '}'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                return new CodeBody(statements, startPos, endPos);
            };
            return (CodeBody)Tokens.TryGetTree(codeParser);
        }

        public CodeBody TryParseBodyOrStatement(bool allowEmpty = false)
        {
            Func<ASTNode> bodyParser = () =>
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
                    if (allowEmpty && Tokens.ConsumeToken(TokenType.SemiColon) != null)
                    {
                        body = new CodeBody(null, CurrentPosition.GetModifiedPosition(0, -1, -1), CurrentPosition);
                    }
                    else
                        return Error("Expected a code body or single statement!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                }

                return body;
            };
            return (CodeBody)Tokens.TryGetTree(bodyParser);
        }

        public Statement TryParseInnerStatement(bool throwError = false)
        {
            Func<ASTNode> statementParser = () =>
            {
                var statement = TryParseLocalVarDecl() ??
                                TryParseAssignStatement() ??
                                TryParseIf() ??
                                TryParseWhile() ??
                                TryParseFor() ??
                                TryParseDoUntil() ??
                                TryParseContinue() ??
                                TryParseBreak() ??
                                TryParseStop() ??
                                TryParseReturn() ??
                                (Statement)null;

                if (statement == null && throwError)
                    return Error("Expected a valid statement!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                return statement;
            };
            return (Statement)Tokens.TryGetTree(statementParser);
        }

        public VariableDeclaration TryParseLocalVarDecl()
        {
            Func<ASTNode> declarationParser = () =>
            {
                if (Tokens.ConsumeToken(TokenType.LocalVariable) == null)
                    return null;

                ASTNode type = TryParseType();
                if (type == null)
                    return Error("Expected variable type!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                if (!Symbols.TryGetSymbol((type as VariableType).Name, out type, OuterClassScope))
                    return Error("The type '" + (type as VariableType).Name + "' does not exist in the current scope!", type.StartPos, type.EndPos);

                var vars = ParseVariableNames();
                if (vars == null)
                    return Error("Malformed variable names!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                foreach (VariableIdentifier ident in vars)
                {
                    if (Symbols.SymbolExistsInCurrentScope(ident.Name))
                        return Error("A variable named '" + ident.Name + "' already exists in this scope!", ident.StartPos, ident.EndPos);
                    Variable variable = new Variable(null, ident, type as VariableType, ident.StartPos, ident.EndPos);
                    Symbols.AddSymbol(variable.Name, variable);
                    NodeVariables.Locals.Add(variable);
                }

                return new VariableDeclaration(type as VariableType, null, vars, vars.First().StartPos, vars.Last().EndPos);
            };
            return (VariableDeclaration)Tokens.TryGetTree(declarationParser);
        }

        public AssignStatement TryParseAssignStatement()
        {
            Func<ASTNode> assignParser = () =>
            {
                var target = TryParseReference();
                var assign = Tokens.ConsumeToken(TokenType.Assign);
                if (assign == null)
                    return null;
                else if (target == null)
                    return Error("Assignments require a variable target (LValue expected).", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var value = TryParseExpression();
                if (value == null)
                    return Error("Assignments require a resolvable expression as value! (RValue expected).", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                // TODO: allow built-in type convertion here!
                if (!TypeEquals(target.ResolveType(), value.ResolveType()))
                    return Error("Cannot assign a value of type '" + value.ResolveType() + "' to a variable of type '" + null + "'."
                        , assign.StartPosition, assign.EndPosition);

                return new AssignStatement(target, value, assign.StartPosition, assign.EndPosition);
            };
            return (AssignStatement)Tokens.TryGetTree(assignParser);
        }

        public IfStatement TryParseIf()
        {
            Func<ASTNode> ifParser = () =>
            {
                var token = Tokens.ConsumeToken(TokenType.If);
                if (token == null)
                    return null;

                if (Tokens.ConsumeToken(TokenType.LeftParenth) == null)
                    return Error("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var condition = TryParseExpression();
                if (condition == null)
                    return Error("Expected an expression as the if-condition!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                if (condition.ResolveType().Name != "bool") // TODO: check/fix!
                    return Error("Expected a boolean result from the condition!", condition.StartPos, condition.EndPos);

                if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                    return Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                CodeBody thenBody = TryParseBodyOrStatement();
                if (thenBody == null)
                    return Error("Expected a statement or code block!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                CodeBody elseBody = null;
                var elsetoken = Tokens.ConsumeToken(TokenType.Else);
                if (elsetoken != null)
                {
                    elseBody = TryParseBodyOrStatement();
                    if (elseBody == null)
                        return Error("Expected a statement or code block!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                }

                return new IfStatement(condition, thenBody, token.StartPosition, token.EndPosition, elseBody);
            };
            return (IfStatement)Tokens.TryGetTree(ifParser);
        }

        public WhileLoop TryParseWhile()
        {
            Func<ASTNode> whileParser = () =>
            {
                var token = Tokens.ConsumeToken(TokenType.While);
                if (token == null)
                    return null;

                if (Tokens.ConsumeToken(TokenType.LeftParenth) == null)
                    return Error("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var condition = TryParseExpression();
                if (condition == null)
                    return Error("Expected an expression as the while condition!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                if (condition.ResolveType().Name != "bool") // TODO: check/fix!
                    return Error("Expected a boolean result from the condition!", condition.StartPos, condition.EndPos);

                if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                    return Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                CodeBody body = TryParseBodyOrStatement(allowEmpty: true);
                if (body == null)
                    return null;

                return new WhileLoop(condition, body, token.StartPosition, token.EndPosition);
            };
            return (WhileLoop)Tokens.TryGetTree(whileParser);
        }

        public DoUntilLoop TryParseDoUntil()
        {
            Func<ASTNode> untilParser = () =>
            {
                var doToken = Tokens.ConsumeToken(TokenType.Do);
                if (doToken == null)
                    return null;

                CodeBody body = TryParseBodyOrStatement();
                if (body == null)
                    return null;

                var untilToken = Tokens.ConsumeToken(TokenType.Until);
                if (untilToken == null)
                    return Error("Expected 'until'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (Tokens.ConsumeToken(TokenType.LeftParenth) == null)
                    return Error("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var condition = TryParseExpression();
                if (condition == null)
                    return Error("Expected an expression as the until condition!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                if (condition.ResolveType().Name != "bool") // TODO: check/fix!
                    return Error("Expected a boolean result from the condition!", condition.StartPos, condition.EndPos);

                if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                    return Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                return new DoUntilLoop(condition, body, untilToken.StartPosition, untilToken.EndPosition);
            };
            return (DoUntilLoop)Tokens.TryGetTree(untilParser);
        }

        public ForLoop TryParseFor()
        {
            Func<ASTNode> forParser = () =>
            {
                var token = Tokens.ConsumeToken(TokenType.For);
                if (token == null)
                    return null;

                if (Tokens.ConsumeToken(TokenType.LeftParenth) == null)
                    return Error("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var initStatement = TryParseInnerStatement();
                if (initStatement.Type != ASTNodeType.AssignStatement && initStatement.Type != ASTNodeType.FunctionCall)
                    return Error("Init statement in a for-loop must be an assignment or a function call!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                    return Error("Expected semi-colon after init statement!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var condition = TryParseExpression();
                if (condition == null)
                    return Error("Expected an expression as the for condition!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                if (condition.ResolveType().Name != "bool") // TODO: check/fix!
                    return Error("Expected a boolean result from the condition!", condition.StartPos, condition.EndPos);

                if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                    return Error("Expected semi-colon after condition!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var updateStatement = TryParseInnerStatement();
                if (updateStatement.Type != ASTNodeType.AssignStatement && initStatement.Type != ASTNodeType.Function
                    && initStatement.Type != ASTNodeType.PrefixOperator && initStatement.Type != ASTNodeType.PostfixOperator) // TODO: what is actually supported?
                    return Error("Init statement in a for-loop must be an assignment, in/decrement or function call!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                    return Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                CodeBody body = TryParseBodyOrStatement(allowEmpty: true);
                if (body == null)
                    return null;

                return new ForLoop(condition, body, initStatement, updateStatement, token.StartPosition, token.EndPosition);
            };
            return (ForLoop)Tokens.TryGetTree(forParser);
        }

        public BreakStatement TryParseBreak()
        {
            Func<ASTNode> statementParser = () =>
            {
                var token = Tokens.ConsumeToken(TokenType.Break);
                if (token == null)
                    return null;

                if (!InLoop && !InSwitch)
                    return Error("The break keyword is only valid inside loops and switch statements!", token.StartPosition, token.EndPosition);

                return new BreakStatement(token.StartPosition, token.EndPosition);
            };
            return (BreakStatement)Tokens.TryGetTree(statementParser);
        }

        public ContinueStatement TryParseContinue()
        {
            Func<ASTNode> statementParser = () =>
            {
                var token = Tokens.ConsumeToken(TokenType.Continue);
                if (token == null)
                    return null;

                if (!InLoop)
                    return Error("The continue keyword is only valid inside loops!", token.StartPosition, token.EndPosition);

                return new ContinueStatement(token.StartPosition, token.EndPosition);
            };
            return (ContinueStatement)Tokens.TryGetTree(statementParser);
        }

        public StopStatement TryParseStop()
        {
            Func<ASTNode> statementParser = () =>
            {
                var token = Tokens.ConsumeToken(TokenType.Stop);
                if (token == null)
                    return null;

                if (!IsState)
                    return Error("The stop keyword is only valid inside state code!", token.StartPosition, token.EndPosition);

                return new StopStatement(token.StartPosition, token.EndPosition);
            };
            return (StopStatement)Tokens.TryGetTree(statementParser);
        }

        public ReturnStatement TryParseReturn()
        {
            Func<ASTNode> statementParser = () =>
            {
                var token = Tokens.ConsumeToken(TokenType.Return);
                if (token == null)
                    return null;

                if (!IsFunction && !IsOperator)
                    return Error("Return statements can only exist in functions and operators!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (CurrentTokenType == TokenType.SemiColon)
                    return new ReturnStatement(token.StartPosition, token.EndPosition);

                var value = TryParseExpression();
                if (value == null)
                    return Error("Expected a return value or a semi-colon!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var type = value.ResolveType();
                if (IsFunction)
                {
                    var func = Node as Function;
                    if (func.ReturnType == null)
                        return Error("Function should not return any value!", token.StartPosition, token.EndPosition);

                    if (!TypeEquals(func.ReturnType, type))
                        return Error("Cannot return a value of type '" + type.Name + "', function should return '" + func.ReturnType.Name + "'."
                            , token.StartPosition, token.EndPosition);
                }
                else if (IsOperator)
                {
                    var op = Node as OperatorDeclaration;
                    if (op.ReturnType == null)
                        return Error("Operator should not return any value!", token.StartPosition, token.EndPosition);

                    if (!TypeEquals(op.ReturnType, type))
                        return Error("Cannot return a value of type '" + type.Name + "', operator should return '" + op.ReturnType.Name + "'."
                            , token.StartPosition, token.EndPosition);
                }

                return new ReturnStatement(token.StartPosition, token.EndPosition, value);
            };
            return (ReturnStatement)Tokens.TryGetTree(statementParser);
        }

        #region Expressions

        public Expression TryParseExpression()
        {
            Func<ASTNode> exprParser = () =>
            {
                var expr = TryParseAtomicExpression();
                if (expr == null)
                    return null;

                while (GlobalLists.ValidOperatorSymbols.Contains(CurrentTokenType))
                {
                    expr = TryParseIfExpression(expr) ?? TryParseInOperator(expr) ?? (Expression)null;
                    if (expr == null)
                        return Error("Could not parse expression/operator!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                }

                return expr;
            };
            return (Expression)Tokens.TryGetTree(exprParser);
        }

        public Expression TryParseScopedExpression()
        {
            Func<ASTNode> exprParser = () =>
            {
                if (Tokens.ConsumeToken(TokenType.LeftParenth) == null)
                    return null;

                var expr = TryParseExpression();
                if (expr == null)
                    return null;

                if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                    return Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                return expr;
            };
            return (Expression)Tokens.TryGetTree(exprParser);
        }

        public Expression TryParseIfExpression(Expression expr)
        {
            Func<ASTNode> ifexprParser = () =>
            {
                return null;
            };
            return (Expression)Tokens.TryGetTree(ifexprParser);
        }

        public Expression TryParseAtomicExpression()
        {
            Func<ASTNode> atomParser = () =>
            {
                return TryParsePreOperator() ?? TryParsePostOperator() ?? TryParseScopedExpression() ?? TryParseExpressionLeaf() ?? (Expression)null;
            };
            return (Expression)Tokens.TryGetTree(atomParser);
        }

        public Expression TryParseInOperator(Expression expr)
        {
            Func<ASTNode> inopParser = () =>
            {
                Expression lhs, rhs, rhs2;
                VariableType lhsType, rhsType, rhs2Type;
                InOpDeclaration opA, opB;
                lhs = expr;
                lhsType = lhs.ResolveType();

                var opA_tok = Tokens.ConsumeToken(CurrentTokenType);
                rhs = TryParseAtomicExpression();
                if (rhs == null)
                    return null; // error?
                rhsType = rhs.ResolveType();

                if (!Symbols.GetInOperator(out opA, opA_tok.Value, lhsType, rhsType))
                    return Error("No operator '" + opA_tok + "' with operands of types '" + lhsType.Name + "' and '" + rhsType.Name + "' was found!",
                        opA_tok.StartPosition, opA_tok.EndPosition);

                while (GlobalLists.ValidOperatorSymbols.Contains(CurrentTokenType))
                {
                    Tokens.PushSnapshot();

                    var opB_tok = Tokens.ConsumeToken(CurrentTokenType);
                    rhs2 = TryParseAtomicExpression();
                    if (rhs == null)
                        return Error("Expected a valid expression as the right hand side of the operator!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    rhs2Type = rhs2.ResolveType();

                    Tokens.PopSnapshot();

                    if (!Symbols.GetInOperator(out opB, opB_tok.Value, rhsType, rhs2Type))
                        return Error("No operator '" + opB_tok + "' with operands of types '" + rhsType.Name + "' and '" + rhs2Type.Name + "' was found!",
                            opB_tok.StartPosition, opB_tok.EndPosition);

                    if (opA.Precedence < opB.Precedence)
                        break;

                    rhs = TryParseInOperator(rhs);
                }

                return new InOpReference(opA, lhs, rhs, lhs.StartPos, rhs.EndPos);
            };
            return (Expression)Tokens.TryGetTree(inopParser);
        }

        public Expression TryParsePreOperator()
        {
            Func<ASTNode> preopParser = () =>
            {
                return null;
            };
            return (Expression)Tokens.TryGetTree(preopParser);
        }

        public Expression TryParsePostOperator()
        {
            Func<ASTNode> postopParser = () =>
            {
                return null;
            };
            return (Expression)Tokens.TryGetTree(postopParser);
        }

        public Expression TryParseExpressionLeaf()
        {
            Func<ASTNode> exprParser = () =>
            {
                //TODO: add 'new' here
                return TryParseFunctionCall() ?? TryParseReference() ?? TryParseLiteral() ?? (Expression)null;
            };
            return (Expression)Tokens.TryGetTree(exprParser);
        }

        public Expression TryParseLiteral()
        {
            Func<ASTNode> literalParser = () =>
            {
                return TryParseInteger() ?? TryParseFloat() ?? TryParseString() ?? TryParseName() ?? TryParseBoolean() ?? (Expression)null;
            };
            return (Expression)Tokens.TryGetTree(literalParser);
        }

        public FunctionCall TryParseFunctionCall()
        {
            Func<ASTNode> callParser = () =>
            {
                // TODO: special parsing for call specifiers (Super/Global)
                var funcRef = TryParseReference();
                if (funcRef == null)
                    return null;

                if (Tokens.ConsumeToken(TokenType.LeftParenth) == null)
                    return null;

                if (funcRef.Node.Type != ASTNodeType.Function)
                    return Error("'" + funcRef.Name + "' is not a function!", funcRef.StartPos, funcRef.EndPos);

                Function func = funcRef.Node as Function;
                List<Expression> parameters = new List<Expression>();
                var currentParam = TryParseExpression();
                foreach (FunctionParameter p in func.Parameters)
                {
                    // TODO: allow automatic type conversion for compatible basic types
                    // TODO: allow optional parameters to be left out.
                    if (currentParam == null || !TypeEquals(currentParam.ResolveType(), p.VarType))
                        return Error("Expected a parameter of type '" + p.VarType.Name + "'!", currentParam.StartPos, currentParam.EndPos);

                    parameters.Add(currentParam);
                    if (Tokens.ConsumeToken(TokenType.Comma) == null)
                        break;
                    currentParam = TryParseExpression();
                }

                if (parameters.Count != func.Parameters.Count)
                    return Error("Expected " + func.Parameters.Count + " parameters to function '" + func.Name + "'!", funcRef.StartPos, funcRef.EndPos);

                if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                    return Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                return new FunctionCall(funcRef, parameters, funcRef.StartPos, CurrentPosition);
            };
            return (FunctionCall)Tokens.TryGetTree(callParser);
        }

        public SymbolReference TryParseReference()
        {
            Func<ASTNode> refParser = () =>
            {
                // TODO: handle function call returns?
                return TryParseCompositeRef() ?? TryParseArrayRef() ?? TryParseBasicRef() ?? (SymbolReference)null;
            };
            return (SymbolReference)Tokens.TryGetTree(refParser);
        }

        public SymbolReference TryParseBasicRef(SymbolReference compositeOuter = null)
        {
            Func<ASTNode> refParser = () =>
            {
                var token = Tokens.ConsumeToken(TokenType.Word);
                if (token == null)
                    return null;

                ASTNode symbol = null;
                if (compositeOuter != null)
                {
                    var containingClass = NodeUtils.GetContainingClass(compositeOuter.ResolveType().Declaration);
                    if (!Symbols.TryGetSymbolFromSpecificScope(token.Value, out symbol, containingClass.GetInheritanceString() + "." + compositeOuter.ResolveType().Name))
                        return Error("'" + compositeOuter.Name + "' has no member named '" + token.Value + "'!", compositeOuter.Node.StartPos, token.EndPosition);
                }
                else if (!Symbols.TryGetSymbol(token.Value, out symbol, NodeUtils.GetOuterClassScope(Node)))
                    return Error("No symbol named '" + token.Value + "' exists in the current scope!", token.StartPosition, token.EndPosition);

                return new SymbolReference(symbol, token.StartPosition, token.EndPosition, token.Value);
            };
            return (SymbolReference)Tokens.TryGetTree(refParser);
        }

        public ArraySymbolRef TryParseArrayRef(SymbolReference compositeOuter = null)
        {
            Func<ASTNode> refParser = () =>
            {
                var token = Tokens.ConsumeToken(TokenType.Word);
                if (token == null)
                    return null;

                ASTNode symbol = null;
                if (compositeOuter != null)
                {
                    var containingClass = NodeUtils.GetContainingClass(compositeOuter.ResolveType().Declaration);
                    if (!Symbols.TryGetSymbolFromSpecificScope(token.Value, out symbol, containingClass.GetInheritanceString() + "." + compositeOuter.ResolveType().Name))
                        return Error("'" + compositeOuter.Name + "' has no member named '" + token.Value + "'!", compositeOuter.Node.StartPos, token.EndPosition);
                }
                else if (!Symbols.TryGetSymbol(token.Value, out symbol, NodeUtils.GetOuterClassScope(Node)))
                    return Error("No symbol named '" + token.Value + "' exists in the current scope!", token.StartPosition, token.EndPosition);

                if (Tokens.ConsumeToken(TokenType.LeftSqrBracket) == null)
                    return null;

                Expression index;
                index = TryParseExpression();
                if (index == null)
                {
                    Log.LogError("Expected a valid expression or number as array index!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                if (Tokens.ConsumeToken(TokenType.RightSqrBracket) == null)
                {
                    Log.LogError("Expected ']'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                //TODO: check that the type is actually an array type.
                return new ArraySymbolRef(symbol, index, token.StartPosition, CurrentPosition, token.Value);
            };
            return (ArraySymbolRef)Tokens.TryGetTree(refParser);
        }

        public CompositeSymbolRef TryParseCompositeRef(SymbolReference compositeOuter = null)
        {
            Func<ASTNode> refParser = () =>
            {
                SymbolReference outer = TryParseArrayRef(compositeOuter) ?? TryParseBasicRef(compositeOuter) ?? (SymbolReference)null;
                if (outer == null)
                    return null;

                if (Tokens.ConsumeToken(TokenType.Dot) == null)
                    return null;

                if (!CompositeTypes.Contains(outer.ResolveType().NodeType))
                    return Error("Left side symbol is not a composite type!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                SymbolReference inner = TryParseCompositeRef(outer) ?? TryParseArrayRef(outer) ?? TryParseBasicRef(outer) ?? (SymbolReference)null;
                if (inner == null)
                    return Error("Expected a valid member name to follow the dot!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                return new CompositeSymbolRef(outer, inner, outer.StartPos, CurrentPosition);
            };
            return (CompositeSymbolRef)Tokens.TryGetTree(refParser);
        }

        public IntegerLiteral TryParseInteger()
        {
            Func<ASTNode> intParser = () =>
            {
                var token = Tokens.ConsumeToken(TokenType.IntegerNumber);
                if (token == null)
                    return null;

                return new IntegerLiteral(Int32.Parse(token.Value, CultureInfo.InvariantCulture), token.StartPosition, token.EndPosition);
            };
            return (IntegerLiteral)Tokens.TryGetTree(intParser);
        }

        public BooleanLiteral TryParseBoolean()
        {
            Func<ASTNode> boolParser = () =>
            {
                if (CurrentTokenType != TokenType.True && CurrentTokenType != TokenType.False)
                    return null;

                var token = Tokens.ConsumeToken(CurrentTokenType);
                return new BooleanLiteral(Boolean.Parse(token.Value), token.StartPosition, token.EndPosition);
            };
            return (BooleanLiteral)Tokens.TryGetTree(boolParser);
        }

        public FloatLiteral TryParseFloat()
        {
            Func<ASTNode> floatParser = () =>
            {
                var token = Tokens.ConsumeToken(TokenType.FloatingNumber);
                if (token == null)
                    return null;

                return new FloatLiteral(Single.Parse(token.Value, CultureInfo.InvariantCulture), token.StartPosition, token.EndPosition);
            };
            return (FloatLiteral)Tokens.TryGetTree(floatParser);
        }

        public NameLiteral TryParseName()
        {
            Func<ASTNode> nameParser = () =>
            {
                var token = Tokens.ConsumeToken(TokenType.Name);
                if (token == null)
                    return null;

                return new NameLiteral(token.Value, token.StartPosition, token.EndPosition);
            };
            return (NameLiteral)Tokens.TryGetTree(nameParser);
        }

        public StringLiteral TryParseString()
        {
            Func<ASTNode> stringParser = () =>
            {
                var token = Tokens.ConsumeToken(TokenType.String);
                if (token == null)
                    return null;

                return new StringLiteral(token.Value, token.StartPosition, token.EndPosition);
            };
            return (StringLiteral)Tokens.TryGetTree(stringParser);
        }

        #endregion
    }
}
