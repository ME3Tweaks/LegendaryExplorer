using System.Collections.Generic;
using System.Linq;
using Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap;
using Gammtek.Conduit.MassEffect3.SFXGame.CodexMap;
using Gammtek.Conduit.MassEffect3.SFXGame.QuestMap;
using ME3Explorer;
using System.Windows.Data;
using System.Globalization;
using System;

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
            UpdateSections();
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
                UpdateSections();
            }
        }

        private string[] _sections = new string[] { "State Events" };
        public string[] Sections
        {
            get => _sections;
            set { SetProperty(ref _sections, value); }
        }

        public PlotEditor parentRef;

        private void UpdateSections()
        {
            if (parentRef == null) return;
            switch(TypeToFind)
            {
                case "TLK String":
                    Sections = new string[] { "Codex Pages", "Codex Sections", "Quest Goals", "Plot Items", "Tasks" };
                    break;

                case "Quest":
                    Sections = new string[] { "Task Evals", "Quests" };
                    break;

                default:
                    Sections = new string[] { "State Events" };
                    break;
            }

            ObjectSectionCombo.SelectedIndex = 0;
        }

        private void UpdateSearch()
        {
            if (parentRef == null) return;
            switch (TypeToFind)
            {
                case "Plot Bool":
                    int boolId = SearchTerm;
                    SearchResults.ItemsSource = parentRef.StateEventMapControl.StateEvents.Where(x => 
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
                    SearchResults.ItemsSource = parentRef.StateEventMapControl.StateEvents.Where(x =>
                        x.Value.HasElements && x.Value.Elements.Any(y =>
                            y.ElementType == BioStateEventElementType.Float && (y as BioStateEventElementFloat)?.GlobalFloat == floatId
                        )
                    );
                    break;
                case "Plot Int":
                    int intId = SearchTerm;
                    SearchResults.ItemsSource = parentRef.StateEventMapControl.StateEvents.Concat(parentRef.ConsequenceMapControl?.StateEvents)
                        .Where(x => x.Value.HasElements && x.Value.Elements.Any(y =>
                            y.ElementType == BioStateEventElementType.Int && (y as BioStateEventElementInt)?.GlobalInt == intId
                        )
                    );
                    break;

                case "TLK String":
                    int stringId = SearchTerm;

                    switch (ObjectSectionCombo.SelectedItem)
                    {
                        case "Codex Pages":
                            SearchResults.ItemsSource = parentRef.CodexMapControl.CodexPages.Where(x => 
                                x.Value.Title == stringId || x.Value.Description == stringId);
                            break;
                        case "Codex Sections":
                            SearchResults.ItemsSource = parentRef.CodexMapControl.CodexSections.Where(x => 
                                x.Value.Title == stringId || x.Value.Description == stringId);
                            break;
                        case "Quest Goals":
                            SearchResults.ItemsSource = parentRef.QuestMapControl.Quests.Where(x => 
                                x.Value.Goals.Any(y => y.Description == stringId || y.Name == stringId));
                            break;
                        case "Plot Items":
                            SearchResults.ItemsSource = parentRef.QuestMapControl.Quests.Where(x =>
                                x.Value.PlotItems.Any(y => y.Name == stringId));
                            break;
                        case "Tasks":
                            SearchResults.ItemsSource = parentRef.QuestMapControl.Quests.Where(x =>
                                x.Value.Tasks.Any(y => y.Description == stringId || y.Name == stringId));
                            break;

                        default: break;
                    }

                    break;

                case "Quest":
                    int questId = SearchTerm;
                    switch (ObjectSectionCombo.SelectedItem)
                    {
                        case "Task Evals":
                            var taskEvals = parentRef.QuestMapControl.BoolStateTaskListsControl.StateTaskLists.Concat(
                                            parentRef.QuestMapControl.FloatStateTaskListsControl.StateTaskLists).Concat(
                                            parentRef.QuestMapControl.IntStateTaskListsControl.StateTaskLists);
                            SearchResults.ItemsSource = taskEvals.Where(x =>
                                x.Value.TaskEvals.Any(y => y.Quest == questId));
                            break;
                        case "Quests":
                            // This is pretty useless but I thought it was dumb not to include
                            SearchResults.ItemsSource = parentRef.QuestMapControl.Quests.Where(x =>
                                x.Key == questId);
                            break;
                        default: break;
                    }
                    break;

                default:
                    break;
            }
        }
        
        private void SearchResults_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SearchResults.SelectedIndex == -1)
            {
                return;
            }
            var selectedItem = SearchResults.SelectedItem;

            switch (selectedItem)
            {
                case KeyValuePair<int, BioStateEvent> stateEvent:
                    parentRef.GoToStateEvent(stateEvent);
                    break;
                case KeyValuePair<int, BioCodexPage> codexPage:
                    parentRef.MainTabControl.SelectedValue = parentRef.CodexMapControl;
                    parentRef.CodexMapControl.GoToCodexPage(codexPage);
                    break;
                case KeyValuePair<int, BioCodexSection> codexSection:
                    parentRef.MainTabControl.SelectedValue = parentRef.CodexMapControl;
                    parentRef.CodexMapControl.GoToCodexSection(codexSection);
                    break;
            }

        }

        private void ObjectSectionCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateSearch();
        }
    }

}
