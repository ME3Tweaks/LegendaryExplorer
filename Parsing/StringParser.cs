using ME3Script.Compiling.Errors;
using ME3Script.Language;
using ME3Script.Language.Tree;
using ME3Script.Lexing;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ME3Script.Parsing
{
    public class StringParser
    {
        private MessageLog Log;
        private TokenStream<String> Tokens;
        private TokenType CurrentTokenType 
            { get { return Tokens.CurrentItem.Type; } }
        private SourcePosition CurrentPosition
            { get { return Tokens.CurrentItem.StartPosition; } }

        private List<ASTNodeType> SemiColonExceptions = new List<ASTNodeType>
        {
            ASTNodeType.WhileLoop,
            ASTNodeType.ForLoop,
            ASTNodeType.IfStatement
        };

        #region Specifier Categories
        private List<TokenType> VariableSpecifiers = new List<TokenType>
        {
            TokenType.ConfigSpecifier,
            TokenType.GlobalConfigSpecifier,
            TokenType.LocalizedSpecifier,
            TokenType.ConstSpecifier,
            TokenType.PrivateSpecifier,
            TokenType.ProtectedSpecifier,
            TokenType.PrivateWriteSpecifier,
            TokenType.ProtectedWriteSpecifier,
            TokenType.RepNotifySpecifier,
            TokenType.DeprecatedSpecifier,
            TokenType.InstancedSpecifier,
            TokenType.DatabindingSpecifier,
            TokenType.EditorOnlySpecifier,
            TokenType.NotForConsoleSpecifier,
            TokenType.EditConstSpecifier,
            TokenType.EditFixedSizeSpecifier,
            TokenType.EditInlineSpecifier,
            TokenType.EditInlineUseSpecifier,
            TokenType.NoClearSpecifier,
            TokenType.InterpSpecifier,
            TokenType.InputSpecifier,
            TokenType.TransientSpecifier,
            TokenType.DuplicateTransientSpecifier,
            TokenType.NoImportSpecifier,
            TokenType.NativeSpecifier,
            TokenType.ExportSpecifier,
            TokenType.NoExportSpecifier,
            TokenType.NonTransactionalSpecifier,
            TokenType.PointerSpecifier,
            TokenType.InitSpecifier,
            TokenType.RepRetrySpecifier,
            TokenType.AllowAbstractSpecifier
        };

        private List<TokenType> ClassSpecifiers = new List<TokenType>
        {
            TokenType.AbstractSpecifier,
            TokenType.ConfigSpecifier,
            TokenType.DependsOnSpecifier,
            TokenType.ImplementsSpecifier,
            TokenType.InstancedSpecifier,
            TokenType.ParseConfigSpecifier,
            TokenType.PerObjectConfigSpecifier,
            TokenType.PerObjectLocalizedSpecifier,
            TokenType.TransientSpecifier,
            TokenType.NonTransientSpecifier,
            TokenType.DeprecatedSpecifier
        };

        private List<TokenType> StructSpecifiers = new List<TokenType>
        {
            TokenType.ImmutableSpecifier,
            TokenType.ImmutableWhenCookedSpecifier,
            TokenType.AtomicSpecifier,
            TokenType.AtomicWhenCookedSpecifier,
            TokenType.StrictConfigSpecifier,
            TokenType.TransientSpecifier,
            TokenType.NativeSpecifier
        };

        private List<TokenType> FunctionSpecifiers = new List<TokenType>
        {
            TokenType.PrivateSpecifier,
            TokenType.ProtectedSpecifier,
            TokenType.PublicSpecifier,
            TokenType.StaticSpecifier,
            TokenType.FinalSpecifier,
            TokenType.ExecSpecifier,
            TokenType.K2CallSpecifier,
            TokenType.K2OverrideSpecifier,
            TokenType.K2PureSpecifier,
            TokenType.SimulatedSpecifier,
            TokenType.SingularSpecifier,
            TokenType.ClientSpecifier,
            TokenType.DemoRecordingSpecifier,
            TokenType.ReliableSpecifier,
            TokenType.ServerSpecifier,
            TokenType.UnreliableSpecifier,
            TokenType.ConstSpecifier,
            TokenType.IteratorSpecifier,
            TokenType.LatentSpecifier,
            TokenType.NativeSpecifier,
            TokenType.NoExportSpecifier
        };

        private List<TokenType> ParameterSpecifiers = new List<TokenType>
        {
            TokenType.CoerceSpecifier,
            TokenType.ConstSpecifier,
            TokenType.InitSpecifier,
            TokenType.OptionalSpecifier,
            TokenType.OutSpecifier,
            TokenType.SkipSpecifier
        };

        private List<TokenType> StateSpecifiers = new List<TokenType>
        {
            TokenType.AutoSpecifier,
            TokenType.SimulatedSpecifier
        };

        #endregion

        public StringParser(TokenStream<String> tokens, MessageLog log = null)
        {
            Log = log ?? new MessageLog();
            Tokens = tokens;
        }

        public ASTNode ParseDocument()
        {
            return TryParseClass();
        }

        #region Parsers
        #region Statements

        private Class TryParseClass()
        {
            Func<ASTNode> classParser = () =>
                {
                    if (Tokens.ConsumeToken(TokenType.Class) == null)
                    {
                        Log.LogError("Expected class declaration!");
                        return null;
                    }

                    var name = Tokens.ConsumeToken(TokenType.Word);
                    if (name == null)
                    {
                        Log.LogError("Expected class name!");
                        return null;
                    }

                    var parentClass = TryParseParent();
                    if (parentClass == null)
                    {
                        Log.LogMessage("No parent class specified for " + name.Value + ", interiting from Object");
                        parentClass = new VariableType("Object", null, null);
                    }

                    var outerClass = TryParseOuter();

                    var specs = ParseSpecifiers(ClassSpecifiers);

                    if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                    {
                        Log.LogError("Expected semi-colon!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }

                    var variables = new List<VariableDeclaration>();
                    var types = new List<VariableType>();
                    while (CurrentTokenType == TokenType.InstanceVariable
                        || CurrentTokenType == TokenType.Struct
                        || CurrentTokenType == TokenType.Enumeration)
                    {
                        if (CurrentTokenType == TokenType.InstanceVariable)
                        {
                            var variable = TryParseVarDecl();
                            if (variable == null)
                            {
                                Log.LogError("Malformed instance variable!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                                return null;
                            }
                            variables.Add(variable);
                        }
                        else
                        {
                            var type = TryParseEnum() ?? TryParseStruct() ?? new VariableType("INVALID", null, null);
                            if (type.Name == "INVALID")
                            {
                                Log.LogError("Malformed type declaration!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                                return null;
                            }
                            types.Add(type);

                            if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                            {
                                Log.LogError("Expected semi-colon!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                                return null;
                            }
                        }
                    }

                    List<Function> funcs = new List<Function>();
                    List<State> states = new List<State>();
                    List<OperatorDeclaration> ops = new List<OperatorDeclaration>();
                    ASTNode declaration;
                    do
                    {
                        declaration = (ASTNode)TryParseFunction() ?? 
                                        (ASTNode)TryParseOperatorDecl() ?? 
                                        (ASTNode)TryParseState() ?? 
                                        (ASTNode)null;
                        if (declaration == null && !Tokens.AtEnd())
                        {
                            Log.LogError("Expected function/state/operator declaration!", 
                                CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                            return null;
                        }

                        if (declaration.Type == ASTNodeType.Function)
                            funcs.Add((Function)declaration);
                        else if (declaration.Type == ASTNodeType.State)
                            states.Add((State)declaration);
                        else
                            ops.Add((OperatorDeclaration)declaration);
                    } while (!Tokens.AtEnd());

                    // TODO: should AST-nodes accept null values? should they make sure they dont present any?
                    return new Class(name.Value, specs, variables, types, funcs, states, parentClass, outerClass, ops, name.StartPosition, name.EndPosition);
                };
            return (Class)Tokens.TryGetTree(classParser);
        }

        public VariableDeclaration TryParseVarDecl()
        {
            Func<ASTNode> declarationParser = () =>
                {
                    if (Tokens.ConsumeToken(TokenType.InstanceVariable) == null)
                        return null;

                    var specs = ParseSpecifiers(VariableSpecifiers);

                    var type = TryParseEnum() ?? TryParseStruct() ?? TryParseType();
                    if (type == null)
                    {
                        Log.LogError("Expected variable type or struct/enum type declaration!", 
                            CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }

                    var vars = ParseVariableNames(); // Struct/Enums also need variables if declared as inline types
                    if (vars == null) // && type.Type != ASTNodeType.Struct && type.Type != ASTNodeType.Enumeration)
                    {
                        Log.LogError("Malformed variable names!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }

                    if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                    {
                        Log.LogError("Expected semi-colon!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }

                    return new VariableDeclaration(type, specs, vars, vars.First().StartPos, vars.Last().EndPos);
                };
            return (VariableDeclaration)Tokens.TryGetTree(declarationParser);
        }

        public VariableDeclaration TryParseLocalVar()
        {
            Func<ASTNode> declarationParser = () =>
                {
                    if (Tokens.ConsumeToken(TokenType.LocalVariable) == null)
                        return null;

                    var type = TryParseType();
                    if (type == null)
                    {
                        Log.LogError("Expected variable type!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }

                    var vars = ParseVariableNames();
                    if (vars == null)
                    {
                        Log.LogError("Malformed variable names!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }

                    return new VariableDeclaration(type, null, vars, vars.First().StartPos, vars.Last().EndPos);
                };
            return (VariableDeclaration)Tokens.TryGetTree(declarationParser);
        }

        public Struct TryParseStruct()
        {
            Func<ASTNode> structParser = () =>
                {
                    if (Tokens.ConsumeToken(TokenType.Struct) == null)
                        return null;

                    var specs = ParseSpecifiers(StructSpecifiers);

                    var name = Tokens.ConsumeToken(TokenType.Word);
                    if (name == null)
                    {
                        Log.LogError("Expected struct name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }

                    var parent = TryParseParent();

                    if (Tokens.ConsumeToken(TokenType.LeftBracket) == null)
                    {
                        Log.LogError("Expected '{'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }

                    var vars = new List<VariableDeclaration>();
                    do
                    {
                        var variable = TryParseVarDecl();
                        if (variable == null)
                        {
                            Log.LogError("Malformed struct content!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                            return null;
                        }
                        vars.Add(variable);
                    } while (CurrentTokenType != TokenType.RightBracket);

                    if (Tokens.ConsumeToken(TokenType.RightBracket) == null)
                    {
                        Log.LogError("Expected '}'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }

                    return new Struct(name.Value, specs, vars, name.StartPosition, name.EndPosition, parent);
                };
            return (Struct)Tokens.TryGetTree(structParser);
        }

        public Enumeration TryParseEnum()
        {
            Func<ASTNode> enumParser = () =>
            {
                if (Tokens.ConsumeToken(TokenType.Enumeration) == null)
                    return null;

                var name = Tokens.ConsumeToken(TokenType.Word);
                if (name == null)
                {
                    Log.LogError("Expected enumeration name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                if (Tokens.ConsumeToken(TokenType.LeftBracket) == null)
                {
                    Log.LogError("Expected '{'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                var identifiers = new List<VariableIdentifier>();
                do
                {
                    var ident = Tokens.ConsumeToken(TokenType.Word);
                    if (ident == null)
                    {
                        Log.LogError("Expected non-empty enumeration!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                    identifiers.Add(new VariableIdentifier(ident.Value, ident.StartPosition, ident.EndPosition));
                    if (Tokens.ConsumeToken(TokenType.Comma) == null && CurrentTokenType != TokenType.RightBracket)
                    {
                        Log.LogError("Malformed enumeration content!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                } while (CurrentTokenType != TokenType.RightBracket);

                if (Tokens.ConsumeToken(TokenType.RightBracket) == null)
                {
                    Log.LogError("Expected '}'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                return new Enumeration(name.Value, identifiers, name.StartPosition, name.EndPosition);
            };
            return (Enumeration)Tokens.TryGetTree(enumParser);
        }

        public Function TryParseFunction()
        {
            Func<ASTNode> stubParser = () =>
                {
                    var specs = ParseSpecifiers(FunctionSpecifiers);

                    if (Tokens.ConsumeToken(TokenType.Function) == null)
                        return null;

                    Token<String> returnType = null, name = null;

                    var firstString = Tokens.ConsumeToken(TokenType.Word);
                    if (firstString == null)
                    {
                        Log.LogError("Expected function name or return type!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                    var secondString = Tokens.ConsumeToken(TokenType.Word);
                    if (secondString == null)
                        name = firstString;
                    else
                    {
                        returnType = firstString;
                        name = secondString;
                    }

                    VariableType retVarType = returnType != null ? 
                        new VariableType(returnType.Value, returnType.StartPosition, returnType.EndPosition) : null;

                    if (Tokens.ConsumeToken(TokenType.LeftParenth) == null)
                    {
                        Log.LogError("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }

                    var parameters = new List<FunctionParameter>();
                    while (CurrentTokenType != TokenType.RightParenth)
                    {
                        var param = TryParseParameter();
                        if (param == null)
                        {
                            Log.LogError("Malformed parameter!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                            return null;
                        }
                        parameters.Add(param);
                        if (Tokens.ConsumeToken(TokenType.Comma) == null && CurrentTokenType != TokenType.RightParenth)
                        {
                            Log.LogError("Unexpected parameter content!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                            return null;
                        }
                    }

                    if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                    {
                        Log.LogError("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }

                    CodeBody body = null;
                    SourcePosition bodyStart = null, bodyEnd = null;
                    if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                    {
                        if (!ParseScopeSpan(TokenType.LeftBracket, TokenType.RightBracket, out bodyStart, out bodyEnd))
                        {
                            Log.LogError("Malformed function body!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                            return null;
                        }
                        body = new CodeBody(null, bodyStart, bodyEnd);
                    }

                    return new Function(name.Value, retVarType, body, specs, parameters, name.StartPosition, name.EndPosition);
                };
            return (Function)Tokens.TryGetTree(stubParser);
        }

        public State TryParseState()
        {
            Func<ASTNode> stateSkeletonParser = () =>
            {
                var specs = ParseSpecifiers(StateSpecifiers);

                if (Tokens.ConsumeToken(TokenType.State) == null)
                    return null;

                var name = Tokens.ConsumeToken(TokenType.Word);
                if (name == null)
                {
                    Log.LogError("Expected state name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                var parent = TryParseParent();

                if (Tokens.ConsumeToken(TokenType.LeftBracket) == null)
                {
                    Log.LogError("Expected '{'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                List<VariableIdentifier> ignores = new List<VariableIdentifier>();
                if (Tokens.ConsumeToken(TokenType.Ignores) != null)
                {
                    do
                    {
                        VariableIdentifier variable = TryParseVariable();
                        if (variable == null)
                        {
                            Log.LogError("Malformed ignore statement!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                            return null;
                        }
                        ignores.Add(variable);
                    } while (Tokens.ConsumeToken(TokenType.Comma) != null);

                    if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                    {
                        Log.LogError("Expected semi-colon!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                }

                var funcs = new List<Function>();
                Function func = TryParseFunction();
                while (func != null)
                {
                    funcs.Add(func);
                    func = TryParseFunction();
                }

                var bodyStart = Tokens.CurrentItem.StartPosition;
                while (Tokens.CurrentItem.Type != TokenType.RightBracket)
                {
                    Tokens.Advance();
                }
                var bodyEnd = Tokens.CurrentItem.StartPosition;

                if (Tokens.ConsumeToken(TokenType.RightBracket) == null)
                {
                    Log.LogError("Expected '}'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                var body = new CodeBody(null, bodyStart, bodyEnd);
                return new State(name.Value, body, specs, parent, funcs, ignores, null, name.StartPosition, name.EndPosition);
            };
            return (State)Tokens.TryGetTree(stateSkeletonParser);
        }

        public OperatorDeclaration TryParseOperatorDecl()
        {
            Func<ASTNode> operatorParser = () =>
            {
                var specs = ParseSpecifiers(FunctionSpecifiers);

                var token = Tokens.ConsumeToken(TokenType.Operator) ??
                    Tokens.ConsumeToken(TokenType.PreOperator) ??
                    Tokens.ConsumeToken(TokenType.PostOperator) ??
                    new Token<String>(TokenType.INVALID);

                if (token.Type == TokenType.INVALID)
                    return null;

                Token<String> precedence = null;
                if (token.Type == TokenType.Operator)
                {
                    if (Tokens.ConsumeToken(TokenType.LeftParenth) == null)
                    {
                        Log.LogError("Expected '('! (Did you forget to specify operator precedence?)", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                    precedence = Tokens.ConsumeToken(TokenType.IntegerNumber);
                    if (precedence == null)
                    {
                        Log.LogError("Expected '('! (Did you forget to specify operator precedence?)", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                    if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                    {
                        Log.LogError("Expected '('! (Did you forget to specify operator precedence?)", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                }

                Token<String> returnType = null, name = null;
                var firstString = TryParseOperatorIdentifier();
                if (firstString == null)
                {
                    Log.LogError("Expected operator name or return type!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }
                var secondString = TryParseOperatorIdentifier();
                if (secondString == null)
                    name = firstString;
                else
                {
                    returnType = firstString;
                    name = secondString;
                }

                VariableType retVarType = returnType != null ?
                    new VariableType(returnType.Value, returnType.StartPosition, returnType.EndPosition) : null;

                if (Tokens.ConsumeToken(TokenType.LeftParenth) == null)
                {
                    Log.LogError("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                var operands = new List<FunctionParameter>();
                while (CurrentTokenType != TokenType.RightParenth)
                {
                    var operandSpecs = ParseSpecifiers(ParameterSpecifiers);

                    var operand = TryParseParameter();
                    if (operand == null)
                    {
                        Log.LogError("Malformed operand!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                    operands.Add(operand);
                    if (Tokens.ConsumeToken(TokenType.Comma) == null && CurrentTokenType != TokenType.RightParenth)
                    {
                        Log.LogError("Unexpected operand content!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                }

                if (token.Type == TokenType.Operator && operands.Count != 2)
                {
                    Log.LogError("In-fix operators requires exactly 2 parameters!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }
                else if (token.Type != TokenType.Operator && operands.Count != 1)
                {
                    Log.LogError("Post/Pre-fix operators requires exactly 1 parameter!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                {
                    Log.LogError("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                CodeBody body = null;
                SourcePosition bodyStart = null, bodyEnd = null;
                if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                {
                    if (!ParseScopeSpan(TokenType.LeftBracket, TokenType.RightBracket, out bodyStart, out bodyEnd))
                    {
                        Log.LogError("Malformed operator body!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                    body = new CodeBody(null, bodyStart, bodyEnd);
                }

                // TODO: determine if operator should be a delimiter! (should only symbol-based ones be?)
                if (token.Type == TokenType.PreOperator)
                    return new PreOpDeclaration(name.Value, false, body, retVarType, operands.First(), specs, name.StartPosition, name.EndPosition);
                else if (token.Type == TokenType.PostOperator)
                    return new PostOpDeclaration(name.Value, false, body, retVarType, operands.First(), specs, name.StartPosition, name.EndPosition);
                else
                    return new InOpDeclaration(name.Value, Int32.Parse(precedence.Value), false, body, retVarType, 
                        operands.First(), operands.Last(), specs, name.StartPosition, name.EndPosition);
            };
            return (OperatorDeclaration)Tokens.TryGetTree(operatorParser);
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

        public Statement TryParseInnerStatement()
        {
            Func<ASTNode> statementParser = () =>
            {
                var statement = TryParseLocalVar() ??
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

                if (CurrentTokenType == TokenType.SemiColon)
                    return new ReturnStatement(token.StartPosition, token.EndPosition);

                var value = TryParseExpression();
                if (value == null)
                {
                    Log.LogError("Expected a return value or a semi-colon!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                return new ReturnStatement(token.StartPosition, token.EndPosition, value);
            };
            return (ReturnStatement)Tokens.TryGetTree(statementParser);
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

                CodeBody body = TryParseBodyOrStatement(allowEmpty:true);
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

                CodeBody body = TryParseBodyOrStatement(allowEmpty:true);
                if (body == null)
                    return null;

                return new ForLoop(condition, body, initStatement, updateStatement, token.StartPosition, token.EndPosition);
            };
            return (ForLoop)Tokens.TryGetTree(forParser);
        }

        #endregion
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
        #region Misc

        public FunctionParameter TryParseParameter()
        {
            Func<ASTNode> paramParser = () =>
            {
                var paramSpecs = ParseSpecifiers(ParameterSpecifiers);

                var type = Tokens.ConsumeToken(TokenType.Word);
                if (type == null)
                {
                    Log.LogError("Expected parameter type!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }
                var variable = TryParseVariable();
                if (variable == null)
                {
                    Log.LogError("Expected parameter name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }
                return new FunctionParameter(
                    new VariableType(type.Value, type.StartPosition, type.EndPosition),
                    paramSpecs, variable, variable.StartPos, variable.EndPos);
            };
            return (FunctionParameter)Tokens.TryGetTree(paramParser);
        }

        public VariableType TryParseType()
        {
            Func<ASTNode> typeParser = () =>
                {
                    // TODO: word or basic datatype? (int float etc)
                    var type = Tokens.ConsumeToken(TokenType.Word);
                    if (type == null)
                    {
                        Log.LogError("Expected type name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                    return new VariableType(type.Value, type.StartPosition, type.EndPosition);
                };
            return (VariableType)Tokens.TryGetTree(typeParser);
        }

        public VariableType TryParseParent()
        {
            Func<ASTNode> parentParser = () =>
            {
                if (Tokens.ConsumeToken(TokenType.Extends) == null)
                    return null;
                var parentName = Tokens.ConsumeToken(TokenType.Word);
                if (parentName == null)
                {
                    Log.LogError("Expected parent name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }
                return new VariableType(parentName.Value, parentName.StartPosition, parentName.EndPosition);
            };
            return (VariableType)Tokens.TryGetTree(parentParser);
        }

        public VariableType TryParseOuter()
        {
            Func<ASTNode> outerParser = () =>
            {
                if (Tokens.ConsumeToken(TokenType.Within) == null)
                    return null;
                var outerName = Tokens.ConsumeToken(TokenType.Word);
                if (outerName == null)
                {
                    Log.LogError("Expected outer class name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }
                return new VariableType(outerName.Value, outerName.StartPosition, outerName.EndPosition);
            };
            return (VariableType)Tokens.TryGetTree(outerParser);
        }

        private Specifier TryParseSpecifier(List<TokenType> category)
        {
            Func<ASTNode> specifierParser = () =>
                {
                    if (category.Contains(CurrentTokenType))
                    {
                        var token = Tokens.ConsumeToken(CurrentTokenType);
                        return new Specifier(token.Value, token.StartPosition, token.EndPosition);
                    }
                    return null;
                };
            return (Specifier)Tokens.TryGetTree(specifierParser);
        }

        public VariableIdentifier TryParseVariable()
        {
            Func<ASTNode> variableParser = () =>
            {
                var name = Tokens.ConsumeToken(TokenType.Word);
                if (name == null)
                    return null;

                if (Tokens.ConsumeToken(TokenType.LeftSqrBracket) != null)
                {
                    var size = Tokens.ConsumeToken(TokenType.IntegerNumber);
                    if (size == null)
                    {
                        Log.LogError("Expected an integer number for size!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                    if (Tokens.ConsumeToken(TokenType.RightSqrBracket) != null)
                    {
                        Log.LogError("Expected ']'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }

                    return new VariableIdentifier(name.Value, 
                        name.StartPosition, name.EndPosition, Int32.Parse(size.Value));
                }

                return new VariableIdentifier(name.Value, name.StartPosition, name.EndPosition);
            };
            return (VariableIdentifier)Tokens.TryGetTree(variableParser);
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

        #endregion
        #endregion
        #region Helpers

        public Token<String> TryParseOperatorIdentifier()
        {
            if (GlobalLists.ValidOperatorSymbols.Contains(CurrentTokenType)
                || CurrentTokenType == TokenType.Word)
                return Tokens.ConsumeToken(CurrentTokenType);

            return null;
        }

        public List<VariableIdentifier> ParseVariableNames()
        {
            List<VariableIdentifier> vars = new List<VariableIdentifier>();
            do
            {
                VariableIdentifier variable = TryParseVariable();
                if (variable == null)
                {
                    Log.LogError("Expected at least one variable name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }
                vars.Add(variable);
            } while (Tokens.ConsumeToken(TokenType.Comma) != null);
            // TODO: This allows a trailing comma before semicolon, intended?
            return vars;
        }

        public List<Specifier> ParseSpecifiers(List<TokenType> specifierCategory)
        {
            List<Specifier> specs = new List<Specifier>();
            Specifier spec = TryParseSpecifier(specifierCategory);
            while (spec != null)
            {
                specs.Add(spec);
                spec = TryParseSpecifier(specifierCategory);
            }
            return specs;
        }

        //TODO: unused?
        private List<Token<String>> ParseScopedTokens(TokenType scopeStart, TokenType scopeEnd)
        {
            var scopedTokens = new List<Token<String>>();
            if (Tokens.ConsumeToken(scopeStart) == null)
            {
                Log.LogError("Expected '" + scopeStart.ToString() + "'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                return null;
            }

            int nestedLevel = 1;
            while (nestedLevel > 0)
            {
                if (CurrentTokenType == TokenType.EOF)
                    return null; // ERROR: Scope ended prematurely, are your scopes unbalanced?
                if (CurrentTokenType == scopeStart)
                    nestedLevel++;
                else if (CurrentTokenType == scopeEnd)
                    nestedLevel--;

                scopedTokens.Add(Tokens.CurrentItem);
                Tokens.Advance();
            }
            // Remove the ending scope token:
            scopedTokens.RemoveAt(scopedTokens.Count - 1);
            return scopedTokens;
        }

        private bool ParseScopeSpan(TokenType scopeStart, TokenType scopeEnd, 
            out SourcePosition startPos, out SourcePosition endPos)
        {
            startPos = null;
            endPos = null;
            if (Tokens.ConsumeToken(scopeStart) == null)
            {
                Log.LogError("Expected '" + scopeStart.ToString() + "'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                return false;
            }
            startPos = Tokens.CurrentItem.StartPosition;

            int nestedLevel = 1;
            while (nestedLevel > 0)
            {
                if (CurrentTokenType == TokenType.EOF)
                {
                    Log.LogError("Scope ended prematurely, are your scopes unbalanced?", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return false;
                }
                if (CurrentTokenType == scopeStart)
                    nestedLevel++;
                else if (CurrentTokenType == scopeEnd)
                    nestedLevel--;

                // If we're at the end token, don't advance so we can check the position properly.
                if (nestedLevel > 0)
                    Tokens.Advance();
            }
            endPos = Tokens.CurrentItem.StartPosition;
            Tokens.Advance();
            return true;
        }

        #endregion
    }
}
