using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LegendaryExplorer.GameInterop;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Misc.ME3Tweaks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorer.Tools.ScriptDebugger
{
    /// <summary>
    /// Interaction logic for ScriptDebuggerWindow.xaml
    /// </summary>
    public partial class ScriptDebuggerWindow : TrackingNotifyPropertyChangedWindowBase
    {
        //MUST BE UPDATED WHEN A NEW VERSION OF THE ASI IS RELEASED!
        public string debuggerASIName => Game switch
        {
            MEGame.LE1 => "LE1ScriptDebugger-v2.asi", // In M3
            MEGame.LE2 => "LE2ScriptDebugger-v2.asi", // In M3
            MEGame.LE3 => "LE3ScriptDebugger-v2.asi", 
            _ => throw new ArgumentOutOfRangeException(nameof(Game))
        };
        private void GetDebuggerASI()
        {
            switch (Game)
            {
                case MEGame.LE1:
                    ModManagerIntegration.RequestASIInstallation(MEGame.LE1, ASIModIDs.LE1_SCRIPT_DEBUGGER, 2);
                    break;
                case MEGame.LE2:
                    ModManagerIntegration.RequestASIInstallation(MEGame.LE2, ASIModIDs.LE2_SCRIPT_DEBUGGER, 2);
                    break;
                case MEGame.LE3:
                    HyperlinkExtensions.OpenURL("https://github.com/ME3Tweaks/LE3-ASI-Plugins/releases/tag/LE3UnrealScriptDebugger-v2.0");
                    break;
            }

        }

        public static readonly string ScriptDebuggerDataFolder = Path.Combine(AppDirectories.AppDataFolder, @"ScriptDebugger\");

        private static readonly Dictionary<MEGame, ScriptDebuggerWindow> Instances = new();
        public static ScriptDebuggerWindow Instance(MEGame game)
        {
            if (!game.IsLEGame())
            {
                throw new ArgumentException(@"Script Debugger does not support this game!", nameof(game));
            }

            return Instances.TryGetValue(game, out var scriptDebuggerWindow) ? scriptDebuggerWindow : null;
        }

        public MEGame Game { get; }

        private bool _isAttached;
        public bool IsAttached
        {
            get => _isAttached;
            set
            {
                SetProperty(ref _isAttached, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _inBreakState;
        public bool InBreakState
        {
            get => _inBreakState;
            set
            { 
                SetProperty(ref _inBreakState, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ObservableCollectionExtended<PropertyValue> Locals { get; } = new();

        public ObservableCollectionExtended<CallStackEntry> CallStack { get; } = new();

        private CallStackEntry _selectedCallStackEntry;
        public CallStackEntry SelectedCallStackEntry
        {
            get => _selectedCallStackEntry;
            set
            {
                if (SetProperty(ref _selectedCallStackEntry, value))
                {
                    SetScriptViewFromCallStack();
                }
            }
        }
        public ObservableCollectionExtended<ScriptStatement> Statements { get; } = new();

        private readonly List<ScriptDatabaseEntry> functionList = new();
        public ObservableCollectionExtended<ScriptDatabaseEntry> FunctionList { get; } = new();

        private ScriptDatabaseEntry _selectedScriptDatabaseEntry;
        public ScriptDatabaseEntry SelectedScriptDatabaseEntry
        {
            get => _selectedScriptDatabaseEntry;
            set
            {
                SetProperty(ref _selectedScriptDatabaseEntry, value);
                SetScriptViewFromFunctionList();
            }
        }

        public ObservableCollectionExtended<BreakPoint> BreakPoints { get; } = new();

        private DebuggerInterface Debugger;

        private ScriptDatabase scriptDatabase;

        public ScriptDebuggerWindow(MEGame game) : base("Script Debugger", true)
        {
            Game = game;
            if (!game.IsLEGame())
            {
                throw new ArgumentException($@"{nameof(game)} does not support Script Debugger", nameof(game));
            }
            if (Instance(game) is not null)
            {
                throw new Exception($"Can only have one instance of {game} Script Debugger open!");
            }
            Instances[game] = this;
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            Title = $"{game.ToGameName(true)} Script Debugger";
            GameOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            GameOpenTimer.Tick += CheckIfGameOpen;

            GameInstalledReq.FullfilledText = $"{game.ToGameName()} is installed";
            GameInstalledReq.UnFullfilledText = $"Can't find {game.ToGameName()} installation!";
            GameInstalledReq.ButtonText = $"Set {game} path";
            
            SetScriptDBBusy("Waiting for Game Path to be set...");
            if (InteropHelper.IsGameInstalled(Game))
            {
                InitScriptDatabase();
            }
        }

        public Requirement.RequirementCommand GameInstalledRequirementCommand { get; set; }
        public Requirement.RequirementCommand ASILoaderInstalledRequirementCommand { get; set; }
        public Requirement.RequirementCommand DebuggerASIInstalledRequirementCommand { get; set; }
        public GenericCommand AttachDebuggerCommand { get; private set; }
        public GenericCommand DetachDebuggerCommand { get; private set; }
        public GenericCommand BreakAllCommand { get; private set; }
        public GenericCommand ResumeCommand { get; private set; }
        public GenericCommand StepIntoCommand { get; private set; }
        public GenericCommand StepOverCommand { get; private set; }
        public GenericCommand StepOutCommand { get; private set; }
        void LoadCommands()
        {
            GameInstalledRequirementCommand = new Requirement.RequirementCommand(IsGameInstalled, () => InteropHelper.SelectGamePath(Game));
            ASILoaderInstalledRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsASILoaderInstalled(Game), () => InteropHelper.OpenASILoaderDownload(Game));
            DebuggerASIInstalledRequirementCommand = new Requirement.RequirementCommand(IsDebuggerASIInstalled, GetDebuggerASI);
            AttachDebuggerCommand = new GenericCommand(AttachDebugger, CanAttachDebugger);
            DetachDebuggerCommand = new GenericCommand(DetachDebugger, CanDetachDebugger);
            BreakAllCommand = new GenericCommand(BreakAll, CanBreakAll);
            ResumeCommand = new GenericCommand(Resume, CanResume);
            StepIntoCommand = new GenericCommand(StepInto, CanResume);
            StepOverCommand = new GenericCommand(StepOver, CanResume);
            StepOutCommand = new GenericCommand(StepOut, CanResume);
        }

        private bool _scriptDatabaseInitStarted;
        private bool IsGameInstalled()
        {
            if (InteropHelper.IsGameInstalled(Game))
            {
                if (!_scriptDatabaseInitStarted)
                {
                    InitScriptDatabase();
                }
                return true;
            }
            return false;
        }

        private void InitScriptDatabase()
        {
            _scriptDatabaseInitStarted = true;
            SetScriptDBBusy("Creating Function Database...");
            StatusBarText = "Creating Function Database...";

            Task.Run(() =>
            {
                try
                {
                    return new ScriptDatabase(Game);
                }
                catch
                {
                    return null;
                }
            }).ContinueWithOnUIThread(prevTask =>
            {
                scriptDatabase = prevTask.Result;
                EndScriptDBBusy();
                if (scriptDatabase is null)
                {
                    StatusBarText = "Function Database could not be created!";
                }
                else
                {
                    StatusBarText = "Function Database Created";
                    functionList.ReplaceAll(scriptDatabase.GetEntries());
                    FunctionList.ReplaceAll(functionList);
                    if (InBreakState && SelectedCallStackEntry is not null)
                    {
                        SetScriptViewFromCallStack();
                    }
                }
            });
        }

        private bool CanResume() => IsAttached && InBreakState;
        private void StepOut()
        {
            if (CanResume())
            {
                Debugger?.StepOut();
                ClearBreakState();
            }
        }

        private void StepOver()
        {
            if (CanResume())
            {
                Debugger?.StepOver();
                ClearBreakState();
            }
        }

        private void StepInto()
        {
            if (CanResume())
            {
                Debugger?.StepInto();
                ClearBreakState();
            }
        }


        private void Resume()
        {
            if (CanResume())
            {
                Debugger?.Resume();
                ClearBreakState();
            }
        }

        private void ClearBreakState()
        {
            Locals.ClearEx();
            CallStack.ClearEx();
            foreach (ScriptStatement scriptStatement in Statements)
            {
                scriptStatement.IsCurrentStatement = false;
            }
            InBreakState = false;
        }

        private bool CanBreakAll() => IsAttached && !InBreakState;

        private void BreakAll()
        {
            if (CanBreakAll())
            {
                Debugger?.BreakASAP();
            }
        }

        private bool CanDetachDebugger() => IsAttached;

        private void DetachDebugger()
        {
            if (CanDetachDebugger() &&
                MessageBoxResult.Yes == MessageBox.Show(this, "Are you sure you want to detach the debugger?", "Detach confirmation", MessageBoxButton.YesNo))
            {
                Debugger?.Detach();
            }
        }

        private bool IsDebuggerASIInstalled()
        {
            if (!InteropHelper.IsGameInstalled(Game))
            {
                return false;
            }
            
            string asiPath = GetDebuggerAsiWritePath();
            return File.Exists(asiPath);
        }

        private string GetDebuggerAsiWritePath()
        {
            string asiDir = InteropHelper.GetAsiDir(Game);
            string interopASIWritePath = Path.Combine(asiDir, debuggerASIName);
            return interopASIWritePath;
        }

        private bool CanAttachDebugger() => GameInstalledReq.IsFullfilled && AsiLoaderInstalledReq.IsFullfilled && DebuggerAsiInstalledReq.IsFullfilled && GameController.TryGetMEProcess(Game, out _);

        private void AttachDebugger()
        {
            if (GameController.TryGetMEProcess(Game, out Process meProcess))
            {
                SetBusy("Attaching...", () =>
                {
                    Debugger?.Detach();
                    Detached();
                });
                Debugger = new DebuggerInterface(Game, meProcess);
                Debugger.OnDetach += Detached;
                Debugger.OnAttach += Attached;
                Debugger.OnBreak += Debugger_OnBreak;
                Debugger.Attach();
                GameOpenTimer.Start();
            }
            else
            {
                MessageBox.Show(this, $"Could not attach debugger to {GameController.GetInteropTargetForGame(Game).ProcessName}.exe, because that process is not running!");
            }
        }

        private void Debugger_OnBreak()
        {
            InBreakState = true;
            CallStack.ReplaceAll(Debugger.CallStack.Select(frame => new CallStackEntry(Debugger, frame)));
            SelectedCallStackEntry = CallStack[0];
            CommandManager.InvalidateRequerySuggested();
        }

        private void SetScriptViewFromFunctionList()
        {
            Statements.ClearEx();
            if (_selectedScriptDatabaseEntry is not null)
            {
                (string functionPath, string filePath, int uIndex, bool _) = _selectedScriptDatabaseEntry;
                if (File.Exists(filePath) && scriptDatabase.GetStatements(filePath, uIndex) is {} statements)
                {
                    Statements.AddRange(statements);
                    foreach (CallStackEntry callStackEntry in CallStack)
                    {
                        if (string.Equals(callStackEntry.FunctionPathInFile, functionPath, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(Path.GetFileNameWithoutExtension(callStackEntry.FunctionFilePath), _selectedScriptDatabaseEntry.FileName, StringComparison.OrdinalIgnoreCase))
                        {
                            Locals.ReplaceAll(Debugger.LoadLocals(callStackEntry.Frame));
                            SetVisualCurrentStatement(callStackEntry.Frame.CurrentPosition);
                            break;
                        }
                    }
                    SetVisualBreakPoints(_selectedScriptDatabaseEntry.FullFunctionPath);
                    return;
                }
                Statements.Add(new ScriptStatement("Could not find function! (Have you edited files since opening the debugger?)", -1));
            }
        }

        private void SetScriptViewFromCallStack()
        {
            if (_selectedCallStackEntry is null)
            {
                Locals.ClearEx();
                return;
            }
            if (scriptDatabase is null)
            {
                Statements.Replace(new ScriptStatement("Waiting for the function database to finish generating...", -1));
                return;
            }
            if (scriptDatabase?.GetFunctionLocationFromPath(_selectedCallStackEntry.FunctionPathInFile, _selectedCallStackEntry.FunctionFilePath) is (int uIndex, bool forcedExport) 
                     && uIndex != 0)
            {
                SelectedScriptDatabaseEntry = new ScriptDatabaseEntry(_selectedCallStackEntry.FunctionPathInFile, _selectedCallStackEntry.FunctionFilePath, uIndex, forcedExport);
                return;
            }
            Statements.Replace(new ScriptStatement("Could not find function! (Have you edited files since starting the game?)", -1));
        }

        private void SetVisualCurrentStatement(ushort currentPosition)
        {
            foreach (ScriptStatement statement in Statements)
            {
                statement.IsCurrentStatement = statement.Position == currentPosition;
            }
        }

        private void SetVisualBreakPoints(string fullFunctionPath)
        {
            var localBreakPoints = BreakPoints.Where(bp => bp.FullFunctionPath == fullFunctionPath).ToList();
            foreach (ScriptStatement statement in Statements)
            {
                statement.HasBreakPoint = localBreakPoints.Any(bp => bp.Position == statement.Position);
            }
        }

        #region BusyHost

        private bool _isBusy;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _busyText;

        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }

        private ICommand _cancelBusyCommand;

        public ICommand CancelBusyCommand
        {
            get => _cancelBusyCommand;
            set => SetProperty(ref _cancelBusyCommand, value);
        }

        public void SetBusy(string busyText, Action onCancel = null)
        {
            BusyText = busyText;
            if (onCancel != null)
            {
                CancelBusyCommand = new GenericCommand(() =>
                {
                    onCancel();
                    EndBusy();
                }, () => true);
            }
            else
            {
                CancelBusyCommand = new DisabledCommand();
            }

            IsBusy = true;
        }

        public void EndBusy()
        {
            IsBusy = false;
        }

        private string _statusBarText;
        public string StatusBarText
        {
            get => _statusBarText;
            set => SetProperty(ref _statusBarText, value);
        }

        #endregion

        #region ScriptDBBusyHost

        private bool _isScriptDBBusy;

        public bool IsScriptDBBusy
        {
            get => _isScriptDBBusy;
            set => SetProperty(ref _isScriptDBBusy, value);
        }

        private string _ScriptDBBusyText;

        public string ScriptDBBusyText
        {
            get => _ScriptDBBusyText;
            set => SetProperty(ref _ScriptDBBusyText, value);
        }

        private ICommand _cancelScriptDBBusyCommand;

        public ICommand CancelScriptDBBusyCommand
        {
            get => _cancelScriptDBBusyCommand;
            set => SetProperty(ref _cancelScriptDBBusyCommand, value);
        }

        public void SetScriptDBBusy(string scriptDBBusyText, Action onCancel = null)
        {
            ScriptDBBusyText = scriptDBBusyText;
            if (onCancel != null)
            {
                CancelScriptDBBusyCommand = new GenericCommand(() =>
                {
                    onCancel();
                    EndScriptDBBusy();
                }, () => true);
            }
            else
            {
                CancelScriptDBBusyCommand = new DisabledCommand();
            }

            IsScriptDBBusy = true;
        }

        public void EndScriptDBBusy()
        {
            IsScriptDBBusy = false;
        }

        #endregion

        private readonly DispatcherTimer GameOpenTimer;
        private void CheckIfGameOpen(object sender, EventArgs e)
        {
            if (!GameController.TryGetMEProcess(Game, out _))
            {
                Detached();
            }
        }

        private void Attached()
        {
            IsAttached = true;
            EndBusy();
            foreach (BreakPoint breakPoint in BreakPoints)
            {
                Debugger?.SetBreakPoint(breakPoint.FullFunctionPath, breakPoint.Position);
            }
        }

        private void Detached()
        {
            Close();
        }

        private void ScriptDebuggerWindow_OnClosing(object sender, CancelEventArgs e)
        {
            GameOpenTimer.Stop();
            IsAttached = false;
            InBreakState = false;
            if (Debugger is not null)
            {
                Debugger.OnDetach -= Detached;
                Debugger.OnAttach -= Attached;
                Debugger.OnBreak -= Debugger_OnBreak;
                Debugger.Dispose();
                Debugger = null;
            }
            EndBusy();
            DataContext = null;
            Instances.Remove(Game);
        }

        private void TreeViewItem_OnExpanded(object sender, RoutedEventArgs e)
        {
            object header = (e.OriginalSource as TreeViewItem)?.Header;
            if (header is ObjectPropertyValue objPropVal && objPropVal.Properties.Count == 1 && objPropVal.Properties[0] is LoadingPropertyValue)
            {
                objPropVal.LoadProperties();
            }
            else if (header is StructPropertyValue structPropVal && structPropVal.Properties.Count == 1 && structPropVal.Properties[0] is LoadingPropertyValue)
            {
                structPropVal.LoadProperties();
            }
        }

        private void FunctionListSearchBox_OnTextChanged(SearchBox sender, string newtext)
        {
            FunctionList.ReplaceAll(functionList.Where(dbEntry => dbEntry.FunctionPath.Contains(newtext, StringComparison.OrdinalIgnoreCase) || dbEntry.FileName.Contains(newtext, StringComparison.OrdinalIgnoreCase)));
        }

        private void Gutter_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ScriptStatement { Position: >= 0 } statement)
            {
                var bp = new BreakPoint(_selectedScriptDatabaseEntry, (ushort)statement.Position);
                if (statement.HasBreakPoint)
                {
                    if (BreakPoints.Remove(bp))
                    {
                        Debugger?.RemoveBreakPoint(bp.FullFunctionPath, bp.Position);
                    }
                }
                else
                {
                    if (!BreakPoints.Contains(bp))
                    {
                        BreakPoints.Add(bp);
                        Debugger?.SetBreakPoint(bp.FullFunctionPath, bp.Position);
                    }
                }
                SetVisualBreakPoints(bp.FullFunctionPath);
            }
        }


        private void BreakPointSearchBox_OnTextChanged(SearchBox sender, string newtext)
        {
            throw new NotImplementedException();
        }

        private void BreakPoints_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is BreakPoint bp)
            {
                SelectedScriptDatabaseEntry = bp.FunctionDBEntry;
                foreach (ScriptStatement scriptStatement in Statements)
                {
                    if (scriptStatement.Position == bp.Position)
                    {
                        FunctionListBox.SelectedItem = scriptStatement;
                        break;
                    }
                }
                e.Handled = true;
                ((ListBox)sender).SelectedItem = null;
            }
        }
    }
}
