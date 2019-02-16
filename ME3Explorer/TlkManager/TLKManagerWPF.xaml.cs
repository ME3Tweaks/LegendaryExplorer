using KFreonLib.MEDirectories;
using ME3Explorer.SharedUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ME3Explorer.TlkManagerNS
{
    /// <summary>
    /// Interaction logic for TLKManagerWPF.xaml
    /// </summary>
    public partial class TLKManagerWPF : Window, INotifyPropertyChanged
    {
        public ObservableCollectionExtended<LoadedTLK> ME1TLKItems { get; private set; } = new ObservableCollectionExtended<LoadedTLK>();
        public ObservableCollectionExtended<LoadedTLK> ME2TLKItems { get; private set; } = new ObservableCollectionExtended<LoadedTLK>();
        public ObservableCollectionExtended<LoadedTLK> ME3TLKItems { get; private set; } = new ObservableCollectionExtended<LoadedTLK>();

        public TLKManagerWPF()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        #region Commands
        public ICommand ME1ReloadTLKs { get; set; }
        public ICommand ME2ReloadTLKs { get; set; }
        public ICommand ME3ReloadTLKs { get; set; }

        public ICommand ME1AutoFindTLK { get; set; }
        public ICommand ME2AutoFindTLK { get; set; }
        public ICommand ME3AutoFindTLK { get; set; }

        private void LoadCommands()
        {
            ME1ReloadTLKs = new RelayCommand(ME1ReloadTLKStrings, ME1GamePathExists);
            ME2ReloadTLKs = new RelayCommand(ME2ReloadTLKStrings, ME2BIOGamePathExists);
            ME3ReloadTLKs = new RelayCommand(ME3ReloadTLKStrings, ME3BIOGamePathExists);

            ME1AutoFindTLK = new RelayCommand(AutoFindTLKME1, ME1GamePathExists);
            ME2AutoFindTLK = new RelayCommand(AutoFindTLKME2, ME2BIOGamePathExists);
            ME3AutoFindTLK = new RelayCommand(AutoFindTLKME3, ME3BIOGamePathExists);
        }

        #endregion

        #region Busy variables
        private bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
            set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(); } }
        }

        private bool _isBusyTaskbar;
        public bool IsBusyTaskbar
        {
            get { return _isBusyTaskbar; }
            set { if (_isBusyTaskbar != value) { _isBusyTaskbar = value; OnPropertyChanged(); } }
        }

        private string _busyText;

        public string BusyText
        {
            get { return _busyText; }
            set { if (_busyText != value) { _busyText = value; OnPropertyChanged(); } }
        }
        #endregion

        private void ME3ReloadTLKStrings(object obj)
        {
            BusyText = "Reloading Mass Effect 3 TLK strings";
            IsBusy = true;
        }

        private void ME2ReloadTLKStrings(object obj)
        {
            BusyText = "Reloading Mass Effect 2 TLK strings";
            IsBusy = true;
        }

        private void ME1ReloadTLKStrings(object obj)
        {
            BusyText = "Reloading Mass Effect TLK strings";
            IsBusy = true;
        }

        private void AutoFindTLKME3(object obj)
        {
            var tlks = Directory.EnumerateFiles(ME3Directory.BIOGamePath, "*.tlk", SearchOption.AllDirectories).Select(x => new LoadedTLK(x, false)).ToList();
            ME3TLKItems.ReplaceAll(tlks);
            SelectLoadedTLKsME3();
        }

        private void AutoFindTLKME2(object obj)
        {
            var tlks = Directory.EnumerateFiles(ME2Directory.BioGamePath, "*.tlk", SearchOption.AllDirectories).Select(x => new LoadedTLK(x, false)).ToList();
            ME2TLKItems.ReplaceAll(tlks);
            SelectLoadedTLKsME2();
        }

        private void AutoFindTLKME1(object obj)
        {
            var tlks = Directory.EnumerateFiles(ME1Directory.gamePath, "*Tlk*", SearchOption.AllDirectories).Select(x => new LoadedTLK(x, false)).ToList();
            ME1TLKItems.ReplaceAll(tlks);
            SelectLoadedTLKsME1();
        }

        private bool ME1GamePathExists(object obj)
        {
            return ME1Directory.gamePath != null && Directory.Exists(ME1Directory.gamePath);
        }

        private bool ME2BIOGamePathExists(object obj)
        {
            return ME2Directory.BioGamePath != null && Directory.Exists(ME2Directory.BioGamePath);
        }

        private bool ME3BIOGamePathExists(object obj)
        {
            return ME3Directory.BIOGamePath != null && Directory.Exists(ME3Directory.BIOGamePath);
        }

        public class LoadedTLK : INotifyPropertyChanged
        {
            public string tlkPath { get; set; }
            public string tlkDisplayPath { get; set; }

            private bool _selectedForLoad;
            public bool selectedForLoad { get { return _selectedForLoad; } set { _selectedForLoad = value; OnPropertyChanged(); } }

            public LoadedTLK(string tlkPath, bool selectedForLoad)
            {
                this.tlkPath = tlkPath;
                this.tlkDisplayPath = System.IO.Path.GetFileName(tlkPath);
                this.selectedForLoad = selectedForLoad;
            }

            #region Property Changed Notification
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
            #endregion
        }

        #region Property Changed Notification
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
        #endregion

        private void ME3TLKLangCombobox_Changed(object sender, SelectionChangedEventArgs e)
        {
            SelectLoadedTLKsME3();
        }

        private void SelectLoadedTLKsME1()
        {
            var tlkLang = ((ComboBoxItem)ME1TLKLangCombobox.SelectedItem).Content.ToString();
            if (tlkLang != "Default")
            {
                tlkLang += ".upk";
            }
            else
            {
                tlkLang = "Tlk.upk";
            }
            foreach (LoadedTLK tlk in ME1TLKItems)
            {
                tlk.selectedForLoad = tlk.tlkPath.EndsWith(tlkLang);
            }
        }

        private void SelectLoadedTLKsME3()
        {
            var tlkLang = ((ComboBoxItem)ME3TLKLangCombobox.SelectedItem).Content.ToString();
            tlkLang += ".tlk";
            foreach (LoadedTLK tlk in ME3TLKItems)
            {
                tlk.selectedForLoad = tlk.tlkPath.EndsWith(tlkLang);
            }
        }

        private void SelectLoadedTLKsME2()
        {
            var tlkLang = ((ComboBoxItem)ME2TLKLangCombobox.SelectedItem).Content.ToString();
            tlkLang += ".tlk";
            foreach (LoadedTLK tlk in ME2TLKItems)
            {
                tlk.selectedForLoad = tlk.tlkPath.EndsWith(tlkLang);
            }
        }

        private void ME2TLKLangCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectLoadedTLKsME2();
        }

        private void ME1TLKLangCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectLoadedTLKsME1();
        }
    }
}
