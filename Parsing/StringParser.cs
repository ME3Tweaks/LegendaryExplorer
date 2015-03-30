using ME3Script.Language;
using ME3Script.Language.Tree;
using ME3Script.Lexing;
using ME3Script.Lexing.Tokenizing;
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
                        parentClass = new Variable("Object"); // Notice: no parent specified, inheriting from object

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
                            var type = TryParseEnum() ?? TryParseStruct() ?? new VariableType("INVALID");
                            if (type.Name == "INVALID")
                                return null; // ERROR: malformed type declaration!
                            types.Add(type);

                            if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                                return null; // ERROR: did you miss a semi-colon?
                        }
                    }

                    // TODO: should AST-nodes accept null values? should they make sure they dont present any?
                    return new Class(name.Value, specs, variables, types, null, null, parentClass, outerClass);
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

                    var vars = ParseVariableNames();
                    if (vars == null && type.Type != ASTNodeType.Struct && type.Type != ASTNodeType.Enumeration)
                        return null; // ERROR(?): malformed variable names?

                    if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                        return null; // ERROR: did you miss a semi-colon?

                    return new VariableDeclaration(type, specs, vars);
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

                    return new VariableDeclaration(type, null, vars);
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

                    return new Struct(name.Value, specs, vars, parent);
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
                    identifiers.Add(new Variable(ident.Value));
                    if (Tokens.ConsumeToken(TokenType.Comma) == null && CurrentTokenType != TokenType.RightBracket)
                        return null; // ERROR: unexpected enum content!
                } while (CurrentTokenType != TokenType.RightBracket);

                if (Tokens.ConsumeToken(TokenType.RightBracket) == null)
                    return null; //ERROR: expected end of struct body!

                return new Enumeration(name.Value, identifiers);
            };
            return (Enumeration)Tokens.TryGetTree(enumParser);
        }

        public FunctionStub TryParseFunction()
        {
            Func<ASTNode> functionParser = () =>
            {
                return null;
            };
            return (FunctionStub)Tokens.TryGetTree(functionParser);
        }

        public FunctionStub TryParseFunctionStub()
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

                    if (Tokens.ConsumeToken(TokenType.LeftParenth) == null)
                        return null; // ERROR: Expected (

                    // TODO: parse function parameters.
                    
                    // TODO: parse function body start/end.

                    return null;
                };
            return (FunctionStub)Tokens.TryGetTree(stubParser);
        }

        #endregion
        #region Expressions
        #endregion
        #region Misc

        public VariableType TryParseType()
        {
            // TODO: word or basic datatype? (int float etc)
            var type = Tokens.ConsumeToken(TokenType.Word);
            if (type == null)
                return null; // ERROR?
            return new VariableType(type.Value);
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
                return new Variable(parentName.Value);
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
                return new Variable(outerName.Value);
            };
            return (Variable)Tokens.TryGetTree(outerParser);
        }

        private Specifier TryParseSpecifier(List<TokenType> category)
        {
            Func<ASTNode> specifierParser = () =>
                {
                    return category.Contains(CurrentTokenType) ?
                        new Specifier(Tokens.ConsumeToken(CurrentTokenType).Value) : null;
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

                    return new StaticArrayVariable(name.Value, Int32.Parse(size.Value));
                }

                return new Variable(name.Value);
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

        #endregion
    }
}
