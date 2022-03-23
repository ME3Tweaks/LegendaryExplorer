using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LegendaryExplorer.GameInterop;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Tools.ScriptDebugger
{
    /// <summary>
    /// Interaction logic for ScriptDebuggerWindow.xaml
    /// </summary>
    public partial class ScriptDebuggerWindow : TrackingNotifyPropertyChangedWindowBase
    {
        //MUST BE UPDATED WHEN A NEW VERSION OF THE ASI IS RELEASED!
        const string debuggerASIName = "UnrealscriptDebugger.asi";

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
            set => SetProperty(ref _isAttached, value);
        }

        private bool _inBreakState;
        public bool InBreakState
        {
            get => _inBreakState;
            set => SetProperty(ref _inBreakState, value);
        }

        public ObservableCollectionExtended<PropertyValue> Locals { get; } = new();

        private DebuggerInterface Debugger;

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
            GameInstalledRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsGameInstalled(Game), () => InteropHelper.SelectGamePath(Game));
            ASILoaderInstalledRequirementCommand = new Requirement.RequirementCommand(() => InteropHelper.IsASILoaderInstalled(Game), () => InteropHelper.OpenASILoaderDownload(Game));
            DebuggerASIInstalledRequirementCommand = new Requirement.RequirementCommand(() => IsDebuggerASIInstalled(Game), () => throw new NotImplementedException());
            AttachDebuggerCommand = new GenericCommand(AttachDebugger, CanAttachDebugger);
            DetachDebuggerCommand = new GenericCommand(DetachDebugger, CanDetachDebugger);
            BreakAllCommand = new GenericCommand(BreakAll, CanBreakAll);
            ResumeCommand = new GenericCommand(Resume, CanResume);
            StepIntoCommand = new GenericCommand(StepInto, CanResume);
            StepOverCommand = new GenericCommand(StepOver, CanResume);
            StepOutCommand = new GenericCommand(StepOut, CanResume);
        }

        private void StepOut()
        {
            Debugger?.StepOut();
            InBreakState = false;
        }

        private void StepOver()
        {
            Debugger?.StepOver();
            InBreakState = false;
        }

        private void StepInto()
        {
            Debugger?.StepInto();
            InBreakState = false;
        }

        private bool CanResume() => IsAttached && InBreakState;

        private void Resume()
        {
            Debugger?.Resume();
            InBreakState = false;
        }

        private bool CanBreakAll() => IsAttached && !InBreakState;

        private void BreakAll()
        {
            Debugger?.BreakASAP();
        }

        private bool CanDetachDebugger() => IsAttached;

        private void DetachDebugger()
        {
            if (MessageBoxResult.Yes == MessageBox.Show(this, "Are you sure you want to detach the debugger?", "Detach confirmation", MessageBoxButton.YesNo))
            {
                Debugger?.Detach();
            }
        }

        private static bool IsDebuggerASIInstalled(MEGame game)
        {
            if (!InteropHelper.IsGameInstalled(game))
            {
                return false;
            }
            
            string asiPath = GetDebuggerAsiWritePath(game);
            return File.Exists(asiPath);
        }

        private static string GetDebuggerAsiWritePath(MEGame game)
        {
            string asiDir = InteropHelper.GetAsiDir(game);
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

        private void Debugger_OnBreak(string info)
        {
            InBreakState = true;
            infoBlock.Text = info;
            Locals.ReplaceAll(Debugger.Locals);
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
        }

        private void Detached()
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
        }

        private void ScriptDebuggerWindow_OnClosing(object sender, CancelEventArgs e)
        {
            Detached();
            DataContext = null;
            Instances.Remove(Game);
        }

        private void TreeViewItem_OnExpanded(object sender, RoutedEventArgs e)
        {
            if ((e.OriginalSource as StretchingTreeViewItem)?.Header is ObjectPropertyValue objPropVal && objPropVal.Properties.Count == 1 && objPropVal.Properties[0] is LoadingPropertyValue)
            {
                objPropVal.LoadProperties();
            }
        }

        private void StringValue_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
