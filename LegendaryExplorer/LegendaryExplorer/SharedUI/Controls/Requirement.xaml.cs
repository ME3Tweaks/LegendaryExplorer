using System;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.Misc;
using Xceed.Wpf.Toolkit.Core.Converters;

namespace LegendaryExplorer.SharedUI.Controls
{
    /// <summary>
    /// Interaction logic for Requirement.xaml
    /// </summary>
    public partial class Requirement : NotifyPropertyChangedControlBase
    {
        public bool IsFullfilled
        {
            get => (bool)GetValue(IsFullfilledProperty);
            set => SetValue(IsFullfilledProperty, value);
        }
        public static readonly DependencyProperty IsFullfilledProperty = DependencyProperty.Register(
            nameof(IsFullfilled), typeof(bool), typeof(Requirement), new PropertyMetadata(default(bool)));
        public RequirementCommand Command
        {
            get => (RequirementCommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }
        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
            nameof(Command), typeof(RequirementCommand), typeof(Requirement), new PropertyMetadata(default(RequirementCommand)));
        public Requirement()
        {
            DataContext = this;
            InitializeComponent();
            this.bind(IsFullfilledProperty, fulfillButton, nameof(fulfillButton.IsEnabled), new InverseBoolConverter());
        }

        private string _fullfilledText;
        public string FullfilledText
        {
            get => _fullfilledText; set => SetProperty(ref _fullfilledText, value);
        }

        private string _unFullfilledText;
        public string UnFullfilledText
        {
            get => _unFullfilledText; set => SetProperty(ref _unFullfilledText, value);
        }

        private string _buttonText;
        public string ButtonText
        {
            get => _buttonText; set => SetProperty(ref _buttonText, value);
        }

        /// <summary>
        /// Should only be used with the Requirement Control
        /// </summary>
        public class RequirementCommand : ICommand
        {
            private readonly Action _fulfill;
            private readonly Func<bool> _isFulfilled;
            /// <summary>
            /// </summary>
            /// <param name="isFulfilled">Returns true if the requirement is fulfilled</param>
            /// <param name="fulfill">Either does what is needed to fulfill the requirement, or instructs the user on how to fulfill it</param>
            public RequirementCommand(Func<bool> isFulfilled, Action fulfill = null)
            {
                _fulfill = fulfill;
                _isFulfilled = isFulfilled;
            }

            /// <summary>
            /// The fulfill action can only be executed when the requirement has NOT been fulfilled.
            /// </summary>
            /// <param name="parameter"></param>
            /// <returns></returns>
            public bool CanExecute(object parameter) => !_isFulfilled.Invoke();

            public void Execute(object parameter) => _fulfill?.Invoke();

            public event EventHandler CanExecuteChanged
            {
                add => CommandManager.RequerySuggested += value;
                remove => CommandManager.RequerySuggested -= value;
            }
        }
    }
}
