using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Caliburn.Micro;
using Gammtek.Conduit;
using Gammtek.Conduit.MassEffect3.SFXGame.CodexMap;
using MassEffect.NativesEditor.Dialogs;
using ME3LibWV;

namespace MassEffect.NativesEditor.ViewModels
{
	public class CodexMapViewModel : PropertyChangedBase
	{
		private BindableCollection<KeyValuePair<int, BioCodexPage>> _codexPages;
		private BindableCollection<KeyValuePair<int, BioCodexSection>> _codexSections;
		private KeyValuePair<int, BioCodexPage> _selectedCodexPage;
		private KeyValuePair<int, BioCodexSection> _selectedCodexSection;

		public CodexMapViewModel()
			: this(null) {}

		public CodexMapViewModel(BioCodexMap codexMap)
		{
			SetFromCodexMap(codexMap ?? new BioCodexMap());
		}

		public bool CanRemoveCodexPage
		{
			get
			{
				if (CodexPages == null || CodexPages.Count <= 0)
				{
					return false;
				}

				return SelectedCodexPage.Value != null;
			}
		}

		public bool CanRemoveCodexSection
		{
			get
			{
				if (CodexSections == null || CodexSections.Count <= 0)
				{
					return false;
				}

				return SelectedCodexSection.Value != null;
			}
		}

		public BindableCollection<KeyValuePair<int, BioCodexPage>> CodexPages
		{
			get { return _codexPages; }
			set
			{
				if (Equals(value, _codexPages))
				{
					return;
				}

				_codexPages = value;

				NotifyOfPropertyChange(() => CodexPages);
				NotifyOfPropertyChange(() => CanRemoveCodexPage);
			}
		}

		public BindableCollection<KeyValuePair<int, BioCodexSection>> CodexSections
		{
			get { return _codexSections; }
			set
			{
				if (Equals(value, _codexSections))
				{
					return;
				}

				_codexSections = value;

				NotifyOfPropertyChange(() => CodexSections);
				NotifyOfPropertyChange(() => CanRemoveCodexSection);
			}
		}

		public KeyValuePair<int, BioCodexPage> SelectedCodexPage
		{
			get { return _selectedCodexPage; }
			set
			{
				if (value.Equals(_selectedCodexPage))
				{
					return;
				}

				_selectedCodexPage = value;

				NotifyOfPropertyChange(() => SelectedCodexPage);
				NotifyOfPropertyChange(() => CanRemoveCodexPage);
			}
		}

		public KeyValuePair<int, BioCodexSection> SelectedCodexSection
		{
			get { return _selectedCodexSection; }
			set
			{
				if (value.Equals(_selectedCodexSection))
				{
					return;
				}

				_selectedCodexSection = value;

				NotifyOfPropertyChange(() => SelectedCodexSection);
				NotifyOfPropertyChange(() => CanRemoveCodexSection);
			}
		}

		public void AddCodexPage()
		{
			if (CodexPages == null)
			{
				CodexPages = InitCollection<KeyValuePair<int, BioCodexPage>>();
			}

			var dlg = new NewObjectDialog
			{
				ContentText = "New codex page",
				ObjectId = GetMaxCodexPageId() + 1
			};

			if (dlg.ShowDialog() == false || dlg.ObjectId < 0)
			{
				return;
			}

			AddCodexPage(dlg.ObjectId);
		}

		public void AddCodexPage(int id, BioCodexPage codexPage = null)
		{
			if (CodexPages == null)
			{
				CodexPages = InitCollection<KeyValuePair<int, BioCodexPage>>();
			}

			if (id < 0)
			{
				return;
			}

			var codexPagePair = new KeyValuePair<int, BioCodexPage>(id, codexPage ?? new BioCodexPage());

			CodexPages.Add(codexPagePair);

			SelectedCodexPage = codexPagePair;
		}

		public void AddCodexSection()
		{
			if (CodexSections == null)
			{
				CodexSections = InitCollection<KeyValuePair<int, BioCodexSection>>();
			}

			var dlg = new NewObjectDialog
			{
				ContentText = "New codex section",
				ObjectId = GetMaxCodexSectionId() + 1
			};

			if (dlg.ShowDialog() == false || dlg.ObjectId < 0)
			{
				return;
			}

			AddCodexSection(dlg.ObjectId);
		}

		// Does not replace existing
		public void AddCodexSection(int id, BioCodexSection codexSection = null)
		{
			if (CodexSections == null)
			{
				CodexSections = InitCollection<KeyValuePair<int, BioCodexSection>>();
			}

			if (CodexSections.Any(pair => pair.Key == id))
			{
				return;
			}

			var codexSectionPair = new KeyValuePair<int, BioCodexSection>(id, codexSection ?? new BioCodexSection());

			CodexSections.Add(codexSectionPair);

			SelectedCodexSection = codexSectionPair;
		}

		public void ChangeCodexPageId()
		{
			if (SelectedCodexPage.Value == null)
			{
				return;
			}

			var dlg = new ChangeObjectIdDialog
			{
				ContentText = string.Format("Change id of codex page #{0}", SelectedCodexPage.Key),
				ObjectId = SelectedCodexPage.Key
			};

			if (dlg.ShowDialog() == false || dlg.ObjectId < 0 || dlg.ObjectId == SelectedCodexPage.Key)
			{
				return;
			}

			var codexSection = SelectedCodexPage.Value;

			CodexPages.Remove(SelectedCodexPage);

			AddCodexPage(dlg.ObjectId, codexSection);
		}

		public void ChangeCodexSectionId()
		{
			if (SelectedCodexSection.Value == null)
			{
				return;
			}

			var dlg = new ChangeObjectIdDialog
			{
				ContentText = string.Format("Change id of codex section #{0}", SelectedCodexSection.Key),
				ObjectId = SelectedCodexSection.Key
			};

			if (dlg.ShowDialog() == false || dlg.ObjectId < 0 || dlg.ObjectId == SelectedCodexSection.Key)
			{
				return;
			}

			var codexSection = SelectedCodexSection.Value;

			CodexSections.Remove(SelectedCodexSection);

			AddCodexSection(dlg.ObjectId, codexSection);
		}

		public void CopyCodexPage()
		{
			if (SelectedCodexPage.Value == null)
			{
				return;
			}

			var dlg = new CopyObjectDialog
			{
				ContentText = string.Format("Copy codex page #{0}", SelectedCodexPage.Key),
				ObjectId = GetMaxCodexPageId() + 1
			};

			if (dlg.ShowDialog() == false || dlg.ObjectId < 0 || SelectedCodexPage.Key == dlg.ObjectId)
			{
				return;
			}

			AddCodexPage(dlg.ObjectId, new BioCodexPage(SelectedCodexPage.Value));
		}

		public void CopyCodexSection()
		{
			if (SelectedCodexSection.Value == null)
			{
				return;
			}

			var dlg = new CopyObjectDialog
			{
				ContentText = string.Format("Copy codex section #{0}", SelectedCodexSection.Key),
				ObjectId = GetMaxCodexSectionId() + 1
			};

			if (dlg.ShowDialog() == false || dlg.ObjectId < 0 || SelectedCodexSection.Key == dlg.ObjectId)
			{
				return;
			}

			AddCodexSection(dlg.ObjectId, new BioCodexSection(SelectedCodexSection.Value));
		}

		public static bool TryFindCodexMap(PCCPackage pcc, out int exportIndex, out int dataOffset)
		{
			var index = pcc.FindClass("BioCodexMap");

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

			if (!TryFindCodexMap(pcc, out exportIndex, out dataOffset))
			{
				return;
			}

			using (var stream = new MemoryStream(pcc.Exports[exportIndex].Data))
			{
				stream.Seek(dataOffset, SeekOrigin.Begin);

				var codexMap = BinaryBioCodexMap.Load(stream);

				CodexPages = InitCollection(codexMap.Pages.OrderBy(pair => pair.Key));
				CodexSections = InitCollection(codexMap.Sections.OrderBy(pair => pair.Key));
			}
		}

		public void RemoveCodexPage()
		{
			if (CodexPages == null || SelectedCodexPage.Value == null)
			{
				return;
			}

			var index = CodexPages.IndexOf(SelectedCodexPage);

			if (!CodexPages.Remove(SelectedCodexPage))
			{
				return;
			}

			if (CodexPages.Any())
			{
				SelectedCodexPage = ((index - 1) >= 0)
					? CodexPages[index - 1]
					: CodexPages.First();
			}
		}

		public void RemoveCodexSection()
		{
			if (CodexSections == null || SelectedCodexSection.Value == null)
			{
				return;
			}

			var index = CodexSections.IndexOf(SelectedCodexSection);

			if (!CodexSections.Remove(SelectedCodexSection))
			{
				return;
			}

			if (CodexSections.Any())
			{
				SelectedCodexSection = ((index - 1) >= 0)
					? CodexSections[index - 1]
					: CodexSections.First();
			}
		}

		public void SaveToPcc(PCCPackage pcc)
		{
			var index = pcc.FindClass("BioCodexMap");

			if (index == 0)
			{
				return;
			}

			var exportIndex = pcc.Exports.FindIndex(entry => entry.idxClass == index);

			if (exportIndex < 0)
			{
				return;
			}

			var codexMapData = pcc.Exports[exportIndex].Data;
			var codexMapProperties = PropertyReader.getPropList(pcc, codexMapData);

			if (codexMapProperties.Count <= 0)
			{
				return;
			}

			var codexMapProperty = codexMapProperties.Find(property => property.TypeVal == PropertyReader.Type.None);
			var codexMapDataOffset = codexMapProperty.offend;

			byte[] bytes;
			var codexMap = new BioCodexMap(CodexSections.ToDictionary(pair => pair.Key, pair => pair.Value),
				CodexPages.ToDictionary(pair => pair.Key, pair => pair.Value));

			// CodexMap
			using (var stream = new MemoryStream())
			{
				((BinaryBioCodexMap) codexMap).Save(stream);

				bytes = stream.ToArray();
			}

			Array.Resize(ref codexMapData, codexMapDataOffset + bytes.Length);
			bytes.CopyTo(codexMapData, codexMapDataOffset);

			var temp = pcc.Exports[exportIndex];
			Array.Resize(ref temp.Data, codexMapData.Length);
			codexMapData.CopyTo(temp.Data, 0);
			pcc.Exports[exportIndex] = temp;
		}

		[NotNull]
		public BioCodexMap ToCodexMap()
		{
			var codexMap = new BioCodexMap
			{
				Pages = CodexPages.ToDictionary(pair => pair.Key, pair => pair.Value),
				Sections = CodexSections.ToDictionary(pair => pair.Key, pair => pair.Value)
			};

			return codexMap;
		}

		protected void SetFromCodexMap(BioCodexMap codexMap)
		{
			if (codexMap == null)
			{
				return;
			}

			CodexPages = InitCollection(codexMap.Pages.OrderBy(pair => pair.Key));
			CodexSections = InitCollection(codexMap.Sections.OrderBy(pair => pair.Key));
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

		private int GetMaxCodexPageId()
		{
			return CodexPages.Any() ? CodexPages.Max(pair => pair.Key) : -1;
		}

		private int GetMaxCodexSectionId()
		{
			return CodexSections.Any() ? CodexSections.Max(pair => pair.Key) : -1;
		}
	}
}
