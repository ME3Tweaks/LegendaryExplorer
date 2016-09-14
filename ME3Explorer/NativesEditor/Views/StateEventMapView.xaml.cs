using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Gammtek.Conduit;
using Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap;
using MassEffect.NativesEditor.Dialogs;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;

namespace MassEffect.NativesEditor.Views
{
	/// <summary>
	///   Interaction logic for StateEventMapView.xaml
	/// </summary>
	public partial class StateEventMapView : INotifyPropertyChanged
    {
		public StateEventMapView()
		{
			InitializeComponent();
            SetFromStateEventMap(new BioStateEventMap());
        }
        private KeyValuePair<int, BioStateEvent> _selectedStateEvent;
        private BioStateEventElement _selectedStateEventElement;
        private ObservableCollection<KeyValuePair<int, BioStateEvent>> _stateEvents;
        
        public bool CanAddStateEventElement
        {
            get { return StateEvents != null && SelectedStateEvent.Value != null; }
        }

        public bool CanRemoveStateEvent
        {
            get { return StateEvents != null && SelectedStateEvent.Value != null; }
        }

        public bool CanRemoveStateEventElement
        {
            get
            {
                if (StateEvents == null || SelectedStateEvent.Value == null)
                {
                    return false;
                }

                return SelectedStateEventElement != null;
            }
        }

        public KeyValuePair<int, BioStateEvent> SelectedStateEvent
        {
            get { return _selectedStateEvent; }
            set
            {
                SetProperty(ref _selectedStateEvent, value);
                OnPropertyChanged(nameof(CanAddStateEventElement));
                OnPropertyChanged(nameof(CanRemoveStateEvent));
                OnPropertyChanged(nameof(CanRemoveStateEventElement));
            }
        }

        public BioStateEventElement SelectedStateEventElement
        {
            get { return _selectedStateEventElement; }
            set
            {
                SetProperty(ref _selectedStateEventElement, value);
                OnPropertyChanged(nameof(CanRemoveStateEventElement));
            }
        }

        public ObservableCollection<KeyValuePair<int, BioStateEvent>> StateEvents
        {
            get { return _stateEvents; }
            set
            {
                SetProperty(ref _stateEvents, value);
            }
        }

        public void AddStateEvent()
        {
            if (StateEvents == null)
            {
                StateEvents = InitCollection<KeyValuePair<int, BioStateEvent>>();
            }

            var dlg = new NewObjectDialog
            {
                ContentText = "New state event",
                ObjectId = GetMaxStateEventId() + 1
            };

            if (dlg.ShowDialog() == false || dlg.ObjectId < 0)
            {
                return;
            }

            AddStateEvent(dlg.ObjectId);
        }

        public void AddStateEvent(int id, BioStateEvent stateEvent = null)
        {
            if (StateEvents == null)
            {
                StateEvents = InitCollection<KeyValuePair<int, BioStateEvent>>();
            }

            if (StateEvents.Any(pair => pair.Key == id))
            {
                return;
            }

            if (stateEvent == null)
            {
                stateEvent = new BioStateEvent(InitCollection<BioStateEventElement>());
            }

            if (!(stateEvent.Elements is ObservableCollection<BioStateEventElement>))
            {
                stateEvent.Elements = InitCollection(stateEvent.Elements);
            }

            var stateEventPair = new KeyValuePair<int, BioStateEvent>(id, stateEvent);

            StateEvents.Add(stateEventPair);

            SelectedStateEvent = stateEventPair;
        }

        public void AddStateEventElement(BioStateEventElementType elementType)
        {
            if (StateEvents == null || SelectedStateEvent.Value == null)
            {
                return;
            }

            switch (elementType)
            {
                case BioStateEventElementType.Bool:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementBool());

                        break;
                    }
                case BioStateEventElementType.Consequence:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementConsequence());

                        break;
                    }
                case BioStateEventElementType.Float:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementFloat());

                        break;
                    }
                case BioStateEventElementType.Function:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementFunction());

                        break;
                    }
                case BioStateEventElementType.Int:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementInt());

                        break;
                    }
                case BioStateEventElementType.LocalBool:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementLocalBool());

                        break;
                    }
                case BioStateEventElementType.LocalFloat:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementLocalFloat());

                        break;
                    }
                case BioStateEventElementType.LocalInt:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementLocalInt());

                        break;
                    }
                case BioStateEventElementType.Substate:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementSubstate(siblingIndices: InitCollection<int>()));

                        break;
                    }
            }
        }

        public void AddSubstateSiblingIndex()
        {
            if (StateEvents == null || SelectedStateEvent.Value == null || SelectedStateEventElement == null)
            {
                return;
            }

            var selectedSubstate = SelectedStateEventElement as BioStateEventElementSubstate;

            if (selectedSubstate == null)
            {
                return;
            }

            if (!(selectedSubstate.SiblingIndices is ObservableCollection<int>))
            {
                selectedSubstate.SiblingIndices = InitCollection(selectedSubstate.SiblingIndices);
            }

            var dlg = new NewObjectDialog
            {
                ContentText = "New substate sibling index",
                ObjectId = 0
            };

            if (dlg.ShowDialog() == false || dlg.ObjectId < 0)
            {
                return;
            }

            selectedSubstate.SiblingIndices.Add(dlg.ObjectId);
        }

        public void CopyStateEvent()
        {
            if (StateEvents == null || SelectedStateEvent.Value == null)
            {
                return;
            }

            var dlg = new CopyObjectDialog
            {
                ContentText = string.Format("Copy state event #{0}", SelectedStateEvent.Key),
                ObjectId = SelectedStateEvent.Key
            };

            if (dlg.ShowDialog() == false || dlg.ObjectId < 0)
            {
                return;
            }

            AddStateEvent(dlg.ObjectId, new BioStateEvent(SelectedStateEvent.Value));
        }

        public void CopyStateEventElement()
        {
            if (StateEvents == null || SelectedStateEvent.Value == null || SelectedStateEventElement == null)
            {
                return;
            }

            var elementType = SelectedStateEventElement.ElementType;

            switch (elementType)
            {
                case BioStateEventElementType.Bool:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementBool(SelectedStateEventElement as BioStateEventElementBool));

                        break;
                    }
                case BioStateEventElementType.Consequence:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementConsequence(SelectedStateEventElement as BioStateEventElementConsequence));

                        break;
                    }
                case BioStateEventElementType.Float:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementFloat(SelectedStateEventElement as BioStateEventElementFloat));

                        break;
                    }
                case BioStateEventElementType.Function:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementFunction(SelectedStateEventElement as BioStateEventElementFunction));

                        break;
                    }
                case BioStateEventElementType.Int:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementInt(SelectedStateEventElement as BioStateEventElementInt));

                        break;
                    }
                case BioStateEventElementType.LocalBool:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementLocalBool(SelectedStateEventElement as BioStateEventElementLocalBool));

                        break;
                    }
                case BioStateEventElementType.LocalFloat:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementLocalFloat(SelectedStateEventElement as BioStateEventElementLocalFloat));

                        break;
                    }
                case BioStateEventElementType.LocalInt:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementLocalInt(SelectedStateEventElement as BioStateEventElementLocalInt));

                        break;
                    }
                case BioStateEventElementType.Substate:
                    {
                        SelectedStateEvent.Value.Elements.Add(new BioStateEventElementSubstate(SelectedStateEventElement as BioStateEventElementSubstate));

                        break;
                    }
            }
        }

        public bool TryFindStateEventMap(IMEPackage pcc, out IExportEntry export, out int dataOffset)
        {
            export = null;
            dataOffset = -1;

            try
            {
                export = pcc.Exports.First(exp => exp.ClassName == "BioStateEventMap");
            }
            catch
            {
                return false;
            }


            var mapProperties = PropertyReader.getPropList(export);

            if (mapProperties.Count <= 0)
            {
                return false;
            }

            var mapProperty = mapProperties.Find(property => property.TypeVal == PropertyReader.Type.None);
            dataOffset = mapProperty.offend;

            return true;
        }

        public void Open(IMEPackage pcc)
        {
            IExportEntry export;
            int dataOffset;

            if (!TryFindStateEventMap(pcc, out export, out dataOffset))
            {
                return;
            }

            using (var stream = new MemoryStream(export.Data))
            {
                stream.Seek(dataOffset, SeekOrigin.Begin);

                var stateEventMap = BinaryBioStateEventMap.Load(stream);
                StateEvents = InitCollection(stateEventMap.StateEvents.OrderBy(stateEvent => stateEvent.Key));
                SetListsAsBindable();
            }
        }

        
        public BioStateEventMap ToStateEventMap()
        {
            var stateEventMap = new BioStateEventMap
            {
                StateEvents = StateEvents.ToDictionary(pair => pair.Key, pair => pair.Value)
            };

            return stateEventMap;
        }

        public void ChangeStateEventId()
        {
            if (SelectedStateEvent.Value == null)
            {
                return;
            }

            var dlg = new ChangeObjectIdDialog
            {
                ContentText = string.Format("Change id of codex page #{0}", SelectedStateEvent.Key),
                ObjectId = SelectedStateEvent.Key
            };

            if (dlg.ShowDialog() == false || dlg.ObjectId < 0 || dlg.ObjectId == SelectedStateEvent.Key)
            {
                return;
            }

            var codexSection = SelectedStateEvent.Value;

            StateEvents.Remove(SelectedStateEvent);

            AddStateEvent(dlg.ObjectId, codexSection);
        }

        public void RemoveStateEvent()
        {
            if (StateEvents == null || SelectedStateEvent.Value == null)
            {
                return;
            }

            var index = StateEvents.IndexOf(SelectedStateEvent);

            if (!StateEvents.Remove(SelectedStateEvent))
            {
                return;
            }

            if (StateEvents.Any())
            {
                SelectedStateEvent = ((index - 1) >= 0)
                    ? StateEvents[index - 1]
                    : StateEvents.First();
            }
        }

        public void RemoveStateEventElement()
        {
            if (StateEvents == null || SelectedStateEvent.Value == null || SelectedStateEventElement == null)
            {
                return;
            }

            var index = SelectedStateEvent.Value.Elements.IndexOf(SelectedStateEventElement);

            if (!SelectedStateEvent.Value.Elements.Remove(SelectedStateEventElement))
            {
                return;
            }

            if (SelectedStateEvent.Value.Elements.Any())
            {
                SelectedStateEventElement = ((index - 1) >= 0)
                    ? SelectedStateEvent.Value.Elements[index - 1]
                    : SelectedStateEvent.Value.Elements.First();
            }
        }

        public void RemoveSubstateSiblingIndex(int siblingIndex)
        {
            if (StateEvents == null || SelectedStateEvent.Value == null || SelectedStateEventElement == null)
            {
                return;
            }

            var selectedSubstate = SelectedStateEventElement as BioStateEventElementSubstate;

            if (selectedSubstate == null)
            {
                return;
            }

            if (siblingIndex < 0 || siblingIndex >= selectedSubstate.SiblingIndices.Count)
            {
                return;
            }

            selectedSubstate.SiblingIndices.RemoveAt(siblingIndex);
        }

        protected void SetListsAsBindable()
        {
            //StateEvents = new ObservableCollection<KeyValuePair<int, BioStateEvent>>(StateEvents);

            foreach (var stateEvent in StateEvents)
            {
                stateEvent.Value.Elements = InitCollection(stateEvent.Value.Elements);

                foreach (var substateStateEventElement in stateEvent.Value.Elements
                    .OfType<BioStateEventElementSubstate>().Select(stateEventElement => stateEventElement))
                {
                    substateStateEventElement.SiblingIndices = InitCollection(substateStateEventElement.SiblingIndices);
                }
            }
        }

        
        private static ObservableCollection<T> InitCollection<T>()
        {
            return new ObservableCollection<T>();
        }

        
        private static ObservableCollection<T> InitCollection<T>(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                ThrowHelper.ThrowArgumentNullException("collection");
            }

            return new ObservableCollection<T>(collection);
        }

        private int GetMaxStateEventId()
        {
            return StateEvents.Any() ? StateEvents.Max(pair => pair.Key) : -1;
        }

        private void SetFromStateEventMap(BioStateEventMap bioStateEventMap)
        {
            if (bioStateEventMap == null)
            {
                return;
            }

            StateEvents = InitCollection(bioStateEventMap.StateEvents);

            SetListsAsBindable();
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

        private void ChangeStateEventId_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ChangeStateEventId();
        }

        private void CopyStateEvent_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CopyStateEvent();
        }

        private void RemoveStateEvent_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            RemoveStateEvent();
        }

        private void AddStateEvent_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            AddStateEvent();
        }

        private void CopyStateEventElement_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CopyStateEventElement();
        }

        private void RemoveStateEventElement_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            RemoveStateEventElement();
        }

        private void AddStateEventElement_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            AddStateEventElement((BioStateEventElementType)NewStateEventElementComboBox.Tag);
        }

        private void AddSubstateSiblingIndex_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            AddSubstateSiblingIndex();
        }

        private void RemoveSubstateSiblingIndex_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            RemoveSubstateSiblingIndex(SubstateStateEventSiblingIndicesListBox.SelectedIndex);
        }
    }
}
