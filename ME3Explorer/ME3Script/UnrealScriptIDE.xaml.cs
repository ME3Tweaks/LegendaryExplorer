using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Indentation.CSharp;
using ME3Explorer;
using ME3Explorer.ME3Script;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using ME3Script;
using ME3Script.Analysis.Visitors;
using ME3Script.Compiling.Errors;
using ME3Script.Language.Tree;

namespace ME3Explorer.ME3Script.IDE
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
                if (foldingManager != null)
                {
                    FoldingManager.Uninstall(foldingManager);
                }
                Document = new TextDocument(value);
                foldingManager = FoldingManager.Install(textEditor.TextArea);
                foldingStrategy.UpdateFoldings(foldingManager, Document);
            });
        }

        private ASTNode _rootNode;
        public ASTNode RootNode
        {
            get => _rootNode;
            set => SetProperty(ref _rootNode, value);
        }

        public UnrealScriptIDE()
        {
            InitializeComponent();
            DataContext = this;
            progressBarTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            progressBarTimer.Tick += ProgressBarTimer_Tick;
            if (StandardLibrary.IsInitialized)
            {
                FullyInitialized = !StandardLibrary.HadInitializationError;
            }
            else
            {
                IsBusy = true;
                BusyText = "Initializing Script Compiler";
                StandardLibrary.Initialized += StandardLibrary_Initialized;
                if (StandardLibrary.IsInitialized)
                {
                    StandardLibrary_Initialized(null, EventArgs.Empty);
                }
            }
        }

        static UnrealScriptIDE()
        {
            using Stream s = typeof(UnrealScriptIDE).Assembly.GetManifestResourceStream("ME3Explorer.Resources.Unrealscript-Mode.xshd");
            if (s != null)
            {
                using var reader = new XmlTextReader(s);
                HighlightingManager.Instance.RegisterHighlighting("Unrealscript", new []{".uc"}, HighlightingLoader.Load(reader, HighlightingManager.Instance));
            }
        }

        public override bool CanParse(ExportEntry exportEntry) =>
            exportEntry.Game == MEGame.ME3 && (exportEntry.ClassName switch
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
            if (IsStandardLibFile())
            {
                UnloadFileLib();
                FullyInitialized = StandardLibrary.IsInitialized;
            }
            else if (Pcc != CurrentFileLib?.Pcc)
            {
                FullyInitialized = false;
                IsBusy = true;
                BusyText = "Compiling local classes";
                UnloadFileLib();
                CurrentFileLib = new FileLib(Pcc);
                CurrentFileLib.InitializationStatusChange += CurrentFileLibOnInitialized;
                if (IsVisible)
                {
                    CurrentFileLib?.Initialize();
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
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new BytecodeEditor(), CurrentLoadedExport)
                {
                    Title = $"Script Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
            if (progressBarTimer != null)
            {
                progressBarTimer.IsEnabled = false; //Stop timer
                progressBarTimer.Tick -= ProgressBarTimer_Tick;
            }
        }

        private void ExportLoaderControl_Loaded(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow(this);
            if (window is { })
            {
                window.Closed += (o, args) => UnloadFileLib();
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

        private void StandardLibrary_Initialized(object sender, EventArgs e)
        {
            if (IsBusy)
            {
                IsBusy = false;
                if (StandardLibrary.HadInitializationError)
                {
                    FullyInitialized = false;
                    MessageBox.Show("Could not build standard lib! One or more of these files in your ME3 installation is missing or corrupted!\n" +
                                    "Core.pcc, Engine.pcc, GameFramework.pcc, GFxUI.pcc, WwiseAudio.pcc, SFXOnlineFoundation.pcc, SFXGame.pcc\n\n" +
                                    "Functionality will be limited to script decompilation.");
                    
                }
                else
                {
                    FullyInitialized = IsStandardLibFile();
                }
                if (CurrentLoadedExport != null)
                {
                    if (IsStandardLibFile())
                    {
                        CurrentFileLib = null;
                        Decompile();
                    }
                    else
                    {
                        CurrentFileLib?.Initialize();
                    }
                }
            }
        }

        public bool IsStandardLibFile() => Pcc != null &&
                                           Path.GetFileName(Pcc.FilePath) switch
                                           {
                                               "Core.pcc" => true,
                                               "Engine.pcc" => true,
                                               "GameFramework.pcc" => true,
                                               "GFxUI.pcc" => true,
                                               "WwiseAudio.pcc" => true,
                                               "SFXOnlineFoundation.pcc" => true,
                                               "SFXGame.pcc" => true,
                                               _ => false
                                           };

        private void UnloadFileLib()
        {
            if (CurrentFileLib is {})
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
                        MessageBox.Show("Could not build script database for this file!\n\n" +
                                        "Functionality will be limited to script decompilation.");
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
                    CurrentFileLib?.Initialize();
                }
            }
        }

        private void ExportLoaderControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                if (!StandardLibrary.IsInitialized && !StandardLibrary.HadInitializationError)
                {
                    //returning without waiting for async method to complete is intended behavior here.
#pragma warning disable CS4014
                    StandardLibrary.InitializeStandardLib();
#pragma warning restore CS4014

                    if (!progressBarTimer.IsEnabled)
                    {
                        progressBarTimer.Start();
                    }
                }

                CurrentFileLib?.Initialize();
            }
        }

        #endregion

        private void outputListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems?.Count == 1 && e.AddedItems[0] is PositionedMessage msg)
            {
                textEditor.Focus();
                textEditor.Select(msg.Start.CharIndex, msg.End.CharIndex - msg.Start.CharIndex);
            }
        }

        private void CompileToBytecode(object sender, RoutedEventArgs e)
        {
            if (ScriptText != null && CurrentLoadedExport != null)
            {
                if (CurrentLoadedExport.ClassName != "Function")
                {
                    outputListBox.ItemsSource = new[] {$"Can only compile functions right now. {(CurrentLoadedExport.IsDefaultObject ? "Defaults" : CurrentLoadedExport.ClassName)} compilation will be added in a future update."};
                    return;
                }
                (_, MessageLog log) = ME3ScriptCompiler.CompileFunction(CurrentLoadedExport, ScriptText, CurrentFileLib);
                outputListBox.ItemsSource = log?.Content;
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
                ASTNode ast = ME3ScriptCompiler.ExportToAstNode(CurrentLoadedExport, CurrentFileLib);
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
                RootNode = ast;
                ScriptText = text;
                Dispatcher.Invoke(() =>
                {
                    if (RootNode is Function func)
                    {
                        textEditor.IsReadOnly = false;
                        int numLocals = func.Locals.Count;
                        int numHeaderLines = numLocals > 0 ? numLocals + 4 : 3;
                        var segments = new TextSegmentCollection<TextSegment>
                        {
                            new TextSegment { StartOffset = 0, EndOffset = Document.GetOffset(numHeaderLines, 0) },
                            new TextSegment { StartOffset = Document.GetOffset(Document.LineCount, 0), Length = 1}
                        };
                        textEditor.TextArea.ReadOnlySectionProvider = new TextSegmentReadOnlySectionProvider<TextSegment>(segments);
                    }
                    else
                    {
                        textEditor.IsReadOnly = true;
                    }
                });

            }
            catch (Exception e) when (!App.IsDebug)
            {
                (RootNode, ScriptText) = (null, $"Error occured while decompiling {CurrentLoadedExport?.InstancedFullPath}:\n\n{e.FlattenException()}");
            }
        }

        private void CompileAST_OnClick(object sender, RoutedEventArgs e)
        {
            if (ScriptText != null)
            {
                MessageLog log;
                (RootNode, log) = ME3ScriptCompiler.CompileAST(ScriptText, CurrentLoadedExport.ClassName);

                if (RootNode != null && log.AllErrors.IsEmpty())
                {
                    if (RootNode is Function func && FullyInitialized && CurrentLoadedExport.Parent is ExportEntry parentExport)
                    {
                        RootNode = ME3ScriptCompiler.CompileFunctionBodyAST(parentExport, ScriptText, func, log, CurrentFileLib);
                    }
                    var codeBuilder = new CodeBuilderVisitor<SyntaxInfoCodeFormatter, (string, SyntaxInfo)>();
                    RootNode.AcceptVisitor(codeBuilder);
                    (ScriptText, _) = codeBuilder.GetOutput();
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
        private readonly BraceFoldingStrategy foldingStrategy = new BraceFoldingStrategy();

        #endregion
    }
}
