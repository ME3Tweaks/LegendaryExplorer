using System;
using System.Collections.Generic;
using System.Linq;
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
                Document.UpdateStarted += DocumentOnUpdateStarted;
            });
        }

        public ICommand FindUsagesInFileCommand { get; set; }
        public ICommand GoToDefinitionCommand { get; set; }
        public ICommand ToggleCommentCommand { get; set; }

        public UnrealScriptIDE() : base("UnrealScript IDE")
        {
            InitializeComponent();
            DataContext = this;
            progressBarTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            progressBarTimer.Tick += ProgressBarTimer_Tick;
            IsBusy = true;
            BusyText = "Initializing Script Compiler";

            textEditor.TextArea.TextEntered += TextAreaOnTextEntered;
            textEditor.TextArea.TextEntering += TextAreaOnTextEntering;
            _definitionLinkGenerator = new DefinitionLinkGenerator(ScrollTo);
            textEditor.TextArea.TextView.ElementGenerators.Add(_definitionLinkGenerator);

            FindUsagesInFileCommand = new GenericCommand(FindUsagesInFile, CanFindReferences);
            GoToDefinitionCommand = new GenericCommand(() => VisualLineDefinitionLinkText.GoToDefinition(contextMenuDefinitionNode, ScrollTo), () => contextMenuDefinitionNode is not null && CurrentFileLib.IsInitialized);
            ToggleCommentCommand = new GenericCommand(ToggleComment, CanToggleComment);
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
            AST = null;
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
            AST = null;
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
            textEditor.TextArea.TextEntering -= TextAreaOnTextEntering;
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
                if (!CurrentLoadedExport.IsDefaultObject && CurrentLoadedExport.IsInDefaultsTree())
                {
                    OutputListBox.ItemsSource = new[] { "Cannot compile properties of a subobject. You can edit the properties in the subobject declaration in the Default__ object this is descended from." };
                    return;
                }
                MessageLog log;
                switch (CurrentLoadedExport.ClassName)
                {
                    case "Class":
                        (_, log) = UnrealScriptCompiler.CompileClass(Pcc, scriptText, CurrentFileLib, CurrentLoadedExport, CurrentLoadedExport.Parent);
                        break;
                    case "Function":
                        (_, log) = UnrealScriptCompiler.CompileFunction(CurrentLoadedExport, scriptText, CurrentFileLib);
                        break;
                    case "State":
                        (_, log) = UnrealScriptCompiler.CompileState(CurrentLoadedExport, scriptText, CurrentFileLib);
                        break;
                    case "ScriptStruct":
                        (_, log) = UnrealScriptCompiler.CompileStruct(CurrentLoadedExport, scriptText, CurrentFileLib);
                        break;
                    case "Enum":
                        (_, log) = UnrealScriptCompiler.CompileEnum(CurrentLoadedExport, scriptText, CurrentFileLib);
                        break;
                    case "Const":
                        OutputListBox.ItemsSource = new[] { "Cannot compile const declarations" };
                        return;
                    case "IntProperty":
                    case "BoolProperty":
                    case "FloatProperty":
                    case "NameProperty":
                    case "StrProperty":
                    case "StringRefProperty":
                    case "ByteProperty":
                    case "ObjectProperty":
                    case "ComponentProperty":
                    case "InterfaceProperty":
                    case "ArrayProperty":
                    case "StructProperty":
                    case "BioMask4Property":
                    case "MapProperty":
                    case "ClassProperty":
                    case "DelegateProperty":
                        OutputListBox.ItemsSource = new[] { "Cannot individually compile property declarations. You can edit them as part of the class." };
                        return;
                    default:
                        (_, log) = UnrealScriptCompiler.CompileDefaultProperties(CurrentLoadedExport, scriptText, CurrentFileLib);
                        break;
                }
                OutputListBox.ItemsSource = log?.Content;
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
                    if (FullyInitialized && !(CurrentLoadedExport.IsClass && CurrentLoadedExport.ObjectNameString is "Object"))
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
                    ScriptText = $"/*Error occurred while decompiling {CurrentLoadedExport?.InstancedFullPath}:\n\n{e.FlattenException()}*/";
                }
            });
        }

        private void TextChanged(object sender, EventArgs e)
        {
            Parse(ScriptText);
        }

        private ASTNode AST;

        private void Parse(string source)
        {
            bool needsTokensReset = true;
            var log = new MessageLog();
            try
            {
                (AST, TokenStream tokens) = UnrealScriptCompiler.CompileOutlineAST(source, CurrentLoadedExport.ClassName, log, Pcc.Game);

                if (AST != null && !log.HasErrors && FullyInitialized)
                {
                    log.Tokens = tokens;
                    switch (AST)
                    {
                        case Class cls:
                            AST = UnrealScriptCompiler.CompileNewClassAST(Pcc, cls, log, CurrentFileLib, out bool vfTableChanged);
                            if (vfTableChanged)
                            {
                                log.LogWarning("Compiling will cause Virtual Function Table to change! All classes that depend on this one will need recompilation to work properly!");
                            }
                            break;
                        case Function func when CurrentLoadedExport.Parent is ExportEntry funcParent:
                            AST = UnrealScriptCompiler.CompileNewFunctionBodyAST(funcParent, func, log, CurrentFileLib);
                            break;
                        case State state when CurrentLoadedExport.Parent is ExportEntry stateParent:
                            AST = UnrealScriptCompiler.CompileNewStateBodyAST(stateParent, state, log, CurrentFileLib);
                            break;
                        case Struct strct when CurrentLoadedExport.Parent is ExportEntry structParent:
                            AST = UnrealScriptCompiler.CompileNewStructAST(structParent, strct, log, CurrentFileLib);
                            break;
                        case Enumeration enumeration when CurrentLoadedExport.Parent is ExportEntry enumParent:
                            AST = UnrealScriptCompiler.CompileNewEnumAST(enumParent, enumeration, log, CurrentFileLib);
                            break;
                        case VariableDeclaration varDecl when CurrentLoadedExport.Parent is ExportEntry varParent:
                            AST = UnrealScriptCompiler.CompileNewVarDeclAST(varParent, varDecl, log, CurrentFileLib);
                            break;
                        case Const cnst:
                            //no additional processing needed for consts
                            break;
                        case DefaultPropertiesBlock propertiesBlock:
                            AST = UnrealScriptCompiler.CompileDefaultPropertiesAST(propertiesBlock, log, CurrentFileLib, CurrentLoadedExport);
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
                textEditor.SyntaxHighlighting = SyntaxInfo.None;
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

        private CompletionWindow completionWindow;
        private void TextAreaOnTextEntered(object sender, TextCompositionEventArgs e)
        {
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
                //case TokenType.Word when currentToken.Value.Length == 1 && completionWindow is null:
                //{
                    
                //    break;
                //}
            }
            
        }

        private void DisplayCompletions(ReadOnlySpan<ScriptToken> tokens, int currentTokenIdx)
        {
            var completionData = new List<ICompletionData>();
            Class currentClass = NodeUtils.GetContainingClass(AST);
            ScriptToken prevToken = tokens[currentTokenIdx - 1];
            ASTNode definitionOfPrevSymbol = GetDefinitionFromToken(prevToken);
            definitionOfPrevSymbol = definitionOfPrevSymbol switch
            {
                VariableDeclaration decl => decl.VarType,
                _ => definitionOfPrevSymbol
            };
            switch (definitionOfPrevSymbol)
            {
                case ObjectType objType:
                {
                    if (prevToken.Type is TokenType.NameLiteral)
                    {
                        //this is a class literal
                        completionData.Add(new KeywordCompletion("static"));
                        completionData.Add(new KeywordCompletion("const"));
                        completionData.Add(new KeywordCompletion("default"));
                        break;
                    }
                    bool varsAccesible = !prevToken.Value.CaseInsensitiveEquals(Keywords.SUPER) && !prevToken.Value.CaseInsensitiveEquals(Keywords.GLOBAL);
                    bool functionsAccesible = !prevToken.Value.CaseInsensitiveEquals(Keywords.DEFAULT);
                    do
                    {
                        if (varsAccesible)
                        {
                            completionData.AddRange(VariableCompletion.GenerateCompletions(objType.VariableDeclarations));
                        }
                        if (objType is Class classType && functionsAccesible)
                        {
                            bool allowIterators = false;
                            for (int i = currentTokenIdx - 1; i >= 0; i--)
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
                            completionData.AddRange(FunctionCompletion.GenerateCompletions(classType.Functions, currentClass, iterators: allowIterators));
                        }
                        objType = objType.Parent as ObjectType;
                    } while (objType is not null);
                    break;
                }
                case Enumeration enumType:
                    completionData.AddRange(enumType.Values.Select(v => new CompletionData(v.Name, $"{v.IntVal}")));
                    break;
                case DynamicArrayType dynArrType:
                    completionData.AddRange(ArrayFunctionCompletion.GenerateCompletions(Pcc.Game, dynArrType));
                    break;
                case null:
                {
                    if (prevToken.Value.CaseInsensitiveEquals(Keywords.CONST))
                    {
                        if (currentTokenIdx > 3)
                        {
                            ScriptToken classNameToken = tokens[currentTokenIdx - 3];
                            if (classNameToken.Type is TokenType.NameLiteral && GetDefinitionFromToken(classNameToken) is Class cls)
                            {
                                do
                                {
                                    completionData.AddRange(cls.TypeDeclarations.OfType<Const>().Select(c => new CompletionData(c.Name, $"{c.Literal?.ResolveType().DisplayName()} {c.Value}")));
                                    cls = cls.Parent as Class;
                                } while (cls is not null);
                            }
                        }
                    }
                    else if (prevToken.Value.CaseInsensitiveEquals(Keywords.STATIC))
                    {
                        if (currentTokenIdx > 3)
                        {
                            ScriptToken classNameToken = tokens[currentTokenIdx - 3];
                            if (classNameToken.Type is TokenType.NameLiteral && GetDefinitionFromToken(classNameToken) is Class cls)
                            {
                                do
                                {
                                    completionData.AddRange(FunctionCompletion.GenerateCompletions(cls.Functions, currentClass, staticsOnly: true));
                                    cls = cls.Parent as Class;
                                } while (cls is not null);
                            }
                        }
                    }
                    break;
                }
            }
            if (completionData.Count > 0)
            {
                completionWindow = new CompletionWindow(textEditor.TextArea)
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
                        if (ast is DefaultPropertiesBlock propBlock)
                        {
                            ast = UnrealScriptCompiler.CompileDefaultPropertiesAST(propBlock, log, CurrentFileLib, CurrentLoadedExport);
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

        private void DocumentOnUpdateStarted(object sender, EventArgs e)
        {
            _definitionLinkGenerator.Reset();
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

        private bool CanFindReferences() => contextMenuDefinitionNode is Function or VariableDeclaration {Outer: ObjectType} or VariableType && CurrentFileLib.IsInitialized;

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
                            return UnrealScriptLookup.FindUsagesInFile(varDecl, CurrentFileLib);
                        case VariableType varType:
                            return UnrealScriptLookup.FindUsagesInFile(varType, CurrentFileLib);
                        case EnumValue enumValue:
                            break;
                    }
                    return null;
                }
                catch (Exception e)
                {
                    return [new EntryStringPair($"Error occured: {e.FlattenException()}")];
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

        private bool CanToggleComment() => Document is not null;

        private void ToggleComment()
        {
            TextArea textArea = textEditor.TextArea;
            Selection selection = textArea.Selection;
            if (!selection.IsEmpty)
            {
                int startLineNum = selection.StartPosition.Line;
                int endLineNum = selection.EndPosition.Line;
                //if the selection was made by dragging up, these must be swapped
                if (startLineNum > endLineNum)
                {
                    (startLineNum, endLineNum) = (endLineNum, startLineNum);
                }
                DocumentLine startLine = Document.GetLineByNumber(startLineNum);
                DocumentLine endLine = Document.GetLineByNumber(endLineNum);
                textArea.Selection = selection = Selection.Create(textArea, startLine.Offset, endLine.EndOffset);
                string text = selection.GetText();
                string[] lines = text.Split('\n');
                int commentCollumn = -1;
                bool hasNonCommentedLines = false;
                foreach (string line in lines)
                {
                    int whitespaceCount = line.CountLeadingWhitespace();
                    if (whitespaceCount < line.Length)
                    {
                        //cast to uint so that -1 is "greater" than all positive numbers
                        commentCollumn = (int)Math.Min((uint)whitespaceCount, (uint)commentCollumn);
                        if (line[whitespaceCount] != '/' || line.Length > whitespaceCount + 1 && line[whitespaceCount + 1] != '/')
                        {
                            hasNonCommentedLines = true;
                        }
                    }
                }
                if (commentCollumn is -1)
                {
                    //oops, all whitespace!
                    return;
                }
                if (hasNonCommentedLines)
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string lineText = lines[i];
                        if (lineText.Length > commentCollumn && !string.IsNullOrWhiteSpace(lineText))
                        {
                            lines[i] = $"{lineText.AsSpan(0, commentCollumn)}//{lineText.AsSpan(commentCollumn)}";
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string lineText = lines[i];
                        if (lineText.IndexOf("//", StringComparison.Ordinal) is var commentStart and > 0 )
                        {
                            lines[i] = $"{lineText.AsSpan(0, commentStart)}{lineText.AsSpan(commentStart + 2)}";
                        }
                    }
                }
                textArea.PerformTextInput(string.Join('\n', lines));
            }
            else
            {
                DocumentLine line = Document.GetLineByNumber(textArea.Caret.Line);
                string lineText = Document.GetText(line);
                ReadOnlySpan<char> indentation = lineText.AsSpan(0, lineText.CountLeadingWhitespace());
                textArea.Selection = Selection.Create(textArea, line.Offset, line.EndOffset);
                if (lineText.Length >= indentation.Length + 2 && lineText.AsSpan(indentation.Length, 2) is "//")
                {
                    textArea.PerformTextInput($"{indentation}{lineText.AsSpan(indentation.Length + 2)}");
                }
                else
                {
                    textArea.PerformTextInput($"{indentation}//{lineText.AsSpan(indentation.Length)}");
                }
            }
        }

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
                SetInlinesTooltip([new Run(text) { Foreground = SyntaxInfo.ColorBrushes[EF.None] }]);
            }

            void SetTooltip(TextBlock content)
            {
                content.Background = ToolTipBackgroundBrush;
                _hoverToolTip.Content = content;
                _hoverToolTip.Background = ToolTipBackgroundBrush;
                _hoverToolTip.PlacementTarget = this; // required for property inheritance
                _hoverToolTip.IsOpen = true;
            }
        }

        private static readonly SolidColorBrush ToolTipBackgroundBrush = new(Color.FromRgb(56, 56, 56));

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
                        if (currentTokenIdx > 0 && tokens.TokensSpan[currentTokenIdx - 1] is {Type: TokenType.LeftBracket} prevToken)
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
    }
}