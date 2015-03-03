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

        private List<TokenType> BasicTypes = new List<TokenType>
        {
            TokenType.Byte,
            TokenType.Int,
            TokenType.Bool,
            TokenType.Float,
            TokenType.String
        };

        // Update!
        private List<TokenType> ClassKeywords = new List<TokenType>
        {
            TokenType.Object,
            TokenType.Actor
        };

        private List<TokenType> StructKeywords = new List<TokenType>
        {
            TokenType.Vector,
            TokenType.Plane,
            TokenType.Rotator,
            TokenType.Coords,
            TokenType.Color,
            TokenType.Region
        };

        private List<TokenType> PropertyTypes = new List<TokenType>
        {
            TokenType.InstanceVariable,
            TokenType.Struct,
            TokenType.Enumeration
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

        public AbstractSyntaxTree ParseClassSkeleton()
        {
            Func<AbstractSyntaxTree> parser = () =>
                {
                    Token<String> classToken = Tokens.ConsumeToken(TokenType.Class);
                    if (classToken == null)
                        return null; // ERROR: Malformed file, class keyword expected.

                    Token<String> nameToken = Tokens.ConsumeToken(TokenType.Word);
                    if (nameToken == null)
                        return null; // ERROR: Malformed file, class name expected.
                    if (IsValidType(nameToken))
                        return null; // ERROR: A class with that name already exists!

                    String parent = ParseParent(ClassKeywords);
                    if (parent == null)
                        parent = "Object"; // WARNING: No parent class supplied, inheriting from Object
                    // TODO: support optional within clause here.

                    var specifiers = ParseTokensFromList(ClassSpecifiers);

                    if (CurrentTokenType != TokenType.SemiColon)
                        return null; // ERROR: ';' expected after class definition header.
                    Tokens.Advance();

                    List<AbstractSyntaxTree> properties = ParseProperties(PropertyTypes);

                    var node = new ClassNode(classToken.Type, nameToken.Value, parent, specifiers);
                    node.AddProperties(properties);
                    
                    return RegisterType(node.TypeName, node);
                };
            return Tokens.TryGetTree(parser);
        }

        public void ParseDefaultProperties()
        {
            throw new NotImplementedException();
        }

        // Function from hell... Refactor!
        private List<AbstractSyntaxTree> TryParseVariables(TokenType variableScope, bool allowDeclaration = false)
        {
            Tokens.PushSnapshot();
            if (Tokens.ConsumeToken(variableScope) == null)
            {
                Tokens.PopSnapshot();
                return null;
            }

            var variables = new List<AbstractSyntaxTree>();
            List<TokenType> specifiers = ParseTokensFromList(VariableSpecifiers);

            Token<String> type = Tokens.CurrentItem;
            AbstractSyntaxTree tree = TryParseEnum() ?? TryParseStruct();
            if (tree != null && allowDeclaration)
            {
                type = new Token<String>(TokenType.Word, (tree as TypeDeclarationNode).TypeName);
                variables.Add(tree);
            }
            else
            {
                Tokens.Advance();
                if (!IsValidType(type))
                {
                    Tokens.PopSnapshot();
                    return null; // ERROR: invalid variable type!
                }
            }

            List<String> variableNames = ParseDelimitedStringTokens(
                ParseTokensBefore(TokenType.SemiColon), TokenType.Comma);
            if (variableNames.Count == 0)
            {
                Tokens.PopSnapshot();
                return null; // ERROR: One or more variable names expected!
            }

            //TODO: check if names are taken!
            foreach (String name in variableNames)
            {
                variables.Add(new VariableNode(TokenType.InstanceVariable, name, type.Value, specifiers));
            }

            Tokens.DiscardSnapshot();
            return variables;
        }

        private AbstractSyntaxTree TryParseStruct()
        {
            Func<AbstractSyntaxTree> parser = () =>
            {
                var structToken = Tokens.ConsumeToken(TokenType.Struct);
                if (structToken == null)
                    return null;

                List<TokenType> specifiers = ParseTokensFromList(StructSpecifiers);
                var structName = Tokens.ConsumeToken(TokenType.Word);
                if (IsValidType(structName))
                    return null; // ERROR: redefenition of 'name'
                String parent = ParseParent(StructKeywords);
                var contents = TryParseScope(TokenType.LeftBracket, TokenType.RightBracket, 
                    () => TryParseVariables(TokenType.InstanceVariable), TokenType.SemiColon);
                if (contents == null)
                    return null; // ERROR: Malformed struct contents!
                return RegisterType(structName.Value,
                    new StructNode(structName.Value, contents as ScopeNode, parent, specifiers));
            };
            return Tokens.TryGetTree(parser);
        }

        private AbstractSyntaxTree TryParseEnum()
        {
            Func<AbstractSyntaxTree> parser = () =>
            {
                var enumToken = Tokens.ConsumeToken(TokenType.Enumeration);
                if (enumToken == null)
                    return null;

                var enumName = Tokens.ConsumeToken(TokenType.Word);
                if (IsValidType(enumName))
                    return null; // ERROR: redefinition of 'name'
                var contentValues = ParseScopedTokens(
                    TokenType.LeftBracket, TokenType.RightBracket);
                var enumValues = ParseDelimitedStringTokens(contentValues, TokenType.Comma);
                if (enumValues.Count == 0)
                    return null; // ERROR: enum has no values!
                return RegisterType(enumName.Value, 
                    new EnumerationNode(enumName.Value, enumValues));
            };
            return Tokens.TryGetTree(parser);
        }

        private AbstractSyntaxTree TryParseScope(TokenType scopeStart, 
            TokenType scopeEnd, Func<List<AbstractSyntaxTree>> contentParser, TokenType delimiter = TokenType.INVALID)
        {
            Func<AbstractSyntaxTree> scopeParser = () =>
            {
                if (Tokens.ConsumeToken(scopeStart) == null)
                    return null;
                var contentTree = new List<AbstractSyntaxTree>();
                List<AbstractSyntaxTree> trees = null;
                while (CurrentTokenType != TokenType.EOF)
                {
                    trees = contentParser();
                    if (trees == null)
                        return null; // ERROR: Unexpected scope contents!
                    contentTree.AddRange(trees);
                    if (delimiter != TokenType.InstanceVariable && Tokens.ConsumeToken(delimiter) == null)
                        return null; // ERROR: 'delimiter' expected!
                    if (CurrentTokenType == scopeEnd)
                        break;
                }
                if (Tokens.ConsumeToken(scopeEnd) == null)
                    return null; // ERROR: Expected end of scope!
                return new ScopeNode(contentTree);
            };
            return Tokens.TryGetTree(scopeParser);
        }

        #region Helpers

        private List<TokenType> ParseTokensFromList(List<TokenType> typeList)
        {
            var tokens = new List<TokenType>();
            while (typeList.Contains(CurrentTokenType))
            {
                tokens.Add(CurrentTokenType);
                Tokens.Advance();
            }
            return tokens;
        }

        private List<AbstractSyntaxTree> ParseProperties(List<TokenType> typeList)
        {
            var properties = new List<AbstractSyntaxTree>();
            while (typeList.Contains(CurrentTokenType))
            {
                List<AbstractSyntaxTree> trees = TryParseVariables(TokenType.InstanceVariable, true) 
                    ?? new List<AbstractSyntaxTree>();
                if (trees.Count == 0)
                {
                    var tree = TryParseStruct() ?? TryParseEnum();
                    if (tree != null)
                        trees.Add(tree);
                }

                if (trees.Count == 0)
                    break; // ERROR: Property declaration expected!
                if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                    break; // ERROR: Semicolon expected after class property declaration!
                properties.AddRange(trees);
            }
            return properties;
        }

        private String ParseParent(List<TokenType> validKeywords)
        {
            if (Tokens.ConsumeToken(TokenType.Extends) == null)
                return null; // ERROR: Expected 'extends' keyword!
            var parent = ParseTokenFromList(validKeywords) ?? Tokens.ConsumeToken(TokenType.Word);
            if (parent == null)
                return null; // ERROR: Expected parent class name!
            // TODO: check identifier validity?
            return parent.Value;
        }

        private Token<String> ParseTokenFromList(List<TokenType> types)
        {
            Token<String> token = null;
            if (!types.Contains(CurrentTokenType))
                return null; // ERROR?
            token = Tokens.CurrentItem;
            Tokens.Advance();
            return token;
        }

        private List<String> ParseDelimitedStringTokens(List<Token<String>> tokens, TokenType delimiter)
        {
            List<String> strings = new List<String>();
            IEnumerator<Token<String>> iterator = tokens.GetEnumerator();
            if (!iterator.MoveNext())
                return null; // ERROR: expected token before 'delimiter'!
            do
            {
                var token = iterator.Current;
                if (token.Type != TokenType.Word)
                    return null; // ERROR?
                bool end = !iterator.MoveNext();
                if ((!end && iterator.Current.Type == delimiter) || end)
                    strings.Add(token.Value);
                else 
                    return null;  // ERROR?
            } while (iterator.MoveNext());

            return strings;
        }

        private List<Token<String>> ParseScopedTokens(TokenType scopeStart, TokenType scopeEnd)
        {
            var scopedTokens = new List<Token<String>>();
            if (Tokens.ConsumeToken(scopeStart) == null)
                return null; // ERROR: expected 'scopeStart' at start of a scope

            int nestedLevel = 1;
            while(nestedLevel > 0)
            {
                if (CurrentTokenType == TokenType.EOF)
                    return null; // ERROR: Scope ended prematurely, are your scoped unbalanced?
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

        private List<Token<String>> ParseTokensBefore(TokenType delimiter)
        {
            var scopedTokens = new List<Token<String>>();
            while (CurrentTokenType != delimiter)
            {
                scopedTokens.Add(Tokens.CurrentItem);
                Tokens.Advance();
            }
            return scopedTokens;
        }

        private bool IsValidType(Token<String> token)
        {
            return Types.SymbolExists(token.Value);
        }

        private AbstractSyntaxTree RegisterType(String name, AbstractSyntaxTree tree)
        {
            return Types.TryRegisterType(name, tree) ? tree : null;
        }

        #endregion
    }
}
