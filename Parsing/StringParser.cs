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
                        parentClass = new Variable("Object", null, null);
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

                    if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                    {
                        Log.LogError("Expected semi-colon!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
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

                var identifiers = new List<Variable>();
                do
                {
                    var ident = Tokens.ConsumeToken(TokenType.Word);
                    if (ident == null)
                    {
                        Log.LogError("Expected non-empty enumeration!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                    identifiers.Add(new Variable(ident.Value, ident.StartPosition, ident.EndPosition));
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

                List<Variable> ignores = new List<Variable>();
                if (Tokens.ConsumeToken(TokenType.Ignores) != null)
                {
                    do
                    {
                        Variable variable = TryParseVariable();
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

                /* TODO: add a function for parsing the operator name
                 * allowed tokens are:
                 * ANY single symbol as recognized by the lexer
                 * ANY symbol combination as recognized by the lexer
                 * an ordinary word from the lexer
                 * 
                 * symbols: '^, !, $, %, &, /, ?, *, +, ~, @, -, >, <, |, :, #' (complete?)
                 * */
                Token<String> returnType = null, name = null;
                var firstString = Tokens.ConsumeToken(TokenType.Word);
                if (firstString == null)
                {
                    Log.LogError("Expected operator name or return type!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
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

        #endregion
        #region Expressions
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

        public Variable TryParseParent()
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
                return new Variable(parentName.Value, parentName.StartPosition, parentName.EndPosition);
            };
            return (Variable)Tokens.TryGetTree(parentParser);
        }

        public Variable TryParseOuter()
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
                return new Variable(outerName.Value, outerName.StartPosition, outerName.EndPosition);
            };
            return (Variable)Tokens.TryGetTree(outerParser);
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

        public Variable TryParseVariable()
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

                    return new StaticArrayVariable(name.Value, Int32.Parse(size.Value), 
                        name.StartPosition, name.EndPosition);
                }

                return new Variable(name.Value, name.StartPosition, name.EndPosition);
            };
            return (Variable)Tokens.TryGetTree(variableParser);
        }

        #endregion
        #endregion
        #region Helpers

        public List<Variable> ParseVariableNames()
        {
            List<Variable> vars = new List<Variable>();
            do
            {
                Variable variable = TryParseVariable();
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
