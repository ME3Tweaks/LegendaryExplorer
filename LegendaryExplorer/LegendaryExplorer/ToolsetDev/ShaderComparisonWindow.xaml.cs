using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Be.Windows.Forms;
using ICSharpCode.AvalonEdit.Document;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.ToolsetDev.ShaderComparer;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;

namespace LegendaryExplorer.ToolsetDev;

/// <summary>
/// Window for comparing shaders from two different packages side-by-side.
/// Shows decompiled HLSL and parameter bindings from the shader cache.
/// </summary>
public partial class ShaderComparisonWindow : NotifyPropertyChangedWindowBase
{
    #region Busy

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

    #region Left Panel Properties

    private IMEPackage _leftPackage;
    private ShaderCache _leftShaderCache;
    private ExportEntry _leftShaderCacheExport;

    private string _leftPackageName = "No package loaded";
    public string LeftPackageName
    {
        get => _leftPackageName;
        set => SetProperty(ref _leftPackageName, value);
    }

    public ObservableCollectionExtended<ExportEntry> LeftMaterials { get; } = [];

    private ExportEntry _selectedLeftMaterial;
    public ExportEntry SelectedLeftMaterial
    {
        get => _selectedLeftMaterial;
        set
        {
            if (SetProperty(ref _selectedLeftMaterial, value))
                OnMaterialChanged(Side.Left);
        }
    }

    public ObservableCollectionExtended<TreeViewShader> LeftShaders { get; } = [];

    private TreeViewShader _selectedLeftShader;
    public TreeViewShader SelectedLeftShader
    {
        get => _selectedLeftShader;
        set
        {
            if (SetProperty(ref _selectedLeftShader, value))
                OnShaderSelected(Side.Left);
        }
    }

    private TextDocument _leftDocument;
    public TextDocument LeftDocument
    {
        get => _leftDocument;
        set => SetProperty(ref _leftDocument, value);
    }

    public ObservableCollectionExtended<BinInterpNode> LeftParameterNodes { get; } = [];

    #endregion

    #region Right Panel Properties

    private IMEPackage _rightPackage;
    private ShaderCache _rightShaderCache;
    private ExportEntry _rightShaderCacheExport;

    private string _rightPackageName = "No package loaded";
    public string RightPackageName
    {
        get => _rightPackageName;
        set => SetProperty(ref _rightPackageName, value);
    }

    public ObservableCollectionExtended<ExportEntry> RightMaterials { get; } = [];

    private ExportEntry _selectedRightMaterial;
    public ExportEntry SelectedRightMaterial
    {
        get => _selectedRightMaterial;
        set
        {
            if (SetProperty(ref _selectedRightMaterial, value))
                OnMaterialChanged(Side.Right);
        }
    }

    public ObservableCollectionExtended<TreeViewShader> RightShaders { get; } = [];

    private TreeViewShader _selectedRightShader;
    public TreeViewShader SelectedRightShader
    {
        get => _selectedRightShader;
        set
        {
            if (SetProperty(ref _selectedRightShader, value))
                OnShaderSelected(Side.Right);
        }
    }

    private TextDocument _rightDocument;
    public TextDocument RightDocument
    {
        get => _rightDocument;
        set => SetProperty(ref _rightDocument, value);
    }

    public ObservableCollectionExtended<BinInterpNode> RightParameterNodes { get; } = [];

    #endregion

    #region Bytecode Match Properties

    private string _leftBytecodeMatchText = "";
    public string LeftBytecodeMatchText
    {
        get => _leftBytecodeMatchText;
        set => SetProperty(ref _leftBytecodeMatchText, value);
    }

    private Brush _leftBytecodeMatchBrush = Brushes.Gray;
    public Brush LeftBytecodeMatchBrush
    {
        get => _leftBytecodeMatchBrush;
        set => SetProperty(ref _leftBytecodeMatchBrush, value);
    }

    private string _rightBytecodeMatchText = "";
    public string RightBytecodeMatchText
    {
        get => _rightBytecodeMatchText;
        set => SetProperty(ref _rightBytecodeMatchText, value);
    }

    private Brush _rightBytecodeMatchBrush = Brushes.Gray;
    public Brush RightBytecodeMatchBrush
    {
        get => _rightBytecodeMatchBrush;
        set => SetProperty(ref _rightBytecodeMatchBrush, value);
    }

    #endregion

    /// <summary>
    /// Hidden BinaryInterpreterWPF instances used to call ReadShaderParameters.
    /// They need CurrentLoadedExport set to the ShaderCache export.
    /// </summary>
    private BinaryInterpreterWPF _leftBinInterp;
    private BinaryInterpreterWPF _rightBinInterp;

    /// <summary>
    /// HexBox controls hosted via WindowsFormsHost for displaying raw shader bytecode.
    /// </summary>
    private HexBox _leftHexBox;
    private HexBox _rightHexBox;

    /// <summary>
    /// ObjectInstanceDB for the game of the left package, used to find matching materials in other packages.
    /// </summary>
    private ObjectInstanceDB _objectInstanceDB;
    private MEGame _objectInstanceDBGame;

    private enum Side { Left, Right }

    public ShaderComparisonWindow()
    {
        DataContext = this;
        InitializeComponent();
        _leftBinInterp = new BinaryInterpreterWPF();
        _rightBinInterp = new BinaryInterpreterWPF();
        _leftHexBox = (HexBox)LeftHexBoxHost.Child;
        _rightHexBox = (HexBox)RightHexBoxHost.Child;
    }

    private void LoadLeftPackage_Click(object sender, RoutedEventArgs e) => LoadPackage(Side.Left);
    private void LoadRightPackage_Click(object sender, RoutedEventArgs e) => LoadPackage(Side.Right);
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void LoadPackage(Side side)
    {
        var dlg = AppDirectories.GetOpenPackageDialog();
        if (dlg.ShowDialog() != true)
            return;

        IsBusy = true;
        BusyText = "Loading package...";

        string filePath = dlg.FileName;

        Task.Run(() => MEPackageHandler.OpenMEPackage(filePath, forceLoadFromDisk: true))
            .ContinueWithOnUIThread((Task<IMEPackage> prevTask) =>
            {
                if (prevTask.Exception is AggregateException ex)
                {
                    IsBusy = false;
                    MessageBox.Show(this, $"Error loading package:\n{ex.InnerException?.Message ?? ex.Message}");
                    return;
                }

                var package = prevTask.Result;

                // Find materials in the package
                var materials = package.Exports
                    .Where(exp => !exp.IsDefaultObject &&
                                  (exp.ClassName == "Material" ||
                                   (exp.IsA("MaterialInstance") && exp.GetProperty<BoolProperty>("bHasStaticPermutationResource") is { Value: true })))
                    .ToList();

                // Find local shader cache
                var shaderCacheExport = package.Exports.FirstOrDefault(exp => exp.ClassName == "ShaderCache");
                ShaderCache shaderCache = null;
                if (shaderCacheExport != null)
                {
                    try
                    {
                        shaderCache = ObjectBinary.From<ShaderCache>(shaderCacheExport);
                    }
                    catch
                    {
                        // Failed to parse shader cache
                    }
                }

                if (side == Side.Left)
                {
                    _leftPackage?.Dispose();
                    _leftPackage = package;
                    _leftShaderCache = shaderCache;
                    _leftShaderCacheExport = shaderCacheExport;
                    LeftPackageName = Path.GetFileName(filePath);
                    LeftShaders.ClearEx();
                    LeftParameterNodes.ClearEx();
                    LeftDocument = new TextDocument();
                    LeftMaterials.ReplaceAll(materials);
                    if (shaderCacheExport != null)
                    {
                        SetCurrentLoadedExport(_leftBinInterp, shaderCacheExport);
                    }

                    // Clear the right panel when a new left package is loaded
                    _rightPackage?.Dispose();
                    _rightPackage = null;
                    _rightShaderCache = null;
                    _rightShaderCacheExport = null;
                    RightPackageName = "No package loaded";
                    RightShaders.ClearEx();
                    RightParameterNodes.ClearEx();
                    RightDocument = new TextDocument();
                    RightMaterials.ClearEx();

                    // Load the ObjectInstanceDB for the game if not already loaded
                    EnsureObjectInstanceDBLoaded(package.Game);
                }
                else
                {
                    _rightPackage?.Dispose();
                    _rightPackage = package;
                    _rightShaderCache = shaderCache;
                    _rightShaderCacheExport = shaderCacheExport;
                    RightPackageName = Path.GetFileName(filePath);
                    RightShaders.ClearEx();
                    RightParameterNodes.ClearEx();
                    RightDocument = new TextDocument();
                    RightMaterials.ReplaceAll(materials);
                    if (shaderCacheExport != null)
                    {
                        SetCurrentLoadedExport(_rightBinInterp, shaderCacheExport);
                    }
                }

                IsBusy = false;
            });
    }

    private void OnMaterialChanged(Side side)
    {
        var material = side == Side.Left ? SelectedLeftMaterial : SelectedRightMaterial;
        var package = side == Side.Left ? _leftPackage : _rightPackage;
        var shaderCache = side == Side.Left ? _leftShaderCache : _rightShaderCache;
        var shaders = side == Side.Left ? LeftShaders : RightShaders;
        var parameterNodes = side == Side.Left ? LeftParameterNodes : RightParameterNodes;

        shaders.ClearEx();
        parameterNodes.ClearEx();
        if (side == Side.Left)
        {
            LeftDocument = new TextDocument();
        }
        else
        {
            RightDocument = new TextDocument();
        }

        if (material == null || package == null)
            return;

        // When the left material changes, try to find and load a matching right package via the ObjectInstanceDB
        if (side == Side.Left)
        {
            FindAndLoadMatchingRightPackage(material);
        }

        LoadShadersForMaterial(material, package, shaderCache, shaders, side);
    }

    /// <summary>
    /// Loads the shader list for a given material into the appropriate panel.
    /// </summary>
    private void LoadShadersForMaterial(ExportEntry material, IMEPackage package, ShaderCache shaderCache,
        ObservableCollectionExtended<TreeViewShader> shaders, Side side)
    {
        IsBusy = true;
        BusyText = "Loading shaders...";

        Task.Run(() =>
        {
            StaticParameterSet sps = material.ClassName switch
            {
                "Material" => (StaticParameterSet)ObjectBinary.From<Material>(material).SM3MaterialResource.ID,
                _ => ObjectBinary.From<MaterialInstance>(material).SM3StaticParameterSet
            };

            var shaderList = new List<TreeViewShader>();

            // Try local shader cache first
            if (shaderCache != null && shaderCache.MaterialShaderMaps.TryGetValue(sps, out MaterialShaderMap msm))
            {
                foreach (MeshShaderMap meshShaderMap in msm.MeshShaderMaps)
                {
                    foreach ((NameReference _, ShaderReference shaderReference) in meshShaderMap.Shaders)
                    {
                        var tvs = new TreeViewShader
                        {
                            Id = shaderReference.Id,
                            ShaderType = shaderReference.ShaderType,
                            Game = package.Game,
                        };
                        if (shaderCache.Shaders.TryGetValue(shaderReference.Id, out Shader shader))
                        {
                            tvs.Bytecode = shader.ShaderByteCode;
                        }
                        shaderList.Add(tvs);
                    }
                }
            }
            else
            {
                // Try ref shader cache
                try
                {
                    MaterialShaderMap msmFromGlobal = RefShaderCacheReader.GetMaterialShaderMap(package.Game, sps, out _);
                    if (msmFromGlobal != null)
                    {
                        foreach (MeshShaderMap meshShaderMap in msmFromGlobal.MeshShaderMaps)
                        {
                            foreach ((NameReference _, ShaderReference shaderReference) in meshShaderMap.Shaders)
                            {
                                shaderList.Add(new TreeViewShader
                                {
                                    Id = shaderReference.Id,
                                    ShaderType = shaderReference.ShaderType,
                                    Game = package.Game,
                                });
                            }
                        }
                    }
                }
                catch
                {
                    // RefShaderCache not available
                }
            }

            return shaderList;
        }).ContinueWithOnUIThread((Task<List<TreeViewShader>> prevTask) =>
        {
            if (prevTask.Exception is AggregateException ex)
            {
                IsBusy = false;
                MessageBox.Show(this, $"Error loading shaders:\n{ex.InnerException?.Message ?? ex.Message}");
                return;
            }

            shaders.ReplaceAll(prevTask.Result);
            IsBusy = false;
        });
    }

    /// <summary>
    /// Loads the ObjectInstanceDB for the given game if it isn't already loaded.
    /// </summary>
    private void EnsureObjectInstanceDBLoaded(MEGame game)
    {
        if (_objectInstanceDB != null && _objectInstanceDBGame == game)
            return;

        _objectInstanceDB = null;
        _objectInstanceDBGame = game;

        string objectDBPath = AppDirectories.GetObjectDatabasePath(game);
        if (!File.Exists(objectDBPath))
            return;

        IsBusy = true;
        BusyText = $"Loading ObjectInstanceDB for {game}...";

        Task.Run(() =>
        {
            using FileStream fs = File.OpenRead(objectDBPath);
            return ObjectInstanceDB.Deserialize(game, fs);
        }).ContinueWithOnUIThread((Task<ObjectInstanceDB> prevTask) =>
        {
            if (prevTask.Exception == null)
            {
                _objectInstanceDB = prevTask.Result;
            }
            IsBusy = false;
        });
    }

    /// <summary>
    /// Uses the ObjectInstanceDB to find another package that contains the same material
    /// (by InstancedFullPath) and loads it into the right panel, auto-selecting the matching material.
    /// </summary>
    private void FindAndLoadMatchingRightPackage(ExportEntry leftMaterial)
    {
        if (_objectInstanceDB == null || _leftPackage == null)
            return;

        string materialIFP = leftMaterial.InstancedFullPath;
        var files = _objectInstanceDB.GetFilesContainingObject(materialIFP);
        if (files == null || files.Count == 0)
        {
            var newIFP = materialIFP;
            if (leftMaterial.IsForcedExport)
            {
                // Strip package
                newIFP = materialIFP.Substring(leftMaterial.GetLinker().Length + 1);
            }
            else
            {
                // Add package
                newIFP = leftMaterial.MemoryFullPath;
            }
            files = _objectInstanceDB.GetFilesContainingObject(newIFP);
            if (files == null || files.Count == 0)
            {
                // Nothing found.
                return;
            }
        }

        // The DB stores paths relative to the game root. Resolve to full paths.
        string defaultGamePath = MEDirectories.GetDefaultGamePath(_leftPackage.Game);
        if (defaultGamePath == null)
            return;

        // Find a file that is different from the left package
        string leftFileFull = _leftPackage.FilePath != null ? Path.GetFullPath(_leftPackage.FilePath) : null;
        string matchedFile = null;
        foreach (string relPath in files)
        {
            string fullPath = Path.IsPathRooted(relPath) ? relPath : Path.Combine(defaultGamePath, relPath);
            if (File.Exists(fullPath) && !string.Equals(Path.GetFullPath(fullPath), leftFileFull, StringComparison.OrdinalIgnoreCase))
            {
                matchedFile = fullPath;
                break;
            }
        }

        // If all files are the same as the left package, just use the first available file
        if (matchedFile == null)
        {
            foreach (string relPath in files)
            {
                string fullPath = Path.IsPathRooted(relPath) ? relPath : Path.Combine(defaultGamePath, relPath);
                if (File.Exists(fullPath))
                {
                    matchedFile = fullPath;
                    break;
                }
            }
        }

        if (matchedFile == null)
            return;

        IsBusy = true;
        BusyText = $"Loading matching package: {Path.GetFileName(matchedFile)}...";

        string fileToLoad = matchedFile;
        Task.Run(() => MEPackageHandler.OpenMEPackage(fileToLoad, forceLoadFromDisk: true))
            .ContinueWithOnUIThread((Task<IMEPackage> prevTask) =>
            {
                if (prevTask.Exception is AggregateException ex)
                {
                    IsBusy = false;
                    return;
                }

                var package = prevTask.Result;

                // Find materials in the right package
                var materials = package.Exports
                    .Where(exp => !exp.IsDefaultObject &&
                                  (exp.ClassName == "Material" ||
                                   (exp.IsA("MaterialInstance") && exp.GetProperty<BoolProperty>("bHasStaticPermutationResource") is { Value: true })))
                    .ToList();

                // Find local shader cache
                var shaderCacheExport = package.Exports.FirstOrDefault(exp => exp.ClassName == "ShaderCache");
                ShaderCache shaderCache = null;
                if (shaderCacheExport != null)
                {
                    try
                    {
                        shaderCache = ObjectBinary.From<ShaderCache>(shaderCacheExport);
                    }
                    catch
                    {
                        // Failed to parse shader cache
                    }
                }

                _rightPackage?.Dispose();
                _rightPackage = package;
                _rightShaderCache = shaderCache;
                _rightShaderCacheExport = shaderCacheExport;
                RightPackageName = Path.GetFileName(fileToLoad);
                RightShaders.ClearEx();
                RightParameterNodes.ClearEx();
                RightDocument = new TextDocument();
                RightMaterials.ReplaceAll(materials);
                if (shaderCacheExport != null)
                {
                    SetCurrentLoadedExport(_rightBinInterp, shaderCacheExport);
                }

                // Auto-select the matching material by InstancedFullPath
                var matchingMaterial = materials.FirstOrDefault(m => m.InstancedFullPath == materialIFP) ?? materials.FirstOrDefault(m => m.MemoryFullPath == materialIFP);
                if (matchingMaterial != null)
                {
                    SelectedRightMaterial = matchingMaterial;
                }

                IsBusy = false;
            });
    }

    private void GetShaderInfo_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedLeftShader == null || SelectedRightShader == null)
        {
            MessageBox.Show(this, "Please select a shader on both the left and right panels.", "Get Shader Info");
            return;
        }

        // Ensure bytecode is available
        byte[] leftBytecode = SelectedLeftShader.Bytecode;
        byte[] rightBytecode = SelectedRightShader.Bytecode;

        if (leftBytecode == null && SelectedLeftShader.Game.IsLEGame())
            leftBytecode = RefShaderCacheReader.GetShaderBytecode(SelectedLeftShader.Game, SelectedLeftShader.Id);
        if (rightBytecode == null && SelectedRightShader.Game.IsLEGame())
            rightBytecode = RefShaderCacheReader.GetShaderBytecode(SelectedRightShader.Game, SelectedRightShader.Id);

        if (leftBytecode == null || rightBytecode == null)
        {
            MessageBox.Show(this, "Shader bytecode is not available for one or both of the selected shaders.", "Get Shader Info");
            return;
        }

        try
        {
            var leftEntries = ShaderInfoReader.GetShaderInfo(leftBytecode, out int leftInstructions);
            var rightEntries = ShaderInfoReader.GetShaderInfo(rightBytecode, out int rightInstructions);

            var window = new ShaderInfoWindow(
                $"Left: {SelectedLeftShader.ShaderType}", leftEntries, leftInstructions,
                $"Right: {SelectedRightShader.ShaderType}", rightEntries, rightInstructions)
            {
                Owner = this
            };
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error reading shader info:\n{ex.Message}", "Get Shader Info");
        }
    }

    private void CopyShaderParameters_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedLeftShader == null || SelectedRightShader == null)
        {
            MessageBox.Show(this, "Please select a shader on both the left and right panels.", "Copy Shader Parameters");
            return;
        }

        if (_leftShaderCache == null || _leftShaderCacheExport == null)
        {
            MessageBox.Show(this, "The left package does not have a shader cache.", "Copy Shader Parameters");
            return;
        }

        if (_rightShaderCache == null)
        {
            MessageBox.Show(this, "The right package does not have a shader cache.", "Copy Shader Parameters");
            return;
        }

        var leftGuid = SelectedLeftShader.Id;
        var rightGuid = SelectedRightShader.Id;

        if (!_leftShaderCache.Shaders.TryGetValue(leftGuid, out Shader leftShader))
        {
            MessageBox.Show(this, "Could not find the selected left shader in the left shader cache.", "Copy Shader Parameters");
            return;
        }

        if (!_rightShaderCache.Shaders.TryGetValue(rightGuid, out Shader rightShader))
        {
            MessageBox.Show(this, "Could not find the selected right shader in the right shader cache.", "Copy Shader Parameters");
            return;
        }

        if (leftShader.GetType() != rightShader.GetType())
        {
            MessageBox.Show(this, $"Cannot copy parameters between different shader types.\n\nLeft: {leftShader.GetType().Name}\nRight: {rightShader.GetType().Name}", "Copy Shader Parameters");
            return;
        }

        IsBusy = true;
        BusyText = "Copying shader parameters...";

        Task.Run(() =>
        {
            // Clone the right shader (copies all fields including parameters)
            var clonedShader = rightShader.Clone();

            // Restore the left shader's identity and bytecode
            clonedShader.Guid = leftShader.Guid;
            clonedShader.ShaderByteCode = leftShader.ShaderByteCode;
            clonedShader.ShaderType = leftShader.ShaderType;
            clonedShader.Frequency = leftShader.Frequency;
            clonedShader.Platform = leftShader.Platform;
            clonedShader.ParameterMapCRC = leftShader.ParameterMapCRC;
            clonedShader.InstructionCount = leftShader.InstructionCount;
            clonedShader.VertexFactoryType = leftShader.VertexFactoryType;

            // Replace the shader in the left cache
            _leftShaderCache.Shaders[leftGuid] = clonedShader;

            // Write the modified shader cache back to the export
            _leftShaderCacheExport.WriteBinary(_leftShaderCache);

            // Save the package
            _leftPackage.Save();
        }).ContinueWithOnUIThread(prevTask =>
        {
            IsBusy = false;

            if (prevTask.Exception is AggregateException ex)
            {
                MessageBox.Show(this, $"Error copying shader parameters:\n{ex.InnerException?.Message ?? ex.Message}", "Copy Shader Parameters");
                return;
            }

            // Reload the left side's parameter bindings
            OnShaderSelected(Side.Left);

            MessageBox.Show(this, "Shader parameters copied successfully and package saved.", "Copy Shader Parameters");
        });
    }

    private void OnShaderSelected(Side side)
    {
        var shader = side == Side.Left ? SelectedLeftShader : SelectedRightShader;
        var parameterNodes = side == Side.Left ? LeftParameterNodes : RightParameterNodes;
        var shaderCacheExport = side == Side.Left ? _leftShaderCacheExport : _rightShaderCacheExport;
        var binInterp = side == Side.Left ? _leftBinInterp : _rightBinInterp;
        var hexBox = side == Side.Left ? _leftHexBox : _rightHexBox;

        parameterNodes.ClearEx();

        if (shader == null)
        {
            if (side == Side.Left)
            {
                LeftDocument = new TextDocument();
                LeftBytecodeMatchText = "";
            }
            else
            {
                RightDocument = new TextDocument();
                RightBytecodeMatchText = "";
            }
            hexBox.ByteProvider = null;
            return;
        }

        // Set decompiled shader text
        var doc = new TextDocument(shader.DissassembledShader);
        if (side == Side.Left)
            LeftDocument = doc;
        else
            RightDocument = doc;

        // Populate the hex box with shader bytecode
        byte[] bytecode = shader.Bytecode;
        if (bytecode == null)
            bytecode = RefShaderCacheReader.GetShaderBytecode(shader.Game, shader.Id);

        if (bytecode != null)
            hexBox.ByteProvider = new DynamicByteProvider(bytecode);
        else
            hexBox.ByteProvider = null;

        // Update bytecode match status for both sides
        UpdateBytecodeMatchStatus();

        // Read parameter bindings from the shader cache binary
        if (shaderCacheExport != null)
        {
            var paramOffset = FindShaderParameterOffset(shaderCacheExport, shader.Id, out string shaderType);
            if (paramOffset.HasValue && shaderType != null)
            {
                try
                {
                    var bin = new EndianReader(shaderCacheExport.GetReadOnlyDataStream()) { Endian = shaderCacheExport.FileRef.Endian };
                    bin.JumpTo(paramOffset.Value);
                    var paramsNode = binInterp.ReadShaderParameters(bin, shaderType, out Exception ex);
                    if (paramsNode?.Items != null)
                    {
                        // Rebase offsets so they are relative to the start of the parameter data
                        long baseOffset = paramOffset.Value;
                        RebaseNodeOffsets(paramsNode, baseOffset);

                        foreach (var item in paramsNode.Items)
                        {
                            if (item is BinInterpNode node)
                            {
                                parameterNodes.Add(node);
                            }
                        }
                    }
                }
                catch
                {
                    parameterNodes.Add(new BinInterpNode { Header = "Error reading parameter bindings" });
                }
            }
        }

        // Compare both trees and highlight differences when both sides have data
        HighlightParameterDifferences();
    }

    /// <summary>
    /// Compares the bytecode of the left and right selected shaders and updates the match status text.
    /// </summary>
    private void UpdateBytecodeMatchStatus()
    {
        byte[] leftBytes = SelectedLeftShader?.Bytecode;
        byte[] rightBytes = SelectedRightShader?.Bytecode;

        if (leftBytes == null && SelectedLeftShader != null)
            leftBytes = RefShaderCacheReader.GetShaderBytecode(SelectedLeftShader.Game, SelectedLeftShader.Id);
        if (rightBytes == null && SelectedRightShader != null)
            rightBytes = RefShaderCacheReader.GetShaderBytecode(SelectedRightShader.Game, SelectedRightShader.Id);

        if (leftBytes == null || rightBytes == null)
        {
            string noData = leftBytes == null && rightBytes == null ? "" : "No bytecode on other side";
            if (leftBytes == null)
            {
                LeftBytecodeMatchText = SelectedLeftShader == null ? "" : "No bytecode available";
                LeftBytecodeMatchBrush = Brushes.Gray;
            }
            else
            {
                LeftBytecodeMatchText = noData;
                LeftBytecodeMatchBrush = Brushes.Gray;
            }
            if (rightBytes == null)
            {
                RightBytecodeMatchText = SelectedRightShader == null ? "" : "No bytecode available";
                RightBytecodeMatchBrush = Brushes.Gray;
            }
            else
            {
                RightBytecodeMatchText = noData;
                RightBytecodeMatchBrush = Brushes.Gray;
            }
            return;
        }

        bool match = leftBytes.AsSpan().SequenceEqual(rightBytes.AsSpan());
        if (match)
        {
            LeftBytecodeMatchText = "Bytecode matches other side";
            LeftBytecodeMatchBrush = Brushes.Green;
            RightBytecodeMatchText = "Bytecode matches other side";
            RightBytecodeMatchBrush = Brushes.Green;
        }
        else
        {
            LeftBytecodeMatchText = $"Bytecode differs (left: {leftBytes.Length} bytes, right: {rightBytes.Length} bytes)";
            LeftBytecodeMatchBrush = Brushes.Red;
            RightBytecodeMatchText = $"Bytecode differs (left: {leftBytes.Length} bytes, right: {rightBytes.Length} bytes)";
            RightBytecodeMatchBrush = Brushes.Red;
        }
    }

    /// <summary>
    /// Recursively adjusts all node offsets and headers so they are relative to the given base offset.
    /// This makes comparing parameter trees from different packages easier.
    /// </summary>
    private static void RebaseNodeOffsets(BinInterpNode node, long baseOffset)
    {
        if (node.Offset >= 0)
        {
            int newOffset = (int)(node.Offset - baseOffset);
            // Replace the absolute offset prefix in the header with the rebased one
            if (node.Header != null && node.Header.StartsWith($"0x{node.Offset:X8}: "))
            {
                node.Header = $"0x{newOffset:X8}: {node.Header.Substring(12)}";
            }
            node.Offset = newOffset;
        }

        foreach (var child in node.Items)
        {
            if (child is BinInterpNode childNode)
            {
                RebaseNodeOffsets(childNode, baseOffset);
            }
        }
    }

    /// <summary>Light red background for nodes whose children contain value differences.</summary>
    private static readonly SolidColorBrush DiffLightRedBrush = new(Color.FromArgb(0xFF, 0xFF, 0xCC, 0xCC));
    /// <summary>Darker red background for nodes where child counts don't match.</summary>
    private static readonly SolidColorBrush DiffDarkRedBrush = new(Color.FromArgb(0xFF, 0xFF, 0x99, 0x99));

    static ShaderComparisonWindow()
    {
        DiffLightRedBrush.Freeze();
        DiffDarkRedBrush.Freeze();
    }

    /// <summary>
    /// Compares the left and right parameter node trees and highlights differences.
    /// </summary>
    private void HighlightParameterDifferences()
    {
        // Clear existing highlights
        foreach (var node in LeftParameterNodes)
            ClearDiffBackground(node);
        foreach (var node in RightParameterNodes)
            ClearDiffBackground(node);

        if (LeftParameterNodes.Count == 0 || RightParameterNodes.Count == 0)
            return;

        CompareNodeLists(LeftParameterNodes, RightParameterNodes);
    }

    /// <summary>
    /// Recursively clears DiffBackground on a node and all its children.
    /// </summary>
    private static void ClearDiffBackground(BinInterpNode node)
    {
        node.DiffBackground = null;
        foreach (var child in node.Items)
        {
            if (child is BinInterpNode childNode)
                ClearDiffBackground(childNode);
        }
    }

    /// <summary>
    /// Compares two lists of nodes at the same tree level and marks differences.
    /// Returns true if any differences were found.
    /// </summary>
    private static bool CompareNodeLists(IList<BinInterpNode> leftNodes, IList<BinInterpNode> rightNodes)
    {
        bool hasDifferences = false;
        int maxCount = Math.Max(leftNodes.Count, rightNodes.Count);
        bool countMismatch = leftNodes.Count != rightNodes.Count;

        for (int i = 0; i < maxCount; i++)
        {
            if (i >= leftNodes.Count)
            {
                // Extra node on the right side only
                MarkSubtree(rightNodes[i], DiffDarkRedBrush);
                hasDifferences = true;
                continue;
            }

            if (i >= rightNodes.Count)
            {
                // Extra node on the left side only
                MarkSubtree(leftNodes[i], DiffDarkRedBrush);
                hasDifferences = true;
                continue;
            }

            var leftNode = leftNodes[i];
            var rightNode = rightNodes[i];

            // Compare children recursively
            var leftChildren = leftNode.Items.OfType<BinInterpNode>().ToList();
            var rightChildren = rightNode.Items.OfType<BinInterpNode>().ToList();

            bool childCountMismatch = leftChildren.Count != rightChildren.Count;
            bool childrenDiffer = CompareNodeLists(leftChildren, rightChildren);

            // Compare this node's own header
            bool headersDiffer = !string.Equals(leftNode.Header, rightNode.Header, StringComparison.Ordinal);

            if (childCountMismatch)
            {
                leftNode.DiffBackground = DiffDarkRedBrush;
                rightNode.DiffBackground = DiffDarkRedBrush;
                hasDifferences = true;
            }
            else if (headersDiffer || childrenDiffer)
            {
                leftNode.DiffBackground = DiffLightRedBrush;
                rightNode.DiffBackground = DiffLightRedBrush;
                hasDifferences = true;
            }
        }

        return hasDifferences;
    }

    /// <summary>
    /// Marks an entire subtree with the given brush.
    /// </summary>
    private static void MarkSubtree(BinInterpNode node, SolidColorBrush brush)
    {
        node.DiffBackground = brush;
        foreach (var child in node.Items)
        {
            if (child is BinInterpNode childNode)
                MarkSubtree(childNode, brush);
        }
    }

    /// <summary>
    /// Scans through the shader cache binary data to find the offset where parameter data starts
    /// for a specific shader identified by its GUID.
    /// </summary>
    private static long? FindShaderParameterOffset(ExportEntry shaderCacheExport, Guid targetGuid, out string shaderType)
    {
        shaderType = null;
        var game = shaderCacheExport.Game;
        var pcc = shaderCacheExport.FileRef;
        var bin = new EndianReader(shaderCacheExport.GetReadOnlyDataStream()) { Endian = pcc.Endian };
        bin.JumpTo(shaderCacheExport.propsEnd());

        try
        {
            // Skip shader cache priority (UDK only)
            if (game == MEGame.UDK)
                bin.ReadInt32();

            // Skip platform byte
            bin.ReadByte();

            // Skip CRC maps
            int mapCount = game is MEGame.ME3 || game.IsLEGame() ? 2 : 1;
            while (mapCount-- > 0)
            {
                int count = bin.ReadInt32();
                bin.Skip(count * 12); // NameReference (8) + uint CRC (4)
            }

            // Skip vertex factory map (ME1 only)
            if (game == MEGame.ME1)
            {
                int vfCount = bin.ReadInt32();
                bin.Skip(vfCount * 12);
            }

            // Read embedded shaders
            int shaderCount = bin.ReadInt32();
            int dataOffset = shaderCacheExport.DataOffset;

            for (int i = 0; i < shaderCount; i++)
            {
                // Each shader entry starts with: ShaderType NameRef, GUID
                bin.ReadNameReference(pcc); // shader type (pre-header)
                Guid guid = bin.ReadGuid();

                if (game == MEGame.UDK)
                    bin.Skip(0x14); // source SHA

                int shaderEndOffset = bin.ReadInt32();

                if (game == MEGame.UDK)
                {
                    int udkCount = bin.ReadInt32();
                    bin.Skip(udkCount * 2);
                }

                bin.ReadByte(); // platform
                bin.ReadByte(); // frequency

                // Some shaders pre-serialize parameters before bytecode
                // We skip over the bytecode block
                int shaderSize = bin.ReadInt32();
                bin.Skip(shaderSize); // shader bytecode

                bin.ReadInt32(); // ParameterMap CRC
                bin.ReadGuid(); // end GUID

                string readShaderType = bin.ReadNameReference(pcc);

                if (game == MEGame.UDK)
                    bin.Skip(0x14); // shader SHA

                bin.ReadInt32(); // instruction count

                // At this point, bin.Position is at the start of the parameter data
                if (guid == targetGuid)
                {
                    shaderType = readShaderType;
                    return bin.Position;
                }

                // Skip to the end of this shader entry
                bin.JumpTo(shaderEndOffset - dataOffset);
            }
        }
        catch
        {
            // Failed to scan shader cache binary
        }

        return null;
    }

    /// <summary>
    /// Uses reflection to set the protected CurrentLoadedExport property on a BinaryInterpreterWPF
    /// without triggering a full binary scan (which LoadExport would do).
    /// </summary>
    private static readonly System.Reflection.PropertyInfo CurrentLoadedExportProperty =
        typeof(ExportLoaderControl).GetProperty(nameof(ExportLoaderControl.CurrentLoadedExport))!;

    private static void SetCurrentLoadedExport(BinaryInterpreterWPF binInterp, ExportEntry export)
    {
        CurrentLoadedExportProperty.GetSetMethod(nonPublic: true)!.Invoke(binInterp, [export]);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _leftPackage?.Dispose();
        _rightPackage?.Dispose();
        _leftBinInterp?.Dispose();
        _rightBinInterp?.Dispose();
        LeftHexBoxHost?.Dispose();
        RightHexBoxHost?.Dispose();
    }
}
