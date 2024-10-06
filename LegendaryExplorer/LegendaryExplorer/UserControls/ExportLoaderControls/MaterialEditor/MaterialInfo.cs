using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.LiveLevelEditor.MatEd;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.MaterialEditor
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

        /// <summary>
        /// Types of objects this material works on
        /// </summary>
        public ObservableCollectionExtended<string> WorksOn { get; } = new();

        public Guid HostingControlGuid { get; set; }

        #region Constructor and initialization

        public void InitMaterialInfo(PackageCache cache)
        {
            UniformTextures.ClearEx();
            Expressions.ClearEx();
            LoadMaterialData(MaterialExport, cache);
            var exprTemp = new List<ExpressionParameter>();
            exprTemp.AddRange(Expressions.OfType<ScalarParameter>().OrderBy(x => x.ParameterName));
            exprTemp.AddRange(Expressions.OfType<VectorParameter>().OrderBy(x => x.ParameterName));
            exprTemp.AddRange(Expressions.OfType<TextureParameter>().OrderBy(x => x.ParameterName));
            Expressions.ReplaceAll(exprTemp);
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
                    foreach (var expr in expressions.Select(x => x.ResolveToEntry(exp.FileRef)).Where(x => x != null && x.IsA("MaterialExpressionScalarParameter")).OfType<ExportEntry>())
                    {
                        var parmName = expr.GetProperty<NameProperty>("ParameterName");
                        if (parmName == null)
                        {
                            parmName = new NameProperty("None", "ParameterName"); // Yes, apparently it can be none, and bioware has a lot of these.
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
                    var scalars = ScalarParameter.GetScalarParameters(exp, true, () => new ScalarParameterMatEd());
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
                    foreach (var expr in expressions.Select(x => x.ResolveToEntry(exp.FileRef)).Where(x => x != null && x.IsA("MaterialExpressionVectorParameter")).OfType<ExportEntry>())
                    {
                        var parmName = expr.GetProperty<NameProperty>("ParameterName");
                        if (parmName == null)
                        {
                            parmName = new NameProperty("None", "ParameterName"); // Yes, apparently it can be none, and bioware has a lot of these.
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
                    var vectors = VectorParameter.GetVectorParameters(exp, true, () => new VectorParameterMatEd());
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
                    foreach (var expr in expressions.Select(x => x.ResolveToEntry(exp.FileRef)).Where(x => x != null && x.IsA("MaterialExpressionTextureSampleParameter")).OfType<ExportEntry>())
                    {
                        var parmName = expr.GetProperty<NameProperty>("ParameterName");
                        if (parmName == null)
                        {
                            parmName = new NameProperty("None", "ParameterName"); // Yes, apparently it can be none, and bioware has a lot of these.
                        }

                        // Technically this may allow duplicates by type. But I don't think that's the case.
                        // But who knows?
                        if (parameterList.All(x => x.ParameterName != parmName.Value.Instanced))
                        {
                            // If this expression is not being overridden
                            var param = TextureParameterMatEd.FromExpression(expr);
                            param.HostingControlGuid = HostingControlGuid;
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
                    var textures = TextureParameter.GetTextureParameters(exp, true,
                        () => new TextureParameterMatEd() { EditingPackage = MaterialExport.FileRef, HostingControlGuid = HostingControlGuid });
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
            if (material.IsA("Material"))
            {
                ReadMaterial(material, cache);
            }
            else if (material.ClassName == "RvrEffectsMaterialUser")
            {
                // Skip to parent
                LoadMaterialData(GetMatParent(material, cache), cache);
            }
            else if (MaterialExport.IsA("MaterialInstanceConstant"))
            {
                ReadMaterialInstanceConstant(material, cache);
            }
            else
            {
                Debugger.Break();
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
                    tex.EditingPackage = MaterialExport.FileRef;
                    tex.HostingControlGuid = HostingControlGuid;
                    UniformTextures.Add(tex);
                }
            }

            // Read-only: Expressions.
            List<ExpressionParameter> parameters = new List<ExpressionParameter>();
            GetAllScalarParameters(MaterialExport, false, cache, parameters);
            GetAllVectorParameters(MaterialExport, false, cache, parameters);
            GetAllTextureParameters(MaterialExport, false, cache, parameters);

            var props = material.GetProperties();
            foreach (var prop in props.OfType<BoolProperty>())
            {
                if (prop.Name.Name.StartsWith("bUsedWith", StringComparison.OrdinalIgnoreCase) && prop.Value)
                {
                    WorksOn.Add(prop.Name.Name.Substring(9));
                }
            }

            Expressions.ReplaceAll(parameters);
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
                var resolved = EntryImporter.ResolveImport(imp, cache, unsafeLoad: true, unsafeLoadDelegate: MaterialEdLoadOnlyUsefulExports);
                if (resolved != null)
                    return resolved;
                return null; // Could not resolve
            }

            if (entry is ExportEntry expP)
                return expP;
            return null;
        }

        public static bool MaterialEdLoadOnlyUsefulExports(ExportEntry arg)
        {
            if (arg.IsA("RvrEffectsMaterialUser") || arg.IsA("Material") || arg.IsA("Texture2D") || arg.ClassName.CaseInsensitiveEquals("TextureCube") || arg.IsA("MaterialExpression"))
                return true;

            return false;
        }
    }
}