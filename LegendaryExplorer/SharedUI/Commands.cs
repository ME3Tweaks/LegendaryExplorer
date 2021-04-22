using System;
using System.Windows.Input;

namespace LegendaryExplorer.SharedUI
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object parameter)
        {
            bool result = _canExecute?.Invoke(parameter) ?? true;
            return result;
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class GenericCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public GenericCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object parameter)
        {
            bool result = _canExecute?.Invoke() ?? true;
            return result;
        }

        public void Execute(object parameter)
        {
            _execute?.Invoke();
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class EnableCommand : ICommand
    {
        private readonly Func<bool> _isEnabled;

        public EnableCommand(Func<bool> isEnabled)
        {
            _isEnabled = isEnabled;
        }
        public bool CanExecute(object parameter) => _isEnabled.Invoke();

        public void Execute(object parameter)
        {
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class DisabledCommand : ICommand
    {
        public bool CanExecute(object parameter) => false;

        public void Execute(object parameter) {}

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
