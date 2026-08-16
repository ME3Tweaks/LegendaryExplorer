using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.AvalonEdit.CodeCompletion;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Language.Util;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Parsing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    /// <summary>
    /// Context passed to completion providers, containing everything needed to generate completions.
    /// </summary>
    internal sealed class CompletionContext
    {
        public required ASTNode Definition { get; init; }
        public required ScriptToken PrevToken { get; init; }
        public required TokenStream Tokens { get; init; }
        public required int CurrentTokenIdx { get; init; }
        public required Class CurrentClass { get; init; }
        public required MEGame Game { get; init; }
        public required Func<ScriptToken, ASTNode> GetDefinitionFromToken { get; init; }
    }

    /// <summary>
    /// Provides code completions for a specific scenario (e.g. object member access, enum values).
    /// </summary>
    internal interface ICompletionProvider
    {
        bool CanProvide(ASTNode definition, ScriptToken prevToken);
        void AddCompletions(List<ICompletionData> completions, CompletionContext context);
    }

    /// <summary>
    /// Completions for class literal access: 'ClassName'.static / .const / .default
    /// </summary>
    internal sealed class ClassLiteralCompletionProvider : ICompletionProvider
    {
        public bool CanProvide(ASTNode definition, ScriptToken prevToken)
            => definition is ObjectType && prevToken.Type is TokenType.NameLiteral;

        public void AddCompletions(List<ICompletionData> completions, CompletionContext context)
        {
            completions.Add(new KeywordCompletion("static"));
            completions.Add(new KeywordCompletion("const"));
            completions.Add(new KeywordCompletion("default"));
        }
    }

    /// <summary>
    /// Completions for object member access: variables and functions on an ObjectType.
    /// </summary>
    internal sealed class ObjectTypeCompletionProvider : ICompletionProvider
    {
        public bool CanProvide(ASTNode definition, ScriptToken prevToken)
            => definition is ObjectType && prevToken.Type is not TokenType.NameLiteral;

        public void AddCompletions(List<ICompletionData> completions, CompletionContext context)
        {
            var objType = (ObjectType)context.Definition;
            ScriptToken prevToken = context.PrevToken;
            bool varsAccessible = !prevToken.Value.CaseInsensitiveEquals(Keywords.SUPER) && !prevToken.Value.CaseInsensitiveEquals(Keywords.GLOBAL);
            bool functionsAccessible = !prevToken.Value.CaseInsensitiveEquals(Keywords.DEFAULT);

            ReadOnlySpan<ScriptToken> tokens = context.Tokens.TokensSpan;
            do
            {
                if (varsAccessible)
                {
                    completions.AddRange(VariableCompletion.GenerateCompletions(objType.VariableDeclarations));
                }
                if (objType is Class classType && functionsAccessible)
                {
                    bool allowIterators = false;
                    for (int i = context.CurrentTokenIdx - 1; i >= 0; i--)
                    {
                        if (tokens[i].Type is TokenType.SemiColon or TokenType.LeftBracket or TokenType.RightBracket)
                        {
                            break;
                        }
                        if (tokens[i].Value.CaseInsensitiveEquals("foreach"))
                        {
                            allowIterators = true;
                            break;
                        }
                    }
                    completions.AddRange(FunctionCompletion.GenerateCompletions(classType.Functions, context.CurrentClass, iterators: allowIterators));
                }
                objType = objType.Parent as ObjectType;
            } while (objType is not null);
        }
    }

    /// <summary>
    /// Completions for enum member access: enum values.
    /// </summary>
    internal sealed class EnumCompletionProvider : ICompletionProvider
    {
        public bool CanProvide(ASTNode definition, ScriptToken prevToken)
            => definition is Enumeration;

        public void AddCompletions(List<ICompletionData> completions, CompletionContext context)
        {
            var enumType = (Enumeration)context.Definition;
            completions.AddRange(enumType.Values.Select(v => new CompletionData(v.Name, $"{v.IntVal}")));
        }
    }

    /// <summary>
    /// Completions for dynamic array member access: array functions like Add, Remove, Find, etc.
    /// </summary>
    internal sealed class DynamicArrayCompletionProvider : ICompletionProvider
    {
        public bool CanProvide(ASTNode definition, ScriptToken prevToken)
            => definition is DynamicArrayType;

        public void AddCompletions(List<ICompletionData> completions, CompletionContext context)
        {
            var dynArrType = (DynamicArrayType)context.Definition;
            completions.AddRange(ArrayFunctionCompletion.GenerateCompletions(context.Game, dynArrType));
        }
    }

    /// <summary>
    /// Completions for class literal keyword access: 'ClassName'.const.X or 'ClassName'.static.X
    /// </summary>
    internal sealed class ClassAccessCompletionProvider : ICompletionProvider
    {
        public bool CanProvide(ASTNode definition, ScriptToken prevToken)
            => definition is null
               && (prevToken.Value.CaseInsensitiveEquals(Keywords.CONST) || prevToken.Value.CaseInsensitiveEquals(Keywords.STATIC));

        public void AddCompletions(List<ICompletionData> completions, CompletionContext context)
        {
            if (context.CurrentTokenIdx <= 3) return;

            ReadOnlySpan<ScriptToken> tokens = context.Tokens.TokensSpan;
            ScriptToken classNameToken = tokens[context.CurrentTokenIdx - 3];
            if (classNameToken.Type is not TokenType.NameLiteral) return;
            if (context.GetDefinitionFromToken(classNameToken) is not Class cls) return;

            if (context.PrevToken.Value.CaseInsensitiveEquals(Keywords.CONST))
            {
                do
                {
                    completions.AddRange(cls.TypeDeclarations.OfType<Const>().Select(c => new CompletionData(c.Name, $"{c.Value}")));
                    cls = cls.Parent as Class;
                } while (cls is not null);
            }
            else if (context.PrevToken.Value.CaseInsensitiveEquals(Keywords.STATIC))
            {
                do
                {
                    completions.AddRange(FunctionCompletion.GenerateCompletions(cls.Functions, context.CurrentClass, staticsOnly: true));
                    cls = cls.Parent as Class;
                } while (cls is not null);
            }
        }
    }
}
