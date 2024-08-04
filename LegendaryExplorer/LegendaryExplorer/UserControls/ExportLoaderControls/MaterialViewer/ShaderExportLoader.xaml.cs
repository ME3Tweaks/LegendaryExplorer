using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Coalesced;
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
using Newtonsoft.Json;
using SharpDX.D3DCompiler;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for ShaderExportLoader.xaml
    /// </summary>
    public partial class ShaderExportLoader : FileExportLoaderControl, IBusyUIHost
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
        public ICommand ExportAllShadersCommand { get; set; }
        public ICommand ImportAllShadersCommand { get; set; }
        public ICommand SearchForShaderCommand { get; set; }

        public ShaderExportLoader() : base("ShaderViewer")
        {
            LoadCommands();
            InitializeComponent();
            DataContext = this;
        }

        public void LoadCommands()
        {
            CreateShadersCopyCommand = new GenericCommand(CreateShadersCopy, CanCreateShadersCopy);
            ReplaceLoadedShaderCommand = new GenericCommand(ReplaceShader, CanCreateShadersCopy);
            ExportAllShadersCommand = new GenericCommand(ExportAllShaders, CanCreateShadersCopy);
            ImportAllShadersCommand = new GenericCommand(ReplaceAllShaders, CanCreateShadersCopy);
            SearchForShaderCommand = new GenericCommand(SearchForShader, ShadersAreLoaded);
        }

        private bool ShadersAreLoaded()
        {
            return MeshShaderMaps.Any();
        }

        private void SearchForShader()
        {
            //if (MeshShaderMaps_TreeView.SelectedItem is TreeViewShader tvs)
            //{
            //    var shaderHash = HLSLDecompiler.DecompileShader(tvs.Bytecode, false).Trim().HashCrc32();
            //    foreach (var msm in MeshShaderMaps)
            //    {
            //        foreach (var shader in msm.Shaders)
            //        {
            //            var testShaderText = HLSLDecompiler.DecompileShader(shader.Bytecode, false).Trim();
            //            var testHash = testShaderText.HashCrc32();
            //            if (shaderHash == testHash)
            //            {
            //                // Found !!
            //                // god damnit its a treeview!!

            //                MessageBox.Show($"Found shader: Index {shader.Index}");
            //                return;
            //            }
            //        }
            //    }
            //}

            // return;
            var shaderText = PromptDialog.Prompt(this, "Paste your unmodified decompiled shader from renderdoc here to search for this shader.", "Paste shader", inputType: PromptDialog.InputType.Multiline);
            if (string.IsNullOrWhiteSpace(shaderText))
                return;


            if (shaderText.StartsWith("// ---- Created with "))
            {
                shaderText = shaderText.Substring(shaderText.IndexOf('\n')); // Remove /r
            }

            // Remove carriage return
            shaderText = shaderText.Replace("\r", "");
            shaderText = shaderText.Trim();

            foreach (var msm in MeshShaderMaps)
            {
                foreach (var shader in msm.Shaders)
                {
                    var testShaderText = HLSLDecompiler.DecompileShader(shader.Bytecode, false).Trim();
                    if (shaderText.Length == testShaderText.Length)
                    {
                        // Found !!
                        // god damnit its a treeview!!

                        MessageBox.Show($"Found shader: Index {shader.Index}");
                        return;
                    }

                    Debug.WriteLine($"{shaderText.Split('\n').Length} vs {testShaderText.Split('\n').Length} {shader.ShaderType}");
                }
            }

            var crc = shaderText.HashCrc32();
            var refHashes = JsonConvert.DeserializeObject<Dictionary<uint, Guid>>(File.ReadAllText(Path.Combine(AppDirectories.ExecFolder, "LE3RefShaderHashes.json")));
            if (refHashes.TryGetValue(crc, out var guid))
            {
                MessageBox.Show($"Found shader in ref cache. Guid: {guid}");
                return;
            }

            //var refCacheF = Path.Combine(LE3Directory.CookedPCPath, "RefShaderCache-PC-D3D-SM5.upk");
            //var refCacheP = MEPackageHandler.OpenMEPackage(refCacheF);
            //var refCache = ObjectBinary.From<ShaderCache>(refCacheP.Exports.First());
            //Dictionary<uint, Guid> shaderHashToGuid = new Dictionary<uint, Guid>();
            //foreach (var s in refCache.Shaders)
            //{
            //    var testShaderText = HLSLDecompiler.DecompileShader(s.Value.ShaderByteCode, false).Trim();
            //    var hash = testShaderText.HashCrc32();
            //    shaderHashToGuid[hash] = s.Key;
            //    //if (shaderText == testShaderText)
            //    //{
            //    //    MessageBox.Show($"Found shader in ref cache. {s.Value.ShaderType} {s.Key}");
            //    //    return;
            //    //}
            //}

            //var shaderMap = JsonConvert.SerializeObject(shaderHashToGuid);
            //File.WriteAllText(@"C:\users\public\shaderMap.json", shaderMap);

            MessageBox.Show($"No shader found with matching decompilation.");
        }

        public ObservableCollectionExtended<TreeViewMeshShaderMap> MeshShaderMaps { get; } = new();

        public override bool CanParse(ExportEntry exportEntry) =>
            !exportEntry.IsDefaultObject && exportEntry.Game != MEGame.UDK &&
            (exportEntry.ClassName == "Material" || exportEntry.IsA("MaterialInstance") &&
                exportEntry.GetProperty<BoolProperty>("bHasStaticPermutationResource"));

        public override void LoadExport(ExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
            OnDemand_Panel.Visibility = Visibility.Visible;
            LoadedContent_Panel.Visibility = Visibility.Collapsed;
        }

        public IEnumerable<TreeViewMeshShaderMap> GetMeshShaderMaps(MaterialShaderMap msm,
            ShaderCache shaderCache = null)
        {
            var result = new List<TreeViewMeshShaderMap>();
            foreach (MeshShaderMap meshShaderMap in msm.MeshShaderMaps)
            {
                var tvmsm = new TreeViewMeshShaderMap { VertexFactoryType = meshShaderMap.VertexFactoryType };
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
                        // Cache bytecode and index
                        tvs.Bytecode = shader.ShaderByteCode;
                        tvs.Index = shaderCache.Shaders.IndexOf(
                            new KeyValuePair<Guid, Shader>(shaderReference.Id, shader));
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
                var elhw = new ExportLoaderHostedWindow(new ShaderExportLoader() { AutoLoad = true }, CurrentLoadedExport)
                {
                    Title = $"Shader Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
        }

        private void MeshShaderMaps_TreeView_OnSelectedItemChanged(object sender,
            RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewShader tvs)
            {
                shaderDissasemblyTextBlock.Text = tvs.DissassembledShader;
            }
        }

        private void MeshShaderMaps_TreeView_Update(TreeViewShader tvs)
        {
            shaderInView = tvs; // Set tvs as currently loaded shader.
            TopShaderInfoTextBlock.Text = TopShaderInfoText; // Set text
            shaderDissasemblyTextBlock.Text = tvs.DissassembledShader;
        }

        private void LoadShaders_Button_Click(object sender, RoutedEventArgs e)
        {
            LoadShaders();
        }

        private void LoadShaders()
        {
            if (GlobalShaderCache != null)
            {
                LoadGlobalShaders();
            }
            else
            {
                LoadPackageShaders();
            }
        }

        private void LoadGlobalShaders()
        {
            MeshShaderMaps.ClearEx();
            var root = new TreeViewMeshShaderMap() { VertexFactoryType = "Global Shaders" };
            MeshShaderMaps.Add(root);
            int i = 0;
            foreach (var shader in GlobalShaderCache.Shaders)
            {
                var tve = new TreeViewShader()
                {
                    Bytecode = shader.Value.ShaderByteCode,
                    Index = i,
                    Game = MEGame.LE3,
                    Id = shader.Key,
                    ShaderType = shader.Value.ShaderType,
                };
                root.Shaders.Add(tve);
                i++;
            }
        }

        /// <summary>
        /// Loads shaders that are referenced and stored in package file format
        /// </summary>
        private void LoadPackageShaders()
        {
            IsBusy = true;
            BusyText = "Loading Shaders";
            Task.Run(() =>
            {
                StaticParameterSet sps = CurrentLoadedExport.ClassName switch
                {
                    "Material" => (StaticParameterSet)ObjectBinary.From<Material>(CurrentLoadedExport)
                        .SM3MaterialResource.ID,
                    _ => ObjectBinary.From<MaterialInstance>(CurrentLoadedExport).SM3StaticParameterSet
                };
                try
                {
                    if (Pcc.Exports.FirstOrDefault(exp => exp.ClassName == "ShaderCache") is
                        { } seekFreeShaderCacheExport)
                    {
                        var seekFreeShaderCache = ObjectBinary.From<ShaderCache>(seekFreeShaderCacheExport);
                        if (seekFreeShaderCache.MaterialShaderMaps.TryGetValue(sps, out MaterialShaderMap msm))
                        {
                            string topInfoText =
                                $"Shaders in #{seekFreeShaderCacheExport.UIndex} SeekFreeShaderCache (Index {seekFreeShaderCache.MaterialShaderMaps.IndexOf(new(sps, msm))})";
                            return (GetMeshShaderMaps(msm, seekFreeShaderCache), topInfoText);
                        }
                    }

                    if (!RefShaderCacheReader.IsShaderOffsetsDictInitialized(Pcc.Game))
                    {
                        BusyText = "Calculating Shader offsets\n(May take ~15s)";
                    }

                    MaterialShaderMap msmFromGlobalCache =
                        RefShaderCacheReader.GetMaterialShaderMap(Pcc.Game, sps, out int fileOffset);
                    if (msmFromGlobalCache != null && CurrentLoadedExport is not null)
                    {
                        var topInfoText =
                            $"Shaders in {RefShaderCacheReader.GlobalShaderFileName(Pcc.Game)} at 0x{fileOffset:X8}";
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

        private bool CanCreateShadersCopy() => CurrentLoadedExport?.ClassName == "Material" && !IsBusy &&
                                               LoadedContent_Panel.Visibility == Visibility.Visible;

        private void CreateShadersCopy()
        {
            IsBusy = true;
            BusyText = "Copying Shaders";
            Task.Run(() =>
            {
                StaticParameterSet sps = CurrentLoadedExport.ClassName switch
                {
                    "Material" => (StaticParameterSet)ObjectBinary.From<Material>(CurrentLoadedExport)
                        .SM3MaterialResource.ID,
                    _ => throw new NotImplementedException(
                        "MaterialInstance shader cloning has not been implemented yet")
                };
                ShaderCache seekFreeShaderCache;
                Guid newMatGuid;
                if (Pcc.Exports.FirstOrDefault(exp => exp.ClassName == "ShaderCache") is { } seekFreeShaderCacheExport)
                {
                    seekFreeShaderCache = ObjectBinary.From<ShaderCache>(seekFreeShaderCacheExport);
                    if (seekFreeShaderCache.MaterialShaderMaps.TryGetValue(sps, out MaterialShaderMap msm))
                    {
                        Dictionary<Guid, Guid> shaderGuidMap =
                            msm.DeepCopyWithNewGuidsInto(seekFreeShaderCache, out newMatGuid);
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
                    seekFreeShaderCacheExport = new ExportEntry(Pcc, 0, "SeekFreeShaderCache",
                        BitConverter.GetBytes(-1), binary: ShaderCache.Create())
                    {
                        Class = Pcc.GetEntryOrAddImport("Engine.ShaderCache", "Class"),
                        ObjectFlags = UnrealFlags.EObjectFlags.LoadForClient | UnrealFlags.EObjectFlags.LoadForEdit |
                                      UnrealFlags.EObjectFlags.LoadForServer | UnrealFlags.EObjectFlags.Standalone
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
                    Dictionary<Guid, Guid> shaderGuidMap =
                        msmFromGlobalCache.DeepCopyWithNewGuidsInto(seekFreeShaderCache, out newMatGuid);
                    Shader[] shaders = RefShaderCacheReader.GetShaders(Pcc.Game, shaderGuidMap.Keys,
                        out UMultiMap<NameReference, uint> shaderTypeCRCMap,
                        out UMultiMap<NameReference, uint> vertexFactoryTypeCRCMap);
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
                MessageBox.Show(Window.GetWindow(this),
                    "This material now has its own unique shaders in the local SeekFreeShaderCache. " +
                    "Porting this material to another package will bring the shaders along to that package's shader cache.\n\n" +
                    "You should change this material's name, so it will not conflict with other instances that use its original shaders.");
            });
        }


        private void ReplaceShader()
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
                DefaultExtension = ".fxc",
                EnsurePathExists = true,
                Title = "Select shader FXC file."
            };
            dlg.Filters.Add(new CommonFileDialogFilter("FXC files", "*.fxc"));

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
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
                    IsBusy = false;
                    throw new NotImplementedException("Failed to replace shader.");
                }
                // Update shaders.
                LoadShaders();
                // Update text box.
                MeshShaderMaps_TreeView_Update(new TreeViewShader { Id = foundShader.Guid, ShaderType = foundShader.ShaderType, Game = Pcc.Game });
                MessageBox.Show(Window.GetWindow(this), "Shader " + seekFreeShaderCache.Shaders[shaderInView.Id].Guid + " has been replaced");
            });
        }


        private void ExportAllShaders()
        {
            IsBusy = true;
            BusyText = "Exporting Shaders";
            var seekFreeShaderCacheExport = Pcc.FindExport("SeekFreeShaderCache");
            ShaderCache seekFreeShaderCache;

            if (seekFreeShaderCacheExport is null)
            {
                throw new Exception("Cant find shader cache.");
            }
            else
            {
                seekFreeShaderCache = ObjectBinary.From<ShaderCache>(seekFreeShaderCacheExport);
            }

            var dlg = new CommonOpenFileDialog("Select folder")
            {
                IsFolderPicker = true
            };
            var dialogResult = dlg.ShowDialog();

            Task.Run(() =>
            {
                if (MeshShaderMaps == null)
                {
                    throw new Exception("Something is wrong, mesh shader maps are not loaded?");
                }
                if (dialogResult != CommonFileDialogResult.Ok)
                {
                    throw new DirectoryNotFoundException("Selection cancelled");
                }
            }).ContinueWithOnUIThread(prevTask =>
            {
                if (prevTask.Exception is AggregateException aggregateException)
                {
                    new ExceptionHandlerDialog(aggregateException).ShowDialog();
                    IsBusy = false;
                    return;
                }

                string selectedPath = dlg.FileName;
                int notFoundShaders = 0;

                // Export all shaders to the selected directory.
                foreach (TreeViewMeshShaderMap TVshaderMap in MeshShaderMaps)
                {
                    string factoryType = TVshaderMap.VertexFactoryType;

                    foreach (TreeViewShader TVshader in TVshaderMap.Shaders)
                    {
                        byte[] shaderFile = new byte[0];
                        string shaderType = "";
                        string fileName = "";
                        int shaderIndex = TVshader.Index;

                        if (seekFreeShaderCache.Shaders.ContainsKey(TVshader.Id))
                        {
                            shaderType = seekFreeShaderCache.Shaders[TVshader.Id].ShaderType;
                            shaderFile = seekFreeShaderCache.Shaders[TVshader.Id].ShaderByteCode;
                        }
                        else
                        {
                            notFoundShaders++;
                            continue;
                        }

                        fileName = shaderIndex + "_" + factoryType + "_" + shaderType;

                        string fullPath = Path.Combine(selectedPath, fileName);
                        fullPath = fullPath.Replace("<", "[").Replace(">", "]"); // fix shader names <> into [] so they can be saved as files
                        fullPath = Path.ChangeExtension(fullPath, "fxc");
                        File.WriteAllBytes(fullPath, shaderFile);
                    }
                }

                MessageBox.Show(Window.GetWindow(this), "All selected shaders have been exported.");
                IsBusy = false;
            });
        }


        private void ReplaceAllShaders()
        {
            IsBusy = true;
            BusyText = "Importing Shaders";
            var seekFreeShaderCacheExport = Pcc.FindExport("SeekFreeShaderCache");
            ShaderCache seekFreeShaderCache;

            if (seekFreeShaderCacheExport is null)
            {
                throw new Exception("Cant find shader cache.");
            }
            else
            {
                seekFreeShaderCache = ObjectBinary.From<ShaderCache>(seekFreeShaderCacheExport);
            }

            var dlg = new CommonOpenFileDialog("Select folder")
            {
                IsFolderPicker = true
            };
            var dialogResult = dlg.ShowDialog();

            Task.Run(() =>
            {
                if (MeshShaderMaps == null)
                {
                    throw new Exception("Something is wrong, mesh shader maps are not loaded?");
                }
                if (dialogResult != CommonFileDialogResult.Ok)
                {
                    throw new DirectoryNotFoundException("Selection cancelled");
                }
            }).ContinueWithOnUIThread(prevTask =>
            {
                if (prevTask.Exception is AggregateException aggregateException)
                {
                    new ExceptionHandlerDialog(aggregateException).ShowDialog();
                    IsBusy = false;
                    return;
                }

                string selectedPath = dlg.FileName;
                int notFoundShaders = 0;

                // Import all shader files with a suffix_edited to the selected shader.
                foreach (TreeViewMeshShaderMap TVshaderMap in MeshShaderMaps)
                {
                    string factoryType = TVshaderMap.VertexFactoryType;

                    foreach (TreeViewShader TVshader in TVshaderMap.Shaders)
                    {
                        byte[] newShaderFile = new byte[0];
                        string shaderType = "";
                        string fileName = "";
                        int shaderIndex = TVshader.Index;
                        Shader shader = null;

                        if (seekFreeShaderCache.Shaders.ContainsKey(TVshader.Id))
                        {
                            shader = seekFreeShaderCache.Shaders[TVshader.Id];
                            shaderType = shader.ShaderType;
                        }
                        else
                        {
                            notFoundShaders++;
                            continue;
                        }

                        fileName = shaderIndex + "_" + factoryType + "_" + shaderType + "_edited";

                        string fullPath = Path.Combine(selectedPath, fileName);
                        fullPath = fullPath.Replace("<", "[").Replace(">", "]");
                        fullPath = Path.ChangeExtension(fullPath, "fxc");

                        if (File.Exists(fullPath) && shader != null)
                        {
                            newShaderFile = ShaderBytecode.FromFile(fullPath);

                            // Insert new bytecode
                            shader.Replace(newShaderFile);

                            // Get Disassembly
                            string dissasembledShader = ShaderBytecode.FromStream(new MemoryStream(newShaderFile)).Disassemble();
                            // Get last line that contains instruction counts
                            string result = string.Join("", dissasembledShader.Split('\n').Reverse().Take(2).ToArray());
                            // Get digits from the result
                            string digits = string.Join("", new String(result.Where(Char.IsDigit).ToArray()));
                            int instructions = int.Parse(digits);

                            // Insert new instruction count
                            shader.InstructionCount = instructions;
                        }
                    }
                }

                // Update the cache
                seekFreeShaderCacheExport.WriteBinary(seekFreeShaderCache);
                // Update shaders.
                LoadShaders();
                // Inform.
                MessageBox.Show(Window.GetWindow(this), "All selected shaders have been imported.");
                IsBusy = false;
            });
        }

        public override void LoadFile(string filepath)
        {
            var fs = File.OpenRead(filepath);
            var magic = fs.ReadStringASCII(4);
            if (magic != "BMSG")
            {
                MessageBox.Show("This is not a global shader cache file.");
                return;
            }

            fs.Position = 0;
            GlobalShaderCache = ShaderCache.ReadGlobalShaderCache(fs, MEGame.LE3); // Todo: Determine this. Might not need to as it is in the header, technically.
            LoadedFile = filepath;
            LoadShaders();
        }

        /// <summary>
        /// Loaded global shader cache
        /// </summary>
        public ShaderCache GlobalShaderCache { get; set; }

        public override bool CanLoadFile()
        {
            return true;
        }

        public override void Save()
        {
            // Not implemented
        }

        public override void SaveAs()
        {
            // Not implemented
        }

        internal override void OpenFile()
        {
            // Not implemented
        }

        public override bool CanSave()
        {
            return false;
        }

        public override string Toolname => "Shader viewer";
        internal override bool CanLoadFileExtension(string extension)
        {
            if (extension == ".bin")
                return true;
            return false;
        }

        private bool ControlLoaded;
        /// <summary>
        /// If shader should automatically be loaded when the control becomes loaded
        /// </summary>
        public bool AutoLoad { get; set; }
        private void ShaderExportLoader_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!ControlLoaded)
            {
                ControlLoaded = true;
                if (AutoLoad)
                {
                    LoadShaders();
                    AutoLoad = false;
                }
            }
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

        /// <summary>
        /// Cached loaded bytecode
        /// </summary>
        public byte[] Bytecode { get; set; }

        private string dissasembledShader;

        public string DissassembledShader
        {
            get
            {
                if (dissasembledShader is null)
                {
                    if (Bytecode == null && RefShaderCacheReader.GetShaderBytecode(Game, Id) is byte[] bytecode)
                    {
                        Bytecode = bytecode;
                    }

                    if (Bytecode == null)
                        return "Shader data not found";

                    if (Game.IsLEGame())
                    {
                        return dissasembledShader = HLSLDecompiler.DecompileShader(Bytecode, true);
                    }
                    else
                    {
                        // OT
                        return dissasembledShader = ShaderBytecode.FromStream(new MemoryStream(Bytecode)).Disassemble();
                    }
                }

                return dissasembledShader;
            }

            set => dissasembledShader = value;
        }
    }
}