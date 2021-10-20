using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
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

        private ASTNode _rootNode;
        public ASTNode RootNode
        {
            get => _rootNode;
            set => SetProperty(ref _rootNode, value);
        }

        public UnrealScriptIDE() : base("UnrealScript IDE")
        {
            InitializeComponent();
            DataContext = this;
            progressBarTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            progressBarTimer.Tick += ProgressBarTimer_Tick;
            IsBusy = true;
            BusyText = "Initializing Script Compiler";

            textEditor.TextArea.TextEntered += TextAreaOnTextEntered;
            _definitionLinkGenerator = new DefinitionLinkGenerator();
            textEditor.TextArea.TextView.ElementGenerators.Add(_definitionLinkGenerator);
        }

        public override bool CanParse(ExportEntry exportEntry) =>
            exportEntry.Game != MEGame.UDK && exportEntry.FileRef.Platform == MEPackage.GamePlatform.PC && (exportEntry.ClassName switch
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
                CurrentFileLib = new FileLib(Pcc);
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
            RootNode = null;
            outputListBox.ItemsSource = null;
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
                            Dispatcher.Invoke(() => new ListDialog(CurrentFileLib.InitializationLog.Content.Select(msg => msg.ToString()), 
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
                textEditor.Focus();
                textEditor.Select(msg.Start.CharIndex, msg.End.CharIndex - msg.Start.CharIndex);
                textEditor.ScrollToLine(msg.Line);
            }
        }

        private void Compile_OnClick(object sender, RoutedEventArgs e)
        {
            if (ScriptText != null && CurrentLoadedExport != null)
            {
                if (CurrentLoadedExport.IsDefaultObject)
                {
                    (_, MessageLog log) =  UnrealScriptCompiler.CompileDefaultProperties(CurrentLoadedExport, ScriptText, CurrentFileLib);
                    outputListBox.ItemsSource = log?.Content;
                }
                else
                {
                    switch (CurrentLoadedExport.ClassName)
                    {
                        case "Function":
                        {
                            (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(CurrentLoadedExport, ScriptText, CurrentFileLib);
                            outputListBox.ItemsSource = log?.Content;
                            break;
                        }
                        case "State":
                        {
                            (_, MessageLog log) = UnrealScriptCompiler.CompileState(CurrentLoadedExport, ScriptText, CurrentFileLib);
                            outputListBox.ItemsSource = log?.Content;
                            break;
                        }
                        case "ScriptStruct":
                        {
                            (_, MessageLog log) = UnrealScriptCompiler.CompileStruct(CurrentLoadedExport, ScriptText, CurrentFileLib);
                            outputListBox.ItemsSource = log?.Content;
                            break;
                        }
                        case "Enum":
                        {
                            (_, MessageLog log) = UnrealScriptCompiler.CompileEnum(CurrentLoadedExport, ScriptText, CurrentFileLib);
                            outputListBox.ItemsSource = log?.Content;
                            break;
                        }
                        default:
                            outputListBox.ItemsSource = new[]
                            {
                                $"{CurrentLoadedExport.ClassName} compilation is not yet supported and will be added in a future update."
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
            try
            {
                ASTNode ast = UnrealScriptCompiler.ExportToAstNode(CurrentLoadedExport, CurrentFileLib, null);
                if (ast is null)
                {
                    (RootNode, ScriptText) = (null, "Could not decompile!");
                    return;
                }
                var codeBuilder = new CodeBuilderVisitor<SyntaxInfoCodeFormatter, (string, SyntaxInfo)>();
                ast.AcceptVisitor(codeBuilder);
                (string text, SyntaxInfo syntaxInfo) = codeBuilder.GetOutput();
                Dispatcher.Invoke(() =>
                {
                    textEditor.SyntaxHighlighting = syntaxInfo;
                });
                _definitionLinkGenerator.Reset();
                Dispatcher.Invoke(() =>
                {
                    if (ast is Function or State or Struct or Enumeration or DefaultPropertiesBlock && FullyInitialized)
                    {
                        try
                        {
                            (ASTNode astNode, MessageLog log, TokenStream<string> tokens) = UnrealScriptCompiler.CompileAST(text, CurrentLoadedExport.ClassName, Pcc.Game, ast is DefaultPropertiesBlock);

                            if (!log.HasErrors)
                            {
                                //compile body to ast so that symbol tokens will be associated with their definitions
                                if (astNode is Function function && CurrentLoadedExport?.Parent is ExportEntry funcParent)
                                {
                                    UnrealScriptCompiler.CompileNewFunctionBodyAST(funcParent, function, log, CurrentFileLib);
                                }
                                else if (astNode is State state && CurrentLoadedExport?.Parent is ExportEntry stateParent)
                                {
                                    UnrealScriptCompiler.CompileNewStateBodyAST(stateParent, state, log, CurrentFileLib);
                                }
                                else if (astNode is Struct strct && CurrentLoadedExport?.Parent is ExportEntry structParent)
                                {
                                    UnrealScriptCompiler.CompileNewStructAST(structParent, strct, log, CurrentFileLib);
                                }
                                else if (astNode is Enumeration enumeration && CurrentLoadedExport?.Parent is ExportEntry enumParent)
                                {
                                    UnrealScriptCompiler.CompileNewEnumAST(enumParent, enumeration, log, CurrentFileLib);
                                }
                                else if (astNode is DefaultPropertiesBlock propertiesBlock && CurrentLoadedExport?.Class is ExportEntry classExport)
                                {
                                    UnrealScriptCompiler.CompileDefaultPropertiesAST(classExport, propertiesBlock, log, CurrentFileLib);
                                }
                                _definitionLinkGenerator.SetTokens(tokens);
                                outputListBox.ItemsSource = log.Content;
                            }
                        }
                        catch (Exception e)
                        {
                            //
                        }
                    }

                    RootNode = ast;
                    ScriptText = text;
                    textEditor.IsReadOnly = RootNode is not (Function or State or Struct or Enumeration or DefaultPropertiesBlock);
                });

            }
            catch (Exception e) when (!App.IsDebug)
            {
                (RootNode, ScriptText) = (null, $"Error occured while decompiling {CurrentLoadedExport?.InstancedFullPath}:\n\n{e.FlattenException()}");
            }
        }

        private void TextChanged(object sender, EventArgs e)
        {
            bool needsTokensReset = true;
            (ASTNode ast, MessageLog log, TokenStream<string> tokens) = UnrealScriptCompiler.CompileAST(ScriptText, CurrentLoadedExport.ClassName, Pcc.Game, CurrentLoadedExport.IsDefaultObject);
            try
            {

                if (ast != null && !log.HasErrors && FullyInitialized && (ast is Function or State or Struct or Enumeration && CurrentLoadedExport.Parent is ExportEntry || ast is DefaultPropertiesBlock && CurrentLoadedExport.Class is ExportEntry))
                {
                    switch (ast)
                    {
                        case Function func:
                            ast = UnrealScriptCompiler.CompileNewFunctionBodyAST((ExportEntry)CurrentLoadedExport.Parent, func, log, CurrentFileLib);
                            break;
                        case State state:
                            ast = UnrealScriptCompiler.CompileNewStateBodyAST((ExportEntry)CurrentLoadedExport.Parent, state, log, CurrentFileLib);
                            break;
                        case Struct strct:
                            ast = UnrealScriptCompiler.CompileNewStructAST((ExportEntry)CurrentLoadedExport.Parent, strct, log, CurrentFileLib);
                            break;
                        case Enumeration enumeration:
                            ast = UnrealScriptCompiler.CompileNewEnumAST((ExportEntry)CurrentLoadedExport.Parent, enumeration, log, CurrentFileLib);
                            break;
                        case DefaultPropertiesBlock propertiesBlock:
                            ast = UnrealScriptCompiler.CompileDefaultPropertiesAST((ExportEntry) CurrentLoadedExport.Class, propertiesBlock, log, CurrentFileLib);
                            break;
                        default:
                            return;
                    }

                    _definitionLinkGenerator.SetTokens(tokens);
                    needsTokensReset = false;
                    var syntaxInfo = new SyntaxInfo();
                    if (tokens.Any())
                    {
                        int firstLine = tokens.First().StartPos.Line - 1;
                        int lastLine = tokens.Last().EndPos.Line - 1;
                        //while (lastLine >= firstLine)
                        //{
                        //    syntaxInfo[lastLine].Clear();
                        //    lastLine--;
                        //}

                        int currentLine = firstLine;
                        int currentPos = 0;
                        foreach (Token<string> token in tokens)
                        {
                            int tokLine = token.StartPos.Line - 1;
                            if (tokLine > currentLine)
                            {
                                currentLine = tokLine;
                                currentPos = 0;
                            }

                            while (syntaxInfo.Count <= currentLine + 1)
                            {
                                syntaxInfo.Add(new List<SyntaxSpan>());
                            }

                            int tokStart = token.StartPos.Column;
                            int tokEnd = token.EndPos.Column;
                            if (tokStart > currentPos)
                            {
                                syntaxInfo[currentLine].Add(new SyntaxSpan(EF.None, tokStart - currentPos));
                            }

                            syntaxInfo[currentLine].Add(new SyntaxSpan(token.SyntaxType, tokEnd - tokStart));
                            currentPos = tokEnd;
                        }
                    }

                    textEditor.SyntaxHighlighting = syntaxInfo;
                }
            }
            catch (ParseException)
            {
                log.LogError("Parse Failed!");
            }
            catch (Exception exception)// when (!LegendaryExplorerCoreLib.IsDebug)
            {
                log.LogError($"Exception: {exception.Message}");
            }
            finally
            {
                if (needsTokensReset)
                {
                    _definitionLinkGenerator.Reset();
                }
                outputListBox.ItemsSource = log.Content;
            }

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
            if (ScriptText != null)
            {
                MessageLog log;
                (RootNode, log, _) = UnrealScriptCompiler.CompileAST(ScriptText, CurrentLoadedExport.ClassName, Pcc.Game, CurrentLoadedExport.IsDefaultObject);

                if (RootNode != null && !log.HasErrors)
                {
                    if (FullyInitialized)
                    {
                        if (RootNode is DefaultPropertiesBlock propBlock)
                        {
                            if (CurrentLoadedExport.Class is ExportEntry classExport)
                            {
                                RootNode = UnrealScriptCompiler.CompileDefaultPropertiesAST(classExport, propBlock, log, CurrentFileLib);
                            }
                        }
                        else if (CurrentLoadedExport.Parent is ExportEntry parentExport)
                        {
                            RootNode = RootNode switch
                            {
                                Function func => UnrealScriptCompiler.CompileNewFunctionBodyAST(parentExport, func, log, CurrentFileLib),
                                State state => UnrealScriptCompiler.CompileNewStateBodyAST(parentExport, state, log, CurrentFileLib),
                                Struct strct => UnrealScriptCompiler.CompileNewStructAST(parentExport, strct, log, CurrentFileLib),
                                Enumeration enumeration => UnrealScriptCompiler.CompileNewEnumAST(parentExport, enumeration, log, CurrentFileLib),
                                _ => RootNode
                            };
                        }
                    }
                    var codeBuilder = new CodeBuilderVisitor<SyntaxInfoCodeFormatter, (string, SyntaxInfo)>();
                    RootNode.AcceptVisitor(codeBuilder);
                    (string text, SyntaxInfo syntaxInfo) = codeBuilder.GetOutput();
                    ScriptText = text;
                    textEditor.SyntaxHighlighting = syntaxInfo;
                }

                outputListBox.ItemsSource = log.Content;
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
    }
}