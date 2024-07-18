using System;
using System.Linq;
using System.Windows;
using LegendaryExplorer.Tools.LiveLevelEditor.MatEd;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Xceed.Wpf.Toolkit;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.MaterialEditor
{
    /// <summary>
    /// Interaction logic for MaterialEditorExportLoader.xaml
    /// </summary>
    public partial class MaterialEditorExportLoader : ExportLoaderControl
    {

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
                    value.InitMaterialInfo(new PackageCache());
                    IsLoadingData = false; // Texture loading can occur on background but it won't have any editor effects
                }
            }
        }

        public bool SupportsExpressionEditing => CurrentLoadedExport != null && CurrentLoadedExport.IsA("MaterialInstanceConstant");

        public MaterialEditorExportLoader() : base("MaterialEditorLLE")
        {
            InitializeComponent();
        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return !exportEntry.IsDefaultObject && exportEntry.IsA("MaterialInterface");
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
            MatInfo = new MaterialInfo() { MaterialExport = exportEntry };
            MatInfo.InitMaterialInfo(new PackageCache()); // Todo: Move this to when it becomes visible via OnLoaded to improve performance.
            RefreshBindings();
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
            RefreshBindings();
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                var elhw = new ExportLoaderHostedWindow(new MaterialEditorExportLoader(), CurrentLoadedExport)
                {
                    Title = $"Material Editor - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
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

        }

        private void GenerateMaterialInstanceConstant_Click(object sender, RoutedEventArgs e)
        {
            var newExport = ConvertMaterialToInstance(CurrentLoadedExport);
            if (Window.GetWindow(this) is PackageEditorWindow pew)
            {
                pew.GoToNumber(newExport.UIndex);
            }
        }
    }
}
