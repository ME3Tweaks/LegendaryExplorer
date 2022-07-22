using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Parsing;

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
                }

                if (foldingManager != null)
                {
                    FoldingManager.Uninstall(foldingManager);
                }
                Document = new TextDocument(value);
                foldingManager = FoldingManager.Install(textEditor.TextArea);
                foldingStrategy.UpdateFoldings(foldingManager, Document);
                Document.TextChanged += TextChanged;
            });
        }

        public ICommand FindUsagesInFileCommand { get; set; }

        public UnrealScriptIDE() : base("UnrealScript IDE")
        {
            InitializeComponent();
            DataContext = this;
            progressBarTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            progressBarTimer.Tick += ProgressBarTimer_Tick;
            IsBusy = true;
            BusyText = "Initializing Script Compiler";

            textEditor.TextArea.TextEntered += TextAreaOnTextEntered;
            _definitionLinkGenerator = new DefinitionLinkGenerator(ScrollTo);
            textEditor.TextArea.TextView.ElementGenerators.Add(_definitionLinkGenerator);

            FindUsagesInFileCommand = new GenericCommand(FindUsagesInFile, CanFindReferences);
        }

        public override bool CanParse(ExportEntry exportEntry) =>
            exportEntry.Game != MEGame.UDK 
            && (exportEntry.FileRef.Platform == MEPackage.GamePlatform.PC || exportEntry.Game.IsLEGame()) // LE games all should have identical bytecode, but we do not support it (but some users might try anyways)
            && (exportEntry.ClassName switch
            {
                "Class" => true,
                "State" => true,
                "Function" => true,
                "Enum" => true,
                "ScriptStruct" => true,
                _ => false
            } || exportEntry.IsDefaultObject);

        public override void LoadExport(ExportEntry export)
        {
            if (CurrentLoadedExport != export)
            {
                UnloadExport();
            }
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
                    CurrentFileLib?.InitializeAsync();
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
                    CurrentFileLib?.InitializeAsync();
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
            ScriptText = string.Empty;
            OutputListBox.ItemsSource = null;
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                var elhw = new ExportLoaderHostedWindow(new UnrealScriptIDE(), CurrentLoadedExport)
                {
                    Title = $"Script Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
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
            }
            textEditor.TextArea.TextEntered -= TextAreaOnTextEntered;
        }


        private void ExportLoaderControl_Loaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window is { })
            {
                window.Closed += (_, _) => UnloadFileLib();
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
                BusyProgressBarValue = 20;
            }
            else if (BusyProgressBarValue < BusyProgressBarMax)
            {
                //we're making these values up
                BusyProgressBarValue += Math.Min(15, Math.Max((BusyProgressBarMax - BusyProgressBarValue) / 5, 2));
            }

            if (BusyProgressBarValue >= BusyProgressBarMax)
            {
                BusyProgressIndeterminate = true;
            }
        }

        private void UnloadFileLib()
        {
            if (CurrentFileLib is { })
            {
                CurrentFileLib.Dispose();
                CurrentFileLib.InitializationStatusChange -= CurrentFileLibOnInitialized;
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
                    CurrentFileLib?.InitializeAsync();
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

                CurrentFileLib?.InitializeAsync();
            }
            else
            {
                IsBusy = false;
            }
        }

        #endregion

        private void outputListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems?.Count == 1 && e.AddedItems[0] is PositionedMessage msg)
            {
                ScrollTo(msg.Start, msg.End - msg.Start);
            }
        }

        private void ScrollTo(int start, int length)
        {
            textEditor.Focus();
            textEditor.Select(start, length);
            var location = textEditor.Document.GetLocation(start);
            textEditor.ScrollTo(location.Line, location.Column);
        }

        private void Compile_OnClick(object sender, RoutedEventArgs e)
        {
            string scriptText = ScriptText;
            if (scriptText != null && CurrentLoadedExport != null)
            {
                if (CurrentLoadedExport.IsDefaultObject)
                {
                    (_, MessageLog log) =  UnrealScriptCompiler.CompileDefaultProperties(CurrentLoadedExport, scriptText, CurrentFileLib);
                    OutputListBox.ItemsSource = log?.Content;
                }
                else
                {
                    switch (CurrentLoadedExport.ClassName)
                    {
                        case "Class":
                            {
                                (_, MessageLog log) = UnrealScriptCompiler.CompileClass(Pcc, scriptText, CurrentFileLib, CurrentLoadedExport, CurrentLoadedExport.Parent);
                                OutputListBox.ItemsSource = log?.Content;
                                break;
                            }
                        case "Function":
                        {
                            (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(CurrentLoadedExport, scriptText, CurrentFileLib);
                            OutputListBox.ItemsSource = log?.Content;
                            break;
                        }
                        case "State":
                        {
                            (_, MessageLog log) = UnrealScriptCompiler.CompileState(CurrentLoadedExport, scriptText, CurrentFileLib);
                            OutputListBox.ItemsSource = log?.Content;
                            break;
                        }
                        case "ScriptStruct":
                        {
                            (_, MessageLog log) = UnrealScriptCompiler.CompileStruct(CurrentLoadedExport, scriptText, CurrentFileLib);
                            OutputListBox.ItemsSource = log?.Content;
                            break;
                        }
                        case "Enum":
                        {
                            (_, MessageLog log) = UnrealScriptCompiler.CompileEnum(CurrentLoadedExport, scriptText, CurrentFileLib);
                            OutputListBox.ItemsSource = log?.Content;
                            break;
                        }
                        default:
                            OutputListBox.ItemsSource = new[]
                            {
                                $"{CurrentLoadedExport.ClassName} compilation is not supported."
                            };
                            break;
                    }
                }
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
                    ASTNode ast = UnrealScriptCompiler.ExportToAstNode(CurrentLoadedExport, CurrentFileLib, null);
                    if (ast is null)
                    {
                        ScriptText = "Could not decompile!";
                        return;
                    }
                    _definitionLinkGenerator.Reset();
                    if (FullyInitialized)
                    {
                        var codeBuilder = new CodeBuilderVisitor<PlainTextCodeFormatter>();
                        ast.AcceptVisitor(codeBuilder);
                        string source = codeBuilder.GetOutput();
                        ScriptText = source;
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
                catch (Exception e) //when (!App.IsDebug)
                {
                    ScriptText = $"Error occured while decompiling {CurrentLoadedExport?.InstancedFullPath}:\n\n{e.FlattenException()}";
                }
            });
        }

        private void TextChanged(object sender, EventArgs e)
        {
            Parse(ScriptText);
        }

        private void Parse(string source)
        {
            bool needsTokensReset = true;
            var log = new MessageLog();
            try
            {
                (ASTNode ast, TokenStream tokens) = UnrealScriptCompiler.CompileOutlineAST(source, CurrentLoadedExport.ClassName, log, Pcc.Game, CurrentLoadedExport.IsDefaultObject);

                if (ast != null && !log.HasErrors && FullyInitialized)
                {
                    log.Tokens = tokens;
                    switch (ast)
                    {
                        case Class cls:
                            ast = UnrealScriptCompiler.CompileNewClassAST(Pcc, cls, log, CurrentFileLib, out bool vfTableChanged);
                            if (vfTableChanged)
                            {
                                log.LogWarning("Compiling will cause Virtual Function Table to change! All classes that depend on this one will need recompilation to work properly!");
                            }
                            break;
                        case Function func when CurrentLoadedExport.Parent is ExportEntry funcParent:
                            ast = UnrealScriptCompiler.CompileNewFunctionBodyAST(funcParent, func, log, CurrentFileLib);
                            break;
                        case State state when CurrentLoadedExport.Parent is ExportEntry stateParent:
                            ast = UnrealScriptCompiler.CompileNewStateBodyAST(stateParent, state, log, CurrentFileLib);
                            break;
                        case Struct strct when CurrentLoadedExport.Parent is ExportEntry structParent:
                            ast = UnrealScriptCompiler.CompileNewStructAST(structParent, strct, log, CurrentFileLib);
                            break;
                        case Enumeration enumeration when CurrentLoadedExport.Parent is ExportEntry enumParent:
                            ast = UnrealScriptCompiler.CompileNewEnumAST(enumParent, enumeration, log, CurrentFileLib);
                            break;
                        case VariableDeclaration varDecl when CurrentLoadedExport.Parent is ExportEntry varParent:
                            ast = UnrealScriptCompiler.CompileNewVarDeclAST(varParent, varDecl, log, CurrentFileLib);
                            break;
                        case DefaultPropertiesBlock propertiesBlock when CurrentLoadedExport.Class is ExportEntry classExport:
                            ast = UnrealScriptCompiler.CompileDefaultPropertiesAST(classExport, propertiesBlock, log, CurrentFileLib);
                            break;
                        default:
                            return;
                    }
                    log.Tokens = null;

                    _definitionLinkGenerator.SetTokens(tokens);
                    needsTokensReset = false;

                    SetSyntaxHighlighting(tokens);
                }
            }
            catch (ParseException)
            {
                log.LogError("Parse Failed!");
            }
            catch (Exception exception) // when (!LegendaryExplorerCoreLib.IsDebug)
            {
                log.LogError($"Exception: {exception.Message}");
            }
            finally
            {
                if (needsTokensReset)
                {
                    _definitionLinkGenerator.Reset();
                }
                OutputListBox.ItemsSource = log.Content;
            }
        }

        private void SetSyntaxHighlighting(TokenStream tokens)
        {
            List<int> lineLookup = tokens.LineLookup.Lines;
            if (!tokens.Any() || lineLookup.Count <= 0)
            {
                textEditor.SyntaxHighlighting = new SyntaxInfo();
                return;
            }
            var lineToIndex = new List<int>(lineLookup.Count);

            var tokensSpan = tokens.TokensSpan;

            var syntaxSpans = new List<SyntaxSpan>(tokensSpan.Length);

            int i = 0, j = 0;
            for (; i < lineLookup.Count - 1 && j < tokensSpan.Length; ++i)
            {
                int nextLine = lineLookup[i + 1];

                lineToIndex.Add(j);
                for (;j < tokensSpan.Length && tokensSpan[j].StartPos < nextLine; ++j)
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

            textEditor.SyntaxHighlighting = new SyntaxInfo(lineToIndex, syntaxSpans, commentSpans);
        }


        //CompletionWindow completionWindow;
        private void TextAreaOnTextEntered(object sender, TextCompositionEventArgs e)
        {
            //TODO: code completion
            // if (e.Text == ".")
            // {
            //     completionWindow = new CompletionWindow(textEditor.TextArea);
            //     IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;
            //     data.Add(new CompletionData("foo", "baz"));
            //     data.Add(new CompletionData("bar"));
            //     completionWindow.Show();
            //     completionWindow.Closed += delegate {
            //         completionWindow = null;
            //     };
            // }
        }

        private void CompileAST_OnClick(object sender, RoutedEventArgs e)
        {
            string scriptText = ScriptText;
            if (scriptText != null)
            {
                var log = new MessageLog();
                (ASTNode ast, _) = UnrealScriptCompiler.CompileOutlineAST(scriptText, CurrentLoadedExport.ClassName, log, Pcc.Game, CurrentLoadedExport.IsDefaultObject);

                if (ast != null && !log.HasErrors)
                {
                    if (FullyInitialized)
                    {
                        if (ast is DefaultPropertiesBlock propBlock)
                        {
                            if (CurrentLoadedExport.Class is ExportEntry classExport)
                            {
                                ast = UnrealScriptCompiler.CompileDefaultPropertiesAST(classExport, propBlock, log, CurrentFileLib);
                            }
                        }
                        else if (ast is Class cls)
                        {
                            ast = UnrealScriptCompiler.CompileNewClassAST(Pcc, cls, log, CurrentFileLib, out bool vfTableChanged);
                            if (vfTableChanged)
                            {
                                log.LogWarning("Virtual function table changed!");
                            }
                        }
                        else if (CurrentLoadedExport.Parent is ExportEntry parentExport)
                        {
                            ast = ast switch
                            {
                                Function func => UnrealScriptCompiler.CompileNewFunctionBodyAST(parentExport, func, log, CurrentFileLib),
                                State state => UnrealScriptCompiler.CompileNewStateBodyAST(parentExport, state, log, CurrentFileLib),
                                Struct strct => UnrealScriptCompiler.CompileNewStructAST(parentExport, strct, log, CurrentFileLib),
                                Enumeration enumeration => UnrealScriptCompiler.CompileNewEnumAST(parentExport, enumeration, log, CurrentFileLib),
                                VariableDeclaration varDecl => UnrealScriptCompiler.CompileNewVarDeclAST(parentExport, varDecl, log, CurrentFileLib),
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

        private bool CanFindReferences() => contextMenuDefinitionNode is Function && CurrentFileLib.IsInitialized;

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
                case EnumValue enumValue:
                    itemName = enumValue.Name;
                    break;
                default:
                    MessageBox.Show($"Cannot find usages of a {definitonNode.GetType().FullName}.");
                    return;
            }
            BusyText = $"Finding usages of {itemName}...";
            Task.Run(() =>
            {
                try
                {
                    switch (definitonNode)
                    {
                        case Function func:
                            return UnrealScriptLookup.FindUsagesInFile(func, CurrentFileLib);
                        case VariableDeclaration varDecl:
                            break;
                        case VariableType varType:
                            break;
                        case EnumValue enumValue:
                            break;
                    }
                    return null;
                }
                catch (Exception e)
                {
                    return new List<EntryStringPair> { new EntryStringPair($"Error occured: {e.FlattenException()}") };
                }
            }).ContinueWithOnUIThread(prevTask =>
            {
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

    }
}