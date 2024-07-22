using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Language.Util;
using LegendaryExplorerCore.UnrealScript.Utilities;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;
using static LegendaryExplorerCore.Unreal.UnrealFlags;
using LegendaryExplorerCore.UnrealScript.Lexing;

namespace LegendaryExplorerCore.UnrealScript.Parsing
{
    internal sealed class CodeBodyParser : StringParserBase
    {
        private const int NOPRECEDENCE = int.MaxValue;
        private readonly ASTNode Node;
        private readonly CodeBody Body;
        private readonly Class Self;
        private readonly MEGame Game;

        private readonly CaseInsensitiveDictionary<Label> Labels = new();

        private readonly Stack<(string scope, bool isStructScope)> ExpressionScopes;

        private bool IsFunction => Node.Type == ASTNodeType.Function;
        private bool IsState => Node.Type == ASTNodeType.State;

        private readonly bool InStaticFunction;

        private int _loopCount;
        private bool InLoop => _loopCount > 0;
        private readonly Stack<VariableType> SwitchTypes;
        private bool InForEachIterator;
        private bool InForEachBody;
        private readonly Stack<List<Label>> LabelNests;
        private bool InSwitch => SwitchTypes.Count > 0;
        private bool InNew;

        //these have to be checked against labels after the whole body is parsed
        private readonly List<Statement> gotoStatements = new();
        private readonly VariableDeclaration SelfDeclaration;

        public static void ParseFunction(Function func, MEGame game, SymbolTable symbols, MessageLog log)
        {
            symbols.PushScope(func.Name);

            var tokenStream = func.Body.Tokens;

            var bodyParser = new CodeBodyParser(tokenStream, game, func.Body, symbols, func, log);

            var body = bodyParser.ParseBody();

            bool hasStructDefaults = false;
            if (func.Locals.Any())
            {
                bool isNotInState = func.Outer is not State;
                var validator = new ClassValidationVisitor(log, symbols, ValidationPass.ClassAndStructMembersAndFunctionParams);
                var validator2 = new ClassValidationVisitor(log, symbols, ValidationPass.BodyPass);
                foreach (VariableDeclaration local in func.Locals)
                {
                    validator.VisitVarDecl(local, false);
                    validator2.VisitVarDecl(local, false);
                    hasStructDefaults |= ((local.VarType as StaticArrayType)?.ElementType ?? local.VarType) is Struct s && (game.IsGame3()  ? s.DefaultProperties.Statements.Any() : isNotInState);
                }
            }
            if (hasStructDefaults)
            {
                func.Flags |= EFunctionFlags.HasDefaults;
            }
            else
            {
                func.Flags &= ~EFunctionFlags.HasDefaults;
            }

            if (func.ReturnType is null)
            {
                //remove redundant return;
                if (body.Statements.Count > 0 && body.Statements[^1] is ReturnStatement { Value: null })
                {
                    body.Statements.RemoveAt(body.Statements.Count - 1);
                }
            }
            else
            {
                if (body.EndPos != body.StartPos && !DoesReturn(body))
                {
                    log.LogWarning($"Function '{func.Name}' might reach end without returning a value!", func.EndPos);
                }

                static bool DoesReturn(CodeBody cb)
                {
                    if (cb.Statements.Count == 0)
                    {
                        return false;
                    }

                    switch (cb.Statements[^1])
                    {
                        case ReturnStatement:
                            return true;
                        case IfStatement ifStatement:
                            if (ifStatement.Else is null)
                            {
                                return false;
                            }
                            return DoesReturn(ifStatement.Then) && DoesReturn(ifStatement.Else);
                        //todo: more thorough analysis. switch statements?
                        default:
                            return false;
                    }
                }
            }

            //parse default parameter values
            if (func.HasOptionalParms)
            {
                foreach (FunctionParameter param in func.Parameters.Where(p => p.IsOptional))
                {
                    var unparsedBody = param.UnparsedDefaultParam;
                    if (unparsedBody is null)
                    {
                        continue;
                    }

                    var paramTokenStream = unparsedBody.Tokens;

                    var paramParser = new CodeBodyParser(paramTokenStream, game, unparsedBody, symbols, func, log);
                    var parsed = paramParser.ParseExpression();
                    if (parsed is null)
                    {
                        throw paramParser.ParseError("Could not parse default parameter value!", unparsedBody);
                    }
                    
                    VariableType valueType = parsed.ResolveType();
                    if (!bodyParser.TypeCompatible(param.VarType, valueType, parsed.StartPos))
                    {
                        paramParser.TypeError($"Could not assign value of type '{valueType.DisplayName()}' to variable of type '{param.VarType.DisplayName()}'!", unparsedBody);
                    }
                    AddConversion(param.VarType, ref parsed);
                    param.DefaultParameter = parsed;
                }
            }

            func.Body = body;

            symbols.PopScope();
        }

        public static void ParseState(State state, MEGame game, SymbolTable symbols, MessageLog log = null, bool parseFunctions = true)
        {
            symbols.PushScope(state.Name);

            var tokenStream = state.Body.Tokens;
            var bodyParser = new CodeBodyParser(tokenStream, game, state.Body, symbols, state, log);

            var body = bodyParser.ParseBody();

            //remove redundant stop;
            if (body.Statements.Count > 0 && body.Statements.Last() is StopStatement)
            {
                body.Statements.RemoveAt(body.Statements.Count - 1);
            }

            state.Labels = bodyParser.LabelNests.Pop();

            state.Body = body;

            if (parseFunctions)
            {
                foreach (Function stateFunction in state.Functions)
                {
                    ParseFunction(stateFunction, game, symbols, log);
                }
            }

            symbols.PopScope();
        }

        public static void ParseReplicationBlock(Class cls, MEGame game, SymbolTable symbols, MessageLog log)
        {
            symbols.RevertToObjectStack();
            symbols.GoDirectlyToStack(cls.GetScope());
            TokenStream tokenStream = cls.ReplicationBlock.Tokens;
            var repBlockParser = new CodeBodyParser(tokenStream, game, cls.ReplicationBlock, symbols, cls, log);
            repBlockParser.ParseReplicationBlock();
        }

        private CodeBodyParser(TokenStream tokens, MEGame game, CodeBody body, SymbolTable symbols, ASTNode containingNode, MessageLog log = null)
        {
            Game = game;
            Log = log ?? new MessageLog();
            Symbols = symbols;
            Tokens = tokens;
            _loopCount = 0;
            SwitchTypes = new Stack<VariableType>();
            Node = containingNode;
            if (Node is Function func)
            {
                InStaticFunction = func.IsStatic;
            }
            Body = body;
            Self = NodeUtils.GetContainingClass(body);
            SelfDeclaration = new VariableDeclaration(Self, default, "Self");

            ExpressionScopes = new();
            ExpressionScopes.Push((Symbols.CurrentScopeName, false));

            LabelNests = new Stack<List<Label>>();
            LabelNests.Push(new List<Label>());
        }

        private void ParseReplicationBlock()
        {
            if (Equals(Body.StartPos, Body.EndPos))
            {
                Body.Statements = new List<Statement>();
                return;
            }
            do
            {
                if (Tokens.CurrentItem.StartPos.Equals(Body.StartPos))
                    break;
                Tokens.Advance();
            } while (!Tokens.AtEnd());
            if (Tokens.AtEnd())
                throw ParseError("Could not find the code body for the current node, please contact the maintainers of this compiler!");

            var statements = new List<Statement>();
            var replicatedVariables = new HashSet<string>();
            do
            {
                ScriptToken ifToken = Consume(IF);
                if (ifToken == null) break;
                ifToken.SyntaxType = EF.Keyword;

                if (Consume(TokenType.LeftParenth) == null) throw ParseError($"Expected '(' after '{IF}'!", CurrentPosition);

                Expression condition = ParseExpression() ?? throw ParseError($"Expected an expression as the {IF} condition!", CurrentPosition);

                VariableType conditionType = condition.ResolveType();
                if (conditionType != SymbolTable.BoolType)
                {
                    TypeError("Expected a boolean result from the condition!", condition);
                }

                if (Consume(TokenType.RightParenth) == null) throw ParseError($"Expected ')' after {IF} condition!", CurrentPosition);
                if (Matches(TokenType.LeftBracket))
                {
                    throw ParseError("If statements in replication blocks do not use brackets.", PrevToken);
                }

                if (Consume(TokenType.Word) is not ScriptToken varToken)
                {
                    throw ParseError("Expected a variable name after the condition statement!", CurrentPosition);
                }

                var symbols = new List<SymbolReference>();

                while (true)
                {
                    if (Self.LookupVariable(varToken.Value) is VariableDeclaration varDecl)
                    {
                        if (varDecl.Outer != Self)
                        {
                            TypeError("Cannot define a replication condition for a member in a parent class!", varToken);
                        }
                        if (!replicatedVariables.Add(varDecl.Name))
                        {
                            TypeError($"A replication condition for '{varDecl.Name}' has already been defined!", varToken);
                        }
                        if (varDecl.VarType is DynamicArrayType)
                        {
                            TypeError("Dynamic arrays cannot be replicated!", varToken);
                        }
                        varDecl.Flags |= EPropertyFlags.Net;
                        symbols.Add(NewSymbolReference(varDecl, varToken, false));
                    }
                    else
                    {
                        TypeError($"{Self.Name} has no member named '{varToken.Value}'!", varToken);
                        symbols.Add(NewSymbolReference(new VariableType("Error"), varToken, false));
                    }
                    if (Matches(TokenType.SemiColon))
                    {
                        break;
                    }
                    if (!Matches(TokenType.Comma))
                    {
                        throw ParseError("Expected either a ';' or a ',' after the variable name!", CurrentPosition);
                    }
                    varToken = Consume(TokenType.Word);
                    if (varToken is null)
                    {
                        throw ParseError("Expected a variable name after the ','", CurrentPosition);
                    }
                }
                
                statements.Add(new ReplicationStatement(condition, symbols, ifToken.StartPos, PrevToken.EndPos));
            } while (!Tokens.AtEnd());
            if (Tokens.CurrentItem.Type != TokenType.EOF && !Tokens.CurrentItem.StartPos.Equals(Body.EndPos))
            {
                ParseError("Could not parse a valid statement, even though the current code body has supposedly not ended yet.", CurrentPosition);
            }
            Body.Statements = statements;
        }

        private CodeBody ParseBody()
        {
            if (Equals(Body.StartPos, Body.EndPos))
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
                throw ParseError("Could not find the code body for the current node, please contact the maintainers of this compiler!");

            var body = ParseBlock(false, IsFunction);
            if (body == null)
                return null;
            Body.Statements = body.Statements;

            if (Tokens.CurrentItem.Type != TokenType.EOF && !Tokens.CurrentItem.StartPos.Equals(Body.EndPos))
            {
                ParseError("Could not parse a valid statement, even though the current code body has supposedly not ended yet.", CurrentPosition);
            }
            var labels = LabelNests.Peek();
            foreach (Statement stmnt in gotoStatements)
            {
                switch (stmnt)
                {
                    case Goto {ContainingForEach: null} g:
                    {
                        if (labels.FirstOrDefault(l => l.Name.CaseInsensitiveEquals(g.LabelName)) is Label label)
                        {
                            g.Label = label;
                        }
                        else
                        {
                            ParseError($"Could not find label '{g.LabelName}'! (gotos cannot jump out of or into a foreach)", g);
                        }

                        break;
                    }
                    case StateGoto {LabelExpression: NameLiteral nameLiteral} sg when labels.FirstOrDefault(l => l.Name.CaseInsensitiveEquals(nameLiteral.Value)) is null:
                        ParseError($"Could not find label '{nameLiteral.Value}'! (gotos cannot jump out of or into a foreach)", sg);
                        break;
                }
            }
            return Body;
        }

        private CodeBody ParseBlock(bool requireBrackets = true, bool functionBody = false)
        {
            if (requireBrackets && Consume(TokenType.LeftBracket) == null) throw ParseError("Expected '{'!", CurrentPosition);

            bool pastVarDecls = false;
            var statements = new List<Statement>();
            var startPos = CurrentPosition;
            var current = ParseDeclarationOrStatement();
            while (current != null)
            {
                if (!SemiColonExceptions.Contains(current.Type) && Consume(TokenType.SemiColon) == null)
                {
                    ParseError("Expected semi-colon after statement!", CurrentPosition);
                }

                if (current is not VariableDeclaration)
                {
                    pastVarDecls = true;

                    if (current is Label label)
                    {
                        if (!Labels.TryAdd(label.Name, label))
                        {
                            ParseError($"Label '{label.Name}' already exists on line {Tokens.LineLookup.GetLineFromCharIndex(Labels[label.Name].StartPos)}!", label);
                        }
                        else
                        {
                            LabelNests.Peek().Add(label);
                        }
                    }
                    statements.Add(current);
                }
                else if (pastVarDecls)
                {
                    ParseError("Variable declarations must come before all other statements", current);
                }
                else if (!functionBody)
                {
                    ParseError("Can only declare variables at the top of a function!", current);
                }

                if (CurrentToken.Type == TokenType.EOF)
                {
                    break;
                }
                current = ParseDeclarationOrStatement();
            }

            var endPos = CurrentPosition;
            if (requireBrackets && Consume(TokenType.RightBracket) == null) throw ParseError("Expected '}'!", CurrentPosition);

            return new CodeBody(statements, startPos, endPos);
        }

        #region Statements

        private CodeBody ParseBlockOrStatement(bool allowEmpty = false)
        {
            CodeBody body;
            var single = ParseStatement();
            if (single != null)
            {
                var content = new List<Statement> { single };
                body = new CodeBody(content, single.StartPos, single.EndPos);
            }
            else
            {
                body = ParseBlock();
            }

            if (body == null)
            {
                if (allowEmpty && Consume(TokenType.SemiColon) != null)
                {
                    body = new CodeBody(null, CurrentPosition - 1, CurrentPosition);
                }
                else
                {
                    throw ParseError("Expected a code block or single statement!", CurrentPosition);
                }
            }

            return body;
        }

        private Statement ParseDeclarationOrStatement()
        {
            while (true)
            {
                try
                {
                    Statement statement = CurrentIs(LOCAL) ? ParseLocalVarDecl() : ParseStatement();
                    if (statement is not null)
                    {
                        return statement;
                    }
                    if (CurrentIs(TokenType.RightBracket) || !Synchronize())
                    {
                        return null;
                    }
                }
                catch (ParseException)
                {
                    if (!Synchronize())
                    {
                        return null;
                    }
                }
            }
        }

        //makes a rough attempt to find the next statement after a parse error has occured
        //returns false if it gets to the end of a block or file without finding one
        private bool Synchronize()
        {
            while (ExpressionScopes.Count > 1)
            {
                ExpressionScopes.Pop();
            }
            Tokens.Advance();
            while (!Tokens.AtEnd() && !CurrentIs(TokenType.RightBracket))
            {
                if (PrevToken.Type == TokenType.SemiColon)// || CurrentToken.StartPos.Line > PrevToken.EndPos.Line)
                {
                    return true;
                }

                if (CurrentTokenType is TokenType.Word)
                {
                    switch (CurrentToken.Value)
                    {
                        case RETURN:
                        case IF:
                        case SWITCH:
                        case WHILE:
                        case FOR:
                        case FOREACH:
                        case DO:
                        case CONTINUE:
                        case BREAK:
                        case GOTO:
                        case STOP:
                        case CASE:
                        case ASSERT:
                            return true;
                    }
                }

                Tokens.Advance();
            }
            return false;
        }

        private Statement ParseStatement()
        {
            if (CurrentIs(LOCAL))
            {
                var errorStatement = new ErrorStatement(ParseLocalVarDecl());
                ParseError("Can only declare variables at the top of a function!", errorStatement);
                return errorStatement;
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
                ScriptToken labelToken = Consume(TokenType.Word);
                labelToken.SyntaxType = EF.Label;
                return new Label(labelToken.Value, 0, labelToken.StartPos, Consume(TokenType.Colon).EndPos);
            }

            Expression expr = ParseExpression();
            if (expr == null)
            {
                return null;
            }

            if (Consume(TokenType.Assign) is { } assign)
            {
                assign.SyntaxType = EF.Operator;
                if (!IsLValue(expr))
                {
                    ParseError("Assignments require a variable on the left! (LValue expected).", expr);
                }

                var value = ParseExpression();
                if (value == null) throw ParseError("Assignments require an expression on the right! (RValue expected).", CurrentPosition);

                VariableType exprType = expr.ResolveType();
                if (!TypeCompatible(exprType, value.ResolveType(), value.StartPos))
                {
                    TypeError($"Cannot assign a value of type '{value.ResolveType().DisplayName() ?? "None"}' to a variable of type '{exprType.DisplayName()}'.", assign);
                }
                AddConversion(exprType, ref value);

                if (expr is SymbolReference { Node: VariableDeclaration decl})
                {
                    if (decl.IsConst)
                    {
                        Log.LogWarning("Assigning to a 'const' variable! Other code may not account for it changing.", expr.StartPos, expr.EndPos);
                    }
                    else if (ReferenceEquals(decl, SelfDeclaration))
                    {
                        TypeError($"{SELF} is immutable! You cannot assign a different value to it.", expr);
                    }
                    if (expr is DefaultReference && !decl.Flags.Has(EPropertyFlags.Config))
                    {
                        Log.LogWarning("Changing the default value of a non-config property may cause errors", expr.StartPos, expr.EndPos);
                    }
                }

                return new AssignStatement(expr, value, expr.StartPos, value.EndPos);
            }

            if (ExpressionHasEffect(expr))
            {
            }
            else
            {
                ParseError("Expression-only statements must have an effect!", expr);
            }

            return new ExpressionOnlyStatement(expr, expr.StartPos, expr.EndPos);

            static bool ExpressionHasEffect(Expression expression)
            {
                return expression is FunctionCall or DelegateCall or PostOpReference or CompositeSymbolRef {InnerSymbol: FunctionCall or DelegateCall} 
                       || expression is PreOpReference preOp && preOp.Operator.HasOutParams || expression is InOpReference inOp && inOp.Operator.HasOutParams 
                       || expression is DynArrayOperation and not DynArrayLength
                       || expression is ConditionalExpression ternary && ExpressionHasEffect(ternary.TrueExpression) && ExpressionHasEffect(ternary.FalseExpression);
            }
        }

        private VariableDeclaration ParseLocalVarDecl()
        {
            var startPos = CurrentPosition;
            if (!Matches(LOCAL, EF.Keyword)) return null;

            VariableType type = ParseTypeRef();
            if (type == null) throw ParseError($"Expected variable type after '{LOCAL}'!", CurrentPosition);
            type.Outer = Body;
            if (!Symbols.TryResolveType(ref type)) TypeError($"The type '{type.DisplayName()}' does not exist in the current scope!", type);
            if (type is Struct)
            {
                PrevToken.SyntaxType = EF.Struct;
                Tokens.AddDefinitionLink(type, PrevToken);
            }
            else if (type is Enumeration)
            {
                PrevToken.SyntaxType = EF.Enum;
                Tokens.AddDefinitionLink(type, PrevToken);
            }
            else if (PrevToken.Type == TokenType.RightArrow)
            {
                ScriptToken innerTypeToken = Tokens.Prev(2);
                if (type is DynamicArrayType arrayType)
                {
                    if (arrayType.ElementType is Struct)
                    {
                        innerTypeToken.SyntaxType = EF.Struct;
                    }
                    else if (arrayType.ElementType is Enumeration)
                    {
                        innerTypeToken.SyntaxType = EF.Enum;
                    }
                }
                Tokens.AddDefinitionLink(type, innerTypeToken);
            }
            else
            {
                Tokens.AddDefinitionLink(type, PrevToken);
            }

            var var = ParseVariableName();
            if (var == null) throw ParseError("Malformed or missing variable name!", CurrentPosition);

            if (var.Name.CaseInsensitiveEquals("ReturnValue"))
            {
                TypeError("Cannot name a parameter 'ReturnValue'! It is a reserved word!", var.StartPos, var.EndPos);
            }

            if (Symbols.SymbolExistsInCurrentScope(var.Name))
            {
                TypeError($"A variable named '{var.Name}' already exists in this scope!", var);
                return null;
            }
            if (Symbols.SymbolExistsInParentScopes(var.Name))
            {
                Log.LogWarning($"A symbol named '{var.Name}' exists in a parent scope. Are you sure you want to shadow it?", var.StartPos, var.EndPos);
            }

            var varDecl = new VariableDeclaration(type, EPropertyFlags.None, var.Name, var.Size, null, startPos, var.EndPos);
            Symbols.AddSymbol(varDecl.Name, varDecl);
            if (Node is Function func)
            {
                func.Locals.Add(varDecl);
            }
            else
            {
                ParseError("States cannot declare variables!", varDecl);
            }
            varDecl.Outer = Node;

            Tokens.AddDefinitionLink(varDecl, var.StartPos, var.EndPos - var.StartPos);

            return varDecl;
        }

        private IfStatement ParseIf()
        {
            var token = Consume(IF);
            if (token == null) return null;
            token.SyntaxType = EF.Keyword;

            if (Consume(TokenType.LeftParenth) == null) throw ParseError($"Expected '(' after '{IF}'!", CurrentPosition);

            var condition = ParseExpression();
            if (condition == null) throw ParseError($"Expected an expression as the {IF} condition!", CurrentPosition);

            VariableType conditionType = condition.ResolveType();
            if (conditionType != SymbolTable.BoolType)
            {
                TypeError("Expected a boolean result from the condition!", condition);
            }

            if (Consume(TokenType.RightParenth) == null) throw ParseError($"Expected ')' after {IF} condition!", CurrentPosition);

            CodeBody thenBody = ParseBlockOrStatement();
            if (thenBody == null) throw ParseError("Expected a statement or code block!", CurrentPosition);

            CodeBody elseBody = null;
            var elsetoken = Consume(ELSE);
            if (elsetoken != null)
            {
                elsetoken.SyntaxType = EF.Keyword;
                elseBody = ParseBlockOrStatement();
                if (elseBody == null) throw ParseError("Expected a statement or code block!", CurrentPosition);
            }

            return new IfStatement(condition, thenBody, elseBody, token.StartPos, elseBody?.EndPos ?? thenBody.EndPos);
        }

        private ReturnStatement ParseReturn()
        {
            var token = Consume(RETURN);
            if (token == null) return null;
            token.SyntaxType = EF.Keyword;

            if (!IsFunction) throw ParseError("Return statements can only exist in functions!", CurrentPosition);

            var func = (Function)Node;
            if (CurrentTokenType == TokenType.SemiColon)
            {
                if (func.ReturnType is not null)
                {
                    TypeError("Missing return value!", token);
                }
                return new ReturnStatement(null, token.StartPos, token.EndPos);
            }

            var value = ParseExpression();
            if (value == null) throw ParseError("Expected a return value or a semi-colon!", CurrentPosition);

            var type = value.ResolveType();
            if (func.ReturnType == null)
            {
                ParseError("Function should not return a value!", token);
            }
            else if (!TypeCompatible(func.ReturnType, type, value.StartPos))
            {
                TypeError($"Cannot return a value of type '{type.DisplayName()}', function should return '{func.ReturnType.DisplayName()}'.", token);
            }
            else
            {
                AddConversion(func.ReturnType, ref value);
            }

            return new ReturnStatement(value, token.StartPos, token.EndPos);
        }

        private SwitchStatement ParseSwitch()
        {
            var token = Consume(SWITCH);
            if (token == null) return null;
            token.SyntaxType = EF.Keyword;

            if (Consume(TokenType.LeftParenth) == null) throw ParseError($"Expected '(' after '{SWITCH}'!", CurrentPosition);

            var expression = ParseExpression();
            if (expression == null) throw ParseError("Expected an expression as the switch value!", CurrentPosition);

            if (Consume(TokenType.RightParenth) == null) throw ParseError("Expected ')'!", CurrentPosition);

            SwitchTypes.Push(expression.ResolveType());
            CodeBody body = ParseBlockOrStatement();
            SwitchTypes.Pop();
            if (body == null) throw ParseError("Expected switch code block!", CurrentPosition);
            List<Statement> statements = body.Statements;
            if (statements.Any())
            {
                Statement firstStatement = statements[0];
                if (firstStatement is not CaseStatement && firstStatement is not DefaultCaseStatement)
                {
                    ParseError($"First statement in a '{SWITCH}' body must be a '{CASE}' or '{DEFAULT}' statement!", firstStatement);
                }
                //switch must have a default case
                if (!statements.Any(statement => statement is DefaultCaseStatement))
                {
                    statements.Add(new DefaultCaseStatement(body.EndPos, body.EndPos));
                }
                DefaultCaseStatement defaultCaseStatement = null;
                for (int i = statements.Count - 1; i >= 0; i--)
                {
                    Statement statement = statements[i];
                    if (defaultCaseStatement is null)
                    {
                        switch (statement)
                        {
                            case CaseStatement:
                                TypeError("A case statement cannot come after the default case!", statement);
                                break;
                            case DefaultCaseStatement dcs:
                                defaultCaseStatement = dcs;
                                break;
                        }
                    }
                    else if (statement is DefaultCaseStatement)
                    {
                        TypeError("Cannot have multiple default cases in a single switch!", defaultCaseStatement);
                    }
                }
            }
            else
            {
                ParseError("Switch statement must have a body!", body);
            }

            return new SwitchStatement(expression, body, token.StartPos, token.EndPos);
        }

        private WhileLoop ParseWhile()
        {
            var token = Consume(WHILE);
            if (token == null) return null;
            token.SyntaxType = EF.Keyword;

            if (Consume(TokenType.LeftParenth) == null) throw ParseError("Expected '('!", CurrentPosition);

            var condition = ParseExpression();
            if (condition == null) throw ParseError("Expected an expression as the while condition!", CurrentPosition);
            if (condition.ResolveType() != SymbolTable.BoolType)
            {
                TypeError("Expected a boolean result from the condition!", condition);
            }

            if (Consume(TokenType.RightParenth) == null) throw ParseError("Expected ')'!", CurrentPosition);

            _loopCount++;
            CodeBody body = ParseBlockOrStatement(allowEmpty: true);
            _loopCount--;
            if (body == null) return null;

            return new WhileLoop(condition, body, token.StartPos, token.EndPos);
        }

        private ForLoop ParseFor()
        {
            var token = Consume(FOR);
            if (token == null) return null;
            token.SyntaxType = EF.Keyword;

            if (Consume(TokenType.LeftParenth) == null) throw ParseError("Expected '('!", CurrentPosition);

            var initStatement = ParseStatement();
            if (initStatement != null && initStatement.Type != ASTNodeType.AssignStatement && initStatement.Type != ASTNodeType.FunctionCall)
            {
                TypeError("Init statement in a for-loop must be an assignment or a function call!", initStatement);
            }

            if (Consume(TokenType.SemiColon) == null) throw ParseError("Expected semi-colon after init statement!", CurrentPosition);

            var condition = ParseExpression();
            if (condition == null) throw ParseError("Expected an expression as the for condition!", CurrentPosition);
            if (condition.ResolveType() != SymbolTable.BoolType)
            {
                TypeError("Expected a boolean result from the condition!", condition);
            }

            if (Consume(TokenType.SemiColon) == null) throw ParseError("Expected semi-colon after condition!", CurrentPosition);

            var updateStatement = ParseStatement();

            if (Consume(TokenType.RightParenth) == null) throw ParseError("Expected ')'!", CurrentPosition);

            _loopCount++;
            CodeBody body = ParseBlockOrStatement(allowEmpty: true);
            _loopCount--;
            if (body == null) return null;

            return new ForLoop(initStatement, condition, updateStatement, body, token.StartPos, token.EndPos);
        }

        private ForEachLoop ParseForEach()
        {
            var token = Consume(FOREACH);
            if (token == null) return null;
            token.SyntaxType = EF.Keyword;

            InForEachIterator = true;
            Expression iterator = CompositeRef();
            InForEachIterator = false;

            FunctionCall fc = iterator switch
            {
                FunctionCall call => call,
                CompositeSymbolRef {InnerSymbol: FunctionCall cfc} => cfc,
                _ => null
            };
            if (fc != null)
            {
                if (fc.Function.Node is not Function func || !func.Flags.Has(EFunctionFlags.Iterator))
                {
                    TypeError($"Expected an iterator function call or dynamic array iterator after '{FOREACH}'!", iterator);
                }
                else if (func.Parameters.Count >= 2 && fc.Arguments[0].ResolveType() is ClassType iteratorValueType && fc.Arguments[1].ResolveType() is Class objClass)
                {
                    if (!objClass.SameAsOrSubClassOf(iteratorValueType.ClassLimiter.Name))
                    {
                        TypeError("Second argument to iterator function must be the same class or a subclass of the class passed as the first argument!", fc.Arguments[1]);
                    }
                }
            }
            else if (iterator is null)
            {
                throw ParseError("Expected an iterator expression after '{FOREACH}'!", CurrentPosition);
            }
            else if (iterator is not DynArrayIterator && iterator is not CompositeSymbolRef {InnerSymbol: DynArrayIterator})
            {
                TypeError($"Expected an iterator function call or dynamic array iterator after '{FOREACH}'!", iterator);
            }

            int gotoStatementsIdx = gotoStatements.Count;
            bool alreadyInForEach = InForEachBody;
            InForEachBody = true;
            _loopCount++;
            LabelNests.Push(new List<Label>());
            CodeBody body = ParseBlockOrStatement(allowEmpty: true);

            _loopCount--;
            InForEachBody = alreadyInForEach;
            if (body == null) return null;

            var forEach = new ForEachLoop(iterator, body, token.StartPos, token.EndPos);

            var labels = LabelNests.Pop();
            if (gotoStatementsIdx < gotoStatements.Count)
            {
                for (; gotoStatementsIdx < gotoStatements.Count; gotoStatementsIdx++)
                {
                    var stmnt = gotoStatements[gotoStatementsIdx];
                    if (stmnt is Goto {ContainingForEach: null} g)
                    {
                        g.ContainingForEach = forEach;
                        if (labels.FirstOrDefault(l => l.Name.CaseInsensitiveEquals(g.LabelName)) is Label label)
                        {
                            g.Label = label;
                        }
                        else
                        {
                            ParseError($"Could not find label '{g.LabelName}'! (gotos cannot jump out of or into a foreach)", g.Label);
                        }
                    }
                }
            }

            return forEach;
        }

        private DoUntilLoop ParseDoUntil()
        {
            var doToken = Consume(DO);
            if (doToken == null) return null;
            doToken.SyntaxType = EF.Keyword;

            _loopCount++;
            CodeBody body = ParseBlockOrStatement();
            _loopCount--;
            if (body == null) return null;

            var untilToken = Consume(UNTIL);
            if (untilToken == null) throw ParseError("Expected 'until'!", CurrentPosition);
            untilToken.SyntaxType = EF.Keyword;

            if (Consume(TokenType.LeftParenth) == null) throw ParseError("Expected '('!", CurrentPosition);

            var condition = ParseExpression();
            if (condition == null) throw ParseError("Expected an expression as the until condition!", CurrentPosition);
            if (condition.ResolveType() != SymbolTable.BoolType)
            {
                TypeError("Expected a boolean result from the condition!", condition);
            }

            if (Consume(TokenType.RightParenth) == null) throw ParseError("Expected ')'!", CurrentPosition);

            return new DoUntilLoop(condition, body, untilToken.StartPos, untilToken.EndPos);
        }

        private ContinueStatement ParseContinue()
        {
            var token = Consume(CONTINUE);
            if (token == null) return null;
            token.SyntaxType = EF.Keyword;

            if (!InLoop) ParseError("The continue keyword is only valid inside loops!", token);

            return new ContinueStatement(token.StartPos, token.EndPos);
        }

        private BreakStatement ParseBreak()
        {
            var token = Consume(BREAK);
            if (token == null) return null;
            token.SyntaxType = EF.Keyword;

            if (!InLoop && !InSwitch) ParseError("The break keyword is only valid inside loops and switch statements!", token);

            return new BreakStatement(token.StartPos, token.EndPos);
        }

        private Statement ParseGoto()
        {
            var gotoToken = Consume(GOTO);
            if (gotoToken == null) return null;
            gotoToken.SyntaxType = EF.Keyword;

            if (IsState && !InForEachBody)
            {
                Expression labelExpr;
                if (CurrentIs(TokenType.Word) && Tokens.LookAhead(1).Type == TokenType.SemiColon)
                {
                    //error production
                    ScriptToken label = Consume(TokenType.Word);
                    labelExpr = new ErrorExpression(label.StartPos, label.EndPos, label);
                    ParseError("gotos in State bodies expect an expression that evaluates to a name, not a bare label. Put single quotes around a label name to make it a name literal", label);
                }
                else
                {
                    labelExpr = ParseExpression();
                    if (labelExpr == null)
                    {
                        throw ParseError($"Expected a name expression after '{GOTO}'!");
                    }
                }

                var stateGoto = new StateGoto(labelExpr, gotoToken.StartPos, labelExpr.EndPos);
                gotoStatements.Add(stateGoto);
                return stateGoto;
            }

            var labelToken = Consume(TokenType.Word) ?? throw ParseError($"Expected a label after '{GOTO}'!");
            labelToken.SyntaxType = EF.Label;
            var gotoStatement = new Goto(labelToken.Value, gotoToken.StartPos, labelToken.EndPos);
            gotoStatements.Add(gotoStatement);
            return gotoStatement;
        }

        private StopStatement ParseStop()
        {
            var token = Consume(STOP);
            if (token == null) return null;
            token.SyntaxType = EF.Keyword;

            if (!IsState) ParseError("The stop keyword is only valid inside state code!", token);

            return new StopStatement(token.StartPos, token.EndPos);
        }

        private CaseStatement ParseCase()
        {
            var token = Consume(CASE);
            if (token == null) return null;
            token.SyntaxType = EF.Keyword;

            if (!InSwitch) ParseError("Case statements can only exist inside switch blocks!", token);

            var value = ParseExpression();
            if (value == null) throw ParseError("Expected an expression specifying the case value", CurrentPosition);
            var switchType = SwitchTypes.Count > 0 ? SwitchTypes.Peek() : null;
            if (switchType is not null)
            {
                if (switchType == SymbolTable.IntType && value is IntegerLiteral caseLit)
                {
                    caseLit.NumType = INT;
                }
                if (switchType == SymbolTable.ByteType && value is IntegerLiteral possibleByteLiteral)
                {
                    if (possibleByteLiteral.Value is < byte.MinValue or > byte.MaxValue)
                    {
                        TypeError("Since the switch expression is of type 'byte', this number must be in the range 0-255.", value);
                    }
                    //AddConversion will auto-convert it to a byte literal
                }
                else if (!(switchType is Enumeration && value is IntegerLiteral) && !NodeUtils.TypeEqual(switchType, value.ResolveType()))
                {
                    TypeError("Case expression must evaluate to the same type as the switch expression!", value);
                }
                AddConversion(switchType, ref value);
            }
            else
            {
                TypeError("Could not resolve type of switch expression!", value);
            }
            if (Consume(TokenType.Colon) == null) throw ParseError("Expected colon after case expression!", CurrentPosition);

            return new CaseStatement(value, token.StartPos, token.EndPos);
        }

        private DefaultCaseStatement ParseDefault()
        {
            var token = Consume(DEFAULT);
            if (token == null) return null;
            token.SyntaxType = EF.Keyword;

            if (!InSwitch) ParseError("Default statements can only exist inside switch blocks!", token);

            if (Consume(TokenType.Colon) == null) throw ParseError("Expected colon after default statement!", CurrentPosition);

            return new DefaultCaseStatement(token.StartPos, token.EndPos);
        }

        private AssertStatement ParseAssert()
        {
            var token = Consume(ASSERT);
            if (token == null) return null;
            token.SyntaxType = EF.Keyword;

            if (!Matches(TokenType.LeftParenth))
            {
                throw ParseError($"Expected '(' after {ASSERT}!", CurrentPosition);
            }

            var expr = ParseExpression() ?? throw ParseError($"Expected an expression in {ASSERT} statement!", CurrentPosition);
            VariableType conditionType = expr.ResolveType();
            if (conditionType != SymbolTable.BoolType)
            {
                TypeError($"Expected a boolean result from the {ASSERT} expression!", expr.StartPos, expr.EndPos);
            }

            if (!Matches(TokenType.RightParenth))
            {
                throw ParseError($"Expected ')' after expression in {ASSERT} statement!", CurrentPosition);
            }
            return new AssertStatement(expr, token.StartPos, PrevToken.EndPos);
        }

        #endregion

        #region Expressions

        private Expression ParseExpression() => Ternary();

        private Expression Ternary()
        {
            var expr = BinaryExpression(NOPRECEDENCE);
            if (expr == null) return null;

            if (Matches(TokenType.QuestionMark))
            {
                if (!TypeCompatible(SymbolTable.BoolType, expr.ResolveType(), expr.StartPos))
                {
                    TypeError("Expected a boolean expression before a '?'!", expr);
                }
                Expression trueExpr = Ternary();
                if (trueExpr is null)
                {
                    throw ParseError("Expected expression after '?'!", CurrentPosition);
                }

                if (!Matches(TokenType.Colon))
                {
                    throw ParseError("Expected ':' after true branch in conditional statement!", CurrentPosition);
                }
                Expression falseExpr = Ternary();
                VariableType trueType = trueExpr.ResolveType();
                VariableType falseType = falseExpr.ResolveType();
                if (falseExpr is IntegerLiteral falseIntLit)
                {
                    if (trueType == SymbolTable.IntType)
                    {
                        falseIntLit.NumType = INT;
                        falseType = falseIntLit.ResolveType();
                    }
                    else if (trueType == SymbolTable.ByteType || trueType is Enumeration)
                    {
                        falseIntLit.NumType = BYTE;
                        falseType = falseIntLit.ResolveType();
                    }
                }
                else if (trueExpr is IntegerLiteral trueIntLit)
                {
                    if (falseType == SymbolTable.IntType)
                    {
                        trueIntLit.NumType = INT;
                        trueType = trueIntLit.ResolveType();
                    }
                    else if (falseType == SymbolTable.ByteType || falseType is Enumeration)
                    {
                        trueIntLit.NumType = BYTE;
                        trueType = trueIntLit.ResolveType();
                    }
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
                else if (trueType is ClassType trueClassType && falseType is ClassType falseClassType)
                {
                    expr = new ConditionalExpression(expr, trueExpr, falseExpr, expr.StartPos, falseExpr.EndPos)
                    {
                        ExpressionType = new ClassType(NodeUtils.GetCommonBaseClass((Class)trueClassType.ClassLimiter, (Class)falseClassType.ClassLimiter))
                    };
                }
                else
                {
                    TypeError("True and false results in conditional expression must match types!", trueExpr.StartPos, falseExpr.EndPos);
                }
            }

            return expr;
        }

        private Expression BinaryExpression(int maxPrecedence)
        {
            Expression expr = Unary();
            if (expr is null)
            {
                return null;
            }
            while (IsOperator(out bool isRightShift, out TokenType opType))
            {
                CurrentToken.SyntaxType = EF.Operator;
                Expression lhs = expr;

                if (lhs is DynArrayLength && (opType is TokenType.AddAssign or TokenType.SubAssign or TokenType.MulAssign or TokenType.DivAssign))
                {
                    ParseError($"The {LENGTH} property of a dynamic array can only be changed by direct assignment!", lhs);
                }

                var possibleMatches = new List<InOpDeclaration>();
                int precedence = 0;
                foreach (InOpDeclaration opDecl in Symbols.GetInfixOperators(opType))
                {
                    precedence = opDecl.Precedence;
                    possibleMatches.Add(opDecl);
                }

                if (possibleMatches.Count == 0 || precedence >= maxPrecedence)
                {
                    break; //don't handle at this precedence level
                }

                ScriptToken opToken = Consume(CurrentTokenType);
                if (isRightShift)
                {
                    CurrentToken.SyntaxType = EF.Operator;
                    Consume(TokenType.RightArrow);
                }
                Expression rhs = BinaryExpression(precedence);
                if (rhs == null)
                {
                    throw ParseError($"Expected expression after '{opType}' operator!", CurrentPosition);
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
                    bool isEqualEqual = opType is TokenType.Equals;
                    bool isComparison = isEqualEqual || opType is TokenType.NotEquals;
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
                    else if (isComparison && lType is Struct typeStruct && rType?.PropertyType == EPropertyType.Struct)
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
                            TypeError("Cannot compare structs of different types!", opToken);
                        }
                    }
                    else if (isComparison && lType is DelegateType && rType == SymbolTable.BoolType)
                    {
                        //seems wrong but this exists in SFXPawn_Player::ImpactWithPower
                        expr = new DelegateComparison(isEqualEqual, lhs, rhs, lhs.StartPos, rhs.EndPos);
                        Log.LogWarning("Comparison of a delegate to boolean. Was this intended?", expr.StartPos, expr.EndPos);
                    }
                    else
                    {
                        ParseError($"No valid operator found for '{lType.DisplayName()}' '{opType}' '{rType.DisplayName()}'!", opToken);
                        expr = new ErrorExpression(lhs.StartPos, rhs.EndPos, Tokens.GetTokensInRange(lhs.StartPos, rhs.EndPos).ToArray());
                    }
                }
                else if (matches > 1)
                {
                    ParseError($"Ambiguous operator overload! {matches} equally valid possibilites for '{lType.DisplayName()}' '{opType}' '{rType.DisplayName()}'!", opToken);
                    expr = new ErrorExpression(lhs.StartPos, rhs.EndPos, Tokens.GetTokensInRange(lhs.StartPos, rhs.EndPos).ToArray());
                }
                else
                {
                    if (bestMatch.LeftOperand.VarType is Class {IsInterface: true} c)
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
                    
                    expr = new InOpReference(bestMatch, lhs, rhs, lhs.StartPos, rhs.EndPos);
                }
            }

            return expr;

            bool IsOperator(out bool isRightShift, out TokenType opType)
            {
                opType = CurrentToken.Type;

                //Lexer can't recognize >> as the right-shift operator, because of the conflicting array<delegate<delName>> syntax, so do it manually here
                isRightShift = false;
                if (CurrentToken.Type == TokenType.RightArrow && Tokens.LookAhead(1).Type == TokenType.RightArrow)
                {
                    //check to see if there is any whitespace between them. Otherwise > > would be recognized as the right shift operator! 
                    isRightShift = Tokens.LookAhead(1).StartPos == CurrentToken.StartPos + 1;
                    if (isRightShift)
                    {
                        opType = TokenType.RightShift;
                        return true;
                    }
                }
                else if (CurrentToken.Type is TokenType.Word)
                {
                    if (CurrentToken.Value.CaseInsensitiveEquals("Dot"))
                    {
                        opType = TokenType.DotProduct;
                        return true;
                    }
                    if (CurrentToken.Value.CaseInsensitiveEquals("Cross"))
                    {
                        opType = TokenType.CrossProduct;
                        return true;
                    }
                    if (CurrentToken.Value.CaseInsensitiveEquals("ClockwiseFrom"))
                    {
                        opType = TokenType.ClockwiseFrom;
                        return true;
                    }
                    return false;
                }
                return Symbols.InFixOperatorSymbols.Contains(opType);
            }
        }

        private Expression Unary()
        {
            var start = CurrentPosition;
            Expression expr;
            if (Consume(TokenType.Increment, TokenType.Decrement) is { } preFixToken)
            {
                preFixToken.SyntaxType = EF.Operator;
                expr = CompositeRef();
                if (expr is null)
                {
                    return null;
                }
                if (expr is DynArrayLength)
                {
                    ParseError($"The {LENGTH} property of a dynamic array can only be changed by direct assignment!", expr);
                }
                if (!IsLValue(expr))
                {
                    TypeError($"Cannot {(preFixToken.Type == TokenType.Increment ? "in" : "de")}crement an rvalue!", expr);
                }
                VariableType exprType = expr.ResolveType();
                if (exprType != SymbolTable.IntType && exprType != SymbolTable.ByteType)
                {
                    throw ParseError($"Only ints and bytes can be {(preFixToken.Type == TokenType.Increment ? "in" : "de")}cremented!", expr);
                }
                PreOpDeclaration opDeclaration = Symbols.GetPreOp(preFixToken.Type, exprType);
                return new PreOpReference(opDeclaration, expr, preFixToken.StartPos, expr.EndPos);
            }
            if (Matches(TokenType.ExclamationMark, EF.Operator))
            {
                expr = Unary();
                if (expr is null)
                {
                    return null;
                }
                VariableType exprType = expr.ResolveType();
                if (exprType != SymbolTable.BoolType)
                {
                    throw ParseError("'!' can only be used with expressions that evaluate to a boolean!", expr);
                }

                PreOpDeclaration opDeclaration = Symbols.GetPreOp(TokenType.ExclamationMark, exprType);
                return new PreOpReference(opDeclaration, expr, start, expr.EndPos);
            }
            if (Matches(TokenType.MinusSign, EF.Operator))
            {
                expr = Unary();
                if (expr is null)
                {
                    return null;
                }
                VariableType exprType = expr.ResolveType();
                if (exprType == SymbolTable.ByteType)
                {
                    exprType = SymbolTable.IntType;
                }
                switch (expr)
                {
                    case IntegerLiteral intLit:
                        intLit.Value *= -1;
                        intLit.NumType = INT;
                        return intLit;
                    case FloatLiteral floatLit:
                        floatLit.Value *= -1;
                        return floatLit;
                }

                if (exprType != SymbolTable.FloatType && exprType != SymbolTable.IntType && !(exprType.Name.CaseInsensitiveEquals("Vector") && exprType is not DynamicArrayType))
                {
                    throw ParseError("Unary '-' can only be used with expressions that evaluate to float, int, or Vector!", expr);
                }

                return new PreOpReference(Symbols.GetPreOp(TokenType.MinusSign, exprType), expr, start, expr.EndPos);
            }
            if (Matches(TokenType.Complement, EF.Operator))
            {
                expr = Unary();
                if (expr is null)
                {
                    return null;
                }
                if (expr is IntegerLiteral intLit)
                {
                    intLit.NumType = INT;
                }
                VariableType exprType = expr.ResolveType();
                if (exprType != SymbolTable.IntType)
                {
                    throw ParseError("'~' can only be used with expressions that evaluate to int!", expr);
                }

                PreOpDeclaration opDeclaration = Symbols.GetPreOp(TokenType.Complement, exprType);
                return new PreOpReference(opDeclaration, expr, start, expr.EndPos);
            }

            expr = CompositeRef();
            if (expr is null)
            {
                return null;
            }

            if (Consume(TokenType.Increment, TokenType.Decrement) is {} postFixToken)
            {
                postFixToken.SyntaxType = EF.Operator;
                if (expr is DynArrayLength)
                {
                    TypeError($"The {LENGTH} property of a dynamic array can only be changed by direct assignment!", expr);
                }
                VariableType exprType = expr.ResolveType();
                if (!IsLValue(expr))
                {
                    TypeError($"Cannot {(postFixToken.Type == TokenType.Increment ? "in" : "de")}crement an rvalue!", expr);
                }

                if (exprType != SymbolTable.IntType && exprType != SymbolTable.ByteType)
                {
                    TypeError($"Only ints and bytes can be {(postFixToken.Type == TokenType.Increment ? "in" : "de")}cremented!", expr);
                }
                PostOpDeclaration opDeclaration = Symbols.GetPostOp(postFixToken.Type, exprType);
                expr = new PostOpReference(opDeclaration, expr, expr.StartPos, postFixToken.EndPos);
            }

            return expr;
        }

        private static void AddConversion(VariableType destType, ref Expression expr)
        {
            if (expr is NoneLiteral noneLit)
            {
                if (destType.PropertyType == EPropertyType.Delegate)
                {
                    noneLit.IsDelegateNone = true;
                }
                else if (destType.PropertyType == EPropertyType.Interface)
                {
                    expr = new PrimitiveCast(ECast.ObjectToInterface, destType, noneLit, noneLit.StartPos, noneLit.EndPos);
                    return;
                }
            }
            if (expr?.ResolveType() is { } type && type.PropertyType != destType.PropertyType)
            {
                ECast cast = CastHelper.PureCastType(CastHelper.GetConversion(destType, type));
                switch (expr)
                {
                    case IntegerLiteral intLit:
                        switch (cast)
                        {
                            case ECast.ByteToInt:
                                intLit.NumType = INT;
                                return;
                            case ECast.IntToByte:
                                intLit.NumType = BYTE;
                                intLit.Value &= 0xFF;
                                return;
                            case ECast.IntToFloat:
                                expr = new FloatLiteral(intLit.Value, intLit.StartPos, intLit.EndPos);
                                return;
                            case ECast.ByteToFloat:
                                expr = new FloatLiteral(intLit.Value, intLit.StartPos, intLit.EndPos);
                                return;
                        }
                        break;
                    case FloatLiteral floatLit:
                        switch (cast)
                        {
                            case ECast.FloatToByte:
                                expr = new IntegerLiteral((int)floatLit.Value, floatLit.StartPos, floatLit.EndPos) { NumType = BYTE };
                                return;
                            case ECast.FloatToInt:
                                expr = new IntegerLiteral((int)floatLit.Value, floatLit.StartPos, floatLit.EndPos) { NumType = INT };
                                return;
                        }
                        break;
                    case ConditionalExpression condExpr:
                        AddConversion(destType, ref condExpr.TrueExpression);
                        AddConversion(destType, ref condExpr.FalseExpression);
                        return;
                }
                if ((byte)cast != 0 && cast != ECast.Max)
                {
                    expr = new PrimitiveCast(cast, destType, expr, expr.StartPos, expr.EndPos);
                }
            }
        }

        private Expression CompositeRef()
        {
            Expression result = InnerCompositeRef();

            //if this is a reference to a constant, we need to replace it with the constant's value
            if (result is SymbolReference {Node: Const c})
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

                if (InStaticFunction)
                {
                    if (lhs is FunctionCall { Function.Node: Function { IsStatic: false } })
                    {
                        TypeError("Cannot call an instance function from a static function!", lhs);
                    }
                    else if (lhs is DelegateCall { DelegateReference.Node: VariableDeclaration { Outer: Class } })
                    {
                        TypeError("Cannot call an instance delegate from a static function!", lhs);
                    }
                    else if (lhs is not DefaultReference && lhs is SymbolReference { Node: VariableDeclaration { Outer: Class}})
                    {
                        TypeError("Cannot access an instance variable from a static function!", lhs);
                    }
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
                    bool isStaticAccess = false;
                    if (Matches(CONST, EF.Keyword))
                    {
                        if (lhsType is not ClassType || lhs is not ObjectLiteral)
                        {
                            throw ParseError($"'{CONST} can only be used after a class literal'!", CurrentPosition);
                        }
                        if (!Matches(TokenType.Dot))
                        {
                            throw ParseError($"Expected '.' after '{CONST}'!", CurrentPosition);
                        }
                        isConst = true;
                    }
                    else if (CurrentIs(STATIC) && lhsType?.PropertyType == EPropertyType.Object)
                    {
                        CurrentToken.SyntaxType = EF.Keyword;
                        Tokens.Advance();
                        if (!Matches(TokenType.Dot))
                        {
                            throw ParseError($"Expected '.' after '{STATIC}'!", CurrentPosition);
                        }
                        isStaticAccess = true;
                    }

                    if (lhsType is not ClassType && !isStaticAccess && !CompositeTypes.Contains(lhsType?.NodeType ?? ASTNodeType.INVALID))
                    {
                        TypeError("Left side symbol is not of a composite type!", PrevToken.StartPos); //TODO: write a better error message
                    }
                    Class containingClass = NodeUtils.GetContainingClass(lhsType);
                    if (containingClass == null)
                    {
                        TypeError("Could not resolve type of expression!", lhs);
                    }
                    string specificScope = containingClass?.GetInheritanceString();
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
                    if (lhsType is not ClassType && lhsType != containingClass)
                    {
                        specificScope += $".{lhsType.Name}";
                    }

                    if (lhsType is ClassType && !isConst && !isStaticAccess && !CurrentIs(DEFAULT))
                    {
                        specificScope = "Object.Field.Struct.State.Class";
                    }
                    bool isStructMemberExpression = lhsType is Struct;
                    
                    if (CurrentIs(DEFAULT))
                    {
                        switch (lhsType)
                        {
                            case Class:
                                Tokens.AddDefinitionLink(lhsType, CurrentToken);
                                break;
                            case ClassType classType:
                                Tokens.AddDefinitionLink(classType.ClassLimiter, CurrentToken);
                                break;
                        }
                    }

                    ExpressionScopes.Push((specificScope, isStructMemberExpression));

                    Expression rhs = CallOrAccess(isStaticAccess);

                    ExpressionScopes.Pop();
                    
                    if (rhs is null)
                    {
                        throw ParseError("Expected a valid member name to follow the dot!", CurrentPosition);
                    }
                    //TODO: check if rhs is a type that makes sense eg. no int literals

                    if (isConst)
                    {
                        if (rhs is not SymbolReference {Node: Const})
                        {
                            TypeError("Expected member after 'const.' to be a Const!", rhs);
                        }
                    }

                    if (lhsType.PropertyType == EPropertyType.Interface && rhs is FunctionCall fc)
                    {
                        fc.IsCalledOnInterface = true;
                    }

                    if (isStructMemberExpression && rhs is DelegateCall)
                    {
                        TypeError("Cannot call delegate properties on a struct!", rhs);
                    }

                    switch (rhs)
                    {
                        case ArraySymbolRef asr:
                        {
                            bool isClassContext = lhsType is ClassType && (isStaticAccess || asr.Array is DefaultReference);
                            var csf = new CompositeSymbolRef(lhs, asr.Array, isClassContext, lhs.StartPos, asr.Array.EndPos)
                            {
                                IsStructMemberExpression = isStructMemberExpression
                            };
                            asr.StartPos = csf.StartPos;
                            asr.Array = csf;
                            lhs = asr;
                            break;
                        }
                        case DynArrayIterator dai:
                        {
                            bool isClassContext = lhsType is ClassType && (isStaticAccess || dai.DynArrayExpression is DefaultReference);
                            var csf = new CompositeSymbolRef(lhs, dai.DynArrayExpression, isClassContext, lhs.StartPos, dai.DynArrayExpression.EndPos)
                            {
                                IsStructMemberExpression = isStructMemberExpression
                            };
                            dai.StartPos = csf.StartPos;
                            dai.DynArrayExpression = csf;
                            lhs = dai;
                            break;
                        }
                        case DynArrayLength dal:
                        {
                            bool isClassContext = lhsType is ClassType && (isStaticAccess || dal.DynArrayExpression is DefaultReference);
                            var csf = new CompositeSymbolRef(lhs, dal.DynArrayExpression, isClassContext, lhs.StartPos, dal.DynArrayExpression.EndPos)
                            {
                                IsStructMemberExpression = isStructMemberExpression
                            };
                            dal.StartPos = csf.StartPos;
                            dal.DynArrayExpression = csf;
                            lhs = dal;
                            break;
                        }
                        case SymbolReference {Node: EnumValue ev} sr:
                            lhs = NewSymbolReference(ev, new ScriptToken(TokenType.Word, sr.Name, sr.StartPos, sr.EndPos), false);
                            break;
                        default:
                        {
                            bool isClassContext = lhsType is ClassType && (isStaticAccess || rhs is DefaultReference);
                            lhs = new CompositeSymbolRef(lhs, rhs, isClassContext, lhs.StartPos, rhs.EndPos)
                            {
                                IsStructMemberExpression = isStructMemberExpression
                            };
                            break;
                        }
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
            if (Matches(ADD, EF.Function))
            {
                ExpectLeftParen(ADD);
                Expression countArg = ValidateArgument("count", ADD, SymbolTable.IntType);
                ExpectRightParen();
                return new DynArrayAdd(dynArrayRef, countArg, dynArrayRef.StartPos, PrevToken.EndPos);
            }
            if (Matches(ADDITEM, EF.Function))
            {
                ExpectLeftParen(ADDITEM);
                Expression valueArg = ValidateArgument("value", ADDITEM, elementType);
                ExpectRightParen();
                return new DynArrayAddItem(dynArrayRef, valueArg, dynArrayRef.StartPos, PrevToken.EndPos);
            }
            if (Matches(INSERT, EF.Function))
            {
                ExpectLeftParen(INSERT);
                Expression indexArg = ValidateArgument("index", INSERT, SymbolTable.IntType);
                ExpectComma();
                Expression countArg = ValidateArgument("count", INSERT, SymbolTable.IntType);
                ExpectRightParen();
                return new DynArrayInsert(dynArrayRef, indexArg, countArg, dynArrayRef.StartPos, PrevToken.EndPos);
            }
            if (Matches(INSERTITEM, EF.Function))
            {
                ExpectLeftParen(INSERTITEM);
                Expression indexArg = ValidateArgument("index", INSERTITEM, SymbolTable.IntType);
                ExpectComma();
                Expression valueArg = ValidateArgument("value", INSERTITEM, elementType);
                ExpectRightParen();
                return new DynArrayInsertItem(dynArrayRef, indexArg, valueArg, dynArrayRef.StartPos, PrevToken.EndPos);
            }
            if (Matches(REMOVE, EF.Function))
            {
                ExpectLeftParen(REMOVE);
                Expression indexArg = ValidateArgument("index", REMOVE, SymbolTable.IntType);
                ExpectComma();
                Expression countArg = ValidateArgument("count", REMOVE, SymbolTable.IntType);
                ExpectRightParen();
                return new DynArrayRemove(dynArrayRef, indexArg, countArg, dynArrayRef.StartPos, PrevToken.EndPos);
            }
            if (Matches(REMOVEITEM, EF.Function))
            {
                ExpectLeftParen(REMOVEITEM);
                Expression valueArg = ValidateArgument("value", REMOVEITEM, elementType);
                ExpectRightParen();
                return new DynArrayRemoveItem(dynArrayRef, valueArg, dynArrayRef.StartPos, PrevToken.EndPos);
            }
            if (Matches(FIND, EF.Function))
            {
                ExpectLeftParen(FIND);
                if (elementType is Struct s)
                {
                    Expression memberNameArg = ParseExpression();
                    if (memberNameArg == null)
                    {
                        throw ParseError("Expected function argument!", CurrentPosition);
                    }

                    if (memberNameArg is not NameLiteral nameLiteral)
                    {
                        ParseError($"Expected 'membername' argument to '{FIND}' to be a name literal!", memberNameArg);
                        nameLiteral = new NameLiteral("");
                    }

                    if (s.VariableDeclarations.FirstOrDefault(varDecl => varDecl.Name.CaseInsensitiveEquals(nameLiteral.Value)) is not VariableDeclaration variableDeclaration)
                    {
                        ParseError($"Struct '{s.Name}' does not have a member named '{nameLiteral.Value}'!", memberNameArg);
                        variableDeclaration = null;
                    }

                    ExpectComma();
                    Expression valueArg = ValidateArgument("value", FIND, variableDeclaration?.VarType);
                    ExpectRightParen();
                    return new DynArrayFindStructMember(dynArrayRef, memberNameArg, valueArg, dynArrayRef.StartPos, PrevToken.EndPos)
                    {
                        MemberType = variableDeclaration?.VarType
                    };
                }
                else
                {
                    Expression valueArg = ValidateArgument("value", FIND, elementType);
                    ExpectRightParen();
                    return new DynArrayFind(dynArrayRef, valueArg, dynArrayRef.StartPos, PrevToken.EndPos);
                }
            }
            else if (Matches(SORT, EF.Function))
            {
                if (Game <= MEGame.ME2) //TODO: verify sort exists for LE1/2
                {
                    throw ParseError($"'{SORT}' is not a valid dynamic array function in {Game}", CurrentPosition);
                }
                ExpectLeftParen(SORT);
                Expression comparefunctionArg = ParseExpression();
                if (comparefunctionArg == null)
                {
                    throw ParseError("Expected function argument!", CurrentPosition);
                }

                bool correctType = false;
                if (comparefunctionArg.ResolveType() is DelegateType delType)
                {
                    Function delFunc = delType.DefaultFunction;
                    if (delFunc.ReturnType == SymbolTable.IntType && delFunc.Parameters.Count == 2 && TypeCompatible(delFunc.Parameters[0].VarType, elementType, comparefunctionArg.StartPos)
                                                                                                   && TypeCompatible(delFunc.Parameters[1].VarType, elementType, comparefunctionArg.StartPos))
                    {
                        correctType = true;
                    }
                }

                if (!correctType)
                {
                    TypeError($"Expected 'comparefunction' argument to '{SORT}' to be a delegate that takes two parameters of type '{elementType.DisplayName()}' and returns an int!", comparefunctionArg);
                }
                
                ExpectRightParen();
                return new DynArraySort(dynArrayRef, comparefunctionArg, dynArrayRef.StartPos, PrevToken.EndPos);
            }
            else
            {
                throw ParseError("Expected a dynamic array operation!", CurrentPosition);
            }

            void ExpectLeftParen(string funcName)
            {
                if (!Matches(TokenType.LeftParenth))
                {
                    throw ParseError($"Expected '(' after '{funcName}'!", CurrentPosition);
                }
            }
            void ExpectComma()
            {
                if (!Matches(TokenType.Comma))
                {
                    throw ParseError($"Expected ',' after argument!", CurrentPosition);
                }
            }
            void ExpectRightParen()
            {
                if (!Matches(TokenType.RightParenth))
                {
                    throw ParseError($"Expected ')' after argument list!", CurrentPosition);
                }
            }

            Expression ValidateArgument(string argumentName, string functionName, VariableType expectedType)
            {
                Expression arg = ParseExpression();
                if (arg == null)
                {
                    throw ParseError("Expected function argument!", CurrentPosition);
                }

                if (!TypeCompatible(expectedType, arg.ResolveType(), arg.StartPos))
                {
                    if (expectedType is not DelegateType) //seems wrong, but required to parse bioware classes, so...
                    {
                        TypeError($"Expected '{argumentName}' argument to '{functionName}' to evaluate to '{expectedType.DisplayName()}'!", arg);
                    }
                }
                AddConversion(expectedType, ref arg);
                return arg;
            }
        }

        public Expression CallOrAccess(bool isStaticAccess = false)
        {
            Expression expr = MetaCast();
            if (expr is null)
            {
                return null;
            }

            if (isStaticAccess)
            {
                if (expr is not SymbolReference {Node: Function { IsStatic: true } })
                {
                    TypeError("'static.' can only be used for calling a function with the 'static' modifier!", expr);
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
            if (exprType is not DynamicArrayType && exprType is not StaticArrayType)
            {
                TypeError("Can only use array access operator on an array!", expr);
            }

            ExpressionScopes.Push(ExpressionScopes.Last());
            Expression arrIndex = ParseExpression();
            ExpressionScopes.Pop();
            if (arrIndex == null)
            {
                throw ParseError("Expected an array index!", CurrentPosition);
            }

            //basic sanity checking for literal indexes
            if (arrIndex is IntegerLiteral intLiteral)
            {
                intLiteral.NumType = INT;
                if (intLiteral.Value < 0)
                {
                    TypeError("Array index cannot be negative!", intLiteral);
                }
                if (exprType is StaticArrayType staticArrayType && intLiteral.Value >= staticArrayType.Length)
                {
                    TypeError("Array index cannot be outside bounds of static array size!", intLiteral);
                }
            }

            VariableType arrIndexType = arrIndex.ResolveType();
            if (arrIndexType != SymbolTable.IntType && arrIndexType != SymbolTable.ByteType)
            {
                TypeError("Array index must be or evaluate to an integer!", arrIndex);
            }

            if (Consume(TokenType.RightSqrBracket) is {} endTok)
            {
                expr = new ArraySymbolRef(expr, arrIndex, expr.StartPos, endTok.EndPos);
                //marking the ] as being of the array element type so that autocomplete will work
                Tokens.AddDefinitionLink(expr.ResolveType(), endTok);
            }
            else
            {
                throw ParseError("Expected ']'!", CurrentPosition);
            }

            return expr;
        }

        private Expression FinishCall(Expression expr, out bool succeeded)
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
                    case VariableDeclaration {VarType: DelegateType delType}:
                        isDelegateCall = true;
                        func = delType.DefaultFunction;
                        break;
                }
                if (func != null)
                {
                    if (func.Flags.Has(EFunctionFlags.Latent))
                    {
                        if (Node is not State)
                        {
                            TypeError($"Can only call Latent functions from {STATE} code!", funcRef);
                        }
                        else if (InForEachBody)
                        {
                            TypeError($"Cannot call Latent functions from within a {FOREACH}!", funcRef);
                        }
                    }
                    var arguments = new List<Expression>();
                    ExpressionScopes.Push(ExpressionScopes.Last());
                    for (int i = 0; i < func.Parameters.Count; i++)
                    {
                        FunctionParameter p = func.Parameters[i];
                        if (i == func.Parameters.Count - 1 ? CurrentIs(TokenType.RightParenth) : Matches(TokenType.Comma))
                        {
                            if (p.IsOptional)
                            {
                                arguments.Add(null);
                                continue;
                            }

                            ParseError("Missing non-optional argument!", CurrentPosition);
                        }

                        var paramStartPos = CurrentPosition;
                        Expression currentArg = ParseExpression();

                        if (currentArg == null)
                        {
                            bool remainingArgsAreOptional = true;
                            for (int j = i; j < func.Parameters.Count; j++)
                            {
                                remainingArgsAreOptional &= func.Parameters[j].IsOptional;
                                arguments.Add(null);
                            }
                            if (remainingArgsAreOptional)
                            {
                                break;
                            }
                            throw ParseError($"Expected an argument of type '{p.VarType.DisplayName()}'!", paramStartPos);
                        }

                        VariableType argType = currentArg.ResolveType();
                        if (!TypeCompatible(p.VarType, argType, currentArg.StartPos, p.Flags.Has(EPropertyFlags.CoerceParm)))
                        {
                            TypeError($"Expected an argument of type '{p.VarType.DisplayName()}'!", currentArg);
                        }

                        AddConversion(p.VarType, ref currentArg);

                        if (p.IsOut && !p.IsConst)
                        {
                            void CheckIfConst(SymbolReference symbolReference)
                            {
                                if (symbolReference.Node is VariableDeclaration varDecl)
                                {
                                    if (varDecl.IsConst)
                                    {
                                        TypeError("Argument given to an out parameter cannot be 'const'!", symbolReference);
                                    }
                                    if (ReferenceEquals(varDecl, SelfDeclaration))
                                    {
                                        TypeError($"{SELF} is immutable! It cannot be given as an argument to an out parameter.", symbolReference);
                                    }
                                }
                            }

                            switch (currentArg)
                            {
                                case SymbolReference symRef:
                                    CheckIfConst(symRef);
                                    break;
                                case ConditionalExpression { TrueExpression: SymbolReference trueSymRef, FalseExpression: SymbolReference falseSymRef}:
                                    CheckIfConst(trueSymRef);
                                    CheckIfConst(falseSymRef);
                                    break;
                                default:
                                    TypeError("Argument given to an out parameter must be an lvalue!", currentArg);
                                    break;
                            }

                            if (currentArg is ArraySymbolRef {IsDynamic: true} 
                                || currentArg is ConditionalExpression cndExp && (cndExp.TrueExpression is ArraySymbolRef { IsDynamic: true } || cndExp.FalseExpression is ArraySymbolRef { IsDynamic: true}))
                            {
                                Log.LogWarning("Argument given to an out parameter should not be a dynamic array element!\n" +
                                               "Modification of the parameter inside the function will NOT modify the array.", currentArg.StartPos, currentArg.EndPos);
                            }

                            VariableType parmType = p.VarType;
                            if (func.Flags.Has(EFunctionFlags.Iterator) && arguments.Count > 0 && arguments[0]?.ResolveType() is ClassType cType &&
                                parmType is Class baseClass && func.Parameters[0].VarType is ClassType originalClassType && baseClass.SameAsOrSubClassOf(originalClassType.ClassLimiter.Name))
                            {
                                parmType = cType.ClassLimiter;
                            }
                            if (!(parmType is Enumeration && currentArg is IntegerLiteral) && !NodeUtils.TypeEqual(parmType, argType))
                            {
                                if (parmType is DynamicArrayType {ElementType: Class classA} && argType is DynamicArrayType {ElementType: Class classB} && classA.SameAsOrSubClassOf(classB))
                                {
                                    Log.LogWarning($"Array types mismatch, but '{classA.Name}' is a subclass of '{classB.Name}' so this might work. Google 'Array Covariance' for why this is dangerous.", currentArg.StartPos, currentArg.EndPos);
                                }
                                else
                                {
                                    TypeError($"Expected an argument of type '{p.VarType.DisplayName()}'! Arguments given to an out parameter must be the exact same type.", currentArg);
                                }
                            }
                        }

                        arguments.Add(currentArg);
                        if (Consume(TokenType.Comma) == null) break;
                    }

                    ExpressionScopes.Pop();
                    if (arguments.Count != func.Parameters.Count)
                    {
                        if (func.HasOptionalParms)
                        {
                            int numRequiredParams = func.Parameters.Count(param => !param.IsOptional);
                            if (arguments.Count > func.Parameters.Count || arguments.Count < numRequiredParams)
                            {
                                ParseError($"Expected between {numRequiredParams} and {func.Parameters.Count} parameters to function '{func.Name}'!", funcRef);
                            }
                            else
                            {
                                for (int i = arguments.Count; i < func.Parameters.Count; i++)
                                {
                                    arguments.Add(null);
                                }
                            }
                        }
                        else
                        {
                            ParseError($"Expected {func.Parameters.Count} parameters to function '{func.Name}'!", funcRef);
                        }
                    }

                    if (!Matches(TokenType.RightParenth)) throw ParseError("Expected ')'!", CurrentPosition);

                    if (func.ReturnType is not null)
                    {
                        //hack to make autocomplete work
                        Tokens.AddDefinitionLink(func.ReturnType, PrevToken);
                    }

                    if (isDelegateCall)
                    {
                        return new DelegateCall(funcRef, arguments, funcRef.StartPos, PrevToken.EndPos);
                    }
                    return new FunctionCall(funcRef, arguments, funcRef.StartPos, PrevToken.EndPos);
                }
            }

            if (InForEachIterator)
            {
                return Iterator(expr);
            }

            //bit hacky. dynamic cast when the typename is also a variable name in this scope
            if (NotInContext && expr.GetType() == typeof(SymbolReference) && Symbols.TryGetType(((SymbolReference)expr).Name, out VariableType destType))
            {
                ScriptToken typeToken = null;
                foreach (ScriptToken token in Tokens)
                {
                    if (token.StartPos.Equals(expr.StartPos) && token.EndPos.Equals(expr.EndPos))
                    {
                        typeToken = token;
                        typeToken.SyntaxType = destType is Struct ? EF.Struct : EF.Class;
                        Tokens.AddDefinitionLink(destType, typeToken);
                    }
                }
                return ParsePrimitiveOrDynamicCast(typeToken ?? new ScriptToken(TokenType.Word, destType.Name, expr.StartPos, expr.EndPos), destType);
            }

            if (InNew)
            {
                Tokens.Advance(-1);
                succeeded = true;
                return expr;
            }

            //if there is a collision between a function name and another symbol, this may be required to disambiguate
            if (expr is SymbolReference notFunc && Symbols.TryGetSymbolInScopeStack(notFunc.Name, out Function secondChanceFunc, ExpressionScopes.Peek().scope))
            {
                return FinishCall(NewSymbolReference(secondChanceFunc, new ScriptToken(TokenType.Word, notFunc.Name, expr.StartPos, expr.EndPos), expr is DefaultReference), out succeeded);
            }

            throw ParseError("Can only call functions and delegates!", expr);
        }

        private Expression Iterator(Expression expr)
        {
            if (expr.ResolveType() is DynamicArrayType dynArrType)
            {
                ExpressionScopes.Push(ExpressionScopes.Last());

                Expression valueArg = CompositeRef() ?? throw ParseError("Expected argument to dynamic array iterator!", CurrentPosition);
                if (!NodeUtils.TypeEqual(valueArg.ResolveType(), dynArrType.ElementType) && (Game.IsGame3() ||
                    //documentation says this shouldn't be allowed, but bioware code does this in ME2
                    !(valueArg.ResolveType() is Class argClass && dynArrType.ElementType is Class dynArrClass && dynArrClass.SameAsOrSubClassOf(argClass))))
                {
                    string elementType = dynArrType.ElementType.DisplayName();
                    TypeError($"Iterator variable for an '{ARRAY}<{elementType}>' must be of type '{elementType}'", expr);
                }
                if (valueArg is not SymbolReference)
                {
                    TypeError("Iterator variable must be an lvalue!", valueArg);
                }

                Expression indexArg = null;
                if (!Matches(TokenType.RightParenth))
                {
                    if (!Matches(TokenType.Comma))
                    {
                        throw ParseError("Expected either a ')' after the first argument, or a ',' before a second argument!", CurrentPosition);
                    }

                    if (!Matches(TokenType.RightParenth))
                    {
                        indexArg = CompositeRef() ?? throw ParseError("Expected argument to dynamic array iterator!", CurrentPosition);
                        if (indexArg.ResolveType() != SymbolTable.IntType)
                        {
                            TypeError("Index variable must be an int!", indexArg);
                        }
                        if (indexArg is not SymbolReference)
                        {
                            TypeError("Index variable must be an lvalue!", valueArg);
                        }

                        if (!Matches(TokenType.RightParenth))
                        {
                            throw ParseError("Expected a ')' after second argument!", CurrentPosition);
                        }
                    }
                }
                ExpressionScopes.Pop();
                return new DynArrayIterator(expr, (SymbolReference)valueArg, (SymbolReference)indexArg, expr.StartPos, PrevToken.EndPos);
            }

            throw ParseError($"Expected an iterator function or dynamic array after {FOREACH}!", expr);
        }

        private Expression MetaCast()
        {
            if (CurrentIs(CLASS) && Tokens.LookAhead(1).Type == TokenType.LeftArrow)
            {
                //metacast
                var castToken = Consume(CLASS);
                castToken.SyntaxType = EF.Keyword;
                Consume(TokenType.LeftArrow);
                if (Consume(TokenType.Word) is { } limiter)
                {
                    if (!Matches(TokenType.RightArrow))
                    {
                        throw ParseError("Expected '>' after limiter class!", CurrentPosition);
                    }

                    if (Symbols.TryGetType(limiter.Value, out VariableType destType) && destType is Class limiterType)
                    {
                        limiter.SyntaxType = EF.Class;
                        Tokens.AddDefinitionLink(limiterType, limiter);
                        if (!Matches(TokenType.LeftParenth))
                        {
                            throw ParseError("Expected '(' at start of cast!", CurrentPosition);
                        }
                        Expression expr = ParseExpression();
                        if (!Matches(TokenType.RightParenth))
                        {
                            throw ParseError("Expected ')' at end of cast expression!", CurrentPosition);
                        }
                        var exprType = expr.ResolveType();
                        if (exprType is ClassType exprClassType)
                        {
                            if (exprClassType.ClassLimiter == limiterType)
                            {
                                TypeError("Cannot cast to same type!", expr);
                            }
                            else if (!limiterType.SameAsOrSubClassOf(exprClassType.ClassLimiter.Name))
                            {
                                if (((Class)exprClassType.ClassLimiter).SameAsOrSubClassOf(limiterType.Name))
                                {
                                    TypeError("Casting to a less-derived type is pointless!", expr);
                                }
                                else
                                {
                                    TypeError("Cannot cast to an unrelated type!", expr);
                                }
                            }
                        }
                        else if (exprType is not Class classType || !classType.Name.CaseInsensitiveEquals(OBJECT))
                        {
                            TypeError("Cannot cast to a class type from a non-class type!", expr);
                        }
                        //TODO: different AST type for Metaclass?
                        return new CastExpression(new ClassType(destType), expr, castToken.StartPos, PrevToken.EndPos);
                    }

                    throw ParseError($"'{limiter.Value}' is not a Class!", CurrentPosition);
                }

                throw ParseError("Expecting class name in class limiter!", CurrentPosition);
            }

            return Primary();
        }

        private Expression Primary()
        {
            Expression literal = ParseLiteral();
            if (literal != null)
            {
                return literal;
            }

            ScriptToken token = CurrentToken;
            if (NotInContext)
            {
                if (Matches(SELF, EF.Keyword))
                {
                    Tokens.AddDefinitionLink(Self, PrevToken);
                    if (InStaticFunction)
                    {
                        TypeError($"'{SELF}' cannot be used in a static function!");
                    }
                    return new SymbolReference(SelfDeclaration, SELF, token.StartPos, token.EndPos);
                }

                if (Matches(NEW, EF.Keyword))
                {
                    return ParseNew();
                }

                if (Matches(SUPER, EF.Keyword))
                {
                    return ParseSuper();
                }

                if (Matches(GLOBAL, EF.Keyword))
                {
                    Tokens.AddDefinitionLink(Self, PrevToken);
                    if (!Matches(TokenType.Dot))
                    {
                        throw ParseError($"Expected '.' after '{GLOBAL}'!", CurrentPosition);
                    }

                    bool isState = false;
                    if (Node.Outer is State)
                    {
                        isState = true;
                        ExpressionScopes.Push((Self.GetInheritanceString(), false));
                    }
                
                    if (!Matches(TokenType.Word))
                    {
                        throw ParseError($"Expected function name after '{GLOBAL}'!", CurrentPosition);
                    }

                    ScriptToken funcName = PrevToken;
                    funcName.SyntaxType = EF.Function;
                    var basicRef = ParseBasicRefOrCast(funcName) as SymbolReference;
                    if (basicRef?.Node is Function func)
                    {
                        CheckAccesibility(func, funcName);
                    }
                    else
                    {
                        throw ParseError($"Expected function name after '{GLOBAL}'!", basicRef?.StartPos ?? CurrentPosition, basicRef?.EndPos ?? -1);
                    }

                    basicRef.IsGlobal = true;

                    if (isState)
                    {
                        ExpressionScopes.Pop();
                    }
                    return basicRef;
                }
            }

            bool isDefaultRef = false;
            if (Matches(DEFAULT, EF.Keyword))
            {
                if (NotInContext)
                {
                    Tokens.AddDefinitionLink(Self, PrevToken);
                }
                if (!Matches(TokenType.Dot))
                {
                    throw ParseError($"Expected '.' after '{DEFAULT}'!", CurrentPosition);
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
                throw ParseError("Expected property name after 'default.'!", CurrentPosition);
            }

            if (NotInContext && Matches(TokenType.LeftParenth))
            {
                Expression expr = Ternary();
                if (expr == null)
                {
                    throw ParseError("Expected expression after '('!", CurrentPosition);
                }
                if (!Matches(TokenType.RightParenth))
                {
                    throw ParseError("Expected closing ')' after expression!", token.StartPos, CurrentPosition);
                }

                return expr;
            }

            return null;
            //currently making callers handle nulls, which allows for more specific error messages
            throw ParseError("Expected Expression!");
        }

        private bool NotInContext => ExpressionScopes.Count == 1 || ExpressionScopes.First() == ExpressionScopes.Last();

        private Expression ParseSuper()
        {
            var superToken = PrevToken;
            Class superClass;
            Class superSpecifier = null;
            State state = Node switch
            {
                State s => s,
                Function { Outer: State s2 } => s2,
                _ => null
            };
            try
            {
                if (Matches(TokenType.LeftParenth))
                {
                    if (Consume(TokenType.Word) is {} className)
                    {
                        if (!Symbols.TryGetType(className.Value, out VariableType vartype))
                        {
                            throw ParseError($"No class named '{className.Value}' found!", className);
                        }
                        className.SyntaxType = EF.Class;

                        if (vartype is not Class super)
                        {
                            throw ParseError($"'{vartype.DisplayName()}' is not a class!", className);
                        }

                        Tokens.AddDefinitionLink(super, className);
                        superSpecifier = super;
                        superClass = super;
                        if (Self == superClass)
                        {
                            LogWarning($"'Super({superClass.Name}).' does nothing, this function is already in '{superClass.Name}'. Use an unqualified 'Super.' instead.");
                        }
                        if (!Self.SameAsOrSubClassOf(superClass))
                        {
                            TypeError($"'{superClass.Name}' is not a superclass of '{Self.Name}'!", className);
                        }
                    }
                    else
                    {
                        throw ParseError("Expected superclass specifier after '('!", CurrentPosition);
                    }

                    if (!Matches(TokenType.RightParenth))
                    {
                        throw ParseError("Expected ')' after superclass specifier!", CurrentPosition);
                    }
                    if (state?.Parent != null)
                    {
                        state = state.Parent;
                    }
                }
                else
                {
                    if (state?.Parent != null)
                    {
                        superClass = Self;
                        state = state.Parent;
                    }
                    else if (state is not null)
                    {
                        state = null;
                        superClass = Self;
                    }
                    else
                    {
                        state = null;
                        superClass = Self.Parent as Class ?? throw ParseError($"Can't use '{SUPER}' in a class with no parent!", PrevToken);
                    }
                }

                if (!Matches(TokenType.Dot) || !Matches(TokenType.Word))
                {
                    throw ParseError($"Expected function name after '{SUPER}'!", CurrentPosition);
                }

                ScriptToken functionName = PrevToken;
                functionName.SyntaxType = EF.Function;
                string specificScope;
                //try to find function in parent states
                while (state != null)
                {
                    var stateClass = (Class)state.Outer;
                    if (stateClass != superClass && stateClass.SameAsOrSubClassOf(superClass))
                    {
                        //Walk up the state inheritance chain until we get to one that is in the specified superclass (or an ancestor)
                        state = state.Parent;
                        continue;
                    }
                    specificScope = $"{stateClass.GetInheritanceString()}.{state.Name}";
                    if (Symbols.TryGetSymbolInScopeStack(functionName.Value, out ASTNode funcNode, specificScope) && funcNode is Function)
                    {
                        Tokens.AddDefinitionLink(funcNode, functionName);
                        Tokens.AddDefinitionLink(funcNode.Outer, superToken);
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
                    throw ParseError($"No function named '{functionName.Value}' found in a superclass!", functionName);
                }

                if (symbol is Function func)
                {
                    CheckAccesibility(func, functionName);
                }
                else
                {
                    TypeError($"Expected function name after '{SUPER}'!", functionName);
                }

                Tokens.AddDefinitionLink(symbol, functionName);
                Tokens.AddDefinitionLink(symbol.Outer, superToken);
                return new SymbolReference(symbol, functionName.Value, functionName.StartPos, functionName.EndPos)
                {
                    IsSuper = true,
                    SuperSpecifier = superSpecifier
                };
            }
            catch
            {
                //make sure there's a definition link even for incomplete super expressions. This enables code completion.
                if (Self.Parent is Class super)
                {
                    Tokens.AddDefinitionLink(super, superToken);
                }
                throw;
            }
        }

        private Expression ParseNew()
        {
            ScriptToken token = PrevToken;
            Expression outerObj = null;
            Expression objName = null;
            Expression flags = null;

            if (Matches(TokenType.LeftParenth))
            {
                outerObj = ParseExpression();
                if (outerObj == null)
                {
                    throw ParseError($"Expected 'outerobject' argument to '{NEW}' expression!", CurrentPosition);
                }

                if (Matches(TokenType.Comma))
                {
                    objName = ParseExpression();
                    if (objName == null)
                    {
                        throw ParseError($"Expected 'name' argument to '{NEW}' expression!", CurrentPosition);
                    }

                    if (!TypeCompatible(SymbolTable.StringType, objName.ResolveType(), objName.StartPos))
                    {
                        TypeError($"The 'name' argument to a '{NEW}' expression must be a string!", objName);
                    }

                    if (Matches(TokenType.Comma))
                    {
                        flags = ParseExpression();
                        if (flags == null)
                        {
                            throw ParseError($"Expected 'flags' argument to '{NEW}' expression!", CurrentPosition);
                        }

                        if (!TypeCompatible(SymbolTable.IntType, flags.ResolveType(), objName.StartPos))
                        {
                            TypeError($"The 'flags' argument to a '{NEW}' expression must be an int!", flags);
                        }
                    }
                }

                if (!Matches(TokenType.RightParenth))
                {
                    throw ParseError($"Expected ')' at end of '{NEW}' expression's argument list!");
                }
            }

            InNew = true;
            Expression objClass = ParseExpression();
            InNew = false;
            if (objClass == null)
            {
                throw ParseError($"Expected '{NEW}' expression's class type!", CurrentPosition);
            }

            if ((objClass.ResolveType() as ClassType)?.ClassLimiter is not Class newClass)
            {
                throw ParseError($"'{NEW}' expression must specify a class type!", objClass); //TODO: write better error message
            }

            if (newClass.SameAsOrSubClassOf("Actor"))
            {
                TypeError($"'{newClass.Name}' is a subclass of 'Actor'! Use the 'Spawn' function to create new 'Actor' instances.", objClass);
            }

            var outerObjType = outerObj?.ResolveType();
            if (outerObjType != null)
            {
                if (outerObjType is not Class outerClass || !outerClass.SameAsOrSubClassOf(newClass.OuterClass.Name))
                {
                    TypeError($"OuterObject argument for a '{NEW}' expression of type '{newClass.Name}' must be an object of class '{newClass.OuterClass.Name}'!", outerObj);
                }
            }

            Expression template = null;
            if (Matches(TokenType.LeftParenth))
            {
                template = ParseExpression();
                if (template == null)
                {
                    throw ParseError($"Expected 'template' argument to '{NEW}' expression!", CurrentPosition);
                }

                var templateType = template.ResolveType();
                if (templateType is not Class templateClass || !newClass.SameAsOrSubClassOf(templateClass))
                {
                    TypeError($"Template argument for a '{NEW}' expression of type '{newClass.Name}' must be an object of that class or a parent class!");
                }

                if (!Matches(TokenType.RightParenth))
                {
                    throw ParseError($"Expected ')' after 'template' argument in '{NEW}' expression!", CurrentPosition);
                }
            }

            return new NewOperator(outerObj, objName, flags, objClass, template, token.StartPos, PrevToken.EndPos);
        }

        private Expression ParseBasicRefOrCast(ScriptToken token, bool isDefaultRef = false)
        {
            (string specificScope, bool isStructScope) = ExpressionScopes.Peek();
            if (!Symbols.TryGetSymbolInScopeStack(token.Value, out ASTNode symbol, specificScope))
            {
                //primitive or dynamic cast, or enum
                if (NotInContext && !isDefaultRef && Symbols.TryGetType(token.Value, out VariableType destType))
                {
                    Tokens.AddDefinitionLink(destType, token);
                    if (destType is Enumeration enm && Matches(TokenType.Dot))
                    {
                        token.SyntaxType = EF.Enum;
                        if (Consume(TokenType.Word) is {} enumValName 
                         && enm.Values.FirstOrDefault(val => val.Name.CaseInsensitiveEquals(enumValName.Value)) is EnumValue enumValue)
                        {
                            Tokens.AddDefinitionLink(enm, enumValName);
                            return NewSymbolReference(enumValue, enumValName, false);
                        }
                        throw ParseError("Expected valid enum value!", CurrentPosition);
                    }
                    if (destType is Const cnst)
                    {
                        return cnst.Literal;
                    }
                    if (!Matches(TokenType.LeftParenth))
                    {
                        throw ParseError("Expected '(' after typename in cast expression!", CurrentPosition);
                    }

                    token.SyntaxType = SymbolTable.IsPrimitive(destType) ? EF.Keyword :
                                                      destType is Struct ? EF.Struct :
                                                                           EF.Class;
                    return ParsePrimitiveOrDynamicCast(token, destType);
                }
                else if (!isDefaultRef && Symbols.TryGetType(token.Value, out destType) && destType is Const cnst)
                {
                    Tokens.AddDefinitionLink(destType, token);
                    symbol = cnst;
                }
                else
                {
                    //TODO: better error message
                    TypeError($"{specificScope} has no member named '{token.Value}'!", token);
                    symbol = new VariableType("ERROR");
                }
            }

            if (isDefaultRef && symbol.Outer is Function)
            {
                TypeError($"{(symbol is FunctionParameter ? "Parameters" : "Local variables")} do not have default values!", token);
            }

            if (isStructScope && symbol.Outer is not Struct)
            {
                TypeError($"{specificScope} has no member named '{token.Value}'!", token);
                symbol = new VariableType("ERROR");
            }
            
            if (symbol is Function func)
            {
                CheckAccesibility(func, token);
            }
            return NewSymbolReference(symbol, token, isDefaultRef);
        }

        private void CheckAccesibility(Function func, ScriptToken token)
        {
            if (func.Flags.Has(EFunctionFlags.Private) || func.Flags.Has(EFunctionFlags.Protected))
            {
                Class symbolClass = NodeUtils.GetContainingClass(func);
                if (symbolClass != Self)
                {
                    if (func.Flags.Has(EFunctionFlags.Private))
                    {
                        LogWarning($"'{func.Name}' is a private function in '{symbolClass.Name}'! You should not call it from another class.", token);
                    }
                    else if (!Self.SameAsOrSubClassOf(symbolClass.Name))
                    {
                        LogWarning($"'{func.Name}' is a protected function in '{symbolClass.Name}'! You should only call it from a subclass.", token);
                    }
                }
            }
        }

        private Expression ParsePrimitiveOrDynamicCast(ScriptToken token, VariableType destType)
        {
            ScriptToken castToken = token;

            Expression expr = ParseExpression();
            if (expr is null)
            {
                throw ParseError("Expected expression!", CurrentToken);
            }
            if (!Matches(TokenType.RightParenth))
            {
                throw ParseError("Expected ')' at end of cast expression!", CurrentPosition);
            }
            //marking the ) as being of the cast type so that autocomplete will work
            Tokens.AddDefinitionLink(destType, PrevToken);

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
                if (!srcClass.SameAsOrSubClassOf(destClass) && !destClass.SameAsOrSubClassOf(srcClass) && !isInterfaceCast)
                {
                    TypeError($"Cannot cast between unrelated classes '{srcClass.Name}' and '{destClass.Name}'!", castToken.StartPos, CurrentPosition);
                }

                return new CastExpression(destType, expr, castToken.StartPos, CurrentPosition)
                {
                    IsInterfaceCast = isInterfaceCast
                };
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
                TypeError($"Cannot cast from '{exprType.DisplayName()}' to '{destType.DisplayName()}'!", castToken.StartPos, CurrentPosition);
            }

            return new PrimitiveCast(CastHelper.PureCastType(cast), destType, expr, castToken.StartPos, CurrentPosition);
        }

        #endregion
        
        private static bool IsLValue(Expression expr)
        {
            //TODO: is this correct?
            return expr is SymbolReference or DynArrayLength;
        }
    }
}
