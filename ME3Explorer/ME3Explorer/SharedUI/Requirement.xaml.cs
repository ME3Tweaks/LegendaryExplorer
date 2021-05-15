using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FontAwesome5;
using ME3Explorer.SharedUI;
using Xceed.Wpf.Toolkit.Core.Converters;

namespace ME3Explorer.AnimationExplorer
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


    [ValueConversion(typeof(bool), typeof(EFontAwesomeIcon))]
    public class EnabledIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value switch
            {
                true => EFontAwesomeIcon.Solid_TimesCircle,
                _ => EFontAwesomeIcon.Solid_CheckCircle
            };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }


    [ValueConversion(typeof(bool), typeof(Brush))]
    public class EnabledBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value switch
            {
                true => Brushes.Red,
                _ => Brushes.Green
            };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }
}
