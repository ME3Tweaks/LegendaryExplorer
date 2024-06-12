using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Gammtek.Conduit.MassEffect3.SFXGame.CodexMap;
using LegendaryExplorer.Tools.PlotEditor.Dialogs;
using static LegendaryExplorer.Tools.TlkManagerNS.TLKManagerWPF;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Tools.PlotEditor
{
	/// <summary>
	///   Interaction logic for CodexMapView.xaml.
	/// </summary>
	public partial class CodexMapView : NotifyPropertyChangedControlBase
	{
        public static IMEPackage package;

		/// <summary>
		///   Initializes a new instance of the <see cref="CodexMapView" /> class.
		/// </summary>
		public CodexMapView()
		{
            InitializeComponent();
            SetFromCodexMap(new BioCodexMap());
        }

        private ObservableCollection<KeyValuePair<int, BioCodexPage>> _codexPages;
        private ObservableCollection<KeyValuePair<int, BioCodexSection>> _codexSections;
        private KeyValuePair<int, BioCodexPage> _selectedCodexPage;
        private KeyValuePair<int, BioCodexSection> _selectedCodexSection;

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

        public ObservableCollection<KeyValuePair<int, BioCodexPage>> CodexPages
        {
            get => _codexPages;
            set
            {
                SetProperty(ref _codexPages, value);
                OnPropertyChanged(nameof(CanRemoveCodexPage));
                //CodexPagesListBox.ItemsSource = CodexPages;
            }
        }

        public ObservableCollection<KeyValuePair<int, BioCodexSection>> CodexSections
        {
            get => _codexSections;
            set
            {
                SetProperty(ref _codexSections, value);
                OnPropertyChanged(nameof(CanRemoveCodexSection));
            }
        }

        public KeyValuePair<int, BioCodexPage> SelectedCodexPage
        {
            get => _selectedCodexPage;
            set
            {
                SetProperty(ref _selectedCodexPage, value);
                OnPropertyChanged(nameof(CanRemoveCodexPage));
            }
        }

        public KeyValuePair<int, BioCodexSection> SelectedCodexSection
        {
            get => _selectedCodexSection;
            set
            {
                SetProperty(ref _selectedCodexSection, value);
                OnPropertyChanged(nameof(CanRemoveCodexSection));
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
            if (package.Game == MEGame.LE1)
            {
                codexPagePair.Value.IsLE1 = true;
            }
            
            CodexPages.Add(codexPagePair);

            SelectedCodexPage = codexPagePair;
        }

        public void addCodexSection()
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

            addCodexSection(dlg.ObjectId);
        }

        // Does not replace existing
        public void addCodexSection(int id, BioCodexSection codexSection = null)
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
                ContentText = $"Change id of codex page #{SelectedCodexPage.Key}",
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
                ContentText = $"Change id of codex section #{SelectedCodexSection.Key}",
                ObjectId = SelectedCodexSection.Key
            };

            if (dlg.ShowDialog() == false || dlg.ObjectId < 0 || dlg.ObjectId == SelectedCodexSection.Key)
            {
                return;
            }

            var codexSection = SelectedCodexSection.Value;

            CodexSections.Remove(SelectedCodexSection);

            addCodexSection(dlg.ObjectId, codexSection);
        }

        public void CopyCodexPage()
        {
            if (SelectedCodexPage.Value == null)
            {
                return;
            }

            var dlg = new CopyObjectDialog
            {
                ContentText = $"Copy codex page #{SelectedCodexPage.Key}",
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
                ContentText = $"Copy codex section #{SelectedCodexSection.Key}",
                ObjectId = GetMaxCodexSectionId() + 1
            };

            if (dlg.ShowDialog() == false || dlg.ObjectId < 0 || SelectedCodexSection.Key == dlg.ObjectId)
            {
                return;
            }

            addCodexSection(dlg.ObjectId, new BioCodexSection(SelectedCodexSection.Value));
        }

        public void GoToCodexPage(KeyValuePair<int, BioCodexPage> codexPage)
        {
            CodexTabControl.SelectedValue = CodexPagesTab;
            SelectedCodexPage = codexPage;
            CodexPagesListBox.ScrollIntoView(SelectedCodexPage);
            CodexPagesListBox.Focus();
        }

        public void GoToCodexSection(KeyValuePair<int, BioCodexSection> codexSection)
        {
            CodexTabControl.SelectedValue = CodexSectionsTab;
            SelectedCodexSection = codexSection;
            CodexSectionsListBox.ScrollIntoView(SelectedCodexSection);
            CodexSectionsListBox.Focus();
        }

        public static bool TryFindCodexMap(IMEPackage pcc, out ExportEntry export, out int dataOffset)
        {
            export = null;
            dataOffset = -1;

            try
            {
                export = pcc.Exports.First(exp => exp.ClassName == "BioCodexMap");
            }
            catch
            {
                return false;
            }

            dataOffset = export.propsEnd();

            return true;
        }

        public void Open(IMEPackage pcc)
        {
            if (!TryFindCodexMap(pcc, out ExportEntry export, out int dataOffset))
            {
                return;
            }

            using (var stream = new MemoryStream(export.Data))
            {
                stream.Seek(dataOffset, SeekOrigin.Begin);
                var codexMap = BinaryBioCodexMap.Load(stream, pcc.Game is MEGame.ME3 or MEGame.LE3 ? Encoding.UTF8 : Encoding.Latin1);

                CodexPages = InitCollection(codexMap.Pages.OrderBy(pair => pair.Key));
                CodexSections = InitCollection(codexMap.Sections.OrderBy(pair => pair.Key));
            }

            foreach (var page in CodexPages)
            {
                page.Value.TitleAsString = GlobalFindStrRefbyID(page.Value.Title, pcc.Game, null);
            }

            foreach (var section in CodexSections)
            {
                section.Value.TitleAsString = GlobalFindStrRefbyID(section.Value.Title, pcc.Game, null);
            }

            package = pcc;
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

        public void removeCodexSection()
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

        public void SaveToPcc(IMEPackage pcc)
        {
            ExportEntry export;
            try
            {
                export = pcc.Exports.First(exp => exp.ClassName == "BioCodexMap");
            }
            catch
            {
                return;
            }

            byte[] codexMapData = export.Data;

            if (!export.GetProperties(includeNoneProperties: true).Any())
            {
                return;
            }

            var codexMapDataOffset = export.propsEnd();

            byte[] bytes;
            var codexMap = new BioCodexMap(CodexSections.ToDictionary(pair => pair.Key, pair => pair.Value),
                CodexPages.ToDictionary(pair => pair.Key, pair => pair.Value));

            // CodexMap
            using (var stream = new MemoryStream())
            {
                ((BinaryBioCodexMap)codexMap).Save(stream);

                bytes = stream.ToArray();
            }

            Array.Resize(ref codexMapData, codexMapDataOffset + bytes.Length);
            bytes.CopyTo(codexMapData, codexMapDataOffset);

            export.Data = codexMapData;
        }
        
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
        
        private static ObservableCollection<T> InitCollection<T>()
        {
            return new ObservableCollection<T>();
        }

        
        private static ObservableCollection<T> InitCollection<T>(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(collection));
            }

            return new ObservableCollection<T>(collection);
        }

        private int GetMaxCodexPageId()
        {
            return CodexPages.Any() ? CodexPages.Max(pair => pair.Key) : -1;
        }

        private int GetMaxCodexSectionId()
        {
            return CodexSections.Any() ? CodexSections.Max(pair => pair.Key) : -1;
        }

        private void ChangeCodexPageId_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ChangeCodexPageId();
        }

        private void CopyCodexPage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CopyCodexPage();
        }

        private void RemoveCodexPage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            RemoveCodexPage();
        }

        private void ChangeCodexSectionId_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ChangeCodexSectionId();
        }

        private void CopyCodexSection_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CopyCodexSection();
        }

        private void RemoveCodexSection_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            removeCodexSection();
        }

        private void AddCodexSection_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            addCodexSection();
        }

        private void AddCodexPage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            AddCodexPage();
        }

        private void txt_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if(package != null)
            {
                txt_cdxPgeDesc.Text = GlobalFindStrRefbyID(SelectedCodexPage.Value?.Description ?? 0, package);
                txt_cdxPgeTitle.Text = GlobalFindStrRefbyID(SelectedCodexPage.Value?.Title ?? 0, package);
                txt_cdxSecDesc.Text = GlobalFindStrRefbyID(SelectedCodexSection.Value?.Description ?? 0, package);
                txt_cdxSecTitle.Text = GlobalFindStrRefbyID(SelectedCodexSection.Value?.Title ?? 0, package);

                if (SelectedCodexPage.Value != null) SelectedCodexPage.Value.TitleAsString = txt_cdxPgeTitle.Text;
                if (SelectedCodexSection.Value != null) SelectedCodexSection.Value.TitleAsString = txt_cdxSecTitle.Text;
            }
        }
    }
}
