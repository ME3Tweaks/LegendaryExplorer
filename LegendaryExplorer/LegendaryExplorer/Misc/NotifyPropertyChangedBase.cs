using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace LegendaryExplorer.Misc
{
    public abstract class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        public virtual void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public abstract class NotifyPropertyChangedWindowBase: Window, INotifyPropertyChanged
    {
        public NotifyPropertyChangedWindowBase()
        {
            // RegisterWindow only subscribes to events here - it doesn't apply anything yet - so calling it
            // from this base constructor is fine even though InitializeComponent() (which runs after this
            // constructor, in the derived class) hasn't executed. It internally waits for SourceInitialized
            // (required by DarkNet's title-bar theming - too late by Loaded) and Loaded (required for the
            // WPF resource-dictionary merge, since windows with their own local <Window.Resources> block
            // would otherwise have an earlier merge silently discarded).
            Themes.ThemeManager.RegisterWindow(this);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        //Disable Automation of this window. The only thing this "feature" ever does is cause memory leaks.
        protected override AutomationPeer OnCreateAutomationPeer() => new FakeWindowPeer(this);
        private class FakeWindowPeer : WindowAutomationPeer
        {
            public FakeWindowPeer(Window window) : base(window) { }
            protected override List<AutomationPeer> GetChildrenCore() => null;
        }
    }

    public abstract class NotifyPropertyChangedControlBase : UserControl, INotifyPropertyChanged
    {
        public NotifyPropertyChangedControlBase()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        //Disable Automation of this control. The only thing this "feature" ever does is cause memory leaks.
        protected override AutomationPeer OnCreateAutomationPeer() => new FakeControlPeer(this);
        private class FakeControlPeer : UserControlAutomationPeer
        {
            public FakeControlPeer(UserControl control) : base(control) { }
            protected override List<AutomationPeer> GetChildrenCore() => null;
        }
    }
}
