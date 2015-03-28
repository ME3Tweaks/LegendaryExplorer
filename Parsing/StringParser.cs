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
        #endregion

        public StringParser(StringLexer lexer)
        {
            Tokens = new TokenStream<String>(lexer);
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

                    List<Specifier> specs = new List<Specifier>();
                    while (CurrentTokenType != TokenType.SemiColon)
                    {
                        Specifier spec = TryParseSpecifier(ClassSpecifiers);
                        if (spec == null)
                            return null; // ERROR: Expected class specifier or semicolon!
                        specs.Add(spec);
                    }

                    return new Class(name.Value, specs, null, null, null, parentClass, outerClass);
                };
            return (Class)Tokens.TryGetTree(classParser);
        }

        #endregion
        #region Expressions
        #endregion
        #region Misc

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

        #endregion
        #endregion
        #region Helpers

        #endregion
    }
}
