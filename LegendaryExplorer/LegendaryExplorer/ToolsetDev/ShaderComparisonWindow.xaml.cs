using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Be.Windows.Forms;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
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
    private static readonly SolidColorBrush HlslDiffBrush = new(Color.FromArgb(0x60, 0xFF, 0x66, 0x66));

    private readonly LineDiffColorizer _leftLineDiffColorizer = new();
    private readonly LineDiffColorizer _rightLineDiffColorizer = new();

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
        set
        {
            if (_leftDocument != null)
                _leftDocument.Changed -= OnDocumentTextChanged;
            if (SetProperty(ref _leftDocument, value) && _leftDocument != null)
                _leftDocument.Changed += OnDocumentTextChanged;
        }
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
        set
        {
            if (_rightDocument != null)
                _rightDocument.Changed -= OnDocumentTextChanged;
            if (SetProperty(ref _rightDocument, value) && _rightDocument != null)
                _rightDocument.Changed += OnDocumentTextChanged;
        }
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

    #region HLSL Match Properties

    private string _hlslMatchText = "";
    public string HlslMatchText
    {
        get => _hlslMatchText;
        set => SetProperty(ref _hlslMatchText, value);
    }

    private Brush _hlslMatchBrush = Brushes.Green;
    public Brush HlslMatchBrush
    {
        get => _hlslMatchBrush;
        set => SetProperty(ref _hlslMatchBrush, value);
    }

    private Visibility _hlslMatchVisibility = Visibility.Collapsed;
    public Visibility HlslMatchVisibility
    {
        get => _hlslMatchVisibility;
        set => SetProperty(ref _hlslMatchVisibility, value);
    }

    #endregion

    /// <summary>
    /// Hidden BinaryInterpreterWPF instances
    /// They need CurrentLoadedExport set to the ShaderCache export.
    /// </summary>
    private BinaryInterpreterWPF _leftBinInterp;
    private BinaryInterpreterWPF _rightBinInterp;

    /// <summary>
    /// Effective shader cache export used for parameter reading. When the material's shaders are in the
    /// local SeekFreeShaderCache this is the same as _leftShaderCacheExport/_rightShaderCacheExport;
    /// when they come from the ref shader cache it points into a temporary in-memory package.
    /// </summary>
    private ExportEntry _leftEffectiveShaderCacheExport;
    private ExportEntry _rightEffectiveShaderCacheExport;

    /// <summary>
    /// Temporary in-memory packages that hold a copy of the ref shaders for the currently selected material.
    /// Disposed whenever a new material is selected or the window is closed.
    /// </summary>
    private IMEPackage _leftTempRefPackage;
    private IMEPackage _rightTempRefPackage;

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

        LeftShaderTextEditor.TextArea.TextView.LineTransformers.Add(_leftLineDiffColorizer);
        RightShaderTextEditor.TextArea.TextView.LineTransformers.Add(_rightLineDiffColorizer);
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
        {
            // Clean up any temp ref package for this side when the material is cleared
            if (side == Side.Left)
            {
                _leftTempRefPackage?.Dispose();
                _leftTempRefPackage = null;
                _leftEffectiveShaderCacheExport = null;
            }
            else
            {
                _rightTempRefPackage?.Dispose();
                _rightTempRefPackage = null;
                _rightEffectiveShaderCacheExport = null;
            }

            UpdateHlslDiffHighlights();
            return;
        }

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
            IMEPackage tempPackage = null;
            ExportEntry tempShaderCacheExport = null;

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
                            VertexFactoryType = meshShaderMap.VertexFactoryType
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
                                    VertexFactoryType = meshShaderMap.VertexFactoryType
                                });
                            }
                        }

                        // Build a temporary in-memory package containing the ref shaders so that
                        // FindShaderParameterOffset can scan its binary for parameter data.
                        var staticParamSets = new HashSet<StaticParameterSet> { sps };
                        ShaderCache refShaderCache = ShaderCacheManipulator.GetRefShaders(package.Game, staticParamSets);
                        tempPackage = MEPackageHandler.CreateMemoryEmptyLevel("TempRefShaderCache", package.Game);
                        ShaderCacheManipulator.AddShadersToFile(tempPackage, refShaderCache);
                        tempShaderCacheExport = tempPackage.FindExport("SeekFreeShaderCache");
                    }
                }
                catch
                {
                    // RefShaderCache not available
                    tempPackage?.Dispose();
                    tempPackage = null;
                    tempShaderCacheExport = null;
                }
            }

            return (shaderList, tempShaderCacheExport, tempPackage);
        }).ContinueWithOnUIThread((Task<(List<TreeViewShader>, ExportEntry, IMEPackage)> prevTask) =>
        {
            if (prevTask.Exception is AggregateException ex)
            {
                IsBusy = false;
                MessageBox.Show(this, $"Error loading shaders:\n{ex.InnerException?.Message ?? ex.Message}");
                return;
            }

            var (shaderList, refCacheExport, tempPkg) = prevTask.Result;

            if (side == Side.Left)
            {
                _leftTempRefPackage?.Dispose();
                _leftTempRefPackage = tempPkg;
                // If we got a temp export (ref cache), use it; otherwise fall back to the local one
                _leftEffectiveShaderCacheExport = refCacheExport ?? _leftShaderCacheExport;
                if (_leftEffectiveShaderCacheExport != null)
                    SetCurrentLoadedExport(_leftBinInterp, _leftEffectiveShaderCacheExport);
            }
            else
            {
                _rightTempRefPackage?.Dispose();
                _rightTempRefPackage = tempPkg;
                _rightEffectiveShaderCacheExport = refCacheExport ?? _rightShaderCacheExport;
                if (_rightEffectiveShaderCacheExport != null)
                    SetCurrentLoadedExport(_rightBinInterp, _rightEffectiveShaderCacheExport);
            }

            shaders.ReplaceAll(shaderList);
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
            //var rightEntries = ShaderInfoReader.GetShaderInfo(rightBytecode, out int rightInstructions);

            var window = new ShaderInfoWindow(
                $"Left: {SelectedLeftShader.ShaderType}", leftEntries, leftInstructions,
                $"Right: {SelectedRightShader.ShaderType}", [], 32) //rightEntries, rightInstructions)
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
        var shaderCacheExport = side == Side.Left ? _leftEffectiveShaderCacheExport : _rightEffectiveShaderCacheExport;
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
            UpdateHlslDiffHighlights();
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
        UpdateHlslDiffHighlights();

        // When the left shader changes, auto-select the matching shader on the right side
        // if both panels are showing the same material (by name).
        if (side == Side.Left && shader != null &&
            SelectedLeftMaterial != null && SelectedRightMaterial != null &&
            string.Equals(SelectedLeftMaterial.ObjectName.Name, SelectedRightMaterial.ObjectName.Name, StringComparison.OrdinalIgnoreCase))
        {
            var matchingRight = RightShaders.FirstOrDefault(s => s.ShaderType == shader.ShaderType && s.VertexFactoryType == shader.VertexFactoryType);
            if (matchingRight != null && !ReferenceEquals(matchingRight, SelectedRightShader))
            {
                SelectedRightShader = matchingRight;
            } else
            {
                SelectedRightShader = null;
            }
        }
    }

    private void OnDocumentTextChanged(object sender, DocumentChangeEventArgs e)
    {
        UpdateHlslDiffHighlights();
    }

    private void UpdateHlslDiffHighlights()
    {
        _leftLineDiffColorizer.DiffLines.Clear();
        _rightLineDiffColorizer.DiffLines.Clear();

        var leftLines = LeftDocument?.Text?.Split(["\r\n", "\n"], StringSplitOptions.None);
        var rightLines = RightDocument?.Text?.Split(["\r\n", "\n"], StringSplitOptions.None);

        if (leftLines == null || rightLines == null || leftLines.Length == 0 || rightLines.Length == 0
            || string.IsNullOrEmpty(LeftDocument?.Text) || string.IsNullOrEmpty(RightDocument?.Text))
        {
            HlslMatchVisibility = Visibility.Collapsed;
            LeftShaderTextEditor.TextArea.TextView.Redraw();
            RightShaderTextEditor.TextArea.TextView.Redraw();
            return;
        }

        int maxLines = Math.Max(leftLines.Length, rightLines.Length);
        for (int i = 0; i < maxLines; i++)
        {
            string leftLine = i < leftLines.Length ? leftLines[i] : null;
            string rightLine = i < rightLines.Length ? rightLines[i] : null;

            bool leftIgnored = IsIgnoredDiffLine(leftLine);
            bool rightIgnored = IsIgnoredDiffLine(rightLine);

            if (leftLine == null)
            {
                if (!rightIgnored)
                    _rightLineDiffColorizer.DiffLines.Add(i + 1);
                continue;
            }

            if (rightLine == null)
            {
                if (!leftIgnored)
                    _leftLineDiffColorizer.DiffLines.Add(i + 1);
                continue;
            }

            if (leftIgnored || rightIgnored)
                continue;

            if (!string.Equals(leftLine, rightLine, StringComparison.Ordinal))
            {
                _leftLineDiffColorizer.DiffLines.Add(i + 1);
                _rightLineDiffColorizer.DiffLines.Add(i + 1);
            }
        }

        LeftShaderTextEditor.TextArea.TextView.Redraw();
        RightShaderTextEditor.TextArea.TextView.Redraw();

        bool hasDiffs = _leftLineDiffColorizer.DiffLines.Count > 0 || _rightLineDiffColorizer.DiffLines.Count > 0;
        HlslMatchVisibility = Visibility.Visible;
        if (hasDiffs)
        {
            HlslMatchText = "HLSL - DIFFERENT!";
            HlslMatchBrush = Brushes.Red;
        }
        else
        {
            HlslMatchText = "HLSL - identical";
            HlslMatchBrush = Brushes.Green;
        }
    }

    private static bool IsIgnoredDiffLine(string line)
    {
        if (line == null)
            return true;

        return line.TrimStart().StartsWith("//", StringComparison.Ordinal);
    }

    private sealed class LineDiffColorizer : DocumentColorizingTransformer
    {
        public HashSet<int> DiffLines { get; } = [];

        protected override void ColorizeLine(DocumentLine line)
        {
            if (!DiffLines.Contains(line.LineNumber))
                return;

            ChangeLinePart(line.Offset, line.EndOffset, element =>
            {
                element.TextRunProperties.SetBackgroundBrush(HlslDiffBrush);
            });
        }
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
        HlslDiffBrush.Freeze();
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
    /// <para>
    /// For non-leaf nodes (nodes that have children), a branch is only compared when at least
    /// one of its direct children has a header containing "NumBytes" and that value is greater
    /// than zero. Branches without a NumBytes child are structural/resource nodes that carry no
    /// scalar data; branches whose NumBytes equals zero hold garbage data and should be ignored.
    /// Leaf nodes (no children) are always compared directly.
    /// </para>
    /// </summary>
    private static bool CompareNodeLists(IList<BinInterpNode> leftNodes, IList<BinInterpNode> rightNodes)
    {
        bool hasDifferences = false;
        int maxCount = Math.Max(leftNodes.Count, rightNodes.Count);

        for (int i = 0; i < maxCount; i++)
        {
            if (i >= leftNodes.Count)
            {
                // Extra node on the right side only — only flag it if the branch has valid data
                var rightChildren = rightNodes[i].Items.OfType<BinInterpNode>().ToList();
                if (rightChildren.Count == 0 || (TryFindValidCount(rightNodes[i], rightChildren, out int nb) && nb > 0))
                {
                    MarkSubtree(rightNodes[i], DiffDarkRedBrush);
                    hasDifferences = true;
                }
                continue;
            }

            if (i >= rightNodes.Count)
            {
                // Extra node on the left side only — only flag it if the branch has valid data
                var leftChildren = leftNodes[i].Items.OfType<BinInterpNode>().ToList();
                if (leftChildren.Count == 0 || (TryFindValidCount(leftNodes[i], leftChildren, out int nb) && nb > 0))
                {
                    MarkSubtree(leftNodes[i], DiffDarkRedBrush);
                    hasDifferences = true;
                }
                continue;
            }

            var leftNode = leftNodes[i];
            var rightNode = rightNodes[i];

            var leftChildren2 = leftNode.Items.OfType<BinInterpNode>().ToList();
            var rightChildren2 = rightNode.Items.OfType<BinInterpNode>().ToList();

            // For non-leaf nodes apply the NumBytes filter before comparing children
            bool bothLeaves = leftChildren2.Count == 0 && rightChildren2.Count == 0;
            if (!bothLeaves)
            {
                bool leftHasCount = TryFindValidCount(leftNode, leftChildren2, out int leftCount);
                bool rightHasCount = TryFindValidCount(rightNode, rightChildren2, out int rightCount);

                // Count is 0 on either side (when a count node IS present) — uninitialised data, skip entirely
                if ((leftHasCount || rightHasCount) && (leftCount == 0 || rightCount == 0))
                    continue;
                // If neither side has a count child at this level, this is a structural container node —
                // fall through and recurse so that deeper NumBytes/NumResources nodes are still evaluated.
            }

            // Compare children recursively (highlights differences within the valid branch)
            bool childCountMismatch = leftChildren2.Count != rightChildren2.Count;
            bool childrenDiffer = CompareNodeLists(leftChildren2, rightChildren2);

            // Compare this node's own header
            bool headersDiffer = !string.Equals(leftNode.Header, rightNode.Header, StringComparison.Ordinal);
            if (headersDiffer && leftNode.Header.Contains("File offset") && rightNode.Header.Contains("File offset"))
            {
                headersDiffer = false;
            }
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
    /// Searches <paramref name="children"/> for the first node whose header contains the text
    /// "NumBytes" and attempts to parse its value from the " = &lt;value&gt;" portion of the header.
    /// Returns <see langword="true"/> and sets <paramref name="numBytes"/> when a parseable
    /// NumBytes node is found; returns <see langword="false"/> otherwise.
    /// </summary>
    private static bool TryFindNumBytes(IList<BinInterpNode> children, out int numBytes)
    {
        numBytes = 0;
        foreach (var child in children)
        {
            if (child.Header != null && child.Header.Contains("NumBytes", StringComparison.Ordinal))
            {
                int eqIdx = child.Header.IndexOf("NumBytes: ", StringComparison.Ordinal);
                if (eqIdx >= 0)
                {
                    string valueStr = child.Header[(eqIdx + 10)..];
                    int digitEnd = 0;
                    while (digitEnd < valueStr.Length && char.IsAsciiDigit(valueStr[digitEnd]))
                        digitEnd++;
                    if (digitEnd > 0 && int.TryParse(valueStr[..digitEnd], out numBytes))
                        return true;
                }
                return false; // NumBytes node found but value could not be parsed
            }
        }
        return false; // No NumBytes child found
    }

    /// <summary>
    /// Searches <paramref name="children"/> for the first node whose header contains the text
    /// "NumResources" and attempts to parse its value from the " = &lt;value&gt;" portion of the header.
    /// Returns <see langword="true"/> and sets <paramref name="numResources"/> when a parseable
    /// NumResources node is found; returns <see langword="false"/> otherwise.
    /// </summary>
    private static bool TryFindNumResources(IList<BinInterpNode> children, out int numResources)
    {
        numResources = 0;
        foreach (var child in children)
        {
            if (child.Header != null && child.Header.Contains("NumResources", StringComparison.Ordinal))
            {
                int eqIdx = child.Header.IndexOf("NumResources: ", StringComparison.Ordinal);
                if (eqIdx >= 0)
                {
                    string valueStr = child.Header[(eqIdx + 14)..];
                    int digitEnd = 0;
                    while (digitEnd < valueStr.Length && char.IsAsciiDigit(valueStr[digitEnd]))
                        digitEnd++;
                    if (digitEnd > 0 && int.TryParse(valueStr[..digitEnd], out numResources))
                        return true;
                }
                return false; // NumResources node found but value could not be parsed
            }
        }
        return false; // No NumResources child found
    }

    /// <summary>
    /// Dispatches to <see cref="TryFindNumResources"/> when <paramref name="parentNode"/>'s header
    /// indicates an <c>FShaderResourceParameter</c>, and to <see cref="TryFindNumBytes"/> otherwise
    /// (including <c>FShaderParameter</c>).
    /// </summary>
    private static bool TryFindValidCount(BinInterpNode parentNode, IList<BinInterpNode> children, out int count)
    {
        Debug.WriteLine(parentNode.Header);
        if (parentNode.Header != null && parentNode.Header.Contains("FShaderResourceParameter", StringComparison.Ordinal))
            return TryFindNumResources(children, out count);
        return TryFindNumBytes(children, out count);
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
        _leftTempRefPackage?.Dispose();
        _rightTempRefPackage?.Dispose();
        _leftBinInterp?.Dispose();
        _rightBinInterp?.Dispose();
        LeftHexBoxHost?.Dispose();
        RightHexBoxHost?.Dispose();
    }
}
