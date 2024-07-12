using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GongSolutions.Wpf.DragDrop;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using Image = LegendaryExplorerCore.Textures.Image;
using MessageBox = System.Windows.MessageBox;
using PixelFormat = LegendaryExplorerCore.Textures.PixelFormat;

namespace LegendaryExplorer.Tools.LiveLevelEditor.MatEd
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
            SaveMaterialPackageCommand = new GenericCommand(SaveMaterialPackage, () => MatInfo != null);
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
            var idxLink = otherMat.idxLink;
            var package = ExportCreator.CreatePackageExport(otherMat.FileRef, "LLEMatEd");
            otherMat.idxLink = package.UIndex;
            EntryExporter.ExportExportToPackage(otherMat, newPackage, out var newentry, cache, rop);
            otherMat.idxLink = idxLink; // Restore the export
            MatInfo = new MaterialInfo() { MaterialExport = newentry as ExportEntry };
        }

        public void SendToGame()
        {
            // Prepare for shipment
            var package = MatInfo.MaterialExport.FileRef.SaveToStream(false); // Don't waste time compressing
            package.Position = 0;
            var newP = MEPackageHandler.OpenMEPackageFromStream(package, "LLEMaterialPackage.pcc");

            //PackageEditorWindow pe = new PackageEditorWindow();
            //pe.LoadPackage(newP);
            //pe.Show();
            //return;

            var matExp = newP.FindExport(MatInfo.MaterialExport.InstancedFullPath);
            // Rename for Memory Uniqueness when loaded into the game
            foreach (var exp in newP.Exports.Where(x => x.idxLink == 0))
            {
                exp.ObjectName = new NameReference(exp.ObjectName.Name + $"_LLEMATED_{GetRandomString(8)}", exp.ObjectName.Number);
            }

            LoadMaterialInGameDelegate(newP, matExp.InstancedFullPath);
        }

        public async void SaveMaterialPackage()
        {
            SerializeMaterialSettingsToPackage();

            string extension = ".pcc";
            var fileFilter = $"*{extension}|*{extension}";
            var d = new SaveFileDialog { Filter = fileFilter };
            if (d.ShowDialog() == true)
            {
                await MatInfo.MaterialExport.FileRef.SaveAsync(d.FileName);
                MessageBox.Show("Done");
            }
        }

        private void SerializeMaterialSettingsToPackage()
        {
            if (MatInfo.MaterialExport.ClassName == "Material")
            {
                if (MatInfo.Expressions.Any())
                {
                    // We need to convert this to a material instance constant
                    CommitSettingsToMIC(ConvertMaterialToInstance(MatInfo.MaterialExport));
                }
            }

            if (MatInfo.MaterialExport.IsA("MaterialInstanceConstant"))
            {
                CommitSettingsToMIC(MatInfo.MaterialExport);
            }

        }

        private ExportEntry ConvertMaterialToInstance(ExportEntry matExp)
        {
            // Create the export
            var matInstConst = ExportCreator.CreateExport(matExp.FileRef, new NameReference(matExp.ObjectName.Name + "_matInst"),
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

        private void CommitSettingsToMIC(ExportEntry matInstConst)
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
                if (vp is VectorParameterMatEd vpme)
                    vpme.IsDefaultParameter = false; // Mark modified
                SendVectorUpdate(vp);
            }
        }

        private void VectorG_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoadingData) return;
            if (sender is FrameworkElement fe && fe.DataContext is VectorParameter vp)
            {
                if (vp is VectorParameterMatEd vpme)
                    vpme.IsDefaultParameter = false; // Mark modified
                SendVectorUpdate(vp);
            }
        }

        private void VectorB_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoadingData) return;
            if (sender is FrameworkElement fe && fe.DataContext is VectorParameter vp)
            {
                if (vp is VectorParameterMatEd vpme)
                    vpme.IsDefaultParameter = false; // Mark modified
                SendVectorUpdate(vp);
            }
        }

        private void VectorA_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoadingData) return;
            if (sender is FrameworkElement fe && fe.DataContext is VectorParameter vp)
            {
                if (vp is VectorParameterMatEd vpme)
                    vpme.IsDefaultParameter = false; // Mark modified
                SendVectorUpdate(vp);
            }
        }

        private void Scalar_Changed(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoadingData) return;
            if (sender is FrameworkElement fe && fe.DataContext is ScalarParameter sp)
            {
                if (sp is ScalarParameterMatEd spme)
                    spme.IsDefaultParameter = false; // Mark modified
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

        public MatEdTexture(IMEPackage pcc, int texIdx, PackageCache cache)
        {
            MatEditorTextureLoader.InitTexture(this, pcc, texIdx, cache);
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
        public static void InitTexture(IMatEdTexture mt, IMEPackage pcc, int texIdx, PackageCache cache)
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
                    var resolved = EntryImporter.ResolveImport(mt.TextureImp, cache);
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



}
