using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
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
using LegendaryExplorer.Tools.AssetViewer;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.UserControls.ExportLoaderControls.MaterialEditor;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using Xceed.Wpf.Toolkit;
using Image = LegendaryExplorerCore.Textures.Image;
using MessageBox = System.Windows.MessageBox;
using PixelFormat = LegendaryExplorerCore.Textures.PixelFormat;

namespace LegendaryExplorer.Tools.LiveLevelEditor.MatEd
{
    /// <summary>
    /// Interaction logic for MaterialEditorLLE.xaml
    /// </summary>
    public partial class MaterialEditorLLE : TrackingNotifyPropertyChangedWindowBase, IDropTarget, IBusyUIHost
    {
        #region Instancing
        private static readonly Dictionary<MEGame, MaterialEditorLLE> Instances = new();
        public static MaterialEditorLLE Instance(MEGame game)
        {
            if (!game.IsLEGame())
                throw new ArgumentException(@"Material Editor does not support this game!", nameof(game));

            return Instances.TryGetValue(game, out var matEd) ? matEd : null;
        }

        public static void LoadMaterial(ExportEntry export)
        {
            if (Instance(export.Game) != null)
            {
                //Instance(export.Game).Lo = export;
                //Instance(export.Game).LoadAssetViewerMap();
            }
            else
            {
                AssetViewerWindow avw = new AssetViewerWindow(export);
                avw.Show();
            }
        }

        #endregion

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
        
        public ICommand PreviewOnMeshCommand { get; set; }
        public ICommand SaveMaterialPackageCommand { get; set; }

        public MaterialEditorLLE(MEGame game, Action<IMEPackage, string> loadMaterialDelegate, Action<ScalarParameter> updateScalarDelegate, Action<VectorParameter> updateVectorDelegate) : base("Material Editor LLE", true)
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
            SaveMaterialPackageCommand = new GenericCommand(SaveMaterialPackage, () => MEELC.MatInfo != null);
        }

        public void LoadMaterialIntoEditor(ExportEntry otherMat)
        {
            var newPackage = MEPackageHandler.CreateMemoryEmptyPackage(@"LLEMaterialEditor.pcc", Game);
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
            if (newentry is ExportEntry exp)
            {
                MEELC.LoadExport(exp);
            }
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

        /// <summary>
        /// This makes it easier to code against
        /// </summary>
        private MaterialInfo MatInfo => MEELC.MatInfo;

        private void SerializeMaterialSettingsToPackage()
        {
            if (MatInfo.MaterialExport.ClassName == "Material")
            {
                if (MatInfo.Expressions.Any())
                {
                    // We need to convert this to a material instance constant
                    MEELC.CommitSettingsToMIC(MEELC.ConvertMaterialToInstance(MatInfo.MaterialExport));
                }
            }

            if (MatInfo.MaterialExport.IsA("MaterialInstanceConstant"))
            {
                MEELC.CommitSettingsToMIC(MatInfo.MaterialExport);
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



        private void MaterialEditor_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (PreloadMaterial != null)
            {
                LoadMaterialIntoEditor(PreloadMaterial);
                PreloadMaterial = null;
            }
        }
    }
}
