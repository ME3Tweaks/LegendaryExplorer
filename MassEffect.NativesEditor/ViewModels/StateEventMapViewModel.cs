using System.Collections.Generic;
using System.IO;
using System.Linq;
using Caliburn.Micro;
using Gammtek.Conduit;
using Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap;
using MassEffect.NativesEditor.Dialogs;
using ME3LibWV;

namespace MassEffect.NativesEditor.ViewModels
{
	public class StateEventMapViewModel : PropertyChangedBase
	{
		private KeyValuePair<int, BioStateEvent> _selectedStateEvent;
		private BioStateEventElement _selectedStateEventElement;
		private BindableCollection<KeyValuePair<int, BioStateEvent>> _stateEvents;

		public StateEventMapViewModel()
			: this(null) {}

		public StateEventMapViewModel(BioStateEventMap stateEventMap)
		{
			SetFromStateEventMap(stateEventMap ?? new BioStateEventMap());
		}

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
				if (value.Equals(_selectedStateEvent))
				{
					return;
				}

				_selectedStateEvent = value;

				NotifyOfPropertyChange(() => SelectedStateEvent);
				NotifyOfPropertyChange(() => CanAddStateEventElement);
				NotifyOfPropertyChange(() => CanRemoveStateEvent);
				NotifyOfPropertyChange(() => CanRemoveStateEventElement);
			}
		}

		public BioStateEventElement SelectedStateEventElement
		{
			get { return _selectedStateEventElement; }
			set
			{
				if (Equals(value, _selectedStateEventElement))
				{
					return;
				}

				_selectedStateEventElement = value;

				NotifyOfPropertyChange(() => SelectedStateEventElement);
				NotifyOfPropertyChange(() => CanRemoveStateEventElement);
			}
		}

		public BindableCollection<KeyValuePair<int, BioStateEvent>> StateEvents
		{
			get { return _stateEvents; }
			set
			{
				if (Equals(value, _stateEvents))
				{
					return;
				}

				_stateEvents = value;

				NotifyOfPropertyChange(() => StateEvents);
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

			if (!(stateEvent.Elements is BindableCollection<BioStateEventElement>))
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

			if (!(selectedSubstate.SiblingIndices is BindableCollection<int>))
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

		public bool TryFindStateEventMap(PCCPackage pcc, out int exportIndex, out int dataOffset)
		{
			var index = pcc.FindClass("BioStateEventMap");

			exportIndex = -1;
			dataOffset = -1;

			if (index == 0)
			{
				return false;
			}

			exportIndex = pcc.Exports.FindIndex(entry => entry.idxClass == index);

			if (exportIndex < 0)
			{
				return false;
			}

			var mapData = pcc.Exports[exportIndex].Data;
			var mapProperties = PropertyReader.getPropList(pcc, mapData);

			if (mapProperties.Count <= 0)
			{
				return false;
			}

			var mapProperty = mapProperties.Find(property => property.TypeVal == PropertyReader.Type.None);
			dataOffset = mapProperty.offend;

			return true;
		}

		public void Open(PCCPackage pcc)
		{
			int exportIndex;
			int dataOffset;

			if (!TryFindStateEventMap(pcc, out exportIndex, out dataOffset))
			{
				return;
			}

			using (var stream = new MemoryStream(pcc.Exports[exportIndex].Data))
			{
				stream.Seek(dataOffset, SeekOrigin.Begin);

				var stateEventMap = BinaryBioStateEventMap.Load(stream);
				StateEvents = InitCollection(stateEventMap.StateEvents.OrderBy(stateEvent => stateEvent.Key));
			}
		}

		[NotNull]
		public BioStateEventMap ToStateEventMap()
		{
			var stateEventMap = new BioStateEventMap
			{
				StateEvents = StateEvents.ToDictionary(pair => pair.Key, pair => pair.Value)
			};

			return stateEventMap;
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
			//StateEvents = new BindableCollection<KeyValuePair<int, BioStateEvent>>(StateEvents);

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

		[NotNull]
		private static BindableCollection<T> InitCollection<T>()
		{
			return new BindableCollection<T>();
		}

		[NotNull]
		private static BindableCollection<T> InitCollection<T>(IEnumerable<T> collection)
		{
			if (collection == null)
			{
				ThrowHelper.ThrowArgumentNullException("collection");
			}

			return new BindableCollection<T>(collection);
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
	}
}
