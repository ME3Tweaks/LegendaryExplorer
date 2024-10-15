using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Search;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Resources;
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
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
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
            get => (SelectedTreeViewShader != null) ? "LoadedShader GUID: " + SelectedTreeViewShader.Id : "LoadedShader GUID: null";
        }

        // currently loaded tree view shader.
        private TreeViewShader SelectedTreeViewShader => MeshShaderMaps_TreeView?.SelectedItem as TreeViewShader;

        private TextDocument _document;
        public TextDocument Document
        {
            get => _document;
            set => SetProperty(ref _document, value);
        }

        private string ShaderText
        {
            get => Document?.Text ?? "";
            set => Dispatcher.Invoke(() =>
            {
                Document = new TextDocument(value);
            });
        }

        public ICommand CreateShadersCopyCommand { get; set; }
        public ICommand ReplaceShaderCommand { get; set; }
        public ICommand ExportShaderMapCommand { get; set; }
        public ICommand SearchForShaderCommand { get; set; }
        public ICommand CompileShaderCommand { get; set; }


        // Scheduled to move out
        public ICommand ReplaceLoadedShaderCommand { get; set; }
        public ICommand ExportAllShadersCommand { get; set; }
        public ICommand ImportAllShadersCommand { get; set; }

        public ShaderExportLoader() : base("ShaderViewer")
        {
            LoadCommands();
            InitializeComponent();
            DataContext = this;
        }

        public void LoadCommands()
        {
            CreateShadersCopyCommand = new GenericCommand(CreateShadersCopy, CanCreateShadersCopy);
            SearchForShaderCommand = new GenericCommand(SearchForShader, ShadersAreLoaded);
            ExportShaderMapCommand = new GenericCommand(ExportMapShaders, ShadersAreLoaded);
            ReplaceShaderCommand = new GenericCommand(ReplaceShader, CanCreateShadersCopy);
            CompileShaderCommand = new GenericCommand(CompileShader, ShaderIsSelected);

            // Scheduled to move out
            ExportAllShadersCommand = new GenericCommand(ExportAllShaders, CanCreateShadersCopy);
            ImportAllShadersCommand = new GenericCommand(ReplaceAllShaders, CanCreateShadersCopy);
        }

        private void CompileShader()
        {
            var profile = SelectedTreeViewShader.ShaderType.Contains("VertexShader") ? "vs_5_0" : "ps_5_0";
            var compiled = ShaderBytecode.Compile(ShaderText, "main", profile);
            if (compiled != null)
            {
                InternalReplaceShader(compiled.Bytecode.Data, SelectedTreeViewShader.Id);
            }
        }

        private bool ShaderIsSelected()
        {
            return MeshShaderMaps_TreeView?.SelectedItem is TreeViewShader;
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
                    if (shaderText.Equals(testShaderText, StringComparison.Ordinal))
                    {
                        SelectedItem = shader;
                        MessageBox.Show($"Found shader: Index {shader.Index} {shader.ShaderType}");
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

        public ObservableCollectionExtended<TreeViewMeshShaderMap> MeshShaderMaps { get; } = [];

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

        public IEnumerable<TreeViewMeshShaderMap> GetMeshShaderMaps(MaterialShaderMap msm, ShaderCache localShaderCache = null)
        {
            var result = new List<TreeViewMeshShaderMap>();
            bool isFirst = true;
            foreach (MeshShaderMap meshShaderMap in msm.MeshShaderMaps)
            {
                var tvmsm = new TreeViewMeshShaderMap { VertexFactoryType = meshShaderMap.VertexFactoryType, IsExpanded = isFirst };
                isFirst = false;
                foreach ((NameReference _, ShaderReference shaderReference) in meshShaderMap.Shaders)
                {
                    var tvs = new TreeViewShader
                    {
                        Id = shaderReference.Id,
                        ShaderType = shaderReference.ShaderType,
                        Game = Pcc.Game,
                        Parent = tvmsm,
                    };
                    if (localShaderCache != null && localShaderCache.Shaders.TryGetValue(shaderReference.Id, out Shader shader))
                    {
                        // Cache bytecode and index
                        tvs.Bytecode = shader.ShaderByteCode;
                        tvs.Index = localShaderCache.Shaders.IndexOf(new KeyValuePair<Guid, Shader>(shaderReference.Id, shader));
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
            ShaderText = "";
            TopInfoText = "";
            TopShaderInfoTextBlock.Text = "";
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                var elhw = new ExportLoaderHostedWindow(new ShaderExportLoader { AutoLoad = true }, CurrentLoadedExport)
                {
                    Title = $"Shader Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
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
                ShaderText = tvs.DissassembledShader;
            }
        }

        private void LoadShaders_Button_Click(object sender, RoutedEventArgs e)
        {
            LoadShaders();
        }

        private void LoadShaders(TreeViewShader itemToHighlight = null)
        {
            if (GlobalShaderCache != null)
            {
                LoadGlobalShaders(itemToHighlight);
            }
            else
            {
                LoadPackageShaders(itemToHighlight);
            }
        }

        private void LoadGlobalShaders(TreeViewShader itemToHighlight)
        {
            MeshShaderMaps.ClearEx();
            var root = new TreeViewMeshShaderMap { VertexFactoryType = "Global Shaders", IsExpanded = true };
            MeshShaderMaps.Add(root);
            int i = 0;
            foreach (var shader in GlobalShaderCache.Shaders)
            {
                var tve = new TreeViewShader
                {
                    Bytecode = shader.Value.ShaderByteCode,
                    Index = i,
                    Game = MEGame.LE3,
                    Id = shader.Key,
                    ShaderType = shader.Value.ShaderType,
                    Parent = root,
                };
                // Dumping code.
                //var decomp = HLSLDecompiler.DecompileShader(tve.Bytecode, false);
                //var sanitizedType = tve.ShaderType.Replace("<", "_").Replace(">", "_");
                //var name = Path.Combine($@"C:\users\public\GlobalShader-{i}-{sanitizedType}.hlsl");
                //File.WriteAllText(name, decomp);
                root.Shaders.Add(tve);
                i++;
            }
        }

        /// <summary>
        /// Loads shaders that are referenced and stored in package file format
        /// </summary>
        /// <param name="itemToHighlight"></param>
        private void LoadPackageShaders(TreeViewShader itemToHighlight)
        {
            if (CurrentLoadedExport == null)
                return;

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

                if (itemToHighlight != null)
                {
                    foreach (var stype in MeshShaderMaps)
                    {
                        if (stype.VertexFactoryType == ((TreeViewMeshShaderMap)itemToHighlight.Parent).VertexFactoryType)
                        {
                            foreach (var s in stype.Shaders)
                            {
                                if (s.ShaderType == itemToHighlight.ShaderType)
                                {
                                    SelectedItem = s;
                                    break;
                                }
                            }
                        }
                    }
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
                var newGuid = ShaderCacheManipulator.CopyRefShadersToLocal(CurrentLoadedExport, true);
                if (newGuid == null)
                    throw new Exception("Material Shader Map has disappeared!");
                return newGuid.Value;
            }).ContinueWithOnUIThread(prevTask =>
            {
                if (prevTask.Exception is AggregateException aggregateException)
                {
                    new ExceptionHandlerDialog(aggregateException).ShowDialog();
                    IsBusy = false;
                    return;
                }

                LoadShaders();
                MessageBox.Show(Window.GetWindow(this),
                    "This material now has its own unique shaders in the local SeekFreeShaderCache." +
                    "Porting this material to another package will bring the shaders along to that package's shader cache.\n\n" +
                    "You should change this material's name, so it will not conflict with other instances that use its original shaders.");
            });
        }


        private void ReplaceShader()
        {
            var selectedShaderInfo = MeshShaderMaps_TreeView.SelectedItem as TreeViewShader;
            if (selectedShaderInfo == null)
                return; // Must be selected

            var dlg = new CommonOpenFileDialog
            {
                DefaultExtension = ".fxc",
                EnsurePathExists = true,
                Title = "Select compiled shader file"
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
                return;

            // Scoped using
            using (var testfs = File.OpenRead(dlg.FileName))
            {
                // Not sure what min size of shader is, but definitely at least 20
                if (testfs.Length < 20)
                {
                    MessageBox.Show($"{dlg.FileName} is not a compiled shader file.");
                    return;
                }
                var magic = testfs.ReadStringASCII(4);
                if (magic != "DXBC")
                {
                    MessageBox.Show($"{dlg.FileName} is not a compiled shader file.");
                    return;
                }
            }

            InternalReplaceShader(File.ReadAllBytes(dlg.FileName), selectedShaderInfo.Id);
        }

        private void InternalReplaceShader(byte[] bytecode, Guid id)
        {

            IsBusy = true;
            BusyText = "Replacing shader";

            Task.Run(() =>
            {
                ShaderCacheManipulator.CopyRefShadersToLocal(CurrentLoadedExport); // Clone = false
                var sfscExport = Pcc.FindExport("SeekFreeShaderCache");
                var sfsc = ObjectBinary.From<ShaderCache>(sfscExport);

                if (sfsc.Shaders.TryGetValue(id, out var shader))
                {
                    // Insert new bytecode
                    shader.ReplaceByteCode(bytecode);

                    // Update the cache
                    sfscExport.WriteBinary(sfsc);
                }
                else
                {
                    throw new Exception("Shader ID not found in the cache.");
                }
            }).ContinueWithOnUIThread(prevTask =>
            {
                if (prevTask.Exception is AggregateException aggregateException)
                {
                    new ExceptionHandlerDialog(aggregateException).ShowDialog();
                    IsBusy = false;
                    return;
                }

                // Update shaders.
                LoadShaders(SelectedTreeViewShader);
                // Update text box.
                // MessageBox.Show(Window.GetWindow(this), $"Shader {id} has been replaced.");
            });
        }

        /// <summary>
        /// Exports the current tree of shaders
        /// </summary>
        private void ExportMapShaders()
        {
            var dlg = new CommonOpenFileDialog("Select output folder")
            {
                IsFolderPicker = true
            };
            var dialogResult = dlg.ShowDialog();
            if (dialogResult != CommonFileDialogResult.Ok)
                return;

            IsBusy = true;
            BusyText = "Exporting shader map";
            Task.Run(() =>
            {
                foreach (var root in MeshShaderMaps)
                {
                    var subFolder = Directory.CreateDirectory(Path.Combine(dlg.FileName, root.VertexFactoryType)).FullName;
                    foreach (var shader in root.Shaders)
                    {
                        // < and > are not valid filesystem characters
                        var sanitizedName = shader.ShaderType.Replace("<", "_").Replace(">", "_");
                        var outPath = Path.Combine(subFolder, $"{sanitizedName}.fxc");

                        // Ensure bytecode loaded from ref
                        if (shader.Bytecode == null && RefShaderCacheReader.GetShaderBytecode(shader.Game, shader.Id) is byte[] bytecode)
                        {
                            shader.Bytecode = bytecode;
                        }
                        File.WriteAllBytes(outPath, shader.Bytecode);
                        //var hlsl = HLSLDecompiler.DecompileShader(shader.Bytecode, false).Trim();
                        //File.WriteAllText(outPath, hlsl);
                    }
                }
            }).ContinueWithOnUIThread(task =>
            {
                if (task.Exception is AggregateException aggregateException)
                {
                    new ExceptionHandlerDialog(aggregateException).ShowDialog();
                    IsBusy = false;
                    return;
                }

                IsBusy = false;
                MessageBox.Show("Done.");
            });

        }

        // Todo: Move to experiments as this is not really tied to shader map
        private void ExportAllShaders()
        {
            IsBusy = true;
            BusyText = "Exporting local shaders";
            var seekFreeShaderCacheExport = Pcc.FindExport("SeekFreeShaderCache");
            ShaderCache seekFreeShaderCache = null;

            if (seekFreeShaderCacheExport is null)
            {
                IsBusy = false;
                MessageBox.Show(Window.GetWindow(this), "This package doesn't have a local shader cache.", "No cache", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        byte[] shaderFile = [];
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

        // Todo: Move to package editor experiments as this is not really tied to shader map.
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
                        byte[] newShaderFile = [];
                        string shaderType = "";
                        string fileName = "";
                        int shaderIndex = TVshader.Index;

                        if (seekFreeShaderCache.Shaders.TryGetValue(TVshader.Id, out Shader shader))
                        {
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
                            shader.ReplaceByteCode(newShaderFile);

                            // Get Disassembly
                            string dissasembledShader = ShaderBytecode.FromStream(new MemoryStream(newShaderFile)).Disassemble();
                            // Get last line that contains instruction counts
                            string result = string.Join("", dissasembledShader.Split('\n').Reverse().Take(2).ToArray());
                            // Get digits from the result
                            string digits = string.Join("", new String(result.Where(char.IsDigit).ToArray()));
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

        // LoadFile is supported for GlobalShaderCache file which is not package based
        public override void LoadFile(string filepath)
        {
            using var fs = File.OpenRead(filepath);
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
            // GLOBAL SHADER CACHE - Not implemented yet
        }

        public override void SaveAs()
        {
            // GLOBAL SHADER CACHE - Not implemented yet
        }

        internal override void OpenFile()
        {
            // GLOBAL SHADER CACHE - Not implemented yet
        }

        public override bool CanSave()
        {
            // GLOBAL SHADER CACHE - Not implemented yet
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

        private ITreeItem _selectedItem;
        public ITreeItem SelectedItem { get => _selectedItem; set => SetProperty(ref _selectedItem, value); }

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


    public class TreeViewMeshShaderMap : ITreeItem
    {
        #region Equality
        protected bool Equals(TreeViewMeshShaderMap other)
        {
            return VertexFactoryType == other.VertexFactoryType;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TreeViewMeshShaderMap)obj);
        }

        public override int GetHashCode()
        {
            return (VertexFactoryType != null ? VertexFactoryType.GetHashCode() : 0);
        }
        #endregion

        public string VertexFactoryType { get; set; }
        public ObservableCollectionExtended<TreeViewShader> Shaders { get; } = new();
        public bool IsSelected { get; set; }
        public bool IsExpanded { get; set; }
        public ITreeItem Parent { get; set; } // Will always be null

        public void PrintPretty(string indent, TextWriter str, bool last, ExportEntry associatedExport)
        {
            str.WriteLine(VertexFactoryType);
        }
    }

    public class TreeViewShader : ITreeItem
    {
        #region Equality
        protected bool Equals(TreeViewShader other)
        {
            return Id.Equals(other.Id) && ShaderType == other.ShaderType;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TreeViewShader)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, ShaderType);
        }
        #endregion

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

        public bool IsSelected { get; set; }
        public bool IsExpanded { get; set; }
        public ITreeItem Parent { get; set; }

        public void PrintPretty(string indent, TextWriter str, bool last, ExportEntry associatedExport)
        {
            str.WriteLine(ShaderType);
        }
    }
}