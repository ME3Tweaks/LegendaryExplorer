using ME3Script.Analysis.Symbols;
using ME3Script.Compiling.Errors;
using ME3Script.Language.Tree;
using ME3Script.Language.Util;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
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

        public CodeBodyParser(TokenStream<String> tokens, SymbolTable symbols, ASTNode node, MessageLog log = null)
        {
            Log = log ?? new MessageLog();
            Symbols = symbols;
            Tokens = tokens;
            OuterClassScope = "TODO";
            Node = node;
            // TODO: refactor a better solution to this mess
            if (IsState)
                NodeVariables = (node as State);
            else if (IsFunction)
                NodeVariables = (node as Function);
            else if (IsOperator)
                NodeVariables = (node as OperatorDeclaration);
        }

        private ASTNode Error(String msg, SourcePosition start = null, SourcePosition end = null)
        {
            Log.LogError(msg, start, end);
            return null;
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
                {
                    Log.LogError("Expected '{'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                var statements = new List<Statement>();
                var current = TryParseInnerStatement();
                while (current != null)
                {
                    statements.Add(current);
                    current = TryParseInnerStatement();

                    if (!SemiColonExceptions.Contains(current.Type) && Tokens.ConsumeToken(TokenType.SemiColon) == null)
                    {
                        Log.LogError("Expected semi-colon after statement!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                }

                if (requireBrackets && Tokens.ConsumeToken(TokenType.RightBracket) == null)
                {
                    Log.LogError("Expected '}'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                return new CodeBody(statements, statements.First().StartPos, statements.Last().EndPos);
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
                    {
                        Log.LogError("Expected a code body or single statement!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                }

                return body;
            };
            return (CodeBody)Tokens.TryGetTree(bodyParser);
        }

        public Statement TryParseInnerStatement()
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

                if (statement == null)
                {
                    Log.LogError("Expected a valid statement!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                return null;
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
                {
                    Log.LogError("Assignments require a variable target (LValue expected).", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                var value = TryParseExpression();
                if (value == null)
                {
                    Log.LogError("Assignments require a resolvable expression as value! (RValue expected).", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

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
                {
                    Log.LogError("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                var condition = TryParseExpression();
                if (condition == null)
                {
                    Log.LogError("Expected an expression as the if-condition!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                {
                    Log.LogError("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                CodeBody thenBody = TryParseBodyOrStatement();
                if (thenBody == null)
                    return null;

                CodeBody elseBody = null;
                var elsetoken = Tokens.ConsumeToken(TokenType.Else);
                if (elsetoken != null)
                {
                    elseBody = TryParseBodyOrStatement();
                    if (elseBody == null)
                        return null;
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
                {
                    Log.LogError("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                var condition = TryParseExpression();
                if (condition == null)
                {
                    Log.LogError("Expected an expression as the while condition!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                {
                    Log.LogError("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

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
                {
                    Log.LogError("Expected 'until'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                if (Tokens.ConsumeToken(TokenType.LeftParenth) == null)
                {
                    Log.LogError("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                var condition = TryParseExpression();
                if (condition == null)
                {
                    Log.LogError("Expected an expression as the until condition!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                {
                    Log.LogError("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

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
                {
                    Log.LogError("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                var initStatement = TryParseInnerStatement();
                // TODO: can also be function call, modify comment.
                if (initStatement.Type != ASTNodeType.AssignStatement) //&& initStatement.Type != ASTNodeType.Function)
                {
                    Log.LogError("Init statement in a for-loop must be an assignment or a function call!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                {
                    Log.LogError("Expected semi-colon after init statement!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                var condition = TryParseExpression();
                if (condition == null)
                {
                    Log.LogError("Expected an expression as the while condition!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                {
                    Log.LogError("Expected semi-colon after condition!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                var updateStatement = TryParseInnerStatement();
                // TODO: can also be function call, modify comment.
                if (updateStatement.Type != ASTNodeType.AssignStatement) //&& initStatement.Type != ASTNodeType.Function)
                {
                    Log.LogError("Init statement in a for-loop must be an assignment or a function call!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                {
                    Log.LogError("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

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
                // expr' = TryParseExpressionLeaf() operator TryParseExpression | TryParseExpressionLeaf()
                // ( TryParseExpression ) operator TryParseExpression | ( TryParseExpression ) | expr'
                // 
                return null;
            };
            return (Expression)Tokens.TryGetTree(exprParser);
        }

        public Expression TryParsePreOperator()
        {
            Func<ASTNode> preopParser = () =>
            {
                return null;
            };
            return (Expression)Tokens.TryGetTree(preopParser);
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

                List<Expression> parameters = new List<Expression>();
                var param = TryParseExpression();
                while (param != null)
                {
                    parameters.Add(param);
                    param = TryParseExpression();
                }

                if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                {
                    Log.LogError("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                return new FunctionCall(funcRef, parameters, funcRef.StartPos, CurrentPosition);
            };
            return (FunctionCall)Tokens.TryGetTree(callParser);
        }

        public SymbolReference TryParseReference()
        {
            Func<ASTNode> refParser = () =>
            {
                return TryParseCompositeRef() ?? TryParseArrayRef() ?? TryParseBasicRef() ?? (SymbolReference)null;
            };
            return (SymbolReference)Tokens.TryGetTree(refParser);
        }

        public SymbolReference TryParseBasicRef()
        {
            Func<ASTNode> refParser = () =>
            {
                var token = Tokens.ConsumeToken(TokenType.Word);
                if (token == null)
                    return null;

                return new SymbolReference(token.Value, token.StartPosition, token.EndPosition);
            };
            return (SymbolReference)Tokens.TryGetTree(refParser);
        }

        public ArraySymbolRef TryParseArrayRef()
        {
            Func<ASTNode> refParser = () =>
            {
                var token = Tokens.ConsumeToken(TokenType.Word);
                if (token == null)
                    return null;

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

                return new ArraySymbolRef(token.Value, index, token.StartPosition, CurrentPosition);
            };
            return (ArraySymbolRef)Tokens.TryGetTree(refParser);
        }

        public CompositeSymbolRef TryParseCompositeRef()
        {
            Func<ASTNode> refParser = () =>
            {
                SymbolReference outer = TryParseArrayRef() ?? TryParseBasicRef() ?? (SymbolReference)null;
                if (outer == null)
                    return null;

                if (Tokens.ConsumeToken(TokenType.Dot) == null)
                    return null;

                SymbolReference inner = TryParseCompositeRef() ?? TryParseArrayRef() ?? TryParseBasicRef() ?? (SymbolReference)null;
                if (inner == null)
                {
                    Log.LogError("Expected a valid member name to follow the dot!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

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

                return new IntegerLiteral(Int32.Parse(token.Value), token.StartPosition, token.EndPosition);
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

                return new FloatLiteral(Single.Parse(token.Value), token.StartPosition, token.EndPosition);
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
