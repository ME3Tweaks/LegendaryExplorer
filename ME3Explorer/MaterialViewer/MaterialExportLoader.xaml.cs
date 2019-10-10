using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.Interfaces;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using SharpDX.D3DCompiler;
using Path = System.IO.Path;

namespace ME3Explorer.MaterialViewer
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

        public MaterialExportLoader()
        {
            InitializeComponent();
            DataContext = this;
        }

        public ObservableCollectionExtended<TreeViewMeshShaderMap> MeshShaderMaps { get; } = new ObservableCollectionExtended<TreeViewMeshShaderMap>();

        public override bool CanParse(ExportEntry exportEntry) => !exportEntry.IsDefaultObject && exportEntry.Game != MEGame.UDK &&
                                                                  (exportEntry.ClassName == "Material" || exportEntry.IsOrInheritsFrom("MaterialInstance") &&
                                                                   exportEntry.GetProperty<BoolProperty>("bHasStaticPermutationResource"));

        public override void LoadExport(ExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
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
                    if (Pcc.Exports.FirstOrDefault(exp => exp.ClassName == "ShaderCache") is {} seekFreeShaderCacheExport)
                    {
                        var seekFreeShaderCache = ObjectBinary.From<ShaderCache>(seekFreeShaderCacheExport);
                        if (seekFreeShaderCache.MaterialShaderMaps.TryGetValue(sps, out MaterialShaderMap msm))
                        {
                            return GetMeshShaderMaps(msm, seekFreeShaderCache);
                        }
                    }

                    MaterialShaderMap msmFromGlobalCache = ShaderCacheReader.GetMaterialShaderMap(Pcc.Game, sps);
                    if (msmFromGlobalCache != null)
                    {
                        return GetMeshShaderMaps(msmFromGlobalCache);
                    }
                }
                catch (Exception)
                {
                    //
                }

                return null;
            }).ContinueWithOnUIThread(prevTask =>
            {
                MeshShaderMaps.ClearEx();
                if (prevTask.Result != null)
                {
                    MeshShaderMaps.AddRange(prevTask.Result);
                }
                IsBusy = false;
            });
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
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new BinaryInterpreterWPF(), CurrentLoadedExport)
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
                shaderDissasemblyTextBlock.Text = tvs.DissasembledShader;
            }
        }
    }

    public class TreeViewMeshShaderMap
    {
        public string VertexFactoryType { get; set; }
        public ObservableCollectionExtended<TreeViewShader> Shaders { get; } = new ObservableCollectionExtended<TreeViewShader>();
}

    public class TreeViewShader
    {
        public MEGame Game;
        public Guid Id { get; set; }
        public string ShaderType { get; set; }
        private string dissasembledShader;
        public string DissasembledShader
        {
            get => dissasembledShader ??= ShaderCacheReader.GetShaderDissasembly(Game, Id);
            set => dissasembledShader = value;
        }
    }
}
