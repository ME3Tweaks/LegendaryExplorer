using ICSharpCode.AvalonEdit.Highlighting;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Parsing;
using LegendaryExplorerCore.UnrealScript.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    /// <summary>
    /// Manages script parsing, cancellation, and syntax highlighting construction.
    /// </summary>
    internal sealed class ScriptParseService : IDisposable
    {
        public readonly record struct ParseResult(ASTNode AST, TokenStream Tokens, MessageLog Log, bool NeedsTokensReset);

        private CancellationTokenSource _parseCts;

        /// <summary>
        /// Pure computation: lexes and compiles the source. Safe to call from any thread.
        /// </summary>
        public static ParseResult ParseCore(string source, ExportEntry exportEntry, MEGame game, FileLib fileLib, bool fullyInitialized)
        {
            if (fileLib is null)
                return new ParseResult(null, null, null, true);
            lock (fileLib)
            {
                var log = new MessageLog();
                ASTNode ast = null;
                TokenStream resultTokens = null;
                bool needsTokensReset = true;
                try
                {
                    (ast, TokenStream tokens) = UnrealScriptCompiler.CompileOutlineAST(source, exportEntry.ClassName, log, game);

                    if (ast != null && !log.HasErrors && fullyInitialized)
                    {
                        UnrealScriptOptionsPackage usop = new UnrealScriptOptionsPackage();
                        log.Tokens = tokens;
                        switch (ast)
                        {
                            case Class cls:
                                ast = UnrealScriptCompiler.CompileNewClassAST(exportEntry.FileRef, cls, log, fileLib, out bool vfTableChanged, usop);
                                if (vfTableChanged)
                                {
                                    log.LogWarning("Compiling will cause Virtual Function Table to change! All classes that depend on this one will need recompilation to work properly!");
                                }
                                break;
                            case Function func when exportEntry.Parent is ExportEntry funcParent:
                                if (exportEntry.ObjectName.Name.StartsWith("__lambda__", StringComparison.OrdinalIgnoreCase))
                                {
                                    log.LogError("Lambdas cannot be edited on their own. You can edit them in the function they are defined in.");
                                }
                                ast = UnrealScriptCompiler.CompileNewFunctionBodyAST(funcParent, func, log, fileLib, usop);
                                break;
                            case State state when exportEntry.Parent is ExportEntry stateParent:
                                ast = UnrealScriptCompiler.CompileNewStateBodyAST(stateParent, state, log, fileLib, usop);
                                break;
                            case Struct strct when exportEntry.Parent is ExportEntry structParent:
                                ast = UnrealScriptCompiler.CompileNewStructAST(structParent, strct, log, fileLib, usop);
                                break;
                            case Enumeration enumeration when exportEntry.Parent is ExportEntry enumParent:
                                ast = UnrealScriptCompiler.CompileNewEnumAST(enumParent, enumeration, log, fileLib, usop);
                                break;
                            case VariableDeclaration varDecl when exportEntry.Parent is ExportEntry varParent:
                                ast = UnrealScriptCompiler.CompileNewVarDeclAST(varParent, varDecl, log, fileLib, usop);
                                break;
                            case Const:
                                break;
                            case DefaultPropertiesBlock propertiesBlock:
                                ast = UnrealScriptCompiler.CompileDefaultPropertiesAST(propertiesBlock, log, fileLib, exportEntry, usop);
                                break;
                            default:
                                return new ParseResult(null, null, log, true);
                        }
                        log.Tokens = null;
                        resultTokens = tokens;
                        needsTokensReset = false;
                    }
                }
                catch (ParseException pe)
                {
                    log.LogError($"Parse Failed! {pe.Message}");
                }
                catch (Exception exception)
                {
                    log.LogError($"Exception: {exception}");
                }
                return new ParseResult(ast, resultTokens, log, needsTokensReset);
            }
        }

        /// <summary>
        /// Runs ParseCore on a background thread with cancellation management.
        /// Automatically cancels any previous in-flight parse.
        /// Returns null if cancelled or superseded by a newer parse.
        /// </summary>
        public async Task<ParseResult?> ParseAsync(string source, ExportEntry exportEntry, MEGame game, FileLib fileLib, bool fullyInitialized)
        {
            _parseCts?.Cancel();
            var cts = new CancellationTokenSource();
            _parseCts = cts;

            ParseResult result;
            try
            {
                result = await Task.Run(() => ParseCore(source, exportEntry, game, fileLib, fullyInitialized), cts.Token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            if (cts.Token.IsCancellationRequested || cts != _parseCts) return null;
            return result;
        }

        /// <summary>
        /// Cancels the current in-flight parse, if any.
        /// </summary>
        public void CancelCurrentParse()
        {
            _parseCts?.Cancel();
        }

        /// <summary>
        /// Builds an <see cref="IHighlightingDefinition"/> from a parsed token stream.
        /// Returns <see cref="SyntaxInfo.None"/> if tokens are empty.
        /// </summary>
        public static IHighlightingDefinition BuildSyntaxHighlighting(TokenStream tokens)
        {
            List<int> lineLookup = tokens.LineLookup.Lines;
            if (!tokens.Any() || lineLookup.Count <= 0)
            {
                return SyntaxInfo.None;
            }
            var lineToIndex = new List<int>(lineLookup.Count);

            var tokensSpan = tokens.TokensSpan;

            var syntaxSpans = new List<SyntaxSpan>(tokensSpan.Length);

            int i = 0, j = 0;
            for (; i < lineLookup.Count - 1 && j < tokensSpan.Length; ++i)
            {
                int nextLine = lineLookup[i + 1];

                lineToIndex.Add(j);
                for (; j < tokensSpan.Length && tokensSpan[j].StartPos < nextLine; ++j)
                {
                    ScriptToken token = tokensSpan[j];
                    syntaxSpans.Add(new SyntaxSpan(token.SyntaxType, token.EndPos - token.StartPos, token.StartPos));
                }
            }
            //last line
            lineToIndex.Add(j);
            for (; j < tokensSpan.Length; ++j)
            {
                ScriptToken token = tokensSpan[j];
                syntaxSpans.Add(new SyntaxSpan(token.SyntaxType, token.EndPos - token.StartPos, token.StartPos));
            }

            Dictionary<int, SyntaxSpan> commentSpans = null;
            if (tokens.Comments is not null)
            {
                commentSpans = new Dictionary<int, SyntaxSpan>(tokens.Comments.Count);
                foreach ((int line, ScriptToken token) in tokens.Comments)
                {
                    commentSpans.Add(line, new SyntaxSpan(token.SyntaxType, token.EndPos - token.StartPos, token.StartPos));
                }
            }

            return new SyntaxInfo(lineToIndex, syntaxSpans, commentSpans);
        }

        public void Dispose()
        {
            _parseCts?.Cancel();
            _parseCts?.Dispose();
        }
    }
}
