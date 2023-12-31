using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using DocumentFormat.OpenXml.EMMA;
using GongSolutions.Wpf.DragDrop;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.LiveLevelEditor.MatEd;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.LiveLevelEditor
{
    /// <summary>
    /// Interaction logic for MaterialEditor.xaml
    /// </summary>
    public partial class MaterialEditor : TrackingNotifyPropertyChangedWindowBase, IDropTarget
    {
        public MEGame Game { get; set; }

        /// <summary>
        /// Invoked to push our material to the game for viewing.
        /// </summary>
        private readonly Action<MemoryStream, string> LoadMaterialInGameDelegate;


        private ExportEntry _materialExport;
        public ExportEntry MaterialExport
        {
            get => _materialExport;
            set
            {
                SetProperty(ref _materialExport, value);
                UniformTextures.ClearEx();
                Expressions.ClearEx();
                LoadMaterialData();
            }
        }

        private void LoadMaterialData()
        {
            PackageCache cache = new PackageCache();
            if (MaterialExport.ClassName == "Material")
            {
                var matBin = ObjectBinary.From<Material>(MaterialExport);
                foreach (var texIdx in matBin.SM3MaterialResource.Uniform2DTextureExpressions)
                {
                    var tex = new MatEdTexture(MaterialExport.FileRef, texIdx.TextureIndex);
                    UniformTextures.Add(tex);
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
                        // Do it again with the parent
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
                        // Do it again with the parent
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

                }
                else if (exp.ClassName == "RcvClientEffect") // Todo: Fix name
                {
                    // Skip up to next higher
                }
                else if (exp.IsA("MaterialInstanceConstant"))
                {
                    var scalars = TextureParameter.GetTextureParameters(exp, true);
                    if (scalars == null)
                    {
                        // Do it again with the parent
                        return GetAllTextureParameters(GetMatParent(exp, cache), cache);
                    }

                    return scalars;
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
        public MaterialEditor(MEGame game, Action<MemoryStream, string> loadMaterialDelegate) : base("Material Editor LLE", true)
        {
            Game = game;
            LoadMaterialInGameDelegate = loadMaterialDelegate;
            LoadCommands();
            InitializeComponent();
        }

        private void LoadCommands()
        {

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
            // Rename for Memory Uniqueness. On saving to disk we will strip this off
            foreach (var exp in newPackage.Exports.Where(x => x.idxLink == 0))
            {
                exp.ObjectName = new NameReference(exp.ObjectName.Name + $"_LLEMATED_{GetRandomString(8)}", exp.ObjectName.Number);
            }
            MaterialExport = newentry as ExportEntry; // This will trigger load
        }

        public void SendToGame()
        {
            // Prepare for shipment
            var package = MaterialExport.FileRef.SaveToStream(true);
            LoadMaterialInGameDelegate(package, MaterialExport.InstancedFullPath);
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
        #endregion
    }

    public class MatEdTexture : NotifyPropertyChangedBase
    {
        private string _displayString;
        public string DisplayString { get => _displayString; set => SetProperty(ref _displayString, value); }

        private ExportEntry _textureExp;
        public ExportEntry TextureExp { get => _textureExp; set => SetProperty(ref _textureExp, value); }
        public ImportEntry TextureImp;

        public MatEdTexture(IMEPackage pcc, int texIdx)
        {
            if (texIdx == 0)
            {
                DisplayString = "Null";
                return;
            }
            ExportEntry tex = null;
            if (texIdx < 0)
            {
                TextureImp = pcc.GetImport(texIdx);
                var resolved = EntryImporter.ResolveImport(TextureImp, new PackageCache()); // Kinda slow
                if (resolved != null)
                {
                    DisplayString = $"{resolved.InstancedFullPath} ({resolved.FileRef.FileNameNoExtension}.pcc)";
                    TextureExp = resolved;
                }
                else
                {
                    DisplayString = $"{TextureImp.InstancedFullPath} (Failed to resolve)";
                }
            }
            else
            {
                TextureExp = pcc.GetUExport(texIdx);
                DisplayString = $"{TextureExp.InstancedFullPath}";
            }
        }

    }

    //public abstract class MatEdExpression
    //{
    //    public string ParameterName { get; set; }
    //    /// <summary>
    //    /// Stored as struct because we aren't going to be editing this
    //    /// </summary>
    //    public StructProperty ExpressionGuid { get; set; }
    //}

    //public class MatEdScalarExpression : MatEdExpression
    //{
    //    public float ParameterValue { get; set; }

    //    public static MatEdScalarExpression FromScalarParm(ScalarParameter parm)
    //    {
    //        var expr = new MatEdScalarExpression();
    //        expr.ParameterName = parm.ParameterName;
    //        expr.ParameterValue = parm.ParameterValue;
    //        expr.ExpressionGuid = parm.ExpressionGuid;
    //        return expr;
    //    }

    //}
}
