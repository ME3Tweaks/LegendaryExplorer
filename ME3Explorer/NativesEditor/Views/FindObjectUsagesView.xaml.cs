using System;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Gammtek.Conduit.MassEffect3.SFXGame;
using Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap;

namespace MassEffect.NativesEditor.Views
{
	/// <summary>
	///     Interaction logic for FindObjectUsagesDialog.xaml
	/// </summary>
	public partial class FindObjectUsagesView : INotifyPropertyChanged
	{
		public FindObjectUsagesView()
		{
			InitializeComponent();
        }

        int _searchTerm;
        public int SearchTerm
        {
            get
            {
                return _searchTerm;
            }

            set
            {
                _searchTerm = value;
                UpdateSearch();
            }
        }
        List<BioVersionedNativeObject> searchResults;

        public List<BioVersionedNativeObject> SearchResults
        {
            get
            {
                return searchResults;
            }

            set
            {
                SetProperty(ref searchResults, value);
            }
        }

        public ShellView parentRef;

        void UpdateSearch()
        {
            string typeToFind = ((ComboBoxItem)objectTypeCombo.SelectedItem).Content as string;
            switch (typeToFind)
            {
                case "Plot Bool":
                    int boolId = SearchTerm;
                    searchResultsListBox.ItemsSource = parentRef.StateEventMapControl.StateEvents.Where(x => 
                        x.Value.HasElements && x.Value.Elements.Any(y =>
                            y.ElementType == BioStateEventElementType.Bool && (y as BioStateEventElementBool)?.GlobalBool == boolId
                        )
                    );
                    break;
                case "Plot Float":
                    int floatId = SearchTerm;
                    searchResultsListBox.ItemsSource = parentRef.StateEventMapControl.StateEvents.Where(x =>
                        x.Value.HasElements && x.Value.Elements.Any(y =>
                            y.ElementType == BioStateEventElementType.Float && (y as BioStateEventElementFloat)?.GlobalFloat == floatId
                        )
                    );
                    break;
                case "Plot Int":
                    int intId = SearchTerm;
                    searchResultsListBox.ItemsSource = parentRef.StateEventMapControl.StateEvents.Where(x =>
                        x.Value.HasElements && x.Value.Elements.Any(y =>
                            y.ElementType == BioStateEventElementType.Int && (y as BioStateEventElementInt)?.GlobalInt == intId
                        )
                    );
                    break;
                default:
                    break;
            }
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

        private void searchResultsListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (searchResultsListBox.SelectedIndex == -1)
            {
                return;
            }
            var selectedItem = searchResultsListBox.SelectedItem;
            if (selectedItem is KeyValuePair<int, BioStateEvent>)
            {
                parentRef.MainTabControl.SelectedIndex = 2;
                parentRef.StateEventMapControl.SelectedStateEvent = (KeyValuePair<int, BioStateEvent>)selectedItem;
                parentRef.StateEventMapControl.StateEventMapListBox.ScrollIntoView(selectedItem);
            }
        }

        private void objectTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsInitialized)
            {
                UpdateSearch();
            }
        }
    }
}
