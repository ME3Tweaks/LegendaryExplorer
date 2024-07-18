using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Tools.LiveLevelEditor.MatEd;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Xceed.Wpf.Toolkit;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.MaterialEditor
{
    /// <summary>
    /// Interaction logic for MaterialEditorExportLoader.xaml
    /// </summary>
    public partial class MaterialEditorExportLoader : ExportLoaderControl
    {
        /// <summary>
        /// Used for initial loading of data
        /// </summary>
        private PackageCache initialPackageCache;

        private bool IsLoadingData;
        private MaterialInfo _matInfo;
        public MaterialInfo MatInfo
        {
            get => _matInfo;
            set
            {
                if (SetProperty(ref _matInfo, value) && value != null)
                {
                    IsLoadingData = true;
                    value.InitMaterialInfo(initialPackageCache);
                    IsLoadingData = false; // Texture loading can occur on background but it won't have any editor effects
                }
            }
        }

        private bool _updatesTakeEffectInstantly;
        public bool UpdatesTakeEffectInstantly
        {
            get => _updatesTakeEffectInstantly;
            set => SetProperty(ref _updatesTakeEffectInstantly, value);
        }

        private bool _hasUnsavedChanges;
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set => SetProperty(ref _hasUnsavedChanges, value);
        }

        private bool _materialHasNoShader;
        public bool MaterialHasNoShader
        {
            get => _materialHasNoShader;
            set => SetProperty(ref _materialHasNoShader, value);
        }

        /// <summary>
        /// Value is true after _Loaded is called. False after _Unloaded (which if in tab control, is called when different tab is selected)
        /// </summary>
        private bool ControlIsLoaded;

        public bool SupportsExpressionEditing => CurrentLoadedExport != null && CurrentLoadedExport.IsA("MaterialInstanceConstant");

        public GenericCommand SaveChangesCommand { get; set; }


        public MaterialEditorExportLoader() : base("MaterialEditor")
        {
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            SaveChangesCommand = new GenericCommand(SaveChanges);
        }

        private void SaveChanges()
        {
            if (CurrentLoadedExport != null)
            {
                if (CurrentLoadedExport.ClassName.CaseInsensitiveEquals("Material"))
                {
                    CommitSettingsToMaterial(CurrentLoadedExport);
                }
                else
                {
                    CommitSettingsToMIC(CurrentLoadedExport);
                }
            }
        }

        private void CommitSettingsToMaterial(ExportEntry export)
        {
            var mat = ObjectBinary.From<Material>(export);
            for (int i = 0; i < MatInfo.UniformTextures.Count; i++)
            {
                if (MatInfo.UniformTextures[i].TextureExp.FileRef != export.FileRef)
                {
                    // We must convert this to an import
                    if (export.FileRef.FindEntry(MatInfo.UniformTextures[i].TextureExp.MemoryFullPath, MatInfo.UniformTextures[i].TextureExp.ClassName) != null)
                    {
                        mat.SM3MaterialResource.UniformExpressionTextures[i] = export.FileRef.FindEntry(MatInfo.UniformTextures[i].TextureExp.MemoryFullPath, MatInfo.UniformTextures[i].TextureExp.ClassName).UIndex;
                        continue;
                    }
                    else
                    {
                        Debug.WriteLine("CONVERT PACKAGE REFERENCE!!");
                    }
                }

                mat.SM3MaterialResource.UniformExpressionTextures[i] = MatInfo.UniformTextures[i].TextureExp.UIndex;
            }
            export.WriteBinary(mat);
        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return !exportEntry.IsDefaultObject && exportEntry.IsA("MaterialInterface");
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            UnloadExport();
            CurrentLoadedExport = exportEntry;
            // If control is loaded and visible
            if (IsLoaded)
            {
                LoadData();
            }
        }

        public override void UnloadExport()
        {
            initialPackageCache = new PackageCache();
            MaterialHasNoShader = false;
            CurrentLoadedExport = null;
            RefreshBindings();
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                var elhw = new ExportLoaderHostedWindow(new MaterialEditorExportLoader(), CurrentLoadedExport);
                elhw.Title = GetPoppedOutTitle();
                elhw.Show();
            }
        }

        private string GetPoppedOutTitle()
        {
            return $"Material Editor - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}";
        }

        public override void Dispose()
        {
        }

        private void RefreshBindings()
        {
            OnPropertyChanged(nameof(SupportsExpressionEditing));
        }

        // Subscribe to these to be notified when a value has changed.
        public event EventHandler VectorValueChanged;
        public event EventHandler ScalarValueChanged;

        private void VectorR_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoadingData) return;
            if (sender is FrameworkElement fe && fe.DataContext is VectorParameter vp)
            {
                if (vp is VectorParameterMatEd vpme)
                    vpme.IsDefaultParameter = false; // Mark modified
                VectorValueChanged?.Invoke(vp, EventArgs.Empty);
            }
        }

        private void VectorG_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoadingData) return;
            if (sender is FrameworkElement fe && fe.DataContext is VectorParameter vp)
            {
                if (vp is VectorParameterMatEd vpme)
                    vpme.IsDefaultParameter = false; // Mark modified
                VectorValueChanged?.Invoke(vp, EventArgs.Empty);
            }
        }

        private void VectorB_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoadingData) return;
            if (sender is FrameworkElement fe && fe.DataContext is VectorParameter vp)
            {
                if (vp is VectorParameterMatEd vpme)
                    vpme.IsDefaultParameter = false; // Mark modified
                VectorValueChanged?.Invoke(vp, EventArgs.Empty);
            }
        }

        private void VectorA_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoadingData) return;
            if (sender is FrameworkElement fe && fe.DataContext is VectorParameter vp)
            {
                if (vp is VectorParameterMatEd vpme)
                    vpme.IsDefaultParameter = false; // Mark modified
                VectorValueChanged?.Invoke(vp, EventArgs.Empty);
            }
        }

        private void Scalar_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoadingData) return;
            if (sender is FrameworkElement fe && fe.DataContext is ScalarParameter sp)
            {
                if (sp is ScalarParameterMatEd spme)
                    spme.IsDefaultParameter = false; // Mark modified
                ScalarValueChanged?.Invoke(sp, EventArgs.Empty);
            }
        }

        private void VectorColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            if (IsLoadingData) return;
            if (sender is ColorPicker fe && fe.DataContext is VectorParameter vp && fe.SelectedColor != null)
            {
                IsLoadingData = true; // Suppress extra change notifications
                vp.ParameterValue.W = fe.SelectedColor.Value.R / 255f;
                vp.ParameterValue.X = fe.SelectedColor.Value.G / 255f;
                vp.ParameterValue.Y = fe.SelectedColor.Value.B / 255f;
                vp.ParameterValue.Z = fe.SelectedColor.Value.A / 255f;
                IsLoadingData = false;
                VectorValueChanged?.Invoke(vp, EventArgs.Empty);
            }
        }

        public void CommitSettingsToMIC(ExportEntry matInstConst)
        {
            var matInstConstProps = matInstConst.GetProperties();

            // We're going to be updating these so strip out any existing
            matInstConstProps.RemoveNamedProperty("ScalarParameterValues");
            matInstConstProps.RemoveNamedProperty("VectorParameterValues");
            matInstConstProps.RemoveNamedProperty("TextureParameterValues");

            ArrayProperty<StructProperty> scalarParameters = new ArrayProperty<StructProperty>("ScalarParameterValues");
            ArrayProperty<StructProperty> vectorParameters = new ArrayProperty<StructProperty>("VectorParameterValues");
            ArrayProperty<StructProperty> textureParameters = new ArrayProperty<StructProperty>("TextureParameterValues");

            // Write Scalars
            foreach (var expr in MatInfo.Expressions.OfType<ScalarParameter>())
            {
                if (expr is ScalarParameterMatEd spme && spme.IsDefaultParameter)
                    continue; // Do not write out non-edited default parameters
                scalarParameters.Add(expr.ToStruct());
            }

            // Write Vectors
            foreach (var expr in MatInfo.Expressions.OfType<VectorParameter>())
            {
                if (expr is VectorParameterMatEd spme && spme.IsDefaultParameter)
                    continue; // Do not write out non-edited default parameters
                vectorParameters.Add(expr.ToStruct());
            }

            // Write Textures
            foreach (var expr in MatInfo.Expressions.OfType<TextureParameterMatEd>())
            {
                if (expr is TextureParameterMatEd spme && spme.IsDefaultParameter)
                    continue; // Do not write out non-edited default parameters
                textureParameters.Add(expr.ToStruct());
            }

            if (scalarParameters.Any())
                matInstConstProps.Add(scalarParameters);
            if (vectorParameters.Any())
                matInstConstProps.Add(vectorParameters);
            if (textureParameters.Any())
                matInstConstProps.Add(textureParameters);

            matInstConst.WriteProperties(matInstConstProps);
        }


        public ExportEntry ConvertMaterialToInstance(ExportEntry matExp)
        {
            // Create the export
            var parent = matExp.ParentInstancedFullPath;
            var name = new NameReference(matExp.ObjectName.Name + "_matInst");

            int index = 1;
            var testName = $"{parent}.{name.Instanced}".TrimStart('.'); // remove . if parent was null
            while (matExp.FileRef.FindEntry(testName) != null)
            {
                index++;
                name = new NameReference(matExp.ObjectName.Name + $"_matInst{index}");
                testName = $"{parent}.{name}".TrimStart('.'); // remove . if parent was null
            }


            var matInstConst = ExportCreator.CreateExport(matExp.FileRef, name,
                "MaterialInstanceConstant", matExp.Parent, indexed: false);

            var matInstConstProps = matInstConst.GetProperties();
            var lightingParent = matExp.GetProperty<StructProperty>("LightingGuid");
            if (lightingParent != null)
            {
                lightingParent.Name = "ParentLightingGuid"; // we aren't writing to parent so this is fine
                matInstConstProps.AddOrReplaceProp(lightingParent);
            }

            matInstConstProps.AddOrReplaceProp(new ObjectProperty(matExp.UIndex, "Parent"));
            matInstConstProps.AddOrReplaceProp(CommonStructs.GuidProp(Guid.NewGuid(), "m_Guid")); // IDK if this is used but we're gonna do it anyways

            matInstConst.WriteProperties(matInstConstProps);
            return matInstConst;
        }

        private void MaterialEditor_Loaded(object sender, RoutedEventArgs e)
        {
            // We only check if an export was loaded so initial boot doesn't set this
            if (CurrentLoadedExport != null)
            {
                ControlIsLoaded = true;
                LoadData();
            }
        }

        private void MaterialEditor_Unloaded(object sender, RoutedEventArgs e)
        {
            ControlIsLoaded = false;
        }

        private void LoadData()
        {
            if (ControlIsLoaded && initialPackageCache != null)
            {
                MatInfo = new MaterialInfo() { MaterialExport = CurrentLoadedExport };
                MatInfo.InitMaterialInfo(initialPackageCache);
                RefreshBindings();
                Task.Run(() =>
                {
                    // Since this may take a slight bit of time we background thread it
                    // We also cache the CLE because if it changes while we are fetching result we don't want to apply it
                    // to a different export
                    var mat = CurrentLoadedExport;
                    var isBroken = ShaderCacheManipulator.IsMaterialBroken(mat);
                    if (mat == CurrentLoadedExport)
                    {
                        MaterialHasNoShader = isBroken;
                    }
                });
                initialPackageCache = null; // No longer needed
            }
        }

        private void GenerateMaterialInstanceConstant_Click(object sender, RoutedEventArgs e)
        {
            var newExport = ConvertMaterialToInstance(CurrentLoadedExport);
            var hostingWindow = Window.GetWindow(this);
            if (hostingWindow is PackageEditorWindow pew)
            {
                pew.GoToNumber(newExport.UIndex);
            }
            else if (hostingWindow is ExportLoaderHostedWindow elhw)
            {
                elhw.HostedControl.LoadExport(newExport);
                elhw.Title = GetPoppedOutTitle();
            }
        }
    }
}
