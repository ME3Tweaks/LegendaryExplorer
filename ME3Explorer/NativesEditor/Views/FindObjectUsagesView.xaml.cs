using System;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Gammtek.Conduit.MassEffect3.SFXGame;
using Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap;
using ME3Explorer;

namespace MassEffect.NativesEditor.Views
{
	/// <summary>
	///     Interaction logic for FindObjectUsagesDialog.xaml
	/// </summary>
	public partial class FindObjectUsagesView : NotifyPropertyChangedControlBase
	{
		public FindObjectUsagesView()
		{
			InitializeComponent();
        }

        int _searchTerm;
        public int SearchTerm
        {
            get => _searchTerm;

            set
            {
                SetProperty(ref _searchTerm, value);
                UpdateSearch();
            }
        }

        string _typeToFind;
        public string TypeToFind
        {
            get => _typeToFind;
            set
            {
                SetProperty(ref _typeToFind, value);
                UpdateSearch();
            }
        }

        public PlotEditor parentRef;

        void UpdateSearch()
        {
            if (parentRef == null) return;
            switch (TypeToFind)
            {
                case "Plot Bool":
                    int boolId = SearchTerm;
                    searchResultsListBox.ItemsSource = parentRef.StateEventMapControl.StateEvents.Where(x => 
                        x.Value.HasElements && x.Value.Elements.Any(y =>
                           (y.ElementType == BioStateEventElementType.Bool && (y as BioStateEventElementBool)?.GlobalBool == boolId)
                        || (y.ElementType == BioStateEventElementType.Substate && 
                                (((y as BioStateEventElementSubstate)?.GlobalBool == boolId) 
                                || (y as BioStateEventElementSubstate)?.ParentIndex == boolId)
                                /*|| (y as BioStateEventElementSubstate).SiblingIndices.Contains(boolId)*/)
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
        
        private void searchResultsListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (searchResultsListBox.SelectedIndex == -1)
            {
                return;
            }
            var selectedItem = searchResultsListBox.SelectedItem;
            if (selectedItem is KeyValuePair<int, BioStateEvent> pair)
            {
                parentRef.MainTabControl.SelectedValue = parentRef.StateEventMapControl;
                parentRef.StateEventMapControl.SelectedStateEvent = pair;
                parentRef.StateEventMapControl.StateEventMapListBox.ScrollIntoView(pair);
            }
        }
    }
}
