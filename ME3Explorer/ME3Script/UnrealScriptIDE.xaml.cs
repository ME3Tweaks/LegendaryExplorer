using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3Script;
using ME3Script.Analysis.Visitors;
using ME3Script.Compiling;
using ME3Script.Compiling.Errors;
using ME3Script.Decompiling;
using ME3Script.Language.Tree;
using ME3Script.Language.Util;
using ME3Script.Lexing;
using ME3Script.Parsing;

namespace ME3Explorer.ME3Script
{
    /// <summary>
    /// Interaction logic for UnrealScriptIDE.xaml
    /// </summary>
    public partial class UnrealScriptIDE : ExportLoaderControl
    {
        private string _scriptText;
        public string ScriptText
        {
            get => _scriptText;
            set => SetProperty(ref _scriptText, value);
        }

        private ASTNode _rootNode;

        public ASTNode RootNode
        {
            get => _rootNode;
            set => SetProperty(ref _rootNode, value);
        }

        private bool _fullyInitialized;

        public bool FullyInitialized
        {
            get => _fullyInitialized;
            set => SetProperty(ref _fullyInitialized, value);
        }

        private FileLib CurrentFileLib;

        public UnrealScriptIDE() : base("UnrealScript IDE")
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
                        (RootNode, ScriptText) = ME3ScriptCompiler.DecompileExport(CurrentLoadedExport);
                    }
                    else
                    {
                        CurrentFileLib?.Initialize();
                    }
                }
            }
        }

        public override bool CanParse(ExportEntry exportEntry) =>
            exportEntry.Game == MEGame.ME3 && exportEntry.FileRef.Platform == MEPackage.GamePlatform.PC && (exportEntry.ClassName switch
            {
                "Class" => true,
                "State" => true,
                "Function" => true,
                "Enum" => true,
                "ScriptStruct" => true,
                _ => false
            } || exportEntry.IsDefaultObject);

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
                (RootNode, ScriptText) = ME3ScriptCompiler.DecompileExport(CurrentLoadedExport, CurrentFileLib);
            }
        }

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
                        (RootNode, ScriptText) = ME3ScriptCompiler.DecompileExport(CurrentLoadedExport, CurrentFileLib);
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

        private readonly DispatcherTimer progressBarTimer;
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
            StandardLibrary.Initialized -= StandardLibrary_Initialized;

        }

        private void outputListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems?.Count == 1 && e.AddedItems[0] is PositionedMessage msg)
            {
                scriptTextBox.Focus();
                scriptTextBox.Select(msg.Start.CharIndex, msg.End.CharIndex - msg.Start.CharIndex);
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
                (RootNode, ScriptText) = ME3ScriptCompiler.DecompileExport(CurrentLoadedExport, CurrentFileLib);
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
                    var codeBuilder = new CodeBuilderVisitor();
                    RootNode.AcceptVisitor(codeBuilder);
                    ScriptText = codeBuilder.GetCodeString();
                }

                outputListBox.ItemsSource = log.Content;
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
    }
}
