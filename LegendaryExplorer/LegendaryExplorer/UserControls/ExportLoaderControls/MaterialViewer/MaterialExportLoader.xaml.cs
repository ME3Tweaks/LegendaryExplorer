using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BCnEncoder.Shared.ImageFiles;
using DocumentFormat.OpenXml.Wordprocessing;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Collections;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.WindowsAPICodePack.Dialogs;
using SharpDX;
using SharpDX.D3DCompiler;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for MaterialExportLoader.xaml
    /// </summary>
    public partial class MaterialExportLoader : ExportLoaderControl, IBusyUIHost
    {
        #region Busy variables
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _busyText;
        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }
        #endregion

        private string _topInfoText;
        public string TopInfoText
        {
            get => _topInfoText;
            set => SetProperty(ref _topInfoText, value);
        }

        public string TopShaderInfoText
        {
            get => (shaderInView != null) ? "LoadedShader GUID: " + shaderInView.Id : "LoadedShader GUID: null";
        }

        // currently loaded tree view shader.
        private TreeViewShader shaderInView = null;

        public ICommand CreateShadersCopyCommand { get; set; }
        public ICommand ReplaceLoadedShaderCommand { get; set; }

        public MaterialExportLoader() : base("Material")
        {
            LoadCommands();
            InitializeComponent();
            DataContext = this;
        }

        public void LoadCommands()
        {
            CreateShadersCopyCommand = new GenericCommand(CreateShadersCopy, CanCreateShadersCopy);
            ReplaceLoadedShaderCommand = new GenericCommand(ReplaceShaderCommand, CanCreateShadersCopy);
        }

        public ObservableCollectionExtended<TreeViewMeshShaderMap> MeshShaderMaps { get; } = new();

        public override bool CanParse(ExportEntry exportEntry) => !exportEntry.IsDefaultObject && exportEntry.Game != MEGame.UDK &&
                                                                  (exportEntry.ClassName == "Material" || exportEntry.IsA("MaterialInstance") &&
                                                                   exportEntry.GetProperty<BoolProperty>("bHasStaticPermutationResource"));

        public override void LoadExport(ExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
            OnDemand_Panel.Visibility = Visibility.Visible;
            LoadedContent_Panel.Visibility = Visibility.Collapsed;
        }

        public IEnumerable<TreeViewMeshShaderMap> GetMeshShaderMaps(MaterialShaderMap msm, ShaderCache shaderCache = null)
        {
            var result = new List<TreeViewMeshShaderMap>();
            foreach (MeshShaderMap meshShaderMap in msm.MeshShaderMaps)
            {
                var tvmsm = new TreeViewMeshShaderMap {VertexFactoryType = meshShaderMap.VertexFactoryType};
                foreach ((NameReference shaderType, ShaderReference shaderReference) in meshShaderMap.Shaders)
                {
                    var tvs = new TreeViewShader
                    {
                        Id = shaderReference.Id,
                        ShaderType = shaderReference.ShaderType,
                        Game = Pcc.Game
                    };
                    if (shaderCache != null && shaderCache.Shaders.TryGetValue(shaderReference.Id, out Shader shader))
                    {
                        tvs.DissasembledShader = ShaderBytecode.FromStream(new MemoryStream(shader.ShaderByteCode)).Disassemble();
                        tvs.Index = shaderCache.Shaders.IndexOf(new KeyValuePair<Guid, Shader>(shaderReference.Id, shader));
                    }
                    tvmsm.Shaders.Add(tvs);
                }
                result.Add(tvmsm);
            }
            return result;
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
            MeshShaderMaps.ClearEx();
            shaderDissasemblyTextBlock.Text = "";
            TopInfoText = "";
            TopShaderInfoTextBlock.Text = "";
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                var elhw = new ExportLoaderHostedWindow(new MaterialExportLoader(), CurrentLoadedExport)
                {
                    Title = $"Material Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
        }

        private void MeshShaderMaps_TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewShader tvs)
            {
                MeshShaderMaps_TreeView_Update(tvs);
            }
        }

        private void MeshShaderMaps_TreeView_Update(TreeViewShader tvs)
        {
            shaderInView = tvs; // Set tvs as currently loaded shader.
            TopShaderInfoTextBlock.Text = TopShaderInfoText; // Set text
            shaderDissasemblyTextBlock.Text = tvs.DissasembledShader;
        }

        private void LoadShaders_Button_Click(object sender, RoutedEventArgs e)
        {
            LoadShaders();
        }

        private void LoadShaders()
        {
            IsBusy = true;
            BusyText = "Loading Shaders";
            Task.Run(() =>
            {
                StaticParameterSet sps = CurrentLoadedExport.ClassName switch
                {
                    "Material" => (StaticParameterSet)ObjectBinary.From<Material>(CurrentLoadedExport).SM3MaterialResource.ID,
                    _ => ObjectBinary.From<MaterialInstance>(CurrentLoadedExport).SM3StaticParameterSet
                };
                try
                {
                    if (Pcc.Exports.FirstOrDefault(exp => exp.ClassName == "ShaderCache") is { } seekFreeShaderCacheExport)
                    {
                        var seekFreeShaderCache = ObjectBinary.From<ShaderCache>(seekFreeShaderCacheExport);
                        if (seekFreeShaderCache.MaterialShaderMaps.TryGetValue(sps, out MaterialShaderMap msm))
                        {
                            string topInfoText = $"Shaders in #{seekFreeShaderCacheExport.UIndex} SeekFreeShaderCache (Index {seekFreeShaderCache.MaterialShaderMaps.IndexOf(new(sps, msm))})";
                            return (GetMeshShaderMaps(msm, seekFreeShaderCache), topInfoText);
                        }
                    }

                    if (!RefShaderCacheReader.IsShaderOffsetsDictInitialized(Pcc.Game))
                    {
                        BusyText = "Calculating Shader offsets\n(May take ~15s)";
                    }
                    MaterialShaderMap msmFromGlobalCache = RefShaderCacheReader.GetMaterialShaderMap(Pcc.Game, sps, out int fileOffset);
                    if (msmFromGlobalCache != null && CurrentLoadedExport is not null)
                    {
                        var topInfoText = $"Shaders in {RefShaderCacheReader.GlobalShaderFileName(Pcc.Game)} at 0x{fileOffset:X8}";
                        return (GetMeshShaderMaps(msmFromGlobalCache), topInfoText);
                    }
                }
                catch (Exception)
                {
                    //
                }

                return (null, "MaterialShaderMap not found!");
            }).ContinueWithOnUIThread(prevTask =>
            {
                MeshShaderMaps.ClearEx();
                (IEnumerable<TreeViewMeshShaderMap> treeviewItems, string topInfoText) = prevTask.Result;
                TopInfoText = topInfoText;
                if (treeviewItems != null && CurrentLoadedExport != null)
                {
                    MeshShaderMaps.AddRange(treeviewItems);
                }
                OnDemand_Panel.Visibility = Visibility.Collapsed;
                LoadedContent_Panel.Visibility = Visibility.Visible;
                IsBusy = false;
            });
        }

        private bool CanCreateShadersCopy() => CurrentLoadedExport?.ClassName == "Material" && !IsBusy && LoadedContent_Panel.Visibility == Visibility.Visible;

        private void CreateShadersCopy()
        {
            IsBusy = true;
            BusyText = "Copying Shaders";
            Task.Run(() =>
            {
                StaticParameterSet sps = CurrentLoadedExport.ClassName switch
                {
                    "Material" => (StaticParameterSet)ObjectBinary.From<Material>(CurrentLoadedExport).SM3MaterialResource.ID,
                    _ => throw new NotImplementedException("MaterialInstance shader cloning has not been implemented yet")
                };
                ShaderCache seekFreeShaderCache;
                Guid newMatGuid;
                if (Pcc.Exports.FirstOrDefault(exp => exp.ClassName == "ShaderCache") is { } seekFreeShaderCacheExport)
                {
                    seekFreeShaderCache = ObjectBinary.From<ShaderCache>(seekFreeShaderCacheExport);
                    if (seekFreeShaderCache.MaterialShaderMaps.TryGetValue(sps, out MaterialShaderMap msm))
                    {
                        Dictionary<Guid, Guid> shaderGuidMap = msm.DeepCopyWithNewGuidsInto(seekFreeShaderCache, out newMatGuid);
                        foreach ((Guid oldGuid, Guid newGuid) in shaderGuidMap)
                        {
                            if (!seekFreeShaderCache.Shaders.TryGetValue(oldGuid, out Shader oldShader))
                            {
                                throw new Exception($"Shader {oldGuid} not found!");
                            }
                            Shader newShader = oldShader.Clone();
                            newShader.Guid = newGuid;
                            seekFreeShaderCache.Shaders.Add(newGuid, newShader);
                        }
                        seekFreeShaderCacheExport.WriteBinary(seekFreeShaderCache);
                        return newMatGuid;
                    }
                }
                else
                {
                    seekFreeShaderCacheExport = new ExportEntry(Pcc, 0, "SeekFreeShaderCache", BitConverter.GetBytes(-1), binary: ShaderCache.Create())
                    {
                        Class = Pcc.GetEntryOrAddImport("Engine.ShaderCache", "Class"),
                        ObjectFlags = UnrealFlags.EObjectFlags.LoadForClient | UnrealFlags.EObjectFlags.LoadForEdit | UnrealFlags.EObjectFlags.LoadForServer | UnrealFlags.EObjectFlags.Standalone
                    };
                    Pcc.AddExport(seekFreeShaderCacheExport);
                    seekFreeShaderCache = ObjectBinary.From<ShaderCache>(seekFreeShaderCacheExport);
                }

                if (!RefShaderCacheReader.IsShaderOffsetsDictInitialized(Pcc.Game))
                {
                    BusyText = "Calculating Shader offsets\n(May take ~15s)";
                }
                MaterialShaderMap msmFromGlobalCache = RefShaderCacheReader.GetMaterialShaderMap(Pcc.Game, sps, out _);
                if (msmFromGlobalCache != null && CurrentLoadedExport is not null)
                {
                    Dictionary<Guid, Guid> shaderGuidMap = msmFromGlobalCache.DeepCopyWithNewGuidsInto(seekFreeShaderCache, out newMatGuid);
                    Shader[] shaders = RefShaderCacheReader.GetShaders(Pcc.Game, shaderGuidMap.Keys, 
                        out UMultiMap<NameReference, uint> shaderTypeCRCMap, out UMultiMap<NameReference, uint> vertexFactoryTypeCRCMap);
                    if (shaders is null)
                    {
                        throw new Exception("Unable to retrieve shaders from RefShaderCache");
                    }
                    foreach (Shader oldShader in shaders)
                    {
                        Shader newShader = oldShader.Clone();
                        newShader.Guid = shaderGuidMap[oldShader.Guid];
                        seekFreeShaderCache.Shaders.Add(newShader.Guid, newShader);
                    }
                    foreach ((NameReference key, uint value) in shaderTypeCRCMap)
                    {
                        seekFreeShaderCache.ShaderTypeCRCMap.TryAddUnique(key, value);
                    }
                    foreach ((NameReference key, uint value) in vertexFactoryTypeCRCMap)
                    {
                        seekFreeShaderCache.VertexFactoryTypeCRCMap.TryAddUnique(key, value);
                    }
                    seekFreeShaderCacheExport.WriteBinary(seekFreeShaderCache);
                    return newMatGuid;
                }
                throw new Exception("Material Shader Map has dissapeared!");
            }).ContinueWithOnUIThread(prevTask =>
            {
                if (prevTask.Exception is AggregateException aggregateException)
                {
                    new ExceptionHandlerDialog(aggregateException).ShowDialog();
                    IsBusy = false;
                    return;
                }
                Guid newMatGuid = prevTask.Result;
                if (CurrentLoadedExport.ClassName is "Material")
                {
                    var matBin = ObjectBinary.From<Material>(CurrentLoadedExport);
                    matBin.SM3MaterialResource.ID = newMatGuid;
                    CurrentLoadedExport.WriteBinary(matBin);
                }
                else
                {
                    throw new NotImplementedException("MaterialInstance shader cloning has not been implemented yet");
                }
                LoadShaders();
                MessageBox.Show(Window.GetWindow(this), "This material now has its own unique shaders in the local SeekFreeShaderCache. " +
                                                        "Porting this material to another package will bring the shaders along to that package's shader cache.\n\n" +
                                                        "You should change this material's name, so it will not conflict with other instances that use its original shaders.");
            });
        }


        private void ReplaceShaderCommand()
        {
            IsBusy = true;
            BusyText = "Replacing Shader";
            Shader foundShader;
            var seekFreeShaderCacheExport = Pcc.FindExport("SeekFreeShaderCache");
            ShaderCache seekFreeShaderCache;
            byte[] selectedFile = new byte[0];

            if (seekFreeShaderCacheExport is null)
            {
                throw new Exception("Cant find shader cache.");
            }
            else
            {
                seekFreeShaderCache = ObjectBinary.From<ShaderCache>(seekFreeShaderCacheExport);
            }

            var dlg = new CommonOpenFileDialog
            {
                DefaultExtension = ".bin",
                EnsurePathExists = true,
                Title = "Select shader BIN file."
            };
            dlg.Filters.Add(new CommonFileDialogFilter("BIN files", "*.bin"));

            if(dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                selectedFile = ShaderBytecode.FromFile(dlg.FileName);
            }

            Task.Run(() =>
            {
                if (selectedFile.Length == 0)
                {
                    throw new Exception("You need to select a shader binary file.");
                }

                if (shaderInView != null)
                {
                    if (seekFreeShaderCache.Shaders.ContainsKey(shaderInView.Id))
                    {
                        foundShader = seekFreeShaderCache.Shaders[shaderInView.Id];
                    }
                    else
                    {
                        throw new Exception("Shader ID not found in the cache.");
                    }

                    return shaderInView.Id;
                }
                else
                {
                    throw new Exception("No shader selected!");
                }

            }).ContinueWithOnUIThread(prevTask =>
            {
                if (prevTask.Exception is AggregateException aggregateException)
                {
                    new ExceptionHandlerDialog(aggregateException).ShowDialog();
                    IsBusy = false;
                    return;
                }
                if (seekFreeShaderCache.Shaders.ContainsKey(shaderInView.Id))
                {
                    foundShader = seekFreeShaderCache.Shaders[shaderInView.Id];

                    // Insert new bytecode
                    foundShader.Replace(selectedFile);

                    // Get Disassembly
                    string dissasembledShader = ShaderBytecode.FromStream(new MemoryStream(selectedFile)).Disassemble();
                    // Get last line that contains instruction counts
                    string result = string.Join("", dissasembledShader.Split('\n').Reverse().Take(2).ToArray());
                    // Get digits from the result
                    string digits = string.Join("", new String(result.Where(Char.IsDigit).ToArray()));
                    int instructions = int.Parse(digits);

                    // Insert new instruction count
                    foundShader.InstructionCount = instructions;

                    // Update the cache
                    seekFreeShaderCacheExport.WriteBinary(seekFreeShaderCache);
                }
                else
                {
                    throw new NotImplementedException("Failed to replace shader.");
                }
                // Update shaders.
                LoadShaders();
                // Update text box.
                MeshShaderMaps_TreeView_Update(new TreeViewShader { Id = foundShader.Guid, ShaderType = foundShader.ShaderType, Game = Pcc.Game });
                MessageBox.Show(Window.GetWindow(this), "Shader " + seekFreeShaderCache.Shaders[shaderInView.Id].Guid + " has been replaced");
            });
        }
    }

    public class TreeViewMeshShaderMap
    {
        public string VertexFactoryType { get; set; }
        public ObservableCollectionExtended<TreeViewShader> Shaders { get; } = new();
}

    public class TreeViewShader
    {
        public MEGame Game;
        public Guid Id { get; set; }
        public string Description => $"{ShaderType} ({Index})";
        public string ShaderType { get; set; }
        public int Index { get; set; }

        private string dissasembledShader;
        public string DissasembledShader
        {
            get
            {
                if (dissasembledShader is null)
                {
                    if (RefShaderCacheReader.GetShaderBytecode(Game, Id) is byte[] bytecode)
                    {
                        return dissasembledShader = ShaderBytecode.FromStream(new MemoryStream(bytecode)).Disassemble();
                    }
                    dissasembledShader = "";
                }
                return dissasembledShader;
            }
            set => dissasembledShader = value;
        }
    }
}
