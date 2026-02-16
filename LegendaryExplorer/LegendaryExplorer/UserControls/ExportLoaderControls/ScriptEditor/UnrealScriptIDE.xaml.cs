using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Folding;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Language.Util;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Parsing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor
{
    /// <summary>
    /// Interaction logic for UnrealScriptIDE.xaml
    /// </summary>
    public partial class UnrealScriptIDE : ExportLoaderControl
    {
        public string ScriptText
        {
            get => Document?.Text;
            set => Dispatcher.Invoke(() =>
            {
                if (Document is not null)
                {
                    Document.TextChanged -= TextChanged;
                    Document.Changed -= DocumentOnChanged;
                    Document.UpdateStarted -= DocumentOnUpdateStarted;
                }

                if (foldingManager != null)
                {
                    FoldingManager.Uninstall(foldingManager);
                }
                _definitionLinkGenerator.Reset();
                textEditor.SyntaxHighlighting = SyntaxInfo.None;
                Document = new TextDocument(value);
                foldingManager = FoldingManager.Install(textEditor.TextArea);
                foldingStrategy.UpdateFoldings(foldingManager, Document);
                Document.TextChanged += TextChanged;
                Document.Changed += DocumentOnChanged;
                Document.UpdateStarted += DocumentOnUpdateStarted;
            });
        }

        public ICommand FindUsagesInFileCommand { get; set; }
        public ICommand GoToDefinitionCommand { get; set; }
        public ICommand ToggleCommentCommand { get; set; }
        public ICommand IncreaseFontSizeCommand { get; set; }
        public ICommand DecreaseFontSizeCommand { get; set; }
        public ICommand IncreaseIndentCommand { get; set; }
        public ICommand DecreaseIndentCommand { get; set; }
        public ICommand AddCommentCommand { get; set; }
        public ICommand UncommentCommand { get; set; }

        public UnrealScriptIDE() : base("UnrealScript IDE")
        {
            InitializeComponent();
            DataContext = this;
            progressBarTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            progressBarTimer.Tick += ProgressBarTimer_Tick;
            _parseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _parseTimer.Tick += ParseTimerTick;
            IsBusy = true;
            BusyText = "Initializing Script Compiler";

            textEditor.TextArea.TextEntered += TextAreaOnTextEntered;
            textEditor.TextArea.TextEntering += TextAreaOnTextEntering;
            _definitionLinkGenerator = new DefinitionLinkGenerator(ScrollTo);
            textEditor.TextArea.TextView.ElementGenerators.Add(_definitionLinkGenerator);

            FindUsagesInFileCommand = new GenericCommand(FindUsagesInFile, CanFindReferences);
            GoToDefinitionCommand = new GenericCommand(() => VisualLineDefinitionLinkText.GoToDefinition(contextMenuDefinitionNode, ScrollTo), () => contextMenuDefinitionNode is not null && CurrentFileLib?.IsInitialized == true);
            ToggleCommentCommand = new GenericCommand(() => ToggleComment(CommentAction.Toggle), CanApplyTextEdit);

            IncreaseFontSizeCommand = new GenericCommand(() => textEditor.UpdateFontSize(true));
            DecreaseFontSizeCommand = new GenericCommand(() => textEditor.UpdateFontSize(false));
            IncreaseIndentCommand = new GenericCommand(() => IndentCode(true), CanApplyTextEdit);
            DecreaseIndentCommand = new GenericCommand(() => IndentCode(false), CanApplyTextEdit);
            AddCommentCommand = new GenericCommand(() => ToggleComment(CommentAction.Add), CanApplyTextEdit);
            UncommentCommand = new GenericCommand(() => ToggleComment(CommentAction.Remove), CanApplyTextEdit);

            ApplyThemeColors();
            SyntaxInfo.ThemeChanged += OnThemeChanged;
        }

        public override bool CanParse(ExportEntry exportEntry) =>
            (exportEntry.FileRef.Platform == MEPackage.GamePlatform.PC || exportEntry.Game.IsLEGame())
            /*&& (exportEntry.ClassName switch
            {
                "Class" => true,
                "State" => true,
                "Function" => true,
                "Enum" => true,
                "ScriptStruct" => true,
                _ => false
            } || exportEntry.IsDefaultObject)*/;

        public override void LoadExport(ExportEntry export)
        {
            if (CurrentLoadedExport != export)
            {
                UnloadExport();
            }

            UnrealScriptOptionsPackage usop = new UnrealScriptOptionsPackage();
            CurrentLoadedExport = export;
            if (Pcc != CurrentFileLib?.Pcc)
            {
                FullyInitialized = false;
                IsBusy = true;
                BusyText = "Compiling local classes";
                UnloadFileLib();
                CurrentFileLib = new FileLib(Pcc, true);
                CurrentFileLib.InitializationStatusChange += CurrentFileLibOnInitialized;
                if (IsVisible)
                {
                    CurrentFileLib?.InitializeAsync(usop);
                }
            }
            else if (CurrentFileLib?.IsInitialized == true)
            {
                FullyInitialized = true;
            }
            else
            {
                FullyInitialized = false;
                IsBusy = true;
                BusyText = "Recompiling local classes";
                if (IsVisible)
                {
                    CurrentFileLib?.InitializeAsync(usop);
                }
            }
            if (!IsBusy)
            {
                Decompile();
            }
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
            AST = null;
            ScriptText = string.Empty;
            OutputListBox.ItemsSource = null;
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                var elhw = new ExportLoaderHostedWindow(new UnrealScriptIDE() { PreloadedText = ScriptText }, CurrentLoadedExport)
                {
                    Title = $"Script Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                // Todo: Transfer current content to the popped out window
                elhw.Show();
            }
        }

        /// <summary>
        /// Text to set once everything has loaded, used during popout
        /// </summary>
        public string PreloadedText { get; set; }

        public override void Dispose()
        {
            AST = null;
            _parseTimer.Stop();
            _parseTimer.Tick -= ParseTimerTick;
            _parseService.Dispose();
            _findUsagesCts?.Cancel();
            _findUsagesCts?.Dispose();

            if (progressBarTimer is not null)
            {
                progressBarTimer.IsEnabled = false; //Stop timer
                progressBarTimer.Tick -= ProgressBarTimer_Tick;
            }

            if (CurrentFileLib is not null)
            {
                CurrentFileLib.InitializationStatusChange -= CurrentFileLibOnInitialized;
            }

            if (Document is not null)
            {
                Document.TextChanged -= TextChanged;
                Document.Changed -= DocumentOnChanged;
            }
            textEditor.TextArea.TextEntered -= TextAreaOnTextEntered;
            textEditor.TextArea.TextEntering -= TextAreaOnTextEntering;
            SyntaxInfo.ThemeChanged -= OnThemeChanged;
            if (_parentWindow is not null && _parentWindowClosedHandler is not null)
            {
                _parentWindow.Closed -= _parentWindowClosedHandler;
            }
        }

        private void ExportLoaderControl_Loaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window is not null && window != _parentWindow)
            {
                // Unsubscribe from previous window if any
                if (_parentWindow is not null && _parentWindowClosedHandler is not null)
                {
                    _parentWindow.Closed -= _parentWindowClosedHandler;
                }
                _parentWindow = window;
                _parentWindowClosedHandler = (_, _) => UnloadFileLib();
                window.Closed += _parentWindowClosedHandler;
            }
        }

        #region Busy variables
        private bool _isBusy;

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    IsBusyChanged?.Invoke(this, EventArgs.Empty); //caller will just fetch and update this value
                }
            }
        }

        public event EventHandler IsBusyChanged;

        private bool _busyProgressIndeterminate = true;
        public bool BusyProgressIndeterminate
        {
            get => _busyProgressIndeterminate;
            set => SetProperty(ref _busyProgressIndeterminate, value);
        }

        private string _busyText;
        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }

        private int _busyProgressBarMax = 100;
        public int BusyProgressBarMax
        {
            get => _busyProgressBarMax;
            set => SetProperty(ref _busyProgressBarMax, value);
        }

        private int _busyProgressBarValue;
        public int BusyProgressBarValue
        {
            get => _busyProgressBarValue;
            set => SetProperty(ref _busyProgressBarValue, value);
        }
        #endregion

        #region ScriptLib Handling

        private bool _fullyInitialized;
        public bool FullyInitialized
        {
            get => _fullyInitialized;
            set => SetProperty(ref _fullyInitialized, value);
        }

        private FileLib CurrentFileLib;

        private const int ProgressInitialValue = 20;
        private const int ProgressMaxStep = 15;
        private const int ProgressRemainingDivisor = 5;
        private const int ProgressMinStep = 2;

        private readonly DispatcherTimer progressBarTimer;
        private void ProgressBarTimer_Tick(object sender, EventArgs e)
        {
            if (!IsBusy)
            {
                progressBarTimer.Stop();
                BusyProgressBarValue = 0;
            }

            BusyProgressIndeterminate = false;
            if (BusyProgressBarValue == 0)
            {
                BusyProgressBarValue = ProgressInitialValue;
            }
            else if (BusyProgressBarValue < BusyProgressBarMax)
            {
                //we're making these values up
                BusyProgressBarValue += Math.Min(ProgressMaxStep, Math.Max((BusyProgressBarMax - BusyProgressBarValue) / ProgressRemainingDivisor, ProgressMinStep));
            }

            if (BusyProgressBarValue >= BusyProgressBarMax)
            {
                BusyProgressIndeterminate = true;
            }
        }

        private void UnloadFileLib()
        {
            if (CurrentFileLib is not null)
            {
                CurrentFileLib.InitializationStatusChange -= CurrentFileLibOnInitialized;
                CurrentFileLib.Dispose();
                CurrentFileLib = null;
            }
        }

        private void CurrentFileLibOnInitialized(bool initialized)
        {
            if (initialized)
            {
                if (IsBusy)
                {
                    IsBusy = false;
                    if (CurrentFileLib?.HadInitializationError == true)
                    {
                        FullyInitialized = false;
                        if (MessageBoxResult.Yes == MessageBox.Show("Could not build script database for this file!\n\n" +
                                            "Functionality will be limited to script decompilation.\n\n\n" +
                                            "Do you want to see the compilation error log?", "Script Error", MessageBoxButton.YesNo))
                        {
                            Dispatcher.Invoke(() => new ListDialog(CurrentFileLib.InitializationLog.AllErrors.Select(msg => msg.ToString()),
                                                                   "Initialization Log", "", Window.GetWindow(this)).Show());
                        }
                    }
                    else
                    {
                        FullyInitialized = CurrentFileLib?.IsInitialized == true;
                    }
                    if (CurrentLoadedExport != null)
                    {
                        Decompile();
                    }
                }
            }
            else
            {
                IsBusy = true;
                BusyText = "Recompiling local classes";
                FullyInitialized = false;
                if (IsVisible)
                {
                    CurrentFileLib?.InitializeAsync(new UnrealScriptOptionsPackage());
                }
            }
        }

        private void ExportLoaderControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true && !FullyInitialized)
            {
                if (!progressBarTimer.IsEnabled)
                {
                    progressBarTimer.Start();
                }

                CurrentFileLib?.InitializeAsync(new UnrealScriptOptionsPackage());
            }
            else
            {
                IsBusy = false;
            }
        }

        #endregion

        private void outputListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems is [PositionedMessage msg])
            {
                ScrollTo(msg.Start, msg.End - msg.Start);
            }
        }

        private void ScrollTo(int start, int length)
        {
            textEditor.Focus();
            int textLength = textEditor.Document.TextLength;
            if (start >= textLength)
            {
                start = textLength;
                length = 0;
            }
            else if (start + length > textLength)
            {
                length = textLength - start;
            }
            textEditor.Select(start, length);
            var location = textEditor.Document.GetLocation(start);
            textEditor.ScrollTo(location.Line, location.Column);
        }

        private void Compile_OnClick(object sender, RoutedEventArgs e)
        {
            string scriptText = ScriptText;
            if (scriptText != null && CurrentLoadedExport != null)
            {
                var result = ScriptCompilationService.Compile(CurrentLoadedExport, scriptText, CurrentFileLib);
                if (result.EarlyReturnMessage is not null)
                {
                    OutputListBox.ItemsSource = new[] { result.EarlyReturnMessage };
                    return;
                }
                OutputListBox.ItemsSource = result.Log?.Content;
            }
        }

        private void Decompile_OnClick(object sender, RoutedEventArgs e)
        {
            if (CurrentLoadedExport != null)
            {
                Decompile();
            }
        }

        private void Decompile()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    ASTNode ast = UnrealScriptCompiler.ExportToAstNode(CurrentLoadedExport, CurrentFileLib, new UnrealScriptOptionsPackage());
                    if (ast is null)
                    {
                        ScriptText = "Could not decompile!";
                        return;
                    }
                    _definitionLinkGenerator.Reset();
                    if (FullyInitialized && !(CurrentLoadedExport.IsClass && CurrentLoadedExport.ObjectNameString is "Object"))
                    {
                        var codeBuilder = new CodeBuilderVisitor<PlainTextCodeFormatter>();
                        ast.AcceptVisitor(codeBuilder);
                        string source = PreloadedText ?? codeBuilder.GetOutput();
                        ScriptText = source;
                        PreloadedText = null; // Do not use after first decompile
                        Parse(source);
                    }
                    else
                    {
                        var codeBuilder = new CodeBuilderVisitor<SyntaxInfoCodeFormatter, (string, SyntaxInfo)>();
                        ast.AcceptVisitor(codeBuilder);
                        (string text, SyntaxInfo syntaxInfo) = codeBuilder.GetOutput();
                        ScriptText = text;
                        textEditor.SyntaxHighlighting = syntaxInfo;
                    }

                }
                catch (Exception e) when (!(App.IsDebug && Debugger.IsAttached))
                {
                    ScriptText = $"/*Error occurred while decompiling {CurrentLoadedExport?.InstancedFullPath}:\n\n{e.FlattenException()}*/";
                }
            });
        }

        private void TextChanged(object sender, EventArgs e)
        {
            _parseTimer.Stop();
            //class parsing is slow enough that we can't do it synchronously without degrading typing responsiveness
            if (CurrentLoadedExport.IsClass)
            {
                _parseTimer.Start();
            }
            else
            {
                Parse(ScriptText);
            }
        }

        private ASTNode AST;
        private readonly DispatcherTimer _parseTimer;
        private readonly ScriptParseService _parseService = new();
        private CancellationTokenSource _findUsagesCts;

        private void ParseTimerTick(object sender, EventArgs e)
        {
            _parseTimer.Stop();
            _ = ParseAsync(ScriptText);
        }

        private async Task ParseAsync(string source)
        {
            // Capture state on UI thread
            var exportEntry = CurrentLoadedExport;
            if (exportEntry is null) return;

            var result = await _parseService.ParseAsync(source, exportEntry, exportEntry.FileRef.Game, CurrentFileLib, FullyInitialized);
            if (result.HasValue)
            {
                ApplyParseResult(result.Value);
            }
        }

        private void Parse(string source)
        {
            var exportEntry = CurrentLoadedExport;
            if (exportEntry is null) return;
            var result = ScriptParseService.ParseCore(source, exportEntry, exportEntry.FileRef.Game, CurrentFileLib, FullyInitialized);
            ApplyParseResult(result);
        }

        /// <summary>
        /// Applies parse results to the UI. Must be called on the UI thread.
        /// </summary>
        private void ApplyParseResult(ScriptParseService.ParseResult result)
        {
            AST = result.AST;
            if (!result.NeedsTokensReset && result.Tokens != null)
            {
                _definitionLinkGenerator.SetTokens(result.Tokens);
                textEditor.SyntaxHighlighting = ScriptParseService.BuildSyntaxHighlighting(result.Tokens);
            }
            else
            {
                _definitionLinkGenerator.Reset();
            }
            result.Log?.SortLog();
            OutputListBox.ItemsSource = result.Log?.Content;
        }

        private CompletionWindow completionWindow;
        private void TextAreaOnTextEntered(object sender, TextCompositionEventArgs e)
        {
            //if (completionWindow is not null)
            //{
            //    return;
            //}
            if (_parseTimer.IsEnabled)
            {
                if (e.Text == ".")
                {
                    // Parse immediately so tokens/AST are up-to-date for completions
                    _parseTimer.Stop();
                    _parseService.CancelCurrentParse();
                    Parse(ScriptText);
                }
                else
                {
                    return;
                }
            }
            TokenStream tokens = _definitionLinkGenerator.Tokens;
            int currentTokenIdx = tokens.GetIndexOfTokenAtOffset(textEditor.TextArea.Caret.Offset - 1);
            if (currentTokenIdx < 0)
            {
                return;
            }
            ReadOnlySpan<ScriptToken> tokensSpan = tokens.TokensSpan;
            ScriptToken currentToken = tokensSpan[currentTokenIdx];
            switch (currentToken.Type)
            {
                case TokenType.Dot when currentTokenIdx > 0:
                    {
                        DisplayCompletions(tokensSpan, currentTokenIdx);
                        break;
                    }
                    //case TokenType.Word when currentToken.Value.Length is 1 && GetDefinitionFromToken(currentToken) is ErrorType errorType:
                    //{
                    //    //DisplayCompletions(tokensSpan, currentTokenIdx + 1);
                    //    break;
                    //}
                    //case TokenType.Word when currentToken.Value.Length == 1 && completionWindow is null:
                    //{

                    //    break;
                    //}
            }

        }

        private void DisplayCompletions(ReadOnlySpan<ScriptToken> tokens, int currentTokenIdx)
        {
            ScriptToken prevToken = tokens[currentTokenIdx - 1];
            ASTNode definition = GetDefinitionFromToken(prevToken);
            definition = definition switch
            {
                VariableDeclaration decl => decl.VarType,
                _ => definition
            };

            var context = new CompletionContext
            {
                Definition = definition,
                PrevToken = prevToken,
                Tokens = _definitionLinkGenerator.Tokens,
                CurrentTokenIdx = currentTokenIdx,
                CurrentClass = NodeUtils.GetContainingClass(AST),
                Game = Pcc.Game,
                GetDefinitionFromToken = GetDefinitionFromToken,
            };

            var completionData = new List<ICompletionData>();
            foreach (ICompletionProvider provider in _completionProviders)
            {
                if (provider.CanProvide(definition, prevToken))
                {
                    provider.AddCompletions(completionData, context);
                }
            }

            if (completionData.Count > 0)
            {
                completionWindow?.Close();
                completionWindow = new LEXCompletionWindow(textEditor.TextArea)
                {
                    SizeToContent = SizeToContent.WidthAndHeight
                };
                IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;
                foreach (ICompletionData completion in completionData)
                {
                    data.Add(completion);
                }
                completionWindow.Show();
                completionWindow.Closed += delegate { completionWindow = null; };
            }
        }

        private ASTNode GetDefinitionFromToken(ScriptToken token)
        {
            return _definitionLinkGenerator.GetDefinitionFromOffset(token.StartPos);
        }

        private void CompileAST_OnClick(object sender, RoutedEventArgs e)
        {
            string scriptText = ScriptText;
            if (scriptText != null)
            {
                var log = new MessageLog();
                (ASTNode ast, _) = UnrealScriptCompiler.CompileOutlineAST(scriptText, CurrentLoadedExport.ClassName, log, Pcc.Game);

                if (ast != null && !log.HasErrors)
                {
                    if (FullyInitialized)
                    {
                        UnrealScriptOptionsPackage usop = new UnrealScriptOptionsPackage();
                        if (ast is DefaultPropertiesBlock propBlock)
                        {
                            ast = UnrealScriptCompiler.CompileDefaultPropertiesAST(propBlock, log, CurrentFileLib, CurrentLoadedExport, usop);
                        }
                        else if (ast is Class cls)
                        {
                            ast = UnrealScriptCompiler.CompileNewClassAST(Pcc, cls, log, CurrentFileLib, out bool vfTableChanged, usop);
                            if (vfTableChanged)
                            {
                                log.LogWarning("Virtual function table changed!");
                            }
                        }
                        else if (CurrentLoadedExport.Parent is ExportEntry parentExport)
                        {
                            ast = ast switch
                            {
                                Function func => UnrealScriptCompiler.CompileNewFunctionBodyAST(parentExport, func, log, CurrentFileLib, usop),
                                State state => UnrealScriptCompiler.CompileNewStateBodyAST(parentExport, state, log, CurrentFileLib, usop),
                                Struct strct => UnrealScriptCompiler.CompileNewStructAST(parentExport, strct, log, CurrentFileLib, usop),
                                Enumeration enumeration => UnrealScriptCompiler.CompileNewEnumAST(parentExport, enumeration, log, CurrentFileLib, usop),
                                VariableDeclaration varDecl => UnrealScriptCompiler.CompileNewVarDeclAST(parentExport, varDecl, log, CurrentFileLib, usop),
                                _ => ast
                            };
                        }
                    }
                    var codeBuilder = new CodeBuilderVisitor<SyntaxInfoCodeFormatter, (string, SyntaxInfo)>();
                    ast?.AcceptVisitor(codeBuilder);
                    (string text, SyntaxInfo syntaxInfo) = codeBuilder.GetOutput();
                    ScriptText = text;
                    textEditor.SyntaxHighlighting = syntaxInfo;
                }

                OutputListBox.ItemsSource = log.Content;
            }
        }

        #region AvalonEditor

        private TextDocument _document;
        public TextDocument Document
        {
            get => _document;
            set => SetProperty(ref _document, value);
        }

        private FoldingManager foldingManager;
        private readonly BraceFoldingStrategy foldingStrategy = new();
        private readonly DefinitionLinkGenerator _definitionLinkGenerator;

        private readonly ICompletionProvider[] _completionProviders =
        [
            new ClassLiteralCompletionProvider(),
            new ObjectTypeCompletionProvider(),
            new EnumCompletionProvider(),
            new DynamicArrayCompletionProvider(),
            new ClassAccessCompletionProvider(),
        ];

        private void DocumentOnUpdateStarted(object sender, EventArgs e)
        {
            _definitionLinkGenerator.Reset();
        }

        private void DocumentOnChanged(object sender, DocumentChangeEventArgs e)
        {
            (textEditor.SyntaxHighlighting as SyntaxInfo)?.AdjustForChange(e.Offset, e.InsertionLength, e.RemovalLength);
        }

        #endregion

        private ASTNode contextMenuDefinitionNode;

        private void TextEditor_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var position = textEditor.TextArea.TextView.GetPosition(Mouse.GetPosition(textEditor.TextArea.TextView) + textEditor.TextArea.TextView.ScrollOffset);
            if (position is null)
            {
                contextMenuDefinitionNode = null;
                return;
            }
            var lineLength = textEditor.Document.GetLineByNumber(position.Value.Line).Length + 1;
            if (position.Value.Column == lineLength)
            {
                contextMenuDefinitionNode = null;
                return;
            }
            int offset = textEditor.Document.GetOffset(position.Value.Location);
            contextMenuDefinitionNode = _definitionLinkGenerator.GetDefinitionFromOffset(offset);
        }
        private void TextEditor_OnContextMenuClosing(object sender, ContextMenuEventArgs e) => contextMenuDefinitionNode = null;

        private bool CanFindReferences() => contextMenuDefinitionNode is Function or VariableDeclaration { Outer: ObjectType } or VariableType && CurrentFileLib?.IsInitialized == true;

        private void FindUsagesInFile()
        {
            ASTNode definitonNode = contextMenuDefinitionNode;
            IsBusy = true;
            BusyProgressIndeterminate = true;
            string itemName;
            switch (definitonNode)
            {
                case Function func:
                    itemName = func.Name;
                    break;
                case VariableDeclaration varDecl:
                    itemName = varDecl.Name;
                    break;
                case VariableType varType:
                    itemName = varType.Name;
                    break;
                // EnumValue find-usages is not yet supported
                default:
                    MessageBox.Show($"Cannot find usages of a {definitonNode.GetType().FullName}.");
                    return;
            }
            BusyText = $"Finding usages of {itemName}...";
            _findUsagesCts?.Cancel();
            var findCts = new CancellationTokenSource();
            _findUsagesCts = findCts;
            Task.Run(() =>
            {
                try
                {
                    UnrealScriptOptionsPackage usop = new UnrealScriptOptionsPackage();
                    switch (definitonNode)
                    {
                        case Function func:
                            return UnrealScriptLookup.FindUsagesInFile(func, CurrentFileLib, usop);
                        case VariableDeclaration varDecl:
                            return UnrealScriptLookup.FindUsagesInFile(varDecl, CurrentFileLib, usop);
                        case VariableType varType:
                            return UnrealScriptLookup.FindUsagesInFile(varType, CurrentFileLib, usop);
                    }
                    return null;
                }
                catch (Exception e)
                {
                    return [new EntryStringPair($"Error occured: {e.FlattenException()}")];
                }
            }, findCts.Token).ContinueWithOnUIThread(prevTask =>
            {
                if (findCts.Token.IsCancellationRequested) return;
                IsBusy = false;
                if (prevTask.Result is null)
                {
                    return;
                }
                if (prevTask.Result.IsEmpty())
                {
                    MessageBox.Show($"No usages of '{itemName}' found in this file.");
                    return;
                }
                new ListDialog(prevTask.Result, $"Usages of {itemName}", "", Window.GetWindow(this))
                {
                    DoubleClickEntryHandler = entryItem =>
                    {
                        if (entryItem?.Openable is LEXOpenable openable)
                        {
                            var p = new PackageEditorWindow();
                            p.Show();
                            p.LoadFile(openable.FilePath, openable.EntryUIndex);
                            p.Activate();
                        }
                    }
                }.Show();
            });
        }

        private bool CanApplyTextEdit() => Document is not null;

        private void ToggleComment(CommentAction action)
        {
            TextArea textArea = textEditor.TextArea;
            Selection selection = textArea.Selection;
            if (!selection.IsEmpty)
            {
                int startLineNum = selection.StartPosition.Line;
                int endLineNum = selection.EndPosition.Line;
                if (startLineNum > endLineNum)
                {
                    (startLineNum, endLineNum) = (endLineNum, startLineNum);
                }
                DocumentLine startLine = Document.GetLineByNumber(startLineNum);
                DocumentLine endLine = Document.GetLineByNumber(endLineNum);
                textArea.Selection = selection = Selection.Create(textArea, startLine.Offset, endLine.EndOffset);
                string[] lines = selection.GetText().Split('\n');
                string[] result = ScriptTextEditingService.ToggleCommentLines(lines, action);
                if (result is null) return;
                textArea.PerformTextInput(string.Join('\n', result));
                startLine = Document.GetLineByNumber(startLineNum);
                endLine = Document.GetLineByNumber(endLineNum);
                textArea.Selection = Selection.Create(textArea, startLine.Offset, endLine.EndOffset);
            }
            else
            {
                DocumentLine line = Document.GetLineByNumber(textArea.Caret.Line);
                string lineText = Document.GetText(line);
                textArea.Selection = Selection.Create(textArea, line.Offset, line.EndOffset);
                textArea.PerformTextInput(ScriptTextEditingService.ToggleCommentSingleLine(lineText));
            }
        }

        private void IndentCode(bool indent = true)
        {
            TextArea textArea = textEditor.TextArea;
            Selection selection = textArea.Selection;
            if (!selection.IsEmpty)
            {
                int startLineNum = selection.StartPosition.Line;
                int endLineNum = selection.EndPosition.Line;
                if (startLineNum > endLineNum)
                {
                    (startLineNum, endLineNum) = (endLineNum, startLineNum);
                }
                DocumentLine startLine = Document.GetLineByNumber(startLineNum);
                DocumentLine endLine = Document.GetLineByNumber(endLineNum);
                textArea.Selection = selection = Selection.Create(textArea, startLine.Offset, endLine.EndOffset);
                string[] lines = selection.GetText().Split('\n');
                string[] result = ScriptTextEditingService.IndentLines(lines, indent);
                textArea.PerformTextInput(string.Join('\n', result));
                startLine = Document.GetLineByNumber(startLineNum);
                endLine = Document.GetLineByNumber(endLineNum);
                textArea.Selection = Selection.Create(textArea, startLine.Offset, endLine.EndOffset);
            }
            else
            {
                DocumentLine line = Document.GetLineByNumber(textArea.Caret.Line);
                string lineText = Document.GetText(line);
                var result = ScriptTextEditingService.IndentSingleLine(lineText, indent);
                if (result is null) return;
                var caretPos = textArea.Caret.Offset;
                textArea.Selection = Selection.Create(textArea, line.Offset, line.EndOffset);
                textArea.PerformTextInput(result.Value.lineText);
                textArea.Caret.Offset = caretPos + result.Value.caretDelta;
            }
        }

        private Window _parentWindow;
        private EventHandler _parentWindowClosedHandler;
        private readonly ToolTip _hoverToolTip = new();

        private void TextEditor_OnMouseHover(object sender, MouseEventArgs e)
        {
            var position = textEditor.GetPositionFromPoint(e.GetPosition(textEditor));
            if (textEditor.Document is not null && position is TextViewPosition pos)
            {
                TokenStream tokens = _definitionLinkGenerator.Tokens;
                int currentTokenIdx = tokens.GetIndexOfTokenAtOffset(textEditor.Document.GetOffset(pos.Location));
                ReadOnlySpan<ScriptToken> tokensSpan = tokens.TokensSpan;
                if (currentTokenIdx < 0 || currentTokenIdx >= tokensSpan.Length)
                {
                    return;
                }
                ScriptToken currentToken = tokensSpan[currentTokenIdx];

                if (currentToken.Type is TokenType.StringRefLiteral)
                {
                    //Value does not include the $ for some reason? 
                    if (int.TryParse(currentToken.Value, out int strRef))
                    {
                        SetStringTooltip(TLKManagerWPF.GlobalFindStrRefbyID(strRef, Pcc) ?? "No Data");
                        e.Handled = true;
                    }
                }
                else if (GetDefinitionFromToken(currentToken) is ASTNode node)
                {
                    switch (node)
                    {
                        case Function func:
                            SetInlinesTooltip(XamlCodeBuilder.GetFunctionSignature(func));
                            e.Handled = true;
                            break;
                        case VariableDeclaration varDecl:
                            SetInlinesTooltip(XamlCodeBuilder.GetVariableDeclarationSignature(varDecl));
                            e.Handled = true;
                            break;
                    }
                }
            }

            void SetInlinesTooltip(IEnumerable<Inline> inlines)
            {
                var textBlock = new TextBlock();
                textBlock.Inlines.AddRange(inlines);
                SetTooltip(textBlock);
            }

            void SetStringTooltip(string text)
            {
                SetInlinesTooltip([new Run(text) { Foreground = SyntaxInfo.ColorBrushes[ST.None] }]);
            }

            void SetTooltip(TextBlock content)
            {
                content.Background = SyntaxInfo.BackgroundBrush;
                _hoverToolTip.Content = content;
                _hoverToolTip.Background = SyntaxInfo.BackgroundBrush;
                _hoverToolTip.PlacementTarget = this; // required for property inheritance
                _hoverToolTip.IsOpen = true;
            }
        }

        private void TextEditor_OnMouseHoverStopped(object sender, MouseEventArgs e)
        {
            _hoverToolTip.IsOpen = false;
        }

        private void TextAreaOnTextEntering(object sender, TextCompositionEventArgs e)
        {
            TextArea textArea = textEditor.TextArea;
            int caretOffset = textArea.Caret.Offset;
            TokenStream tokens = _definitionLinkGenerator.Tokens;
            int currentTokenIdx = tokens.GetIndexOfTokenAtOffset(caretOffset);
            ScriptToken currentToken = currentTokenIdx >= 0 ? tokens.TokensSpan[currentTokenIdx] : null;

            switch (e.Text)
            {
                case "\"":
                    if (currentToken?.Type is TokenType.StringLiteral)
                    {
                        if (caretOffset + 1 == currentToken.EndPos && currentToken.Length != currentToken.Value.Length + 1)
                        {
                            //"overwrite" the existing " at the end of the string
                            textArea.Caret.Offset = caretOffset + 1;
                            e.Handled = true;
                        }
                    }
                    else if (currentTokenIdx < 0)
                    {
                        int prevTokenIdx = ~currentTokenIdx - 1;
                        var prevToken = tokens.TokensSpan[prevTokenIdx];
                        if (prevToken.Type is TokenType.StringLiteral && caretOffset == prevToken.EndPos)
                        {
                            //end of an unterminated string literal. inserting a single " is what we want
                            return;
                        }
                        //not in a token, so insert two " and put the caret between them
                        textArea.PerformTextInput("\"\"");
                        textArea.Caret.Offset = caretOffset + 1;
                        e.Handled = true;
                    }
                    break;
                case "'":
                    if (currentToken?.Type is TokenType.NameLiteral)
                    {
                        if (caretOffset + 1 == currentToken.EndPos && currentToken.Length != currentToken.Value.Length + 1)
                        {
                            //"overwrite" the existing ' at the end of the name
                            textArea.Caret.Offset = caretOffset + 1;
                            e.Handled = true;
                        }
                    }
                    else if (currentTokenIdx < 0)
                    {
                        int prevTokenIdx = ~currentTokenIdx - 1;
                        var prevToken = tokens.TokensSpan[prevTokenIdx];
                        if (prevToken.Type is TokenType.NameLiteral && caretOffset == prevToken.EndPos)
                        {
                            //end of an unterminated name literal. inserting a single ' is what we want
                            return;
                        }
                        //not in a token, so insert two ' and put the caret between them
                        textArea.PerformTextInput("''");
                        textArea.Caret.Offset = caretOffset + 1;
                        e.Handled = true;
                    }
                    break;
                case "{":
                    if (currentTokenIdx < 0)
                    {
                        textArea.PerformTextInput("{}");
                        textArea.Caret.Offset = caretOffset + 1;
                        e.Handled = true;
                    }
                    break;
                case "\n":
                    if (currentToken?.Type is TokenType.RightBracket)
                    {
                        if (currentTokenIdx > 0 && tokens.TokensSpan[currentTokenIdx - 1] is { Type: TokenType.LeftBracket } prevToken)
                        {
                            string inBetweenText = Document.GetText(prevToken.EndPos, currentToken.StartPos - prevToken.EndPos);
                            if (!inBetweenText.Contains('\n'))
                            {
                                string lineText = Document.GetText(Document.GetLineByOffset(caretOffset));
                                var indentation = lineText.AsSpan()[..lineText.CountLeadingWhitespace()];
                                textArea.Selection = Selection.Create(textArea, prevToken.StartPos, currentToken.EndPos);
                                textArea.PerformTextInput($"\n{indentation}{{\n{indentation}    \n{indentation}}}");
                                textArea.Caret.Offset -= indentation.Length + 2;
                                e.Handled = true;
                            }
                        }
                    }
                    break;
            }
        }

        private void OnThemeChanged()
        {
            Dispatcher.Invoke(ApplyThemeColors);
        }

        private void ApplyThemeColors()
        {
            textEditor.Background = SyntaxInfo.BackgroundBrush;
            textEditor.Foreground = SyntaxInfo.ColorBrushes[ST.None];
            textEditor.LineNumbersForeground = SyntaxInfo.ColorBrushes[ST.Keyword];
            textEditor.TextArea.TextView.Redraw();
        }

        private void ThemePicker_OnClick(object sender, RoutedEventArgs e)
        {
            IdeThemePicker.ShowThemeEditor(Window.GetWindow(this));
        }
    }
}