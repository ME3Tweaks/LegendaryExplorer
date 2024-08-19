using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GongSolutions.Wpf.DragDrop;
using LegendaryExplorer.GameInterop;
using LegendaryExplorer.GameInterop.InteropTargets;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.UserControls.ExportLoaderControls.MaterialEditor;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using Newtonsoft.Json;
using static LegendaryExplorer.Tools.LiveLevelEditor.LELiveLevelEditorWindow;
using Image = LegendaryExplorerCore.Textures.Image;
using MessageBox = System.Windows.MessageBox;
using PixelFormat = LegendaryExplorerCore.Textures.PixelFormat;

namespace LegendaryExplorer.Tools.LiveLevelEditor.MatEd
{
    /// <summary>
    /// Interaction logic for MaterialEditorLLE.xaml
    /// </summary>
    public partial class MaterialEditorLLE : NotifyPropertyChangedControlBase, IDropTarget, IBusyUIHost, IDisposable
    {
        public MEGame Game { get; set; }

        /// <summary>
        /// Package that is being used for editing. Export loaders will load exclusively out of this package
        /// </summary>
        public IMEPackage EditorPackage { get; set; }

        /// <summary>
        /// Invoked to push our material to the game for viewing.
        /// </summary>
        private readonly Action<IMEPackage, string> LoadMaterialInGameDelegate;

        // If data is being loaded into the editor
        private bool IsLoadingData;

        public InteropTarget GameTarget { get; set; }

        /// <summary>
        /// The main LLE control window
        /// </summary>
        public LELiveLevelEditorWindow LLE { get; set; }

        public ICommand PreviewOnMeshCommand { get; set; }
        public ICommand SaveMaterialPackageCommand { get; set; }
        public ICommand SetMaterialCommand { get; set; }
        public ICommand SetCustomMaterialCommand { get; set; }
        public ICommand RegenMaterialsListCommand { get; set; }

        private int _currentMaterialIdx = 0;
        public int CurrentMaterialIdx
        {
            get => _currentMaterialIdx;
            set => SetProperty(ref _currentMaterialIdx, value);
        }

        public void Initialize(LELiveLevelEditorWindow lle)
        {
            LLE = lle;
            Game = lle.Game;
            LoadCommands();
            InitializeComponent();
            // Todo: On loading material we spawn new package in the editor
            // We must set that material on the dest object or our changes won't make it to the actor... I think
            MEELC.ScalarValueChanged += UpdateScalarParameter;
            MEELC.VectorValueChanged += UpdateVectorParameter;
            MEELC.ShowSaveBar = false;
            GameTarget = GameController.GetInteropTargetForGame(Game);
            if (GameTarget is null)
            {
                throw new Exception($"{Game} does not support Live Material Editor!");
            }

            GameTarget.GameReceiveMessage += GameControllerOnReceiveMessage;
        }


        public ObservableCollectionExtended<string> LoadedMaterials { get; } = new();

        /// <summary>
        /// List of components on the current selected actor
        /// </summary>
        public ObservableCollectionExtended<string> Components { get; } = new();
        private string _selectedComponent;

        public string SelectedComponent
        {
            get => _selectedComponent;
            set
            {
                if (SetProperty(ref _selectedComponent, value) && LLE.SelectedActor != null)
                {
                    // This doesn't work for null values.
                    LLE.SelectedActor.ComponentIdx = Components.IndexOf(value);
                    LLE.SetBusy($"Selecting component: {value}", () => { });
                    string message = $"{InteropCommands.LLE_SELECT_ACTOR} {Path.GetFileNameWithoutExtension(LLE.SelectedActor.FileName)} {LLE.SelectedActor.ActorName} {LLE.SelectedActor.ComponentIdx}";
                    InteropHelper.SendMessageToGame(message, Game);
                }

                if (value == null)
                {
                    CurrentComponentMaterials.ClearEx(); // No component selected
                }
            }
        }


        private void RegenMaterialsList()
        {
            LoadedMaterials.ClearEx();
            InteropHelper.SendMessageToGame(InteropCommands.LME_GET_LOADED_MATERIALS, Game);
        }

        private void UpdateComponents(string json)
        {
            var components = JsonConvert.DeserializeObject<List<string>>(json);
            Components.ReplaceAll(components);
        }

        private void GameControllerOnReceiveMessage(string msg)
        {
            string[] command = msg.Split(" ");
            if (command.Length < 2)
                return;

            if (command[0] != "MATERIALEDITOR")
                return; // Not for us
            Debug.WriteLine($"MaterialEditorLLE Command: {msg}");

            var verb = command[1]; // Message Info
            if (verb == "COMPONENTMATERIALS")
            {
                // Materials on current component
                try
                {
                    var json = string.Join(' ', command.Skip(2));
                    var materials = JsonConvert.DeserializeObject<List<JsonMaterialSource>>(json);
                    CurrentComponentMaterials.ReplaceAll(materials);
                    // CallbackSetCustomMaterial();
                }
                catch (Exception ex)
                {
                    // Do nothing.
                }
            }
            else if (verb == "COMPONENTSLIST")
            {
                try
                {
                    UpdateComponents(string.Join(' ', command.Skip(2))); // Skip tool and verb
                }
                catch
                {

                }
            }
            else if (verb == "LOADEDMATERIAL")
            {
                try
                {
                    LoadedMaterials.Add(command[2]);
                }
                catch (Exception ex)
                {
                    // Do nothing.
                }
            }
        }

        private void LoadCustomMaterialInGame(IMEPackage incomingPackage, string materialIMP)
        {
            InteropHelper.SendMessageToGame($"{InteropCommands.INTEROP_SHOWLOADINGINDICATOR}", Game);
            InteropHelper.SendFileToGame(incomingPackage); // Send package into game for loading
            Task.Run(() =>
            {
                Thread.Sleep(1000);
            }).ContinueWithOnUIThread(x =>
            {
                InteropHelper.SendMessageToGame($"{InteropCommands.INTEROP_LOADPACKAGE} {incomingPackage.FileNameNoExtension}", Game);
            }).ContinueWith(x =>
            {
                Thread.Sleep(1000);
            }).ContinueWithOnUIThread(x =>
            {
                InteropHelper.SendMessageToGame($"{InteropCommands.LME_SET_MATERIAL} {SelectedComponentSlot.SlotIdx} {materialIMP}", Game);
                InteropHelper.SendMessageToGame($"{InteropCommands.INTEROP_HIDELOADINGINDICATOR}", Game);
            });
        }

        private void UpdateScalarParameter(object sender, EventArgs args)
        {
            if (AutoUpdateOnChanges && sender is ScalarParameter obj)
            {
                // Floats sent to game must use localization-specific strings as they will be interpreted by the current locale
                InteropHelper.SendMessageToGame($"{InteropCommands.LME_SET_SCALAR_EXPRESSION} {SelectedComponentSlot.SlotIdx} \"{obj.ParameterName}\" {obj.ParameterValue}", Game);
            }
        }

        private void UpdateVectorParameter(object sender, EventArgs args)
        {
            if (AutoUpdateOnChanges && sender is VectorParameter obj)
            {
                // Floats sent to game must use localization-specific strings as they will be interpreted by the current locale
                InteropHelper.SendMessageToGame($"{InteropCommands.LME_SET_VECTOR_EXPRESSION} {SelectedComponentSlot.SlotIdx} \"{obj.ParameterName}\" {obj.ParameterValue.W} {obj.ParameterValue.X} {obj.ParameterValue.Y} {obj.ParameterValue.Z}", Game);
            }
        }

        private void SetCustomMaterial()
        {
            // Will send message 'COMPONENTMATERIALS'
        }

        /// <summary>
        /// The IFPs of the current selected component.
        /// </summary>
        public ObservableCollectionExtended<JsonMaterialSource> CurrentComponentMaterials { get; } = new();


        private string _selectedMaterial;
        public string SelectedMaterial
        {
            get => _selectedMaterial;
            set
            {
                if (SetProperty(ref _selectedMaterial, value))
                {
                    AutoSetMaterial();
                }
            }
        }

        private void AutoSetMaterial()
        {
            if (!AutoChangeToLoadedMaterial)
                return;

            SetMaterial();
        }


        /// <summary>
        /// Attempts to load the material from the game using the hooked linker filepath capture in the ASI
        /// </summary>
        private void LoadMaterialFromGame()
        {
            if (SelectedComponentSlot == null)
                return;

            ExportEntry preloadMaterial = null;
            if (SelectedComponentSlot.LinkerPath != null)
            {
                void tryFindMaterial(IMEPackage packageToInspect)
                {

                    if (packageToInspect.FindExport(SelectedComponentSlot.MaterialMemoryPath) != null)
                    {
                        preloadMaterial = packageToInspect.FindExport(SelectedComponentSlot.MaterialMemoryPath);
                    }

                    // Try under PersistentLevel.
                    if (preloadMaterial == null &&
                        packageToInspect.FindExport($"TheWorld.PersistentLevel.{SelectedComponentSlot.MaterialMemoryPath}") !=
                        null)
                    {
                        preloadMaterial =
                            packageToInspect.FindExport($"TheWorld.PersistentLevel.{SelectedComponentSlot.MaterialMemoryPath}");
                    }
                }

                if (InteropHelper.GetFilesSentToGame(Game).TryGetValue(SelectedComponentSlot.LinkerPath, out var map) && map.FilePath.CaseInsensitiveEquals(SelectedComponentSlot.LinkerPath))
                {
                    tryFindMaterial(map);
                }

                if (preloadMaterial == null)
                {
                    var destPath = Path.Combine(MEDirectories.GetExecutableFolderPath(Game), SelectedComponentSlot.LinkerPath);
                    if (File.Exists(destPath))
                    {
                        using var package = MEPackageHandler.OpenMEPackage(destPath);
                        tryFindMaterial(package);
                    }

                }

                // Now, we have to find this object somehow...
                //foreach (var f in ActorDict.Keys)
                //{
                //    // Search the loaded level list. That's probably the closest/fastest.
                //    if (MELoadedFiles.GetFilesLoadedInGame(Game).TryGetValue(f, out var path))
                //    {
                //        var package = MEPackageHandler.UnsafePartialLoad(path, x => false);
                //        if (package.FindExport(material) != null)
                //        {
                //            using var autocloseP = MEPackageHandler.OpenMEPackage(path);
                //            preloadMaterial = autocloseP.FindExport(material);
                //            break;
                //        }

                //        // Try under PersistentLevel.
                //        if (package.FindExport($"TheWorld.PersistentLevel.{material}") != null)
                //        {
                //            using var autocloseP = MEPackageHandler.OpenMEPackage(path);
                //            preloadMaterial = autocloseP.FindExport($"TheWorld.PersistentLevel.{material}");
                //            break;
                //        }
                //    }
                //}

                if (preloadMaterial == null)
                {
                    // Guess we keep looking
                    // Is there a way to know what files have loaded in game besides linkerprinter?

                }
            }

            if (preloadMaterial != null)
            {
                // Load the material
                LoadExport(preloadMaterial);
            }
        }

        private void LoadExport(ExportEntry material)
        {
            // Create a new package and port the stuff in.
            EditorPackage = MEPackageHandler.CreateMemoryEmptyPackage(@"LLEMatEd.pcc", Game);
            // Todo: How to force material out of startup only files? e.g. holo material in SFXGame
            EntryExporter.ExportExportToPackage(material, EditorPackage, out var portedMat);
            if (portedMat is ExportEntry exp)
            {
                var package = ExportCreator.CreatePackageExport(exp.FileRef, "LLEMatEd");
                exp.idxLink = package.UIndex; // Move under our custom package

                if (exp.ClassName.CaseInsensitiveEquals("Material"))
                {
                    exp = MEELC.ConvertMaterialToInstance(exp);
                }
                MEELC.LoadExport(exp);
                ShaderEd.LoadExport(MEELC.MatInfo.BaseMaterial);
            }
        }

        private void SetMaterial()
        {
            InteropHelper.SendMessageToGame($"{InteropCommands.LME_SET_MATERIAL} {SelectedComponentSlot.SlotIdx} {SelectedMaterial}", Game);
        }

        private void LoadCommands()
        {
            PreviewOnMeshCommand = new GenericCommand(SendToGame);
            SaveMaterialPackageCommand = new GenericCommand(SaveMaterialPackage, () => MEELC?.MatInfo != null); // ? here as component might not have initialized when this first fires
            SetMaterialCommand = new GenericCommand(SetMaterial, () => SelectedMaterial != null);
            SetCustomMaterialCommand = new GenericCommand(SetCustomMaterial);
            RegenMaterialsListCommand = new GenericCommand(RegenMaterialsList);
        }

        public void SendToGame()
        {
            // Todo: Send default material first, GC, then set material to ensure ours is used.
            // Prepare for shipment
            SerializeMaterialSettingsToPackage();
            var package = MatInfo.MaterialExport.FileRef.SaveToStream(false); // Don't waste time compressing
            package.Position = 0;
            var newP = MEPackageHandler.OpenMEPackageFromStream(package, "LLEMaterialPackage.pcc");


            var matExp = newP.FindExport(MatInfo.MaterialExport.InstancedFullPath);

            // Track imports so we don't break them when we do memory-uniqueness stuff.
            Dictionary<IEntry, List<IEntry>> rootToImportChildrenMap = new();
            foreach (var imp in newP.Imports.Where(x => x.GetRoot() is ExportEntry).ToList())
            {
                if (!rootToImportChildrenMap.TryGetValue(imp.GetRoot(), out var list))
                {
                    list = new List<IEntry>();
                    rootToImportChildrenMap[imp.GetRoot()] = list;
                }

                list.Add(imp);
            }

            // Rename for Memory Uniqueness when loaded into the game
            var matEd = newP.FindExport("LLEMatEd");
            if (matEd == null)
                matEd = ExportCreator.CreatePackageExport(newP, "LLEMatEd");
            matEd.ObjectName = new NameReference(matEd.ObjectName.Name + $"_LLEMATED_{GetRandomString(8)}", matEd.ObjectName.Number);


            foreach (var exp in newP.Exports.Where(x => x.ClassName != "Package").ToList())
            {
                // Do not change these
                if (exp.Parent is { ClassName: "TextureCube" or "Material" })
                    continue;

                // exp.idxLink = matEd.UIndex;
                exp.ObjectName = new NameReference(exp.ObjectName.Name + $"_LLEMATED_{GetRandomString(8)}", exp.ObjectName.Number);

                /*
                // Todo: Do not do this for things that have import children as it will break the imports. For things like physical materials
                var origName = exp.ObjectName;
                exp.ObjectName = new NameReference(exp.ObjectName.Name + $"_LLEMATED_{GetRandomString(8)}", exp.ObjectName.Number);
                if (rootToImportChildrenMap.TryGetValue(exp, out var imps))
                {
                    // Move imports back under original parent
                    var origPackage = ExportCreator.CreatePackageExport(newP, origName);
                    foreach (var imp in imps.Where(x => x.idxLink == exp.UIndex))
                    {
                        imp.idxLink = origPackage.UIndex;
                    }
                }*/
            }

            //PackageEditorWindow pe = new PackageEditorWindow();
            //pe.LoadPackage(newP);
            //pe.Show();
            //return;
            LoadCustomMaterialInGame(newP, matExp.MemoryFullPath);
        }

        public async void SaveMaterialPackage()
        {
            SerializeMaterialSettingsToPackage();

            string extension = ".pcc";
            var fileFilter = $"*{extension}|*{extension}";
            var d = new SaveFileDialog { Filter = fileFilter };
            if (d.ShowDialog() == true)
            {
                //EntryExporter.ExportExportToFile(MatInfo.MaterialExport, d.FileName, out var exp);
                //PackageEditorWindow pw = new PackageEditorWindow();
                //pw.LoadEntry(exp);
                //pw.Show();
                MessageBox.Show("Done");
            }
        }

        /// <summary>
        /// This makes it easier to code against
        /// </summary>
        private MaterialInfo MatInfo => MEELC.MatInfo;

        private void SerializeMaterialSettingsToPackage()
        {
            if (MatInfo.MaterialExport.ClassName == "Material")
            {
                if (MatInfo.Expressions.Any())
                {
                    // We need to convert this to a material instance constant
                    var package = ExportCreator.CreatePackageExport(MEELC.CurrentLoadedExport.FileRef, "LLEMatEd_ChangedTextures");
                    var inst = MEELC.ConvertMaterialToInstance(MatInfo.MaterialExport);
                    MEELC.CommitSettingsToMIC(inst, package);
                    inst.idxLink = package.UIndex; // Move under here
                }
            }

            if (MatInfo.MaterialExport.IsA("MaterialInstanceConstant"))
            {
                var package = ExportCreator.CreatePackageExport(MEELC.CurrentLoadedExport.FileRef, "LLEMatEd_ChangedTextures");
                MEELC.CommitSettingsToMIC(MatInfo.MaterialExport, package);
            }
        }




        /// <summary>
        /// Drag over handler
        /// </summary>
        /// <param name="dropInfo"></param>
        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            if (CanDragDrop(dropInfo, out var exp))
            {
                // dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Copy;
            }
        }

        /// <summary>
        /// Drop handler
        /// </summary>
        /// <param name="dropInfo"></param>
        void IDropTarget.Drop(IDropInfo dropInfo)
        {
            if (CanDragDrop(dropInfo, out var exp))
            {
                LoadExport(exp);
            }
        }

        private bool CanDragDrop(IDropInfo dropInfo, out ExportEntry exp)
        {
            if (dropInfo.Data is TreeViewEntry tve && tve.Parent != null && tve.Entry is ExportEntry texp)
            {
                if (texp.IsA("MaterialInterface"))
                {
                    exp = texp;
                    return true;
                }
            }

            exp = null;
            return false;
        }

        #region Utility
        private static string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private static Random random = new Random();
        private static string GetRandomString(int len)
        {
            var stringChars = new char[len];
            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }
            return new String(stringChars);
        }

        /// <summary>
        /// Converts a texture to a displayable Bitmap
        /// </summary>
        /// <param name="src"></param>
        /// <param name="w"></param>
        /// <param name="h"></param>
        /// <param name="format"></param>
        /// <param name="clearAlpha"></param>
        /// <returns></returns>
        public static Bitmap ConvertRawToBitmapARGB(byte[] src, int w, int h, PixelFormat format, bool clearAlpha = true)
        {
            byte[] tmpData = Image.convertRawToARGB(src, ref w, ref h, format, clearAlpha);
            var bitmap = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            BitmapData bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, bitmap.PixelFormat);
            Marshal.Copy(tmpData, 0, bitmapData.Scan0, tmpData.Length);
            bitmap.UnlockBits(bitmapData);
            return bitmap;
        }
        #endregion

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        private string _busyText;

        public MaterialEditorLLE()
        {
            // Required for use in XAML
        }

        public string BusyText { get => _busyText; set => SetProperty(ref _busyText, value); }

        /// <summary>
        /// Material to immediately load when window opens
        /// </summary>
        public ExportEntry PreloadMaterial { get; set; }

        private void MaterialEditor_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (PreloadMaterial != null)
            {
                LoadExport(PreloadMaterial);
                PreloadMaterial = null;
            }
        }

        public void Dispose()
        {
            GameTarget.GameReceiveMessage -= GameControllerOnReceiveMessage;
            MEELC?.Dispose();
            LLE = null; // lose reference to window
        }


        private Predicate<object> _materialFilter;
        public Predicate<object> MaterialFilter
        {
            get => _materialFilter;
            set
            {
                //this should always trigger, even if the new value is the same
                _materialFilter = value;
                OnPropertyChanged();
            }
        }

        private bool _autoChangeToLoadedMaterial;
        public bool AutoChangeToLoadedMaterial
        {
            get => _autoChangeToLoadedMaterial;
            set => SetProperty(ref _autoChangeToLoadedMaterial, value);
        }

        private JsonMaterialSource _selectedComponentSlot;
        public JsonMaterialSource SelectedComponentSlot
        {
            get => _selectedComponentSlot;
            set
            {
                MEELC?.UnloadExport();
                ShaderEd?.UnloadExport();
                if (SetProperty(ref _selectedComponentSlot, value))
                {
                    if (value != null)
                    {
                        LoadMaterialFromGame();
                    }
                }
            }
        }

        private bool _autoUpdateOnChanges;
        public bool AutoUpdateOnChanges
        {
            get => _autoUpdateOnChanges;
            set => SetProperty(ref _autoUpdateOnChanges, value);
        }

        private bool IsMaterialMatch(object obj)
        {
            var ae = (string)obj;
            string text = materialFilterSearchBox.Text;
            return ae.Contains(text, StringComparison.OrdinalIgnoreCase);
        }

        private void MaterialFilterSearchBox_OnTextChanged(SearchBox sender, string newtext)
        {
            MaterialFilter = string.IsNullOrWhiteSpace(newtext) ? null : IsMaterialMatch;
        }

        /// <summary>
        /// Sets a specific material on the current actor (from AssetDB or Package Editor, most likely)
        /// </summary>
        /// <param name="export"></param>
        public void SetSpecificMaterial(ExportEntry export)
        {
            if (SelectedComponentSlot != null)
            {
                LoadExport(export);
                if (MEELC.CurrentLoadedExport != null)
                {
                    SendToGame();
                }
            }
        }

        /// <summary>
        /// Resets the control
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void GameClosed()
        {
            var autoChange = AutoChangeToLoadedMaterial;
            AutoChangeToLoadedMaterial = false; // Temporarily force off
            LoadedMaterials.ClearEx();
            Components.ClearEx();
            SelectedMaterial = null;
            SelectedComponent = null;
            AutoChangeToLoadedMaterial = autoChange;
        }
    }
}
