using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GongSolutions.Wpf.DragDrop;
using Gu.Wpf.DataGrid2D;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.Tools.LiveLevelEditor.MatEd;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using Image = LegendaryExplorerCore.Textures.Image;
using PixelFormat = LegendaryExplorerCore.Textures.PixelFormat;
using TextureParameter = LegendaryExplorer.Tools.LiveLevelEditor.MatEd.TextureParameter;

namespace LegendaryExplorer.Tools.LiveLevelEditor
{
    /// <summary>
    /// Interaction logic for MaterialEditor.xaml
    /// </summary>
    public partial class MaterialEditor : TrackingNotifyPropertyChangedWindowBase, IDropTarget, IBusyUIHost
    {
        // Todo: Change to ExportLoader.
        public MEGame Game { get; set; }

        /// <summary>
        /// Invoked to push our material to the game for viewing.
        /// </summary>
        private readonly Action<IMEPackage, string> LoadMaterialInGameDelegate;

        /// <summary>
        /// Invoked to update a scalar parameter
        /// </summary>
        private readonly Action<ScalarParameter> SendScalarUpdate;

        /// <summary>
        /// Invoked to update a vector parameter
        /// </summary>
        private readonly Action<VectorParameter> SendVectorUpdate;

        // If data is being loaded into the editor
        private bool IsLoadingData;

        private ExportEntry _materialExport;
        public ExportEntry MaterialExport
        {
            get => _materialExport;
            set
            {
                IsLoadingData = true;
                SetProperty(ref _materialExport, value);
                UniformTextures.ClearEx();
                Expressions.ClearEx();
                LoadMaterialData();
                IsLoadingData = false; // Texture loading can occur on background but it won't have any editor effects
            }
        }

        private void LoadMaterialData()
        {
            PackageCache cache = new PackageCache();
            if (MaterialExport.ClassName == "Material")
            {
                var matBin = ObjectBinary.From<Material>(MaterialExport);
                if (matBin.SM3MaterialResource.UniformExpressionTextures != null)
                {
                    foreach (var texIdx in matBin.SM3MaterialResource.UniformExpressionTextures)
                    {
                        var tex = new MatEdTexture(MaterialExport.FileRef, texIdx);
                        UniformTextures.Add(tex);
                    }
                }
            }
            if (MaterialExport.IsA("MaterialInstanceConstant"))
            {
                Expressions.AddRange(GetAllScalarParameters(MaterialExport, cache));
                Expressions.AddRange(GetAllVectorParameters(MaterialExport, cache));
                Expressions.AddRange(GetAllTextureParameters(MaterialExport, cache));
            }
        }

        private IEnumerable<ScalarParameter> GetAllScalarParameters(ExportEntry exp, PackageCache cache)
        {
            if (exp != null)
            {
                if (exp.ClassName == "Material")
                {
                    // Read default expressions
                }
                else if (exp.ClassName == "RcvClientEffect") // Todo: Fix name
                {
                    // Skip up to next higher
                }
                else if (exp.IsA("MaterialInstanceConstant"))
                {
                    var scalars = ScalarParameter.GetScalarParameters(exp, true);
                    if (scalars == null)
                    {
                        // Do it again with the parent. We are not locally overridding
                        return GetAllScalarParameters(GetMatParent(exp, cache), cache);
                    }

                    return scalars;
                }
            }

            return new List<ScalarParameter>(); // Empty list.
        }

        private IEnumerable<VectorParameter> GetAllVectorParameters(ExportEntry exp, PackageCache cache)
        {
            if (exp != null)
            {
                if (exp.ClassName == "Material")
                {
                    // Read default expressions
                }
                else if (exp.ClassName == "RcvClientEffect") // Todo: Fix name
                {
                    // Skip up to next higher
                }
                else if (exp.IsA("MaterialInstanceConstant"))
                {
                    var vectors = VectorParameter.GetVectorParameters(exp, true);
                    if (vectors == null)
                    {
                        // Do it again with the parent. We are not locally overridding
                        return GetAllVectorParameters(GetMatParent(exp, cache), cache);
                    }

                    return vectors;
                }
            }

            return new List<VectorParameter>(); // Empty list.
        }

        private IEnumerable<TextureParameter> GetAllTextureParameters(ExportEntry exp, PackageCache cache)
        {
            if (exp != null)
            {
                if (exp.ClassName == "Material")
                {
                    // Read default expressions
                }
                else if (exp.ClassName == "RcvClientEffect") // Todo: Fix name
                {
                    // Skip up to next higher
                }
                else if (exp.IsA("MaterialInstanceConstant"))
                {
                    var textures = TextureParameter.GetTextureParameters(exp, true, () => new TextureParameterMatEd());
                    if (textures == null)
                    {
                        // Do it again with the parent. We are not locally overridding
                        return GetAllTextureParameters(GetMatParent(exp, cache), cache);
                    }

                    foreach (var t in textures.OfType<TextureParameterMatEd>())
                    {
                        t.LoadData(MaterialExport.FileRef);
                    }
                    return textures;
                }
            }

            return new List<TextureParameter>(); // Empty list.
        }

        private ExportEntry GetMatParent(ExportEntry exp, PackageCache cache)
        {
            // MaterialInstanceConstant
            var parent = exp.GetProperty<ObjectProperty>("Parent");
            if (parent != null && parent.Value != 0)
            {
                var entry = exp.FileRef.GetEntry(parent.Value);
                if (entry is ImportEntry imp)
                {
                    var resolved = EntryImporter.ResolveImport(imp, cache);
                    if (resolved != null)
                        return resolved;
                    return null; // Could not resolve
                }

                if (entry is ExportEntry expP)
                    return expP;
            }
            return null;
        }

        public ObservableCollectionExtended<MatEdTexture> UniformTextures { get; } = new();
        public ObservableCollectionExtended<ExpressionParameter> Expressions { get; } = new();
        public ICommand PreviewOnMeshCommand { get; set; }
        public ICommand SaveMaterialPackageCommand { get; set; }

        public MaterialEditor(MEGame game, Action<IMEPackage, string> loadMaterialDelegate, Action<ScalarParameter> updateScalarDelegate, Action<VectorParameter> updateVectorDelegate) : base("Material Editor LLE", true)
        {
            Game = game;
            LoadMaterialInGameDelegate = loadMaterialDelegate;
            SendScalarUpdate = updateScalarDelegate;
            SendVectorUpdate = updateVectorDelegate;
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {
            PreviewOnMeshCommand = new GenericCommand(SendToGame);
            SaveMaterialPackageCommand = new GenericCommand(SaveMaterialPackage, () => MaterialExport != null);
        }

        public void LoadMaterialIntoEditor(ExportEntry otherMat)
        {
            var newPackage = MEPackageHandler.CreateEmptyPackage(@"LLEMaterialEditor.pcc", Game);
            var cache = new PackageCache();
            var rop = new RelinkerOptionsPackage()
            {
                Cache = cache,
                PortImportsMemorySafe = true,
            };
            EntryExporter.ExportExportToPackage(otherMat, newPackage, out var newentry, cache, rop);
            MaterialExport = newentry as ExportEntry; // This will trigger load
        }

        public void SendToGame()
        {
            // Prepare for shipment
            var package = MaterialExport.FileRef.SaveToStream(false); // Don't waste time compressing
            package.Position = 0;
            var newP = MEPackageHandler.OpenMEPackageFromStream(package, "LLEMaterialPackage.pcc");
            var matExp = newP.FindExport(MaterialExport.InstancedFullPath);
            // Rename for Memory Uniqueness when loaded into the game
            foreach (var exp in newP.Exports.Where(x => x.idxLink == 0))
            {
                exp.ObjectName = new NameReference(exp.ObjectName.Name + $"_LLEMATED_{GetRandomString(8)}", exp.ObjectName.Number);
            }

            LoadMaterialInGameDelegate(newP, matExp.InstancedFullPath);
        }

        public async void SaveMaterialPackage()
        {
            string extension = ".pcc";
            var fileFilter = $"*{extension}|*{extension}";
            var d = new SaveFileDialog { Filter = fileFilter };
            if (d.ShowDialog() == true)
            {
                await MaterialExport.FileRef.SaveAsync(d.FileName);
                MessageBox.Show("Done");
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
                LoadMaterialIntoEditor(exp);
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
            byte[] tmpData = LegendaryExplorerCore.Textures.Image.convertRawToARGB(src, ref w, ref h, format, clearAlpha);
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
        public string BusyText { get => _busyText; set => SetProperty(ref _busyText, value); }

        /// <summary>
        /// Material to immediately load when window opens
        /// </summary>
        public ExportEntry PreloadMaterial { get; set; }

        private void VectorR_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoadingData) return;
            if (sender is FrameworkElement fe && fe.DataContext is VectorParameter vp)
            {
                SendVectorUpdate(vp);
            }
        }

        private void VectorG_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoadingData) return;
            if (sender is FrameworkElement fe && fe.DataContext is VectorParameter vp)
            {
                SendVectorUpdate(vp);
            }
        }

        private void VectorB_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoadingData) return;
            if (sender is FrameworkElement fe && fe.DataContext is VectorParameter vp)
            {
                SendVectorUpdate(vp);
            }
        }

        private void VectorA_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoadingData) return;
            if (sender is FrameworkElement fe && fe.DataContext is VectorParameter vp)
            {
                SendVectorUpdate(vp);
            }
        }
        
        private void Scalar_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoadingData) return;
            if (sender is FrameworkElement fe && fe.DataContext is ScalarParameter sp)
            {
                SendScalarUpdate(sp);
            }
        }

        private void MaterialEditor_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (PreloadMaterial != null)
            {
                LoadMaterialIntoEditor(PreloadMaterial);
                PreloadMaterial = null;
            }
        }
    }

    public class MatEdTexture : NotifyPropertyChangedBase, IMatEdTexture
    {
        private string _displayString;
        public string DisplayString { get => _displayString; set => SetProperty(ref _displayString, value); }

        private ExportEntry _textureExp;
        public ExportEntry TextureExp { get => _textureExp; set => SetProperty(ref _textureExp, value); }
        public ImportEntry TextureImp { get; set; }

        private BitmapSource _loadedBitMap;
        public BitmapSource LoadedBitMap { get => _loadedBitMap; set => SetProperty(ref _loadedBitMap, value); }

        public MatEdTexture(IMEPackage pcc, int texIdx)
        {
            MatEditorTextureLoader.InitTexture(this, pcc, texIdx);
        }

    }

    public interface IMatEdTexture
    {
        public string DisplayString { get; set; }
        public ExportEntry TextureExp { get; set; }
        public ImportEntry TextureImp { get; set; }
        public BitmapSource LoadedBitMap { get; set; }
    }

    internal static class MatEditorTextureLoader
    {
        public static void InitTexture(IMatEdTexture mt, IMEPackage pcc, int texIdx)
        {
            if (texIdx == 0)
            {
                mt.DisplayString = "Null";
                return;
            }

            ExportEntry tex = null;
            if (texIdx < 0)
            {
                mt.TextureImp = pcc.GetImport(texIdx);
                if (mt.TextureImp.IsTexture())
                {
                    var resolved = EntryImporter.ResolveImport(mt.TextureImp, new PackageCache()); // Kinda slow
                    if (resolved != null)
                    {
                        mt.DisplayString = $"{resolved.InstancedFullPath} ({resolved.FileRef.FileNameNoExtension}.pcc)";
                        mt.TextureExp = resolved;
                    }
                    else
                    {
                        mt.DisplayString = $"{mt.TextureImp.InstancedFullPath} (Failed to resolve)";
                    }
                }
            }
            else
            {
                var texE = pcc.GetUExport(texIdx);
                if (texE.IsTexture() || texE.ClassName == "TextureCube")
                {
                    mt.TextureExp = texE;
                    mt.DisplayString = $"{mt.TextureExp.InstancedFullPath}";
                }
            }

            LoadTexture(mt);
        }

        private static void LoadTexture(IMatEdTexture mt)
        {
            if (mt.TextureExp != null)
            {
                Task.Run(() =>
                {
                    var texExport = mt.TextureExp;
                    if (texExport.ClassName == "TextureCube")
                    {
                        // Correct to the first cube face so we have a preview to show
                        texExport = texExport.GetProperty<ObjectProperty>("FacePosX").ResolveToEntry(texExport.FileRef) as ExportEntry;
                    }
                    var t2d = new Texture2D(texExport);
                    var mip = t2d.GetMipWithDimension(64, 64);
                    if (mip != null)
                    {
                        byte[] data = Texture2D.GetTextureData(mip, mip.Export.Game);
                        var bitmap = MaterialEditor.ConvertRawToBitmapARGB(data, mip.width, mip.height, Image.getPixelFormatType(t2d.TextureFormat), true);
                        var memory = new MemoryStream(bitmap.Height * bitmap.Width * 4 + 54);
                        bitmap.Save(memory, ImageFormat.Bmp);
                        memory.Position = 0;
                        return (BitmapSource)new ImageSourceConverter().ConvertFrom(memory);
                    }
                    return null;
                }).ContinueWithOnUIThread(x =>
                {
                    if (x.Exception == null && x.Result != null)
                    {
                        mt.LoadedBitMap = x.Result;
                    }
                });
            }
        }
    }

    /// <summary>
    /// Material Editor Texture Parameter subclass
    /// </summary>
    class TextureParameterMatEd : TextureParameter, IMatEdTexture, INotifyPropertyChanged
    {
        private string _displayString;
        public string DisplayString { get => _displayString; set => SetProperty(ref _displayString, value); }

        private ExportEntry _textureExp;
        public ExportEntry TextureExp { get => _textureExp; set => SetProperty(ref _textureExp, value); }

        private BitmapSource _loadedBitMap;
        public BitmapSource LoadedBitMap { get => _loadedBitMap; set => SetProperty(ref _loadedBitMap, value); }
        public ImportEntry TextureImp { get; set; }

        public ICommand ReplaceTextureCommand { get; set; }

        public void LoadData(IMEPackage package)
        {
            if (ParameterValue != 0)
            {
                ReplaceTextureCommand = new GenericCommand(ReplaceTexture);
                MatEditorTextureLoader.InitTexture(this, package, ParameterValue);
            }
        }

        private void ReplaceTexture()
        {
            OpenFileDialog selectDDS = new OpenFileDialog
            {
                Title = "Select texture file",
#if WINDOWS
                Filter =
                    "All supported types|*.png;*.dds;*.tga|PNG files (*.png)|*.png|DDS files (*.dds)|*.dds|TGA files (*.tga)|*.tga",
#else
                Filter = "Texture (DDS PNG BMP TGA)|*.dds;*.png;*.bmp;*.tga",
#endif
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
            var result = selectDDS.ShowDialog();
            if (result.HasValue && result.Value)
            {
                Task.Run(() =>
                {
                    //Check aspect ratios
                    var props = TextureExp.GetProperties();
                    var listedWidth = props.GetProp<IntProperty>("SizeX")?.Value ?? 0;
                    var listedHeight = props.GetProp<IntProperty>("SizeY")?.Value ?? 0;

                    Image image;
                    try
                    {
#if WINDOWS
                        image = Image.LoadFromFile(selectDDS.FileName, LegendaryExplorerCore.Textures.PixelFormat.ARGB);
#else
                    image = new Image(selectDDS.FileName);
#endif
                    }
                    catch (TextureSizeNotPowerOf2Exception)
                    {
                        MessageBox.Show("The width and height of a texture must both be a power of 2\n" +
                                        "(1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192)", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Error: {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }

                    if (image.mipMaps[0].origWidth / image.mipMaps[0].origHeight != listedWidth / listedHeight)
                    {
                        MessageBox.Show("Cannot replace texture: Aspect ratios must be the same.");
                        return null;
                    }

                    var texture = new Texture2D(TextureExp);
                    return texture.Replace(image, props, isPackageStored: true);
                }).ContinueWithOnUIThread(x =>
                {
                    // IsBusy = false;
                    MatEditorTextureLoader.InitTexture(this, TextureExp.FileRef, ParameterValue);
                });
            }
        }

        #region PropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion
    }

    //public class MatEdTextureTemplateSelector : DataTemplateSelector
    //{
    //    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    //    {
    //        if (item is MatEdTexture) // Material version
    //            return (DataTemplate)((FrameworkElement)container).FindResource("matEdUniformTextureTemplate"); // Do not change this string as it is used in xaml too
    //        if (item is TextureParameterMatEd) // Expression version
    //            return (DataTemplate)((FrameworkElement)container).FindResource("matEdTextureTemplate"); // Do not change this string as it is used in xaml too
    //        if (item is ScalarParameter)
    //            return (DataTemplate)((FrameworkElement)container).FindResource("matEdScalarTemplate"); // Do not change this string as it is used in xaml too
    //        if (item is VectorParameter)
    //            return (DataTemplate)((FrameworkElement)container).FindResource("matEdVectorTemplate"); // Do not change this string as it is used in xaml too

    //        return base.SelectTemplate(item, container);
    //    }
    //}
}
