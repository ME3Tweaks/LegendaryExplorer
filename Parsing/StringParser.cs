using ME3Script.Language;
using ME3Script.Language.Nodes;
using ME3Script.Language.Types;
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
        private TypeManager Types;

        private TokenType CurrentTokenType { get { return Tokens.CurrentItem.Type; } }

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

        private List<TokenType> BasicSymbols = new List<TokenType>
        {
            TokenType.Byte,
            TokenType.Int,
            TokenType.Bool,
            TokenType.Float,
            TokenType.String
        };

        public StringParser(StringLexer lexer, TypeManager symbols)
        {
            Tokens = new TokenStream<String>(lexer);
            Types = symbols;
        }

        public AbstractSyntaxTree ParseClass()
        {
            throw new NotImplementedException();
        }

        public void BuildClassSkeleton()
        {
            throw new NotImplementedException();
        }

        public void GetDefaultProperties()
        {
            throw new NotImplementedException();
        }

        private AbstractSyntaxTree TryParseVariable(TokenType delimiter)
        {
            Func<AbstractSyntaxTree> parser = () =>
                {
                    TokenType scope = TokenType.StructMember;
                    if (CurrentTokenType == TokenType.InstanceVariable
                        || CurrentTokenType == TokenType.LocalVariable)
                    {
                        scope = CurrentTokenType;
                        Tokens.Advance();
                    }
                    List<TokenType> specifiers = ParseSpecifiers(VariableSpecifiers);
                    Token<String> type = Tokens.CurrentItem;
                    Tokens.Advance();
                    String variableName = Tokens.ConsumeToken(TokenType.Word).Value;

                    if (variableName != null && CurrentTokenType == delimiter && IsValidType(type))
                    {
                        return new VariableNode(scope, variableName, specifiers);
                    }
                    return null;
                };
            return Tokens.TryGetTree(parser);
        }

        #region Helpers

        private List<TokenType> ParseSpecifiers(List<TokenType> specifierList)
        {
            var specifiers = new List<TokenType>();
            while (specifierList.Contains(CurrentTokenType))
            {
                specifiers.Add(CurrentTokenType);
                Tokens.Advance();
            }
            return specifiers;
        }

        private bool IsValidType(Token<String> token)
        {
            return Types.SymbolExists(token.Value);
        }

        private bool RegisterType(String name, AbstractSyntaxTree tree)
        {
            return Types.TryRegisterType(name, tree);
        }

        #endregion
    }
}
