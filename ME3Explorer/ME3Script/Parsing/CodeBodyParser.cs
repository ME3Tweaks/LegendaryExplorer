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
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3Script.Analysis.Visitors;
using ME3Script.Lexing;
using static ME3Script.Utilities.Keywords;

namespace ME3Script.Parsing
{
    public class CodeBodyParser : StringParserBase
    {
        private const int NOPRECEDENCE = int.MaxValue;
        private readonly SymbolTable Symbols;
        private readonly string OuterClassScope;
        private readonly ASTNode Node;
        private readonly CodeBody Body;
        private readonly Class Self;

        private readonly CaseInsensitiveDictionary<Label> Labels = new CaseInsensitiveDictionary<Label>();

        private readonly Stack<string> ExpressionScopes;

        private bool IsFunction => Node.Type == ASTNodeType.Function;
        private bool IsState => Node.Type == ASTNodeType.State;

        private int _loopCount;
        private bool InLoop => _loopCount > 0;
        private readonly Stack<VariableType> SwitchTypes;
        private bool InForEachIterator;
        private bool InForEachBody;
        private readonly Stack<List<Label>> LabelNests;
        private bool InSwitch => SwitchTypes.Count > 0;
        private bool InNew;

        //these have to be checked against labels after the whole body is parsed
        private readonly List<Statement> gotoStatements = new List<Statement>();

        public static void ParseFunction(Function func, string source, SymbolTable symbols, MessageLog log = null)
        {
            symbols.PushScope(func.Name);

            var tokenStream = new TokenStream<string>(new StringLexer(source, log), func.Body.StartPos, func.Body.EndPos);
            var bodyParser = new CodeBodyParser(tokenStream, func.Body, symbols, func, log);

            var body = bodyParser.ParseBody();

            //remove redundant return;
            if (body.Statements.Count > 0 && body.Statements.Last() is ReturnStatement r && r.Value is null)
            {
                body.Statements.RemoveAt(body.Statements.Count - 1);
            }

            //parse default parameter values
            if (func.Flags.Has(FunctionFlags.HasOptionalParms))
            {
                foreach (FunctionParameter param in func.Parameters.Where(p => p.IsOptional))
                {
                    var unparsedBody = param.UnparsedDefaultParam;
                    if (unparsedBody is null)
                    {
                        continue;
                    }

                    var paramTokenStream = new TokenStream<string>(new StringLexer(source, log), unparsedBody.StartPos, unparsedBody.EndPos);
                    var paramParser = new CodeBodyParser(paramTokenStream, unparsedBody, symbols, func, log);
                    var parsed = paramParser.ParseExpression();
                    if (parsed is null)
                    {
                        throw paramParser.Error("Could not parse default parameter value!", unparsedBody.StartPos, unparsedBody.EndPos);
                    }

                    VariableType valueType = parsed.ResolveType();
                    if (!NodeUtils.TypeCompatible(param.VarType, valueType))
                    {
                        throw paramParser.Error($"Could not assign value of type '{valueType}' to variable of type '{param.VarType}'!", unparsedBody.StartPos, unparsedBody.EndPos);
                    }
                    //AddConversion(param.VarType, ref parsed); Done in compiler
                    param.DefaultParameter = parsed;
                }
            }

            func.Body = body;

            symbols.PopScope();
        }

        public static void ParseState(State state, string source, SymbolTable symbols, MessageLog log = null)
        {
            symbols.PushScope(state.Name);

            var tokenStream = new TokenStream<string>(new StringLexer(source, log), state.Body.StartPos, state.Body.EndPos);
            var bodyParser = new CodeBodyParser(tokenStream, state.Body, symbols, state, log);

            var body = bodyParser.ParseBody();

            //remove redundant stop;
            if (body.Statements.Count > 0 && body.Statements.Last() is StopStatement stop)
            {
                body.Statements.RemoveAt(body.Statements.Count - 1);
            }

            state.Labels = bodyParser.LabelNests.Pop();

            state.Body = body;

            symbols.PopScope();
        }

        public CodeBodyParser(TokenStream<string> tokens, CodeBody body, SymbolTable symbols, ASTNode containingNode, MessageLog log = null)
        {
            Log = log ?? new MessageLog();
            Symbols = symbols;
            Tokens = tokens;
            _loopCount = 0;
            SwitchTypes = new Stack<VariableType>();
            Node = containingNode;
            Body = body;
            Self = NodeUtils.GetContainingClass(body);
            OuterClassScope = NodeUtils.GetOuterClassScope(containingNode);


            ExpressionScopes = new Stack<string>();
            ExpressionScopes.Push(Symbols.CurrentScopeName);

            LabelNests = new Stack<List<Label>>();
            LabelNests.Push(new List<Label>());
        }

        public CodeBody ParseBody()
        {
            if (Body.StartPos.Equals(Body.EndPos))
            {
                Body.Statements = new List<Statement>();
                return Body;
            }
            do
            {
                if (Tokens.CurrentItem.StartPos.Equals(Body.StartPos))
                    break;
                Tokens.Advance();
            } while (!Tokens.AtEnd());

            if (Tokens.AtEnd())
                throw Error("Could not find the code body for the current node, please contact the maintainers of this compiler!");

            var body = TryParseBody(false, IsFunction);
            if (body == null)
                return null;
            Body.Statements = body.Statements;

            if (Tokens.CurrentItem.Type != TokenType.EOF && !Tokens.CurrentItem.StartPos.Equals(Body.EndPos))
                throw Error("Could not parse a valid statement, even though the current code body has supposedly not ended yet.", CurrentPosition);
            var labels = LabelNests.Peek();
            foreach (Statement stmnt in gotoStatements)
            {
                switch (stmnt)
                {
                    case Goto g when g.ContainingForEach is null:
                    {
                        if (labels.FirstOrDefault(l => l.Name.CaseInsensitiveEquals(g.LabelName)) is Label label)
                        {
                            g.Label = label;
                        }
                        else
                        {
                            throw Error($"Could not find label '{g.LabelName}'! (gotos cannot jump out of or into a foreach)");
                        }

                        break;
                    }
                    case StateGoto sg when sg.LabelExpression is NameLiteral nameLiteral && labels.FirstOrDefault(l => l.Name.CaseInsensitiveEquals(nameLiteral.Value)) is null:
                        throw Error($"Could not find label '{nameLiteral.Value}'! (gotos cannot jump out of or into a foreach)");
                }
            }
            return Body;
        }

        public CodeBody TryParseBody(bool requireBrackets = true, bool functionBody = false)
        {
            return (CodeBody)Tokens.TryGetTree(CodeParser);
            ASTNode CodeParser()
            {
                if (requireBrackets && Consume(TokenType.LeftBracket) == null) throw Error("Expected '{'!", CurrentPosition);

                bool pastVarDecls = false;
                var statements = new List<Statement>();
                var startPos = CurrentPosition;
                var current = ParseDeclarationOrStatement();
                while (current != null)
                {
                    if (!SemiColonExceptions.Contains(current.Type) && Consume(TokenType.SemiColon) == null) throw Error("Expected semi-colon after statement!", CurrentPosition);
                    if (!(current is VariableDeclaration))
                    {
                        pastVarDecls = true;

                        if (current is Label label)
                        {
                            if (Labels.ContainsKey(label.Name))
                            {
                                throw Error($"Label '{label.Name}' already exists on line {Labels[label.Name].StartPos.Line}!", label.StartPos, label.EndPos);
                            }
                            Labels.Add(label.Name, label);
                            LabelNests.Peek().Add(label);
                        }
                        statements.Add(current);
                    }
                    else if (pastVarDecls)
                    {
                        throw Error("Variable declarations must come before all other statements", CurrentPosition);
                    }
                    else if (!functionBody)
                    {
                        throw Error("Can only declare variables at the top of a function!", CurrentPosition);
                    }

                    if (CurrentToken.Type == TokenType.EOF)
                    {
                        break;
                    }
                    current = ParseDeclarationOrStatement();
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
                var single = ParseStatement();
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

        public Statement ParseDeclarationOrStatement(bool throwError = false)
        {
            if (CurrentIs(LOCAL))
            {
                return ParseLocalVarDecl();
            }
            return ParseStatement(throwError);
        }

        public Statement ParseStatement(bool throwError = false)
        {
            if (CurrentIs(LOCAL))
            {
                throw Error("Can only declare variables at the top of a function!", CurrentPosition);
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
                return ParseForEach();
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

            if (CurrentIs(GOTO))
            {
                return ParseGoto();
            }
            if (CurrentIs(STOP))
            {
                return ParseStop();
            }
            if (CurrentIs(CASE))
            {
                return ParseCase();
            }
            //default can also be used to refer to default properties, so colon check is neccesary
            if (CurrentIs(DEFAULT) && Tokens.LookAhead(1).Type == TokenType.Colon)
            {
                return ParseDefault();
            }

            if (CurrentIs(ASSERT))
            {
                return ParseAssert();
            }

            if (CurrentIs(TokenType.Word) && Tokens.LookAhead(1).Type == TokenType.Colon)
            {
                Token<string> labelToken = Consume(TokenType.Word);
                return new Label(labelToken.Value, 0, labelToken.StartPos, Consume(TokenType.Colon).EndPos);
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

                VariableType exprType = expr.ResolveType();
                if (!NodeUtils.TypeCompatible(exprType, value.ResolveType()))
                {
                    throw Error($"Cannot assign a value of type '{value.ResolveType()?.Name ?? "None"}' to a variable of type '{exprType?.Name}'.", assign.StartPos, assign.EndPos);
                }
                //AddConversion(exprType, ref value); Done in Compiler

                return new AssignStatement(expr, value, assign.StartPos, assign.EndPos);
            }

            if (expr is FunctionCall || expr is DelegateCall || expr is CompositeSymbolRef csr && (csr.InnerSymbol is FunctionCall || csr.InnerSymbol is DelegateCall) 
             || expr is PostOpReference || expr is PreOpReference preOp && preOp.Operator.HasOutParams || expr is InOpReference inOp && inOp.Operator.HasOutParams 
             || expr is DynArrayOperation && !(expr is DynArrayLength))
            {
                return new ExpressionOnlyStatement(expr, expr.StartPos, expr.EndPos);
            }

            throw Error("Expression-only statements must have an effect!", expr.StartPos, expr.EndPos);
        }

        public VariableDeclaration ParseLocalVarDecl()
        {
            var startPos = CurrentPosition;
            if (!Matches(LOCAL)) return null;

            VariableType type = TryParseType();
            if (type == null) throw Error("Expected variable type!", CurrentPosition);
            type.Outer = Body;
            if (!Symbols.TryResolveType(ref type)) throw Error($"The type '{type.Name}' does not exist in the current scope!", type.StartPos, type.EndPos);

            var var = ParseVariableName();
            if (var == null) throw Error("Malformed variable name!", CurrentPosition);


            if (Symbols.SymbolExistsInCurrentScope(var.Name)) throw Error($"A variable named '{var.Name}' already exists in this scope!", var.StartPos, var.EndPos);

            VariableDeclaration varDecl = new VariableDeclaration(type, UnrealFlags.EPropertyFlags.None, var.Name, var.Size, null, startPos, var.EndPos);
            Symbols.AddSymbol(varDecl.Name, varDecl);
            if (Node is Function func)
            {
                func.Locals.Add(varDecl);
            }
            else
            {
                throw Error("States cannot declare variables!", startPos, CurrentPosition);
            }
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

            VariableType conditionType = condition.ResolveType();
            if (conditionType != SymbolTable.BoolType)
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

            return new IfStatement(condition, thenBody, elseBody, token.StartPos, token.EndPos);
        }

        public ReturnStatement ParseReturn()
        {
            var token = Consume(RETURN);
            if (token == null) return null;

            if (!IsFunction) throw Error("Return statements can only exist in functions!", CurrentPosition);

            if (CurrentTokenType == TokenType.SemiColon) return new ReturnStatement(null, token.StartPos, token.EndPos);

            var value = ParseExpression();
            if (value == null) throw Error("Expected a return value or a semi-colon!", CurrentPosition);

            var type = value.ResolveType();
            if (IsFunction)
            {
                var func = (Function)Node;
                if (func.ReturnType == null) throw Error("Function should not return a value!", token.StartPos, token.EndPos);

                if (!NodeUtils.TypeCompatible(func.ReturnType, type)) throw Error($"Cannot return a value of type '{type.Name}', function should return '{func.ReturnType.Name}'.", token.StartPos, token.EndPos);
                //AddConversion(func.ReturnType, ref value); Done in Compiler
            }

            return new ReturnStatement(value, token.StartPos, token.EndPos);
        }

        public SwitchStatement ParseSwitch()
        {
            var token = Consume(SWITCH);
            if (token == null) return null;

            if (Consume(TokenType.LeftParenth) == null) throw Error("Expected '('!", CurrentPosition);

            var expression = ParseExpression();
            if (expression == null) throw Error("Expected an expression as the switch value!", CurrentPosition);

            if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition);

            SwitchTypes.Push(expression.ResolveType());
            CodeBody body = TryParseBodyOrStatement();
            if (body.Statements.Any())
            {
                Statement firstStatement = body.Statements[0];
                if (!(firstStatement is CaseStatement) && !(firstStatement is DefaultCaseStatement))
                {
                    throw Error($"First statement in a Switch body must be a '{CASE}' or '{DEFAULT}' statement!", firstStatement.StartPos, firstStatement.EndPos);
                }
            }
            else
            {
                throw Error("Switch statement must have a body!", body.StartPos, body.EndPos);
            }

            SwitchTypes.Pop();
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
            if (condition.ResolveType() != SymbolTable.BoolType)
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

            var initStatement = ParseStatement();
            if (initStatement != null && initStatement.Type != ASTNodeType.AssignStatement && initStatement.Type != ASTNodeType.FunctionCall)
            {
                throw Error("Init statement in a for-loop must be an assignment or a function call!", CurrentPosition);
            }

            if (Consume(TokenType.SemiColon) == null) throw Error("Expected semi-colon after init statement!", CurrentPosition);

            var condition = ParseExpression();
            if (condition == null) throw Error("Expected an expression as the for condition!", CurrentPosition);
            if (condition.ResolveType() != SymbolTable.BoolType)
                throw Error("Expected a boolean result from the condition!", condition.StartPos, condition.EndPos);

            if (Consume(TokenType.SemiColon) == null) throw Error("Expected semi-colon after condition!", CurrentPosition);

            var updateStatement = ParseStatement();
            //if (updateStatement is null) //this should technically be limited to assignment, increment, decrement, or function call. Don't think it really matters though
            //{
            //    throw Error("Expected an update statement!", CurrentPosition);
            //}

            if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition);

            _loopCount++;
            CodeBody body = TryParseBodyOrStatement(allowEmpty: true);
            _loopCount--;
            if (body == null) return null;

            return new ForLoop(initStatement, condition, updateStatement, body, token.StartPos, token.EndPos);
        }

        public ForEachLoop ParseForEach()
        {
            var token = Consume(FOREACH);
            if (token == null) return null;

            InForEachIterator = true;
            Expression iterator = CompositeRef();
            InForEachIterator = false;

            FunctionCall fc = iterator switch
            {
                FunctionCall call => call,
                CompositeSymbolRef csf when csf.InnerSymbol is FunctionCall cfc => cfc,
                _ => null
            };
            if (fc != null)
            {
                if (!(fc.Function.Node is Function func) || !func.Flags.Has(FunctionFlags.Iterator))
                {
                    throw Error($"Expected an iterator function call or dynamic array iterator after '{FOREACH}'!", iterator.StartPos, iterator.EndPos);
                }
                if (func.Parameters.Count >= 2)
                {
                    var limiter = ((ClassType)fc.Arguments[0].ResolveType()).ClassLimiter;
                    Class objClass = (Class)fc.Arguments[1].ResolveType();
                    if (!objClass.SameAsOrSubClassOf(limiter.Name))
                    {
                        throw Error("Second argument to iterator function must be the same class or a subclass of the class passed as the first argument!", fc.Arguments[1].StartPos, fc.Arguments[1].EndPos);
                    }
                }
            }
            else if (!(iterator is DynArrayIterator) && !(iterator is CompositeSymbolRef c && c.InnerSymbol is DynArrayIterator))
            {
                throw Error($"Expected an iterator function call or dynamic array iterator after '{FOREACH}'!", iterator.StartPos, iterator.EndPos);
            }

            int gotoStatementsIdx = gotoStatements.Count;
            bool alreadyInForEach = InForEachBody;
            InForEachBody = true;
            _loopCount++;
            LabelNests.Push(new List<Label>());
            CodeBody body = TryParseBodyOrStatement(allowEmpty: true);

            _loopCount--;
            InForEachBody = alreadyInForEach;
            if (body == null) return null;

            ForEachLoop forEach = new ForEachLoop(iterator, body, token.StartPos, token.EndPos);

            var labels = LabelNests.Pop();
            if (gotoStatementsIdx < gotoStatements.Count)
            {
                for (; gotoStatementsIdx < gotoStatements.Count; gotoStatementsIdx++)
                {
                    var stmnt = gotoStatements[gotoStatementsIdx];
                    if (stmnt is Goto g && g.ContainingForEach is null)
                    {
                        g.ContainingForEach = forEach;
                        if (labels.FirstOrDefault(l => l.Name.CaseInsensitiveEquals(g.LabelName)) is Label label)
                        {
                            g.Label = label;
                        }
                        else
                        {
                            throw Error($"Could not find label '{g.LabelName}'! (gotos cannot jump out of or into a foreach)");
                        }
                    }
                }
            }

            return forEach;
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
            if (condition.ResolveType() != SymbolTable.BoolType)
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

        public Statement ParseGoto()
        {
            var gotoToken = Consume(GOTO);
            if (gotoToken == null) return null;

            if (IsState && !InForEachBody)
            {
                if (CurrentIs(TokenType.Word) && Tokens.LookAhead(1).Type == TokenType.SemiColon)
                {
                    throw Error("gotos in State bodies expect an expression that evaluates to a name, not a bare label. Put single quotes around a label name to make it a name literal");
                }
                var labelExpr = ParseExpression();
                if (labelExpr == null)
                {
                    throw Error($"Expected a name expression after '{GOTO}'!");
                }

                var stateGoto = new StateGoto(labelExpr, gotoToken.StartPos, labelExpr.EndPos);
                gotoStatements.Add(stateGoto);
                return stateGoto;
            }

            var labelToken = Consume(TokenType.Word) ?? throw Error($"Expected a label after '{GOTO}'!");
            var gotoStatement = new Goto(labelToken.Value, gotoToken.StartPos, labelToken.EndPos);
            gotoStatements.Add(gotoStatement);
            return gotoStatement;
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
            var switchType = SwitchTypes.Peek();
            if (switchType == SymbolTable.IntType && value is IntegerLiteral caseLit)
            {
                caseLit.NumType = INT;
            }
            if (!NodeUtils.TypeEqual(switchType, value.ResolveType()))
            {
                throw Error("Case expression must evaluate to the same type as the switch expression!", value.StartPos, value.EndPos);
            }
            //AddConversion(switchType, ref value); Done in Compiler
            if (Consume(TokenType.Colon) == null) throw Error("Expected colon after case expression!", CurrentPosition);

            return new CaseStatement(value, token.StartPos, token.EndPos);
        }

        public DefaultCaseStatement ParseDefault()
        {
            var token = Consume(DEFAULT);
            if (token == null) return null;

            if (!InSwitch) throw Error("Default statements can only exist inside switch blocks!", CurrentPosition);

            if (Consume(TokenType.Colon) == null) throw Error("Expected colon after default statement!", CurrentPosition);

            return new DefaultCaseStatement(token.StartPos, token.EndPos);
        }

        public AssertStatement ParseAssert()
        {
            var token = Consume(ASSERT);
            if (!Matches(TokenType.LeftParenth))
            {
                throw Error($"Expected '(' after {ASSERT}!", CurrentPosition);
            }

            var expr = ParseExpression() ?? throw Error($"Expected an expression in {ASSERT} statement!", CurrentPosition);
            VariableType conditionType = expr.ResolveType();
            if (conditionType != SymbolTable.BoolType)
            {
                throw Error($"Expected a boolean result from the {ASSERT} expression!", expr.StartPos, expr.EndPos);
            }

            if (!Matches(TokenType.RightParenth))
            {
                throw Error($"Expected ')' after expression in {ASSERT} statement!", CurrentPosition);
            }
            return new AssertStatement(expr, token.StartPos, PrevToken.EndPos);
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
                if (!NodeUtils.TypeCompatible(SymbolTable.BoolType, expr.ResolveType()))
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
                VariableType trueType = trueExpr.ResolveType();
                VariableType falseType = falseExpr.ResolveType();
                if (trueType == SymbolTable.IntType && falseExpr is IntegerLiteral falseLit)
                {
                    falseLit.NumType = INT;
                    falseType = falseLit.ResolveType();
                }
                else if (falseType == SymbolTable.IntType && trueExpr is IntegerLiteral trueLit)
                {
                    trueLit.NumType = INT;
                    trueType = trueLit.ResolveType();
                }

                if (NodeUtils.TypeEqual(trueType, falseType))
                {
                    expr = new ConditionalExpression(expr, trueExpr, falseExpr, expr.StartPos, falseExpr.EndPos);
                }
                else if (trueType is Class trueClass && falseType is Class falseClass)
                {
                    expr = new ConditionalExpression(expr, trueExpr, falseExpr, expr.StartPos, falseExpr.EndPos)
                    {
                        ExpressionType = NodeUtils.GetCommonBaseClass(trueClass, falseClass)
                    };
                }
                else
                {
                    throw Error("True and false results in conditional expression must match types!");
                }
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
                    if (isComparison && (lType is DelegateType && rType is DelegateType 
                                      || lType is DelegateType && rType is null 
                                      || rType is DelegateType && lType is null))
                    {
                        if (lhs is NoneLiteral noneLit)
                        {
                            noneLit.IsDelegateNone = true;
                        }
                        if (rhs is NoneLiteral noneLit2)
                        {
                            noneLit2.IsDelegateNone = true;
                        }
                        expr = new DelegateComparison(isEqualEqual, lhs, rhs, lhs.StartPos, rhs.EndPos);
                    }
                    else if (isComparison && lType is Struct typeStruct && rType.PropertyType == EPropertyType.Struct)
                    {
                        if (lType == rType)
                        {
                            expr = new StructComparison(isEqualEqual, lhs, rhs, lhs.StartPos, rhs.EndPos)
                            {
                                Struct = typeStruct
                            };
                        }
                        else
                        {
                            throw Error("Cannot compare structs of different types!", opToken.StartPos);
                        }
                    }
                    else if (isComparison && lType is DelegateType && rType == SymbolTable.BoolType)
                    {
                        //seems wrong but this exists in SFXPawn_Player::ImpactWithPower
                        expr = new DelegateComparison(isEqualEqual, lhs, rhs, lhs.StartPos, rhs.EndPos);
                        //TODO: log a warning here, as this is almost certainly NOT intended 
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
                    /* Done in Compiler
                    if (bestMatch.LeftOperand.VarType is Class c && c.IsInterface)
                    {
                        VariableType varType = lhs.ResolveType() ?? rhs.ResolveType() ?? c;
                        AddConversion(varType, ref lhs);
                        AddConversion(varType, ref rhs);
                    }
                    else
                    {
                        AddConversion(bestMatch.LeftOperand.VarType, ref lhs);
                        AddConversion(bestMatch.RightOperand.VarType, ref rhs);
                    }
                    */
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
                return SymbolTable.InFixOperatorSymbols.Contains(CurrentToken.Value, StringComparer.OrdinalIgnoreCase);
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
                expr = Unary();
                VariableType exprType = expr.ResolveType();
                if (exprType == SymbolTable.ByteType)
                {
                    exprType = SymbolTable.IntType;
                }
                if (exprType == SymbolTable.FloatType || exprType == SymbolTable.IntType || exprType.Name.CaseInsensitiveEquals("Vector"))
                {
                    switch (expr)
                    {
                        case IntegerLiteral intLit:
                            intLit.Value *= -1;
                            intLit.NumType = INT;
                            return intLit;
                        case FloatLiteral floatLit:
                            floatLit.Value *= -1;
                            return floatLit;
                        default:
                            return new PreOpReference(Symbols.GetPreOp("-", exprType), expr, start, expr.EndPos);
                    }
                }

                throw Error("Unary '-' can only be used with expressions that evaluate to float, int, or Vector!");
            }
            if (Matches(TokenType.Complement))
            {
                expr = Unary();
                if (expr is IntegerLiteral intLit)
                {
                    intLit.NumType = INT;
                }
                VariableType exprType = expr.ResolveType();
                if (exprType == SymbolTable.IntType)
                {
                    PreOpDeclaration opDeclaration = Symbols.GetPreOp("~", exprType);
                    return new PreOpReference(opDeclaration, expr, start, expr.EndPos);
                }

                throw Error("'~' can only be used with expressions that evaluate to int!");
            }

            expr = CompositeRef();

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

        //static void AddConversion(VariableType destType, ref Expression expr)
        //{

        //    if (expr is NoneLiteral noneLit)
        //    {
        //        if (destType.PropertyType == EPropertyType.Delegate)
        //        {
        //            noneLit.IsDelegateNone = true;
        //        }
        //        else if (destType.PropertyType == EPropertyType.Interface)
        //        {
        //            expr = new PrimitiveCast(ECast.ObjectToInterface, destType, noneLit, noneLit.StartPos, noneLit.EndPos);
        //            return;
        //        }
        //    }
        //    if (expr?.ResolveType() is { } type && type.PropertyType != destType.PropertyType)
        //    {
        //        ECast cast = CastHelper.PureCastType(CastHelper.GetConversion(destType, type));
        //        switch (expr)
        //        {
        //            case IntegerLiteral intLit:
        //                switch (cast)
        //                {
        //                    case ECast.ByteToInt:
        //                        intLit.NumType = INT;
        //                        return;
        //                    case ECast.IntToByte:
        //                        intLit.NumType = BYTE;
        //                        return;
        //                    case ECast.IntToFloat:
        //                        expr = new FloatLiteral(intLit.Value, intLit.StartPos, intLit.EndPos);
        //                        return;
        //                    case ECast.ByteToFloat:
        //                        expr = new FloatLiteral(intLit.Value, intLit.StartPos, intLit.EndPos);
        //                        return;
        //                }
        //                break;
        //            case FloatLiteral floatLit:
        //                switch (cast)
        //                {
        //                    case ECast.FloatToByte:
        //                        expr = new IntegerLiteral((int)floatLit.Value, floatLit.StartPos, floatLit.EndPos) { NumType = BYTE };
        //                        return;
        //                    case ECast.FloatToInt:
        //                        expr = new IntegerLiteral((int)floatLit.Value, floatLit.StartPos, floatLit.EndPos) { NumType = INT };
        //                        return;
        //                }
        //                break;
        //            case ConditionalExpression condExpr:
        //                AddConversion(destType, ref condExpr.TrueExpression);
        //                AddConversion(destType, ref condExpr.FalseExpression);
        //                return;
        //        }
        //        if ((byte)cast != 0 && cast != ECast.Max)
        //        {
        //            expr = new PrimitiveCast(cast, destType, expr, expr.StartPos, expr.EndPos);
        //        }
        //    }
        //}

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
                Expression lhs = CallOrAccess();
                if (lhs is null)
                {
                    return null;
                }

                while (Matches(TokenType.Dot))
                {

                    var lhsType = lhs.ResolveType();
                    if (lhsType is DynamicArrayType dynArrType)
                    {
                        //all the dynamic array properties and functions are built-ins
                        lhs = ParseDynamicArrayOperation(lhs, dynArrType.ElementType);
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

                    if (!(lhsType is ClassType) && !isStatic && !CompositeTypes.Contains(lhsType?.NodeType ?? ASTNodeType.INVALID))
                    {
                        throw Error("Left side symbol is not of a composite type!", PrevToken.StartPos); //TODO: write a better error message
                    }
                    Class containingClass = NodeUtils.GetContainingClass(lhsType);
                    if (containingClass == null)
                    {
                        throw Error("Could not resolve type of expression!", lhs.StartPos, lhs.EndPos);
                    }
                    string specificScope = containingClass.GetInheritanceString();
                    if (lhsType is Struct lhsStruct)
                    {
                        var outerStructs = new Stack<string>();
                        while (lhsStruct.Outer is Struct lhsStructOuter)
                        {
                            outerStructs.Push(lhsStructOuter.Name);
                            lhsStruct = lhsStructOuter;
                        }

                        if (outerStructs.Any())
                        {
                            specificScope += $".{string.Join(".", outerStructs)}";
                        }
                    }
                    if (!(lhsType is ClassType) && lhsType != containingClass)
                    {
                        specificScope += $".{lhsType.Name}";
                    }

                    if (lhsType is ClassType && !isStatic && !CurrentIs(DEFAULT))
                    {
                        specificScope = "Object.Field.Struct.State.Class";
                    }

                    ExpressionScopes.Push(specificScope);

                    Expression rhs = CallOrAccess(isStatic);

                    ExpressionScopes.Pop();
                    if (rhs is null)
                    {
                        throw Error("Expected a valid member name to follow the dot!", CurrentPosition);
                    }
                    //TODO: check if rhs is a type that makes sense eg. no int literals

                    if (isConst)
                    {
                        if (!(rhs is SymbolReference symRef) || !(symRef.Node is Const))
                        {
                            throw Error("Expected property after 'const.' to be a Const!", rhs.StartPos, rhs.EndPos);
                        }
                    }

                    if (lhsType.PropertyType == EPropertyType.Interface && rhs is FunctionCall fc)
                    {
                        fc.IsCalledOnInterface = true;
                    }

                    bool isClassContext = isStatic || rhs is DefaultReference;

                    switch (rhs)
                    {
                        case ArraySymbolRef asr:
                        {
                            var csf = new CompositeSymbolRef(lhs, asr.Array, isClassContext, lhs.StartPos, asr.Array.EndPos)
                            {
                                IsStructMemberExpression = lhsType is Struct
                            };
                            asr.StartPos = csf.StartPos;
                            asr.Array = csf;
                            lhs = asr;
                            break;
                        }
                        case DynArrayIterator dai:
                        {
                            var csf = new CompositeSymbolRef(lhs, dai.DynArrayExpression, isClassContext, lhs.StartPos, dai.DynArrayExpression.EndPos)
                            {
                                IsStructMemberExpression = lhsType is Struct
                            };
                            dai.StartPos = csf.StartPos;
                            dai.DynArrayExpression = csf;
                            lhs = dai;
                            break;
                        }
                        case DynArrayLength dal:
                        {
                            var csf = new CompositeSymbolRef(lhs, dal.DynArrayExpression, isClassContext, lhs.StartPos, dal.DynArrayExpression.EndPos)
                            {
                                IsStructMemberExpression = lhsType is Struct
                            };
                            dal.StartPos = csf.StartPos;
                            dal.DynArrayExpression = csf;
                            lhs = dal;
                            break;
                        }
                        case SymbolReference sr when sr.Node is EnumValue ev:
                            lhs = NewSymbolReference(ev, new Token<string>(TokenType.Word, sr.Name, sr.StartPos, sr.EndPos), false);
                            break;
                        default:
                            lhs = new CompositeSymbolRef(lhs, rhs, isClassContext, lhs.StartPos, rhs.EndPos)
                            {
                                IsStructMemberExpression = lhsType is Struct
                            };
                            break;
                    }
                    
                }

                return lhs;
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
                            return new DynArrayFindStructMember(dynArrayRef, memberNameArg, valueArg, dynArrayRef.StartPos, PrevToken.EndPos)
                            {
                                MemberType = variableDeclaration.VarType
                            };
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
                    if (delFunc.ReturnType == SymbolTable.IntType && delFunc.Parameters.Count == 2 && NodeUtils.TypeCompatible(delFunc.Parameters[0].VarType, elementType)
                                                                                                   && NodeUtils.TypeCompatible(delFunc.Parameters[1].VarType, elementType))
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

                if (!NodeUtils.TypeCompatible(expectedType, arg.ResolveType()))
                {
                    if (!(expectedType is DelegateType)) //seems wrong, but required to parse bioware classes, so...
                    {
                        throw Error($"Expected '{argumentName}' argument to '{functionName}' to evaluate to '{expectedType.Name}'!");
                    }
                }
                //AddConversion(expectedType, ref arg); Done in Compiler
                return arg;
            }
        }

        public Expression CallOrAccess(bool isStatic = false)
        {
            Expression expr = MetaCast();
            if (expr is null)
            {
                return null;
            }

            if (isStatic)
            {
                if (!(expr is SymbolReference symRef) || !(symRef.Node is Function fun) || !fun.Flags.Has(FunctionFlags.Static))
                {
                    throw Error("'static.' can only be used for calling a function with the 'static' modifier!", expr.StartPos, expr.EndPos);
                }
            }
            while (true)
            {
                if (Matches(TokenType.LeftParenth))
                {
                    expr = FinishCall(expr, out bool shouldBreak);
                    if (shouldBreak) break;
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
            var exprType = expr.ResolveType();
            if (!(exprType is DynamicArrayType) && !(exprType is StaticArrayType))
            {
                throw Error("Can only use array access operator on an array!", CurrentPosition);
            }

            ExpressionScopes.Push(ExpressionScopes.Last());
            Expression arrIndex = ParseExpression();
            ExpressionScopes.Pop();
            if (arrIndex == null)
            {
                throw Error("Expected an array index!", CurrentPosition);
            }

            //basic sanity checking for literal indexes
            if (arrIndex is IntegerLiteral intLiteral)
            {
                intLiteral.NumType = INT;
                if (intLiteral.Value < 0)
                {
                    throw Error("Array index cannot be negative!");
                }
                if (exprType is StaticArrayType staticArrayType && intLiteral.Value >= staticArrayType.Length)
                {
                    throw Error("Array index cannot be outside bounds of static array size!");
                }
            }

            if (!NodeUtils.TypeCompatible(SymbolTable.IntType, arrIndex.ResolveType()))
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

        public Expression FinishCall(Expression expr, out bool succeeded)
        {
            succeeded = false;
            if (expr is SymbolReference funcRef)
            {
                Function func = null;
                bool isDelegateCall = false;
                switch (funcRef.Node)
                {
                    case Function fn:
                        func = fn;
                        break;
                    case VariableDeclaration varDecl when varDecl.VarType is DelegateType delType:
                        isDelegateCall = true;
                        func = delType.DefaultFunction;
                        break;
                }
                if (func != null)
                {
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

                        var paramStartPos = CurrentPosition;
                        Expression currentParam = ParseExpression();

                        if (currentParam == null || !NodeUtils.TypeCompatible(p.VarType, currentParam.ResolveType(), p.Flags.Has(UnrealFlags.EPropertyFlags.CoerceParm)))
                        {
                            throw Error($"Expected a parameter of type '{p.VarType.Name}'!", paramStartPos, currentParam?.EndPos);
                        }
                        //AddConversion(p.VarType, ref currentParam); Done in Compiler
                        if (p.IsOut)
                        {
                            if (!(currentParam is SymbolReference) && !(currentParam is ConditionalExpression condExpr && condExpr.TrueExpression is SymbolReference 
                                                                                                                       && condExpr.FalseExpression is SymbolReference))
                            {
                                throw Error("Argument given to an out parameter must be an lvalue!", currentParam.StartPos, currentParam.EndPos);
                            }
                            if (!NodeUtils.TypeEqual(p.VarType, currentParam.ResolveType()))
                            {
                                throw Error($"Expected a parameter of type '{p.VarType.Name}'!", currentParam.StartPos, currentParam.EndPos);
                            }
                        }

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

                    if (!Matches(TokenType.RightParenth)) throw Error("Expected ')'!", CurrentPosition);

                    if (isDelegateCall)
                    {
                        return new DelegateCall(funcRef, parameters, funcRef.StartPos, PrevToken.EndPos);
                    }
                    return new FunctionCall(funcRef, parameters, funcRef.StartPos, PrevToken.EndPos);
                }
            }

            if (InForEachIterator)
            {
                //dynamic array iterator
                if (expr.ResolveType() is DynamicArrayType dynArrType)
                {
                    ExpressionScopes.Push(ExpressionScopes.Last());

                    Expression valueArg = CompositeRef() ?? throw Error("Expected argument to dynamic array iterator!", CurrentPosition);
                    if (!NodeUtils.TypeEqual(valueArg.ResolveType(), dynArrType.ElementType))
                    {
                        //ugly hack
                        var builder = new CodeBuilderVisitor();
                        dynArrType.ElementType.AcceptVisitor(builder);
                        string elementType = builder.GetOutput();
                        throw Error($"Iterator variable for an '{ARRAY}<{elementType}>' must be of type '{elementType}'");
                    }
                    if (!(valueArg is SymbolReference))
                    {
                        throw Error("Iterator variable must be an lvalue!", valueArg.StartPos, valueArg.EndPos);
                    }

                    Expression indexArg = null;
                    if (!Matches(TokenType.RightParenth))
                    {
                        if (!Matches(TokenType.Comma))
                        {
                            throw Error("Expected either a ')' after the first argument, or a ',' before a second argument!", CurrentPosition);
                        }

                        if (!Matches(TokenType.RightParenth))
                        {
                            indexArg = CompositeRef() ?? throw Error("Expected argument to dynamic array iterator!", CurrentPosition);
                            if (indexArg.ResolveType() != SymbolTable.IntType)
                            {
                                throw Error("Index variable must be an int!", indexArg.StartPos, indexArg.EndPos);
                            }
                            if (!(indexArg is SymbolReference))
                            {
                                throw Error("Index variable must be an lvalue!", valueArg.StartPos, valueArg.EndPos);
                            }

                            if (!Matches(TokenType.RightParenth))
                            {
                                throw Error("Expected a ')' after second argument!", CurrentPosition);
                            }
                        }
                    }
                    ExpressionScopes.Pop();
                    return new DynArrayIterator(expr, (SymbolReference)valueArg, (SymbolReference)indexArg, expr.StartPos, PrevToken.EndPos);
                }

                throw Error($"Expected an iterator function or dynamic array after {FOREACH}!", expr.StartPos, expr.EndPos);
            }

            //bit hacky. dynamic cast when the typename is also a variable name in this scope
            if (NotInContext && expr.GetType() == typeof(SymbolReference) && Symbols.TryGetType(((SymbolReference)expr).Name, out VariableType destType))
            {
                return ParsePrimitiveOrDynamicCast(new Token<string>(TokenType.Word, destType.Name, expr.StartPos, expr.EndPos), destType);
            }

            if (InNew)
            {
                Tokens.Advance(-1);
                succeeded = true;
                return expr;
            }

            //if there is a collision between a function name and another symbol, this may be required to disambiguate
            if (expr is SymbolReference notFunc && Symbols.TryGetSymbolInScopeStack(notFunc.Name, out Function secondChanceFunc, ExpressionScopes.Peek()))
            {
                return FinishCall(NewSymbolReference(secondChanceFunc, new Token<string>(TokenType.Word, notFunc.Name, expr.StartPos, expr.EndPos), expr is DefaultReference), out succeeded);
            }

            throw Error("Can only call functions and delegates!", expr.StartPos, expr.EndPos);
        }

        public Expression MetaCast()
        {
            if (CurrentIs(CLASS) && Tokens.LookAhead(1).Type == TokenType.LeftArrow)
            {
                //metacast
                var castToken = Consume(CLASS);
                Consume(TokenType.LeftArrow);
                if (Consume(TokenType.Word) is { } limiter)
                {
                    if (!Matches(TokenType.RightArrow))
                    {
                        throw Error("Expected '>' after limiter class!", CurrentPosition);
                    }

                    if (Symbols.TryGetType(limiter.Value, out VariableType destType) && destType is Class limiterType)
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

            return Primary();
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
                return new SymbolReference(new VariableDeclaration(Self, default, "Self"), SELF, token.StartPos, token.EndPos);
            }

            if (Matches(NEW))
            {
                return ParseNew();
            }

            if (Matches(SUPER))
            {
                return ParseSuper();
            }

            if (Matches(GLOBAL))
            {
                if (!Matches(TokenType.Dot))
                {
                    throw Error($"Expected '.' after '{GLOBAL}'!", CurrentPosition);
                }

                bool isState = false;
                if (Node.Outer is State)
                {
                    isState = true;
                    ExpressionScopes.Push(Self.GetInheritanceString());
                }
                
                if (!Matches(TokenType.Dot) || !Matches(TokenType.Word))
                {
                    throw Error($"Expected function name after '{GLOBAL}'!", CurrentPosition);
                }

                var basicRef = ParseBasicRefOrCast(PrevToken) as SymbolReference;
                if (!(basicRef?.Node is Function))
                {
                    throw Error($"Expected function name after '{GLOBAL}'!", basicRef.StartPos, basicRef.EndPos);
                }

                basicRef.IsGlobal = true;

                if (isState)
                {
                    ExpressionScopes.Pop();
                }
                return basicRef;
            }

            bool isDefaultRef = false;
            if (Matches(DEFAULT))
            {
                if (!Matches(TokenType.Dot))
                {
                    throw Error($"Expected '.' after '{DEFAULT}'!", CurrentPosition);
                }

                token = CurrentToken;
                isDefaultRef = true;
            }

            //if (Matches("Outer"))
            //{
            //    if (NotInContext)
            //    {
            //        return NewSymbolReference(Self.OuterClass, token, isDefaultRef);
            //    }
            //    string[] scopeArr = ExpressionScopes.Peek().Split('.');
            //    if (scopeArr.Length > 0 && Symbols.TryGetType(scopeArr.Last(), out VariableType vT) && vT is Class scopeClass)
            //    {
            //        return NewSymbolReference(scopeClass.OuterClass, token, isDefaultRef);
            //    }
            //    Tokens.Advance(-1);
            //}


            if (Matches(TokenType.Word))
            {
                if (Consume(TokenType.NameLiteral) is { } objName)
                {
                    return ParseObjectLiteral(token, objName);
                }

                //if (string.Equals(token.Value, CLASS, StringComparison.OrdinalIgnoreCase))
                //{
                //    if (NotInContext)
                //    {
                //        return NewSymbolReference(new ClassType(Self, Self.StartPos, Self.EndPos), token, isDefaultRef);
                //    }
                //    string[] scopeArr = ExpressionScopes.Peek().Split('.');
                //    if (scopeArr.Length > 0 && Symbols.TryGetType(scopeArr.Last(), out VariableType vT) && vT is Class scopeClass)
                //    {
                //        return NewSymbolReference(new ClassType(scopeClass, scopeClass.StartPos, scopeClass.EndPos), token, isDefaultRef);
                //    }
                //}
                return ParseBasicRefOrCast(token, isDefaultRef);
            }

            if (isDefaultRef)
            {
                throw Error("Expected property name after 'default.'!", CurrentPosition);
            }

            if (NotInContext && Matches(TokenType.LeftParenth))
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
            //currently making callers handle nulls, which allows for more specific error messages
            throw Error("Expected Expression!");
        }

        private bool NotInContext => ExpressionScopes.Count == 1 || ExpressionScopes.First() == ExpressionScopes.Last();

        private Expression ParseSuper()
        {
            Class superClass;
            State state = null;
            Class superSpecifier = null;
            if (Matches(TokenType.LeftParenth))
            {
                if (Consume(TokenType.Word) is {} className)
                {
                    if (!Symbols.TryGetType(className.Value, out VariableType vartype))
                    {
                        throw Error($"No class named '{className.Value}' found!", className.StartPos, className.EndPos);
                    }

                    if (!(vartype is Class super))
                    {
                        throw Error($"'{vartype.Name}' is not a class!", className.StartPos, className.EndPos);
                    }
                    superSpecifier = super;
                    superClass = super;
                    if (!Self.SameAsOrSubClassOf(superClass.Name))
                    {
                        throw Error($"'{superClass.Name}' is not a superclass of '{Self.Name}'!", className.StartPos, className.EndPos);
                    }
                }
                else
                {
                    throw Error("Expected superclass specifier after '('!", CurrentPosition);
                }

                if (!Matches(TokenType.RightParenth))
                {
                    throw Error("Expected ')' after superclass specifier!", CurrentPosition);
                }
            }
            else
            {
                state = Node switch
                {
                    State s => s,
                    Function func when func.Outer is State s2 => s2,
                    _ => null
                };
                if (state?.Parent != null)
                {
                    superClass = Self;
                    state = state.Parent;
                }
                else
                {
                    state = null;
                    superClass = Self.Parent as Class ?? throw Error($"Can't use '{SUPER}' in a class with no parent!", PrevToken.StartPos, PrevToken.EndPos);
                }
            }

            if (!Matches(TokenType.Dot) || !Matches(TokenType.Word))
            {
                throw Error($"Expected function name after '{SUPER}'!", CurrentPosition);
            }

            Token<string> functionName = PrevToken;
            string specificScope;
            //try to find function in parent states
            while (state != null)
            {
                Class stateClass = (Class)state.Outer;
                specificScope = $"{stateClass.GetInheritanceString()}.{state.Name}";
                if (Symbols.TryGetSymbolInScopeStack(functionName.Value, out ASTNode funcNode, specificScope) && funcNode is Function)
                {
                    return new SymbolReference(funcNode, functionName.Value, functionName.StartPos, functionName.EndPos)
                    {
                        IsSuper = true
                    };
                }

                state = state.Parent;
            }

            specificScope = superClass.GetInheritanceString();
            if (!Symbols.TryGetSymbolInScopeStack(functionName.Value, out ASTNode symbol, specificScope))
            {
                throw Error($"No function named '{functionName.Value}' found in a superclass!", functionName.StartPos, functionName.EndPos);
            }

            if (!(symbol is Function))
            {
                throw Error($"Expected function name after '{SUPER}'!", functionName.StartPos, functionName.EndPos);
            }

            return new SymbolReference(symbol, functionName.Value, functionName.StartPos, functionName.EndPos)
            {
                IsSuper = true,
                SuperSpecifier = superSpecifier
            };
        }

        private Expression ParseNew()
        {
            Token<string> token = PrevToken;
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

                    if (!NodeUtils.TypeCompatible(SymbolTable.StringType, objName.ResolveType())) //TODO: should this really be a string, and not a name?
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

                        if (!NodeUtils.TypeCompatible(SymbolTable.IntType, flags.ResolveType()))
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

            InNew = true;
            Expression objClass = ParseExpression();
            InNew = false;
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

        private Expression ParseBasicRefOrCast(Token<string> token, bool isDefaultRef = false)
        {
            string specificScope = ExpressionScopes.Peek();
            if (!Symbols.TryGetSymbolInScopeStack(token.Value, out ASTNode symbol, specificScope))
            {
                //primitive or dynamic cast, or enum
                if (!isDefaultRef && Symbols.TryGetType(token.Value, out VariableType destType))
                {
                    if (destType is Enumeration enm && Matches(TokenType.Dot))
                    {
                        if (Consume(TokenType.Word) is {} enumValName 
                         && enm.Values.FirstOrDefault(val => val.Name.CaseInsensitiveEquals(enumValName.Value)) is EnumValue enumValue)
                        {
                            return NewSymbolReference(enumValue, enumValName, false);
                        }
                        throw Error("Expected valid enum value!", CurrentPosition);
                    }
                    if (destType is Const cnst)
                    {
                        return cnst.Literal;
                    }
                    if (!Matches(TokenType.LeftParenth))
                    {
                        throw Error("Expected '(' after typename in cast expression!", CurrentPosition);
                    }
                    return ParsePrimitiveOrDynamicCast(token, destType);
                }
                //TODO: better error message
                throw Error($"{specificScope} has no member named '{token.Value}'!", token.StartPos, token.EndPos);
            }

            return NewSymbolReference(symbol, token, isDefaultRef);
        }

        private Expression NewSymbolReference(ASTNode symbol, Token<string> token, bool isDefaultRef)
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

            if (isDefaultRef && symRef.Node is Function)
            {
                throw Error("Expected property name!", CurrentPosition);
            }

            return symRef;

        }

        private Expression ParsePrimitiveOrDynamicCast(Token<string> token, VariableType destType)
        {
            Token<string> castToken = token;

            Expression expr = ParseExpression();
            if (!Matches(TokenType.RightParenth))
            {
                throw Error("Expected ')' at end of cast expression!", CurrentPosition);
            }

            var exprType = expr.ResolveType();
            if (destType.Equals(exprType))
            {
                //TODO: warning for unneccessary casts?
                return expr;
            }

            if (destType is Class destClass && exprType is Class srcClass)
            {
                //dynamic cast
                bool isInterfaceCast = destClass.IsInterface || srcClass.IsInterface;
                if (srcClass.SameAsOrSubClassOf(destClass.Name) || destClass.SameAsOrSubClassOf(srcClass.Name) 
                                                                || isInterfaceCast) //interface casts are checked at runtime 
                {
                    return new CastExpression(destType, expr, castToken.StartPos, CurrentPosition)
                    {
                        IsInterfaceCast = isInterfaceCast
                    };
                }
                throw Error($"Cannot cast between unrelated classes '{exprType.Name}' and '{destType?.Name}'!", CurrentPosition);
            }

            if (destType == SymbolTable.StringRefType && expr is IntegerLiteral intLit)
            {
                intLit.NumType = INT;
                exprType = expr.ResolveType();
            }
            //primitive cast
            ECast cast = CastHelper.GetConversion(destType, exprType);
            if (cast == ECast.Max)
            {
                throw Error($"Cannot cast from '{exprType?.Name}' to '{destType?.Name}'!", CurrentPosition);
            }

            return new PrimitiveCast(CastHelper.PureCastType(cast), destType, expr, castToken.StartPos, CurrentPosition);
        }

        private Expression ParseObjectLiteral(Token<string> className, Token<string> objName)
        {
            bool isClassLiteral = className.Value.CaseInsensitiveEquals(CLASS);

            var classType = new VariableType((isClassLiteral ? objName : className).Value);
            if (!Symbols.TryResolveType(ref classType))
            {
                throw Error($"No type named '{classType.Name}' exists!", className.StartPos, className.EndPos);
            }

            if (classType is Class cls)
            {
                if (isClassLiteral)
                {
                    classType = new ClassType(classType);
                }
                else if (cls.SameAsOrSubClassOf("Actor"))
                {
                    throw Error("Object constants must not be Actors!", className.StartPos, objName.EndPos);
                }
                

                return new ObjectLiteral(new NameLiteral(objName.Value, objName.StartPos, objName.EndPos), classType, className.StartPos, objName.EndPos);
            }

            throw Error($"'{classType.Name}' is not a class!", className.StartPos, className.EndPos);
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
