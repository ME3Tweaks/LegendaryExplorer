using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using TerraFX.Interop.Windows;

namespace LegendaryExplorer.Tools.LiveLevelEditor.MatEd
{
    /// <summary>
    /// Contains a bunch of useful information about a specific instance of a material.
    /// </summary>
    public class MaterialInfo : NotifyPropertyChangedBase
    {
        private ExportEntry _baseMaterial;
        /// <summary>
        /// The base material for this instance of a material (e.g. the root material for a MaterialInstanceConstant). If this material is already a Material, it will be the same export.
        /// </summary>
        public ExportEntry BaseMaterial { get => _baseMaterial; set => SetProperty(ref _baseMaterial, value); }


        private ExportEntry _materialExport;
        /// <summary>
        /// The instance this <see cref="MaterialInfo"/> object is for
        /// </summary>
        public ExportEntry MaterialExport
        {
            get => _materialExport;
            set => SetProperty(ref _materialExport, value);
        }

        // Bindable collections for UI
        public ObservableCollectionExtended<MatEdTexture> UniformTextures { get; } = new();
        public ObservableCollectionExtended<ExpressionParameter> Expressions { get; } = new();

        #region Constructor and initialization

        public void InitMaterialInfo(PackageCache cache)
        {
            UniformTextures.ClearEx();
            Expressions.ClearEx();
            LoadMaterialData(MaterialExport, cache);
        }

        #endregion

        #region Reading parameters
        /// <summary>
        /// Gets all scalar parameter expressions that this material can use
        /// </summary>
        /// <param name="exp">Export we are parsing</param>
        /// <param name="baseOnly">If we should only read expressions on the base Material</param>
        /// <param name="cache">Cache for import resolution</param>
        /// <returns></returns>
        private void GetAllScalarParameters(ExportEntry exp, bool baseOnly, PackageCache cache, List<ExpressionParameter> parameterList)
        {
            if (exp != null)
            {
                if (exp.ClassName == "Material")
                {
                    // Read default expressions
                    var expressions = exp.GetProperty<ArrayProperty<ObjectProperty>>("Expressions");
                    if (expressions == null)
                        return;

                    // Read default expressions
                    foreach (var expr in expressions.Select(x => x.ResolveToEntry(exp.FileRef)).Where(x => x.IsA("MaterialExpressionScalarParameter")).OfType<ExportEntry>())
                    {
                        var parmName = expr.GetProperty<NameProperty>("ParameterName");
                        if (parmName == null)
                        {
                            continue; // If this sample has no parameter name we will not be able to configure it so just skip it.
                        }

                        // Technically this may allow duplicates by type. But I don't think that's the case.
                        // But who knows?
                        if (parameterList.All(x => x.ParameterName != parmName.Value.Instanced))
                        {
                            // If this expression is not being overridden
                            var param = ScalarParameterMatEd.FromExpression(expr);
                            parameterList.Add(param);
                        }
                    }
                }
                else if (exp.ClassName == "RvrEffectsMaterialUser")
                {
                    // Skip up to next higher
                    GetAllScalarParameters(GetMatParent(exp, cache), baseOnly, cache, parameterList);
                }
                else if (exp.IsA("MaterialInstanceConstant"))
                {
                    if (baseOnly)
                    {
                        // Only return scalars on the base material.
                        GetAllScalarParameters(GetMatParent(exp, cache), true, cache, parameterList);
                        return;
                    }
                    var scalars = ScalarParameter.GetScalarParameters(exp, true);
                    if (scalars == null)
                    {
                        // Do it again with the parent. We are not locally overridding
                        GetAllScalarParameters(GetMatParent(exp, cache), false, cache, parameterList);
                        return;
                    }
                    parameterList.AddRange(scalars);
                    GetAllScalarParameters(GetMatParent(exp, cache), true, cache, parameterList); // Now get base material expressions
                }
            }
        }

        private void GetAllVectorParameters(ExportEntry exp, bool baseOnly, PackageCache cache, List<ExpressionParameter> parameterList)
        {
            if (exp != null)
            {
                if (exp.ClassName == "Material")
                {
                    var expressions = exp.GetProperty<ArrayProperty<ObjectProperty>>("Expressions");
                    if (expressions == null)
                        return;

                    // Read default expressions
                    foreach (var expr in expressions.Select(x => x.ResolveToEntry(exp.FileRef)).Where(x => x.IsA("MaterialExpressionVectorParameter")).OfType<ExportEntry>())
                    {
                        var parmName = expr.GetProperty<NameProperty>("ParameterName");
                        if (parmName == null)
                        {
                            continue; // If this sample has no parameter name we will not be able to configure it so just skip it.
                        }

                        // Technically this may allow duplicates by type. But I don't think that's the case.
                        // But who knows?
                        if (parameterList.All(x => x.ParameterName != parmName.Value.Instanced))
                        {
                            // If this expression is not being overridden
                            var param = VectorParameterMatEd.FromExpression(expr);
                            parameterList.Add(param);
                        }
                    }
                }
                else if (exp.ClassName == "RvrEffectsMaterialUser")
                {
                    // Skip up to next higher
                    GetAllVectorParameters(GetMatParent(exp, cache), baseOnly, cache, parameterList);
                }
                else if (exp.IsA("MaterialInstanceConstant"))
                {
                    if (baseOnly)
                    {
                        // Only return scalars on the base material.
                        GetAllVectorParameters(GetMatParent(exp, cache), true, cache, parameterList);
                        return;
                    }
                    var vectors = VectorParameter.GetVectorParameters(exp, true);
                    if (vectors == null)
                    {
                        // Do it again with the parent. We are not locally overridding
                        GetAllVectorParameters(GetMatParent(exp, cache), false, cache, parameterList);
                        return;
                    }

                    parameterList.AddRange(vectors);
                    GetAllVectorParameters(GetMatParent(exp, cache), true, cache, parameterList); // Now get base material expressions
                }
            }
        }

        private void GetAllTextureParameters(ExportEntry exp, bool baseOnly, PackageCache cache, List<ExpressionParameter> parameterList)
        {
            if (exp != null)
            {
                if (exp.ClassName == "Material")
                {
                    var expressions = exp.GetProperty<ArrayProperty<ObjectProperty>>("Expressions");
                    if (expressions == null)
                        return;

                    // Read default expressions
                    foreach (var expr in expressions.Select(x => x.ResolveToEntry(exp.FileRef)).Where(x => x.IsA("MaterialExpressionTextureSampleParameter")).OfType<ExportEntry>())
                    {
                        var parmName = expr.GetProperty<NameProperty>("ParameterName");
                        if (parmName == null)
                        {
                            continue; // If this sample has no parameter name we will not be able to configure it so just skip it.
                        }

                        // Technically this may allow duplicates by type. But I don't think that's the case.
                        // But who knows?
                        if (parameterList.All(x => x.ParameterName != parmName.Value.Instanced))
                        {
                            // If this expression is not being overridden
                            var param = TextureParameterMatEd.FromExpression(expr);
                            param.EditingPackage = MaterialExport.FileRef;
                            param.LoadData(expr.FileRef, cache); // Initialize texture data
                            parameterList.Add(param);
                        }
                    }
                }
                else if (exp.ClassName == "RvrEffectsMaterialUser")
                {
                    // Skip up to next higher
                    GetAllTextureParameters(GetMatParent(exp, cache), baseOnly, cache, parameterList);
                }
                else if (exp.IsA("MaterialInstanceConstant"))
                {
                    if (baseOnly)
                    {
                        // Only return scalars on the base material.
                        GetAllTextureParameters(GetMatParent(exp, cache), true, cache, parameterList);
                        return;
                    }
                    var textures = TextureParameter.GetTextureParameters(exp, true, () => new TextureParameterMatEd() { EditingPackage = MaterialExport.FileRef});
                    if (textures == null)
                    {
                        // Do it again with the parent. We are not locally overridding
                        GetAllTextureParameters(GetMatParent(exp, cache), false, cache, parameterList);
                        return;
                    }

                    foreach (var t in textures.OfType<TextureParameterMatEd>())
                    {
                        t.LoadData(MaterialExport.FileRef, cache);
                    }
                    parameterList.AddRange(textures);
                    GetAllTextureParameters(GetMatParent(exp, cache), true, cache, parameterList); // Now get base material expressions

                }
            }
        }
        #endregion

        #region Reading export data
        /// <summary>
        /// Reads data about this material from the given export
        /// </summary>
        /// <param name="material"></param>
        /// <param name="cache"></param>
        public void LoadMaterialData(ExportEntry material, PackageCache cache)
        {
            if (material.ClassName == "Material")
            {
                ReadMaterial(material, cache);
            }
            else if (material.ClassName == "RvrEffectsMaterialUser")
            {
                // Skip to parent
            }
            else if (MaterialExport.IsA("MaterialInstanceConstant"))
            {
                ReadMaterialInstanceConstant(material, cache);
            }
        }

        /// <summary>
        /// Reads data about a material instance constant
        /// </summary>
        /// <param name="material"></param>
        /// <param name="cache"></param>
        private void ReadMaterialInstanceConstant(ExportEntry material, PackageCache cache)
        {
            // No scalars have been set. Technically, we should check only if we never encountered an empty property...
            List<ExpressionParameter> parameters = new List<ExpressionParameter>();
            GetAllScalarParameters(MaterialExport, false, cache, parameters);
            GetAllVectorParameters(MaterialExport, false, cache, parameters);
            GetAllTextureParameters(MaterialExport, false, cache, parameters);
            Expressions.ReplaceAll(parameters);
            LoadMaterialData(GetMatParent(material, cache), cache); // Load in parent data
        }

        /// <summary>
        /// Reads data about a Material (class)
        /// </summary>
        /// <param name="material"></param>
        /// <param name="cache"></param>
        private void ReadMaterial(ExportEntry material, PackageCache cache)
        {
            BaseMaterial = material;
            var matBin = ObjectBinary.From<Material>(material);
            if (matBin.SM3MaterialResource.UniformExpressionTextures != null)
            {
                foreach (var texIdx in matBin.SM3MaterialResource.UniformExpressionTextures)
                {
                    var tex = new MatEdTexture(material.FileRef, texIdx, cache);
                    UniformTextures.Add(tex);
                }
            }
        }
        #endregion

        /// <summary>
        /// Gets the parent of this material if one is set
        /// </summary>
        /// <param name="exp"></param>
        /// <param name="cache"></param>
        /// <returns></returns>
        private ExportEntry GetMatParent(ExportEntry exp, PackageCache cache)
        {
            // MaterialInstanceConstant
            var parent = exp.GetProperty<ObjectProperty>("Parent");
            if (parent != null && parent.Value != 0)
            {
                return InternalResolveParent(exp, parent.Value, cache);
            }

            // Game3. Maybe others but not sure.
            if (exp.ClassName == "RvrEffectsMaterialUser")
            {
                parent = exp.GetProperty<ObjectProperty>("m_pBaseMaterial");
                if (parent != null && parent.Value != 0)
                {
                    return InternalResolveParent(exp, parent.Value, cache);
                }
            }

            Debugger.Break();
            return null;
        }

        private ExportEntry InternalResolveParent(ExportEntry exp, int parentIdx, PackageCache cache)
        {
            var entry = exp.FileRef.GetEntry(parentIdx);
            if (entry is ImportEntry imp)
            {
                var resolved = EntryImporter.ResolveImport(imp, cache);
                if (resolved != null)
                    return resolved;
                return null; // Could not resolve
            }

            if (entry is ExportEntry expP)
                return expP;
            return null;
        }
    }
}
