using ME3Script.Language;
using ME3Script.Language.Tree;
using ME3Script.Lexing;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Parsing
{
    public class StringParser
    {
        private TokenStream<String> Tokens;
        private TokenType CurrentTokenType 
            { get { return Tokens.CurrentItem.Type; } }

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

        public StringParser(TokenStream<String> tokens)
        {
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
                        return null; // ERROR: expected class declaration! (are you missing the class keyword?)

                    var name = Tokens.ConsumeToken(TokenType.Word);
                    if (name == null)
                        return null; // ERROR: expected class name!

                    var parentClass = TryParseParent();
                    if (parentClass == null)
                        parentClass = new Variable("Object", null, null); // Notice: no parent specified, inheriting from object

                    var outerClass = TryParseOuter();

                    var specs = ParseSpecifiers(ClassSpecifiers);

                    if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                        return null; // ERROR: did you miss a semi-colon?

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
                                return null; // ERROR: malformed instance variable!
                            variables.Add(variable);
                        }
                        else
                        {
                            var type = TryParseEnum() ?? TryParseStruct() ?? new VariableType("INVALID", null, null);
                            if (type.Name == "INVALID")
                                return null; // ERROR: malformed type declaration!
                            types.Add(type);

                            if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                                return null; // ERROR: did you miss a semi-colon?
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
                            return null; // ERROR: expected function/state/operator declaration!

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
                        return null; // ERROR: expected variable type or struct/enum type declaration.

                    var vars = ParseVariableNames(); // Struct/Enums also need variables if declared as inline types
                    if (vars == null) // && type.Type != ASTNodeType.Struct && type.Type != ASTNodeType.Enumeration)
                        return null; // ERROR(?): malformed variable names?

                    if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                        return null; // ERROR: did you miss a semi-colon?

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
                        return null; // ERROR: expected variable type

                    var vars = ParseVariableNames();
                    if (vars == null)
                        return null; // ERROR(?): malformed variable names?

                    if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                        return null; // ERROR: did you miss a semi-colon?

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
                        return null; // ERROR: expected struct name!

                    var parent = TryParseParent();

                    if (Tokens.ConsumeToken(TokenType.LeftBracket) == null)
                        return null; // ERROR: expected struct body!

                    var vars = new List<VariableDeclaration>();
                    do
                    {
                        var variable = TryParseVarDecl();
                        if (variable == null)
                            return null; //ERROR: expected variable declaration in struct body.
                        vars.Add(variable);
                    } while (CurrentTokenType != TokenType.RightBracket);

                    if (Tokens.ConsumeToken(TokenType.RightBracket) == null)
                        return null; //ERROR: expected end of struct body!

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
                    return null; // ERROR: expected enum name!

                if (Tokens.ConsumeToken(TokenType.LeftBracket) == null)
                    return null; // ERROR: expected struct body!

                var identifiers = new List<Variable>();
                do
                {
                    var ident = Tokens.ConsumeToken(TokenType.Word);
                    if (ident == null)
                        return null; //ERROR: expected variable declaration in struct body.
                    identifiers.Add(new Variable(ident.Value, ident.StartPosition, ident.EndPosition));
                    if (Tokens.ConsumeToken(TokenType.Comma) == null && CurrentTokenType != TokenType.RightBracket)
                        return null; // ERROR: unexpected enum content!
                } while (CurrentTokenType != TokenType.RightBracket);

                if (Tokens.ConsumeToken(TokenType.RightBracket) == null)
                    return null; //ERROR: expected end of struct body!

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
                        return null; // ERROR: Expected function name! (And returntype)
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
                        return null; // ERROR: Expected (

                    var parameters = new List<FunctionParameter>();
                    while (CurrentTokenType != TokenType.RightParenth)
                    {
                        var param = TryParseParameter();
                        if (param == null)
                            return null; // ERROR: malformed parameter!
                        parameters.Add(param);
                        if (Tokens.ConsumeToken(TokenType.Comma) == null && CurrentTokenType != TokenType.RightParenth)
                            return null; // ERROR: unexpected function parameter content!
                    }

                    if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                        return null; //ERROR: expected )

                    CodeBody body = null;
                    SourcePosition bodyStart = null, bodyEnd = null;
                    if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                    {
                        if (!ParseScopeSpan(TokenType.LeftBracket, TokenType.RightBracket, out bodyStart, out bodyEnd))
                            return null; //ERROR(?): malformed function body! 
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
                    return null; // ERROR: Expected state name!

                var parent = TryParseParent();

                if (Tokens.ConsumeToken(TokenType.LeftBracket) == null)
                    return null; // ERROR: expected state body!

                List<Variable> ignores = new List<Variable>();
                if (Tokens.ConsumeToken(TokenType.Ignores) != null)
                {
                    do
                    {
                        Variable variable = TryParseVariable();
                        if (variable == null)
                            return null; // ERROR: malformed ignore statement!
                        ignores.Add(variable);
                    } while (Tokens.ConsumeToken(TokenType.Comma) != null);

                    if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                        return null; // ERROR: did you miss a semi-colon?
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
                    return null; // ERROR: expected }

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
                        return null; //ERROR: operator precedence!
                    precedence = Tokens.ConsumeToken(TokenType.IntegerNumber);
                    if (precedence == null)
                        return null; //ERROR: operator precedence!
                    if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                        return null; //ERROR: operator precedence!
                }

                Token<String> returnType = null, name = null;
                var firstString = Tokens.ConsumeToken(TokenType.Word);
                if (firstString == null)
                    return null; // ERROR: Expected function name! (And returntype)
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
                    return null; //ERROR: expected (

                var operands = new List<FunctionParameter>();
                while (CurrentTokenType != TokenType.RightParenth)
                {
                    var operandSpecs = ParseSpecifiers(ParameterSpecifiers);

                    var operand = TryParseParameter();
                    if (operand == null)
                        return null; //ERROR: malformed operand!
                    operands.Add(operand);
                    if (Tokens.ConsumeToken(TokenType.Comma) == null && CurrentTokenType != TokenType.RightParenth)
                        return null; // ERROR: unexpected function parameters!
                }

                if (token.Type == TokenType.Operator && operands.Count != 2)
                    return null; // ERROR: infix operators requires exactly 2 parameters!
                else if (token.Type != TokenType.Operator && operands.Count != 1)
                    return null; // ERROR: post/pre-fix operators requires exactly 1 parameter!

                if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                    return null; //ERROR: expected )

                CodeBody body = null;
                SourcePosition bodyStart = null, bodyEnd = null;
                if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                {
                    if (!ParseScopeSpan(TokenType.LeftBracket, TokenType.RightBracket, out bodyStart, out bodyEnd))
                        return null; //ERROR(?): malformed operator body! 
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
                    return null; //ERROR: expected parameter type!
                var variable = TryParseVariable();
                if (variable == null)
                    return null; //ERROR: expected parameter name!
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
                        return null; // ERROR?
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
                    return null;
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
                    return null;
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
                        return null; // ERROR: expected integer size
                    if (Tokens.ConsumeToken(TokenType.RightSqrBracket) != null)
                        return null; // ERROR: expected closing bracket

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
                    return null; // ERROR: Expected a variable name
                vars.Add(variable);
            } while (Tokens.ConsumeToken(TokenType.Comma) != null);
            // TODO: This allows a trailing comma before semicolon, intended?
            return vars;
        }

        public List<Specifier> ParseSpecifiers(List<TokenType> specifierCategory)
        {
            List<Specifier> specs = new List<Specifier>();
            while (specifierCategory.Contains(CurrentTokenType))
            {
                Specifier spec = TryParseSpecifier(specifierCategory);
                if (spec == null)
                    return null; // ERROR: Expected valid specifier
                specs.Add(spec);
            }
            return specs;
        }

        //TODO: unused?
        private List<Token<String>> ParseScopedTokens(TokenType scopeStart, TokenType scopeEnd)
        {
            var scopedTokens = new List<Token<String>>();
            if (Tokens.ConsumeToken(scopeStart) == null)
                return null; // ERROR: expected 'scopeStart' at start of a scope

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
                return false; // ERROR: expected 'scopeStart' at start of a scope
            startPos = Tokens.CurrentItem.StartPosition;

            int nestedLevel = 1;
            while (nestedLevel > 0)
            {
                if (CurrentTokenType == TokenType.EOF)
                    return false; // ERROR: Scope ended prematurely, are your scopes unbalanced?
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
