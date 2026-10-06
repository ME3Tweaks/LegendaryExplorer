using System;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShaderPermutationIdentity = LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler.ShaderPermutationIdentity;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class LE3MaterialShaderCompilerTests
    {
        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public void NativeResourceGateShortCircuitsAnUnimplementedFactory(int outcome)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NativeGateOrdering.pcc", MEGame.LE3);
            var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(
                "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", "FLocalVertexFactory", out var environment));
            var candidate = environment.CreateCandidate() with { Target = environment.Target with
                { VertexFactoryTypeName = "FUnimplementedVertexFactory", VertexFactoryShaderFileName = "UnimplementedVF" } };
            int calls = 0;
            var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(MinimalTemplate()), MaterialResource.Create(),
                [candidate], materialResourceShouldCache: (_, _) => { calls++; return outcome == 0 ? false : outcome == 1 ? true : null; });
            Assert.AreEqual(1, calls); Assert.AreEqual(0, plan.Jobs.Count);
            Assert.AreEqual(outcome == 0, plan.Success);
            Assert.AreEqual(outcome == 0 ? LE3MaterialShaderCompiler.ShaderCompileDisposition.Excluded :
                LE3MaterialShaderCompiler.ShaderCompileDisposition.Unresolved, plan.Decisions.Single().Disposition);
            Assert.AreEqual(outcome == 1 ? LE3MaterialShaderCompiler.ShaderCompileGate.VertexFactoryShouldCache :
                LE3MaterialShaderCompiler.ShaderCompileGate.MaterialResourceShouldCache, plan.Decisions.Single().Gate);
        }

        [TestMethod]
        public void NativeMaterialFactoryUsageFilterPrecedesResourceAndUnknownFactoryGates()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NativeUsageFilter.pcc", MEGame.LE3);
            var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(
                "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", "FLocalVertexFactory", out var environment));
            var original = environment.CreateCandidate();
            var candidate = original with
            {
                Target = environment.Target with { VertexFactoryTypeName = "FUnimplementedVertexFactory", VertexFactoryShaderFileName = "UnimplementedVF" },
                VertexFactoryContext = original.VertexFactoryContext with { UsedWithMaterials = false }
            };
            var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(MinimalTemplate()), MaterialResource.Create(),
                [candidate], materialResourceShouldCache: (_, _) => throw new InvalidOperationException("Native usage filter must short-circuit."));
            Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(0, plan.Jobs.Count);
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderCompileGate.VertexFactoryUsage, plan.Decisions.Single().Gate);
        }

        [TestMethod]
        public void EntirelyExcludedSubsetPreservesExplicitNonemptyFragmentBoundary()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NativeEmptyMeshMap.pcc", MEGame.LE3);
            var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(
                "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", "FLocalVertexFactory", out var environment));
            var translation = compiler.TranslateMaterialHlsl(MinimalTemplate());
            var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(),
                [environment.CreateCandidate()], materialResourceShouldCache: (_, _) => false);
            Assert.IsTrue(plan.Success); Assert.AreEqual(0, plan.Jobs.Count);
            var fragment = compiler.BuildShaderMapFragment(translation, plan, [], Guid.NewGuid(), "ExcludedSubset");
            Assert.IsFalse(fragment.Success); Assert.IsFalse(fragment.IsCompleteShaderSet); Assert.IsNull(fragment.Map);
            StringAssert.Contains(string.Join("; ", fragment.Errors), "nonempty explicit");
        }

        [TestMethod]
        [DataRow(false, 0)]
        [DataRow(false, 1)]
        [DataRow(false, 2)]
        [DataRow(true, 0)]
        [DataRow(true, 1)]
        [DataRow(true, 2)]
        public void MaterialResourceCacheCallbackPreservesTriStateAndInstanceProvenance(bool effectsOwner, int result)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResourceGate.pcc", MEGame.LE3);
            var owner = effectsOwner ? package.CreateExport("Owner", "RvrEffectsMaterialUser", indexed: false) : null;
            var material = package.CreateExport("Material", "Material", owner, indexed: false);
            var instance = package.CreateExport("Instance", "MaterialInstanceConstant", indexed: false);
            var input = new LE3MaterialShaderCompiler.ResolvedMaterialCompilationInput(material, requestedMaterialInstance: instance);
            var compiler = new LE3MaterialShaderCompiler(input);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(
                "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight", "FLocalVertexFactory", out var environment));
            int calls = 0;
            var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(MinimalTemplate()), MaterialResource.Create(),
                [environment.CreateCandidate()], materialResourceShouldCache: (resolved, target) =>
                {
                    calls++; Assert.AreSame(input, resolved); Assert.AreSame(instance, resolved.RequestedMaterialInstance);
                    Assert.AreSame(environment.Target, target);
                    return result == 0 ? null : result == 2;
                });
            Assert.AreEqual(1, calls); Assert.AreEqual(result != 0, plan.Success);
            Assert.AreEqual(result == 2 ? 1 : 0, plan.Jobs.Count);
            Assert.AreEqual(result == 1 ? 1 : 0, plan.ExcludedTargets.Count);
            if (result != 2)
            {
                Assert.AreEqual(LE3MaterialShaderCompiler.ShaderCompileGate.MaterialResourceShouldCache, plan.Decisions.Single().Gate);
                Assert.AreEqual(result == 0 ? LE3MaterialShaderCompiler.ShaderCompileDisposition.Unresolved :
                    LE3MaterialShaderCompiler.ShaderCompileDisposition.Excluded, plan.Decisions.Single().Disposition);
            }
            Assert.IsFalse(LE3MaterialShaderCompiler.ComparePlannedPermutations(plan, []).IsCompleteShaderSet);
        }

        [TestMethod]
        public void EffectsOwnedMaterialDefaultGateIsUnresolvedButShaderExclusionsShortCircuitIt()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3EffectsOwnerGate.pcc", MEGame.LE3);
            var owner = package.CreateExport("Owner", "RvrEffectsMaterialUser", indexed: false);
            var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", owner, indexed: false));
            var translation = compiler.TranslateMaterialHlsl(MinimalTemplate());
            var plan = compiler.BuildKnownSubsetCompilePlan(translation, MaterialResource.Create());
            Assert.IsFalse(plan.Success); Assert.AreEqual(0, plan.Jobs.Count);
            Assert.IsTrue(plan.Decisions.Any(d => d.Gate == LE3MaterialShaderCompiler.ShaderCompileGate.MaterialResourceShouldCache &&
                d.Disposition == LE3MaterialShaderCompiler.ShaderCompileDisposition.Unresolved));
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(
                "TLightPixelShaderFSphericalHarmonicLightPolicyFShadowTexturePolicy", "FLocalVertexFactory", out var excluded));
            var shortCircuit = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), [excluded.CreateCandidate()],
                materialResourceShouldCache: (_, _) => throw new InvalidOperationException("Must not run after native shader exclusion."));
            Assert.IsTrue(shortCircuit.Success); Assert.AreEqual(1, shortCircuit.ExcludedTargets.Count);
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderCompileGate.ShaderTypeShouldCache, shortCircuit.Decisions.Single().Gate);
            var resolved = compiler.BuildKnownSubsetCompilePlan(translation, MaterialResource.Create(), materialResourceShouldCache: (_, _) => true);
            Assert.IsTrue(resolved.Success, string.Join("; ", resolved.Errors)); Assert.IsTrue(resolved.Jobs.Count > 0);
        }

        [TestMethod]
        public void UnresolvedResourceGateWithholdsOtherwiseEligiblePartialJobs()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3PartialResourceGate.pcc", MEGame.LE3);
            var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(
                "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight", "FLocalVertexFactory", out var local));
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(
                "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight", "FGPUSkinVertexFactory", out var gpu));
            var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(MinimalTemplate()), MaterialResource.Create(),
                [local.CreateCandidate(), gpu.CreateCandidate()], materialResourceShouldCache: (_, target) =>
                    target.VertexFactoryTypeName == "FLocalVertexFactory" ? true : null);
            Assert.IsFalse(plan.Success); Assert.AreEqual(0, plan.Jobs.Count); Assert.AreEqual(1, plan.Errors.Count);
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderCompileDisposition.Eligible, plan.Decisions[0].Disposition);
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderCompileGate.MaterialResourceShouldCache, plan.Decisions[1].Gate);
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        public void IfAlwaysEmitsNativeTernaryLocalEvenForConstants(int width)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3IfConstants.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var node = package.CreateExport("If", "MaterialExpressionIf", material, indexed: false);
            var fields = new[] { "A", "B", "AGreaterThanB", "AEqualsB", "ALessThanB" };
            for (int i = 0; i < fields.Length; i++)
            {
                int size = i < 2 ? 1 : width;
                var input = package.CreateExport(fields[i], size == 1 ? "MaterialExpressionConstant" :
                    "MaterialExpressionConstant" + size + "Vector", material, indexed: false);
                for (int c = 0; c < size; c++) input.WriteProperty(new FloatProperty(i + c + 1, "RGBA"[c].ToString()));
                node.WriteProperty(Input(fields[i], input.UIndex));
            }
            // No threshold is part of LE3's callback. Authored extra metadata cannot change it.
            node.WriteProperty(new FloatProperty(100f, "EqualsThreshold"));
            material.WriteProperty(MaskedInput("Opacity", node.UIndex, r: true));
            material.WriteProperty(MaskedInput("WorldPositionOffset", node.UIndex, r: true));
            foreach (bool half in new[] { false, true })
            {
                string type = half ? "half" : "float";
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                string a = Literal(1, 1), b = Literal(1, 2);
                string ternary = $"(({a} >= {b}) ? ({a} > {b} ? {Literal(width, 3)} : {Literal(width, 4)}) : {Literal(width, 5)})";
                string resultType = type + (width == 1 ? "" : width.ToString());
                Assert.AreEqual($"\t{resultType} Local0 = {ternary};\r\n\treturn Local0.r", result.PropertyCode["Opacity"]);
                Assert.AreEqual($"\t{resultType} Local1 = {ternary};\r\n\treturn Local1.r", result.PropertyCode["WorldPositionOffset"]);
                Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
                string Literal(int size, int start) => type + (size == 1 ? "" : size.ToString()) + "(" +
                    string.Join(',', Enumerable.Range(start, size).Select(n => n + ".00000000")) + ")";
            }
        }

        [TestMethod]
        public void IfDynamicUniformAccessUsesForceCastBranchesThenComparisonsInBothStages()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3IfPacking.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var node = package.CreateExport("If", "MaterialExpressionIf", material, indexed: false);
            foreach (string field in new[] { "A", "B", "AGreaterThanB", "AEqualsB", "ALessThanB" })
            {
                var parameter = package.CreateExport(field, "MaterialExpressionScalarParameter", material, indexed: false);
                parameter.WriteProperty(new NameProperty(field, "ParameterName"));
                node.WriteProperty(Input(field, parameter.UIndex));
            }
            material.WriteProperty(Input("Opacity", node.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", node.UIndex));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            string[] order = ["AGreaterThanB", "AEqualsB", "ALessThanB", "B", "A"];
            CollectionAssert.AreEqual(order, result.UniformPixelScalarExpressions.Cast<MaterialUniformExpressionScalarParameter>()
                .Select(p => p.ParameterName.Name).ToArray());
            CollectionAssert.AreEqual(order, result.UniformVertexScalarExpressions.Cast<MaterialUniformExpressionScalarParameter>()
                .Select(p => p.ParameterName.Name).ToArray());
            for (int i = 0; i < 5; i++) Assert.AreSame(result.UniformPixelScalarExpressions[i], result.UniformVertexScalarExpressions[i]);
            foreach (string stage in new[] { "Pixel", "Vertex" })
            {
                string a = $"Uniform{stage}Scalars_1.x", b = $"Uniform{stage}Scalars_0.w";
                string ternary = $"(({a} >= {b}) ? ({a} > {b} ? Uniform{stage}Scalars_0.x : Uniform{stage}Scalars_0.y) : Uniform{stage}Scalars_0.z)";
                StringAssert.Contains(result.PropertyCode[stage == "Pixel" ? "Opacity" : "WorldPositionOffset"], ternary);
            }
        }

        [TestMethod]
        [DataRow("A")]
        [DataRow("B")]
        [DataRow("AGreaterThanB")]
        [DataRow("AEqualsB")]
        [DataRow("ALessThanB")]
        public void IfRequiresAllFiveConnectionsWithExactMissingInputErrors(string missing)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3IfMissing.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var constant = package.CreateExport("Constant", "MaterialExpressionConstant", material, indexed: false);
            var node = package.CreateExport("If", "MaterialExpressionIf", material, indexed: false);
            string[] fields = ["A", "B", "AGreaterThanB", "AEqualsB", "ALessThanB"];
            int firstMissing = Array.IndexOf(fields, missing);
            // Leave later inputs absent too, proving the native field-order diagnostic.
            for (int i = 0; i < firstMissing; i++) node.WriteProperty(Input(fields[i], constant.UIndex));
            material.WriteProperty(Input("Opacity", node.UIndex));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.MaterialHlsl);
            CollectionAssert.AreEqual(new[] { $"Missing If {missing} input" }, result.Errors.ToArray());
        }

        [TestMethod]
        [DataRow("A", false)]
        [DataRow("B", false)]
        [DataRow("A", true)]
        [DataRow("B", true)]
        public void IfRejectsVectorComparisonsButAcceptsNativeScalarMasks(string invalidField, bool mask)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3IfComparisonTypes.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var scalar = package.CreateExport("Scalar", "MaterialExpressionConstant", material, indexed: false);
            var vector = package.CreateExport("Vector", "MaterialExpressionConstant3Vector", material, indexed: false);
            var node = package.CreateExport("If", "MaterialExpressionIf", material, indexed: false);
            var branch = package.CreateExport("Branch", "MaterialExpressionVertexColor", material, indexed: false);
            foreach (string field in new[] { "A", "B" })
                node.WriteProperty(field == invalidField ? mask ? MaskedInput(field, vector.UIndex, r: true) : Input(field, vector.UIndex) : Input(field, scalar.UIndex));
            foreach (string field in new[] { "AGreaterThanB", "AEqualsB", "ALessThanB" }) node.WriteProperty(Input(field, branch.UIndex));
            material.WriteProperty(MaskedInput("Opacity", node.UIndex, r: true));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            if (mask)
            {
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                Assert.IsTrue(result.UsesVertexColor);
                StringAssert.Contains(result.PropertyCode["Opacity"], "float3(0.00000000,0.00000000,0.00000000).r");
            }
            else
            {
                Assert.IsFalse(result.Success);
                CollectionAssert.AreEqual(new[] { $"If input {invalidField} must be of type float." }, result.Errors.ToArray());
                Assert.IsFalse(result.UsesVertexColor);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void IfPromotesScalarBranchesWithoutAddingReplicationCasts(bool dynamicScalar)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3IfPromotion.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var compare = package.CreateExport("Compare", "MaterialExpressionConstant", material, indexed: false);
            var scalar = package.CreateExport("Scalar", dynamicScalar ? "MaterialExpressionScalarParameter" : "MaterialExpressionConstant", material, indexed: false);
            scalar.WriteProperty(new FloatProperty(2f, dynamicScalar ? "DefaultValue" : "R"));
            var vector = package.CreateExport("Vector", "MaterialExpressionConstant3Vector", material, indexed: false);
            var node = package.CreateExport("If", "MaterialExpressionIf", material, indexed: false);
            node.WriteProperty(Input("A", compare.UIndex));
            node.WriteProperty(Input("B", compare.UIndex));
            node.WriteProperty(Input("AGreaterThanB", scalar.UIndex));
            node.WriteProperty(Input("AEqualsB", vector.UIndex));
            node.WriteProperty(Input("ALessThanB", scalar.UIndex));
            material.WriteProperty(Input("DiffuseColor", node.UIndex));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            string branch = dynamicScalar ? "UniformPixelScalars_0.x" : "float(2.00000000)";
            Assert.AreEqual($"\tfloat3 Local0 = ((float(0.00000000) >= float(0.00000000)) ? (float(0.00000000) > float(0.00000000) ? {branch} : float3(0.00000000,0.00000000,0.00000000)) : {branch});\r\n\treturn Local0", result.PropertyCode["DiffuseColor"]);
            Assert.AreEqual(dynamicScalar ? 1 : 0, result.UniformPixelScalarExpressions.Count);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void IfRejectsDistinctVectorBranchesButPromotesMaskedScalarBranches(bool maskedScalar)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3IfBranchTypes.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var compare = package.CreateExport("Compare", "MaterialExpressionConstant", material, indexed: false);
            var two = package.CreateExport("Two", "MaterialExpressionConstant2Vector", material, indexed: false);
            var three = package.CreateExport("Three", "MaterialExpressionConstant3Vector", material, indexed: false);
            var node = package.CreateExport("If", "MaterialExpressionIf", material, indexed: false);
            foreach (string field in new[] { "A", "B", "AGreaterThanB" }) node.WriteProperty(Input(field, compare.UIndex));
            node.WriteProperty(maskedScalar ? MaskedInput("AEqualsB", two.UIndex, r: true) : Input("AEqualsB", two.UIndex));
            node.WriteProperty(Input("ALessThanB", three.UIndex));
            material.WriteProperty(Input("DiffuseColor", node.UIndex));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            if (maskedScalar)
            {
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                StringAssert.Contains(result.PropertyCode["DiffuseColor"], "float3 Local0");
                StringAssert.Contains(result.PropertyCode["DiffuseColor"], "float2(0.00000000,0.00000000).r");
            }
            else
            {
                Assert.IsFalse(result.Success);
                StringAssert.Contains(string.Join("; ", result.Errors), "If arithmetic between native numeric types");
                Assert.IsNull(result.MaterialHlsl);
            }
        }

        [TestMethod]
        [DataRow(1, 1)]
        [DataRow(1, 4)]
        [DataRow(2, 1)]
        [DataRow(2, 2)]
        [DataRow(3, 1)]
        [DataRow(3, 3)]
        [DataRow(4, 1)]
        [DataRow(4, 4)]
        public void FmodPreservesAWidthAndCoercesBForConstantAndMaskedPaths(int widthA, int widthB)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FmodWidths.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = Constant("A", widthA, 5);
            var b = Constant("B", widthB, 3);
            var node = package.CreateExport("Fmod", "MaterialExpressionFmod", material, indexed: false);
            node.WriteProperty(Input("A", a.UIndex));
            node.WriteProperty(Input("B", b.UIndex));
            material.WriteProperty(MaskedInput("Opacity", node.UIndex, r: true));
            material.WriteProperty(MaskedInput("WorldPositionOffset", node.UIndex, r: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex), new(node.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                string type = half ? "half" : "float";
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                string left = Literal(widthA, 5), right = Literal(widthB, 3);
                string bound = widthB == 1 && widthA > 1 ? type + widthA + "(" + string.Join(',', Enumerable.Repeat(right, widthA)) + ")" : right;
                Assert.AreEqual($"\treturn fmod({left},{bound}).r", result.PropertyCode["Opacity"]);
                Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
                node.WriteProperty(MaskedInput("A", a.UIndex, r: true, g: widthA > 1, b: widthA > 2, a: widthA > 3));
                var masked = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(masked.Success, string.Join("; ", masked.Errors));
                string valueType = type + (widthA == 1 ? "" : widthA.ToString());
                Assert.AreEqual($"\t{valueType} Local0 = fmod({left}.{"rgba"[..widthA]},{bound});\r\n\treturn Local0.r", masked.PropertyCode["Opacity"]);
                Assert.AreEqual($"\t{valueType} Local1 = fmod({left}.{"rgba"[..widthA]},{bound});\r\n\treturn Local1.r", masked.PropertyCode["WorldPositionOffset"]);
                node.WriteProperty(Input("A", a.UIndex));
                string Literal(int width, int start) => type + (width == 1 ? "" : width.ToString()) + "(" + string.Join(',', Enumerable.Range(start, width).Select(i => i + ".00000000")) + ")";
            }
            ExportEntry Constant(string name, int width, int start)
            {
                var value = package.CreateExport(name, width == 1 ? "MaterialExpressionConstant" : "MaterialExpressionConstant" + width + "Vector", material, indexed: false);
                for (int i = 0; i < width; i++) value.WriteProperty(new FloatProperty(start + i, "RGBA"[i].ToString()));
                return value;
            }
        }

        [TestMethod]
        [DataRow(2, 3)]
        [DataRow(3, 2)]
        [DataRow(3, 4)]
        [DataRow(4, 2)]
        public void FmodRejectsDistinctVectorWidthsEvenWhenBothUniform(int widthA, int widthB)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FmodCoercion.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = package.CreateExport("A", "MaterialExpressionConstant" + widthA + "Vector", material, indexed: false);
            var b = package.CreateExport("B", "MaterialExpressionConstant" + widthB + "Vector", material, indexed: false);
            var node = package.CreateExport("Fmod", "MaterialExpressionFmod", material, indexed: false);
            node.WriteProperty(Input("A", a.UIndex));
            node.WriteProperty(Input("B", b.UIndex));
            material.WriteProperty(MaskedInput("Opacity", node.UIndex, r: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex), new(node.UIndex)], "Expressions"));
            foreach (bool mask in new[] { false, true })
            {
                node.WriteProperty(mask ? MaskedInput("A", a.UIndex, r: true, g: true, b: widthA > 2, a: widthA > 3) : Input("A", a.UIndex));
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
                Assert.IsFalse(result.Success);
                Assert.IsNull(result.MaterialHlsl);
                StringAssert.Contains(string.Join("; ", result.Errors), $"Fmod coercion failed: Vector{widthB} -> Vector{widthA}");
            }
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void FmodUniformTreesKeepOriginalCpuChildrenDeduplicateAndRoundTrip(bool vectorA, bool vectorB)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FmodUniform.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = Parameter("A", vectorA, new LinearColor(5, -5, 7, 9));
            var b = Parameter("B", vectorB, new LinearColor(3, 4, 5, 6));
            var primary = Node("Primary", a, b);
            var duplicate = Node("Duplicate", a, b);
            var reversed = Node("Reversed", b, a);
            material.WriteProperty(MaskedInput("EmissiveColor", primary.UIndex, r: true));
            material.WriteProperty(MaskedInput("DiffuseColor", duplicate.UIndex, r: true));
            material.WriteProperty(MaskedInput("Opacity", reversed.UIndex, r: true));
            material.WriteProperty(MaskedInput("WorldPositionOffset", primary.UIndex, r: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex), new(primary.UIndex), new(duplicate.UIndex), new(reversed.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                Assert.AreEqual(result.PropertyCode["EmissiveColor"], result.PropertyCode["DiffuseColor"]);
                var results = vectorA ? result.UniformPixelVectorExpressions : result.UniformPixelScalarExpressions;
                var tree = results.OfType<MaterialUniformExpressionFmod>().First();
                var vertex = vectorA ? result.UniformVertexVectorExpressions : result.UniformVertexScalarExpressions;
                Assert.IsTrue(vertex.Any(p => ReferenceEquals(p, tree)));
                if (vectorA == vectorB)
                {
                    string[] names = results.Where(p => p is MaterialUniformExpressionScalarParameter or MaterialUniformExpressionVectorParameter)
                        .Select(p => p is MaterialUniformExpressionScalarParameter scalar ? scalar.ParameterName.Name : ((MaterialUniformExpressionVectorParameter)p).ParameterName.Name).ToArray();
                    CollectionAssert.AreEqual(new[] { "B", "A" }, names);
                    var reverseTree = results.OfType<MaterialUniformExpressionFmod>().Last();
                    Assert.AreNotSame(tree, reverseTree);
                    Assert.AreSame(tree.A, reverseTree.B);
                    Assert.AreSame(tree.B, reverseTree.A);
                }
                using var stream = new MemoryStream();
                tree.Serialize(new SerializingContainer(stream, package));
                stream.Position = 0;
                var reader = new SerializingContainer(stream, package, isLoading: true);
                var restored = (MaterialUniformExpressionFmod)MaterialUniformExpression.Create(reader);
                restored.Serialize(reader);
                Assert.AreEqual(stream.Length, stream.Position);
                Assert.AreEqual(tree.A.GetType(), restored.A.GetType());
                Assert.AreEqual(tree.B.GetType(), restored.B.GetType());
                var context = new UniformExpressionRenderContext(new(), new(), 0, 0, null);
                Check(new LinearColor(5, -5, 7, 9), new LinearColor(3, 4, 5, 6));
                if (vectorA) context.VectorParameterValues["A"] = new LinearColor(-8, 9, -10, 11);
                else context.ScalarParameterValues["A"] = -8;
                if (vectorB) context.VectorParameterValues["B"] = new LinearColor(5, 6, 7, 8);
                else context.ScalarParameterValues["B"] = 5;
                Check(new LinearColor(-8, 9, -10, 11), new LinearColor(5, 6, 7, 8));
                void Check(LinearColor left, LinearColor right)
                {
                    if (!vectorA) left = new LinearColor(left.R, 0, 0, 0);
                    if (!vectorB) right = new LinearColor(right.R, 0, 0, 0);
                    float[] expected = [left.R % right.R, left.G % right.G, left.B % right.B, left.A % right.A];
                    foreach (var expression in new[] { tree, restored })
                    {
                        LinearColor output = new(20, 21, 22, 23);
                        expression.GetNumberValue(context, ref output);
                        float[] actual = [output.R, output.G, output.B, output.A];
                        for (int i = 0; i < 4; i++)
                            if (float.IsNaN(expected[i])) Assert.IsTrue(float.IsNaN(actual[i]));
                            else Assert.AreEqual(expected[i], actual[i]);
                    }
                }
            }
            ExportEntry Parameter(string name, bool vector, LinearColor value)
            {
                var node = package.CreateExport(name, vector ? "MaterialExpressionVectorParameter" : "MaterialExpressionScalarParameter", material, indexed: false);
                node.WriteProperty(new NameProperty(name, "ParameterName"));
                if (vector) node.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
                {
                    new FloatProperty(value.R, "R"), new FloatProperty(value.G, "G"), new FloatProperty(value.B, "B"), new FloatProperty(value.A, "A")
                }, "DefaultValue", isImmutable: true));
                else node.WriteProperty(new FloatProperty(value.R, "DefaultValue"));
                return node;
            }
            ExportEntry Node(string name, ExportEntry left, ExportEntry right)
            {
                var node = package.CreateExport(name, "MaterialExpressionFmod", material, indexed: false);
                node.WriteProperty(Input("A", left.UIndex));
                node.WriteProperty(Input("B", right.UIndex));
                return node;
            }
        }

        [TestMethod]
        public void FmodChecksMissingAThenBButVisitsConnectedBThenA()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FmodOrder.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var constant = package.CreateExport("Constant", "MaterialExpressionConstant", material, indexed: false);
            var a = package.CreateExport("A", "MaterialExpressionAbs", material, indexed: false);
            var b = package.CreateExport("B", "MaterialExpressionFloor", material, indexed: false);
            a.WriteProperty(MaskedInput("Input", constant.UIndex, r: true));
            b.WriteProperty(MaskedInput("Input", constant.UIndex, r: true));
            var node = package.CreateExport("Fmod", "MaterialExpressionFmod", material, indexed: false);
            material.WriteProperty(Input("Opacity", node.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(constant.UIndex), new(a.UIndex), new(b.UIndex), new(node.UIndex)], "Expressions"));
            var missingA = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            CollectionAssert.Contains(missingA.Errors.ToArray(), "Missing Fmod input A");
            node.WriteProperty(Input("A", a.UIndex));
            var missingB = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            CollectionAssert.Contains(missingB.Errors.ToArray(), "Missing Fmod input B");
            node.WriteProperty(Input("B", b.UIndex));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual("\tfloat Local0 = floor(float(0.00000000).r);\r\n\tfloat Local1 = abs(float(0.00000000).r);\r\n\tfloat Local2 = fmod(Local1,Local0);\r\n\treturn Local2", result.PropertyCode["Opacity"]);
            node.WriteProperty(Input("B", node.UIndex));
            var cycle = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsFalse(cycle.Success);
            Assert.IsTrue(cycle.Errors.Any(e => e.Contains("cycle", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        public void NormalizeExpandsDotSquareRootDivideInsteadOfIntrinsic(int width)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NormalizeWidths.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = package.CreateExport("X", width == 1 ? "MaterialExpressionConstant" : "MaterialExpressionConstant" + width + "Vector", material, indexed: false);
            for (int i = 0; i < width; i++) x.WriteProperty(new FloatProperty(i + 2, "RGBA"[i].ToString()));
            var normalize = package.CreateExport("Normalize", "MaterialExpressionNormalize", material, indexed: false);
            normalize.WriteProperty(Input("VectorInput", x.UIndex));
            material.WriteProperty(MaskedInput("Opacity", normalize.UIndex, r: true));
            material.WriteProperty(MaskedInput("WorldPositionOffset", normalize.UIndex, r: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(normalize.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                string type = half ? "half" : "float";
                string valueType = type + (width == 1 ? "" : width.ToString());
                string literal = valueType + "(" + string.Join(',', Enumerable.Range(2, width).Select(i => i + ".00000000")) + ")";
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                Assert.AreEqual($"\treturn ({literal} / sqrt(dot({literal},{literal}))).r", result.PropertyCode["Opacity"]);
                Assert.AreEqual(result.PropertyCode["Opacity"], result.PropertyCode["WorldPositionOffset"]);
                Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
                normalize.WriteProperty(MaskedInput("VectorInput", x.UIndex, r: true, g: width > 1, b: width > 2, a: width > 3));
                var masked = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(masked.Success, string.Join("; ", masked.Errors));
                string input = literal + "." + "rgba"[..width];
                Assert.AreEqual($"\t{type} Local0 = dot({input},{input});\r\n\t{type} Local1 = sqrt(Local0);\r\n\t{valueType} Local2 = ({input} / Local1);\r\n\treturn Local2.r", masked.PropertyCode["Opacity"]);
                Assert.AreEqual($"\t{type} Local3 = dot({input},{input});\r\n\t{type} Local4 = sqrt(Local3);\r\n\t{valueType} Local5 = ({input} / Local4);\r\n\treturn Local5.r", masked.PropertyCode["WorldPositionOffset"]);
                normalize.WriteProperty(Input("VectorInput", x.UIndex));
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NormalizeInternsEquivalentAuthoredTreeAndRetainsNativeCpuLanes(bool vector)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NormalizeUniform.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = package.CreateExport("X", vector ? "MaterialExpressionVectorParameter" : "MaterialExpressionScalarParameter", material, indexed: false);
            x.WriteProperty(new NameProperty("X", "ParameterName"));
            if (vector) x.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
            {
                new FloatProperty(3, "R"), new FloatProperty(4, "G"), new FloatProperty(0, "B"), new FloatProperty(12, "A")
            }, "DefaultValue", isImmutable: true));
            else x.WriteProperty(new FloatProperty(-3, "DefaultValue"));
            var normalize = package.CreateExport("Normalize", "MaterialExpressionNormalize", material, indexed: false);
            normalize.WriteProperty(Input("VectorInput", x.UIndex));
            var dot = package.CreateExport("Dot", "MaterialExpressionDotProduct", material, indexed: false);
            dot.WriteProperty(Input("A", x.UIndex));
            dot.WriteProperty(Input("B", x.UIndex));
            var sqrt = package.CreateExport("Length", "MaterialExpressionSquareRoot", material, indexed: false);
            sqrt.WriteProperty(Input("Input", dot.UIndex));
            var divide = package.CreateExport("Divide", "MaterialExpressionDivide", material, indexed: false);
            divide.WriteProperty(Input("A", x.UIndex));
            divide.WriteProperty(Input("B", sqrt.UIndex));
            material.WriteProperty(Root("EmissiveColor", normalize));
            material.WriteProperty(Root("DiffuseColor", divide));
            material.WriteProperty(Root("WorldPositionOffset", normalize));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(normalize.UIndex), new(dot.UIndex), new(sqrt.UIndex), new(divide.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                Assert.AreEqual(result.PropertyCode["EmissiveColor"], result.PropertyCode["DiffuseColor"]);
                Assert.AreEqual(vector ? 2 : 4, result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(vector ? 2 : 0, result.UniformPixelVectorExpressions.Count);
                var results = vector ? result.UniformPixelVectorExpressions : result.UniformPixelScalarExpressions;
                var vertex = vector ? result.UniformVertexVectorExpressions : result.UniformVertexScalarExpressions;
                Assert.AreEqual(results.Count, vertex.Count);
                for (int i = 0; i < results.Count; i++) Assert.AreSame(results[i], vertex[i]);
                var tree = (MaterialUniformExpressionFoldedMath)results.Last();
                Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Div, tree.Op);
                Assert.AreSame(results[0], tree.A);
                var length = (MaterialUniformExpressionSquareRoot)tree.B;
                var squared = (MaterialUniformExpressionFoldedMath)length.X;
                Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Dot, squared.Op);
                Assert.AreSame(tree.A, squared.A);
                Assert.AreSame(tree.A, squared.B);
                using var stream = new MemoryStream();
                tree.Serialize(new SerializingContainer(stream, package));
                stream.Position = 0;
                var reader = new SerializingContainer(stream, package, isLoading: true);
                var restored = (MaterialUniformExpressionFoldedMath)MaterialUniformExpression.Create(reader);
                restored.Serialize(reader);
                Assert.AreEqual(stream.Length, stream.Position);
                var context = new UniformExpressionRenderContext(new(), new(), 0, 0, null);
                Check(vector ? new Vector4(3f / 13, 4f / 1e-5f, 0, 12) : new Vector4(-1, 0, 0, 0));
                if (vector) context.VectorParameterValues["X"] = new LinearColor(0, 0, 0, 0);
                else context.ScalarParameterValues["X"] = 0;
                Check(Vector4.Zero);
                if (!vector)
                {
                    context.ScalarParameterValues["X"] = 5e-6f;
                    Check(new Vector4(0.5f, 0, 0, 0));
                }
                void Check(Vector4 expected)
                {
                    foreach (var expression in new[] { tree, restored })
                    {
                        LinearColor output = default;
                        expression.GetNumberValue(context, ref output);
                        Assert.AreEqual(expected, (Vector4)output);
                    }
                }
            }
            StructProperty Root(string name, ExportEntry root) => vector ? MaskedInput(name, root.UIndex, r: true, g: true, b: true) : Input(name, root.UIndex);
        }

        [TestMethod]
        public void NormalizeRequiresVectorInputAndReportsCycles()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NormalizeErrors.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var normalize = package.CreateExport("Normalize", "MaterialExpressionNormalize", material, indexed: false);
            material.WriteProperty(Input("Opacity", normalize.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(normalize.UIndex)], "Expressions"));
            var missing = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsFalse(missing.Success);
            CollectionAssert.Contains(missing.Errors.ToArray(), "Missing Normalize input");
            normalize.WriteProperty(Input("VectorInput", normalize.UIndex));
            var cycle = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsFalse(cycle.Success);
            Assert.IsTrue(cycle.Errors.Any(e => e.Contains("cycle", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NormalizeTimeRetainsFrameDependencyAndBlackTemporaryAlpha(bool ignorePause)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NormalizeTime.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
            time.WriteProperty(new BoolProperty(ignorePause, "bIgnorePause"));
            var normalize = package.CreateExport("Normalize", "MaterialExpressionNormalize", material, indexed: false);
            normalize.WriteProperty(Input("VectorInput", time.UIndex));
            material.WriteProperty(Input("Opacity", normalize.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", normalize.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(time.UIndex), new(normalize.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.IsTrue(result.UsesTime);
            Assert.AreEqual(4, result.UniformPixelScalarExpressions.Count);
            Assert.AreEqual(4, result.UniformVertexScalarExpressions.Count);
            for (int i = 0; i < 4; i++) Assert.AreSame(result.UniformPixelScalarExpressions[i], result.UniformVertexScalarExpressions[i]);
            var tree = (MaterialUniformExpressionFoldedMath)result.UniformPixelScalarExpressions[3];
            Assert.IsFalse(tree.IsNotFrameDependent);
            using var stream = new MemoryStream();
            tree.Serialize(new SerializingContainer(stream, package));
            stream.Position = 0;
            var reader = new SerializingContainer(stream, package, isLoading: true);
            var restored = (MaterialUniformExpressionFoldedMath)MaterialUniformExpression.Create(reader);
            restored.Serialize(reader);
            Assert.AreEqual(stream.Length, stream.Position);
            foreach (float value in new[] { 0f, 2f, -3f })
            {
                var context = new UniformExpressionRenderContext(new(), new(), value, value + 5, null);
                float clock = ignorePause ? value + 5 : value;
                // Time writes only R into the Dot child's Black temporary, leaving A=1.
                var expected = new Vector4(clock / MathF.Sqrt(clock * clock + 1), 0, 0, 1);
                foreach (var expression in new[] { tree, restored })
                {
                    LinearColor output = default;
                    expression.GetNumberValue(context, ref output);
                    Assert.AreEqual(expected, (Vector4)output);
                }
            }
        }

        [TestMethod]
        public void NormalizeVisitsComputedInputOncePerPropertyAndDoesNotAddZeroGuard()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NormalizeComputedInput.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var color = package.CreateExport("Color", "MaterialExpressionVertexColor", material, indexed: false);
            var scale = package.CreateExport("Scale", "MaterialExpressionScalarParameter", material, indexed: false);
            scale.WriteProperty(new NameProperty("Scale", "ParameterName"));
            var multiply = package.CreateExport("Multiply", "MaterialExpressionMultiply", material, indexed: false);
            multiply.WriteProperty(MaskedInput("A", color.UIndex, r: true, g: true, b: true));
            multiply.WriteProperty(Input("B", scale.UIndex));
            var normalize = package.CreateExport("Normalize", "MaterialExpressionNormalize", material, indexed: false);
            normalize.WriteProperty(Input("VectorInput", multiply.UIndex));
            material.WriteProperty(Input("EmissiveColor", normalize.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", normalize.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(color.UIndex), new(scale.UIndex), new(multiply.UIndex), new(normalize.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                string type = half ? "half" : "float";
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                Assert.AreEqual($"\t{type}3 Local0 = (UniformPixelScalars_0.x * Parameters.VertexColor.rgb);\r\n\t{type} Local1 = dot(Local0,Local0);\r\n\t{type} Local2 = sqrt(Local1);\r\n\t{type}3 Local3 = (Local0 / Local2);\r\n\treturn Local3", result.PropertyCode["EmissiveColor"]);
                Assert.AreEqual($"\t{type}3 Local4 = (UniformVertexScalars_0.x * Parameters.VertexColor.rgb);\r\n\t{type} Local5 = dot(Local4,Local4);\r\n\t{type} Local6 = sqrt(Local5);\r\n\t{type}3 Local7 = (Local4 / Local6);\r\n\treturn Local7", result.PropertyCode["WorldPositionOffset"]);
            }
            var zero = package.CreateExport("Zero", "MaterialExpressionConstant", material, indexed: false);
            normalize.WriteProperty(Input("VectorInput", zero.UIndex));
            var zeros = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(zeros.Success, string.Join("; ", zeros.Errors));
            Assert.AreEqual("\treturn (float(0.00000000) / sqrt(dot(float(0.00000000),float(0.00000000))))", zeros.PropertyCode["EmissiveColor"]);
        }

        [TestMethod]
        public void GraphAnalysisFollowsResolvedStaticSwitchBranch()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShaderGraphTest.pcc", MEGame.LE3);
            var material = package.CreateExport("TestMaterial", "Material", indexed: false);
            var whenTrue = package.CreateExport("WhenTrue", "MaterialExpressionConstant", material, indexed: false);
            var whenFalse = package.CreateExport("WhenFalse", "MaterialExpressionConstant", material, indexed: false);
            var selector = package.CreateExport("Selector", "MaterialExpressionStaticSwitchParameter", material, indexed: false);

            selector.WriteProperty(new NameProperty("Feature", "ParameterName"));
            selector.WriteProperty(new BoolProperty(true, "DefaultValue"));
            selector.WriteProperty(Input("A", whenTrue.UIndex));
            selector.WriteProperty(Input("B", whenFalse.UIndex));
            material.WriteProperty(Input("EmissiveColor", selector.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>(
                [new ObjectProperty(whenTrue.UIndex), new ObjectProperty(whenFalse.UIndex), new ObjectProperty(selector.UIndex)],
                "Expressions"));

            var authored = new LE3MaterialShaderCompiler(material).AnalyzeGraph();
            Assert.IsTrue(authored.IsValid, string.Join("; ", authored.Errors));
            CollectionAssert.AreEquivalent(new[] { "WhenTrue", "Selector" },
                Array.ConvertAll(authored.ReachableExpressions.ToArray(), e => e.ObjectNameString));
            Assert.AreEqual("WhenFalse", authored.UnreachableExpressions[0].ObjectNameString);

            var parameters = (StaticParameterSet)Guid.Empty;
            parameters.StaticSwitchParameters =
            [
                new StaticParameterSet.StaticSwitchParameter { ParameterName = "Feature", Value = false, bOverride = true }
            ];
            var overridden = new LE3MaterialShaderCompiler(material, parameters).AnalyzeGraph();
            Assert.IsTrue(overridden.IsValid, string.Join("; ", overridden.Errors));
            CollectionAssert.AreEquivalent(new[] { "WhenFalse", "Selector" },
                Array.ConvertAll(overridden.ReachableExpressions.ToArray(), e => e.ObjectNameString));
            Assert.AreEqual("WhenTrue", overridden.UnreachableExpressions[0].ObjectNameString);

            parameters.StaticSwitchParameters[0].bOverride = false;
            var inherited = new LE3MaterialShaderCompiler(material, parameters).AnalyzeGraph();
            Assert.IsTrue(inherited.IsValid, string.Join("; ", inherited.Errors));
            CollectionAssert.AreEquivalent(new[] { "WhenFalse", "Selector" },
                Array.ConvertAll(inherited.ReachableExpressions.ToArray(), e => e.ObjectNameString));
            Assert.AreEqual("WhenTrue", inherited.UnreachableExpressions[0].ObjectNameString);
            parameters.StaticSwitchParameters = [];
            var unmatched = new LE3MaterialShaderCompiler(material, parameters).AnalyzeGraph();
            CollectionAssert.AreEquivalent(new[] { "WhenTrue", "Selector" },
                Array.ConvertAll(unmatched.ReachableExpressions.ToArray(), e => e.ObjectNameString));
        }

        [TestMethod]
        public void ResolvedSwitchMatchesFullNameAndSkipsInvalidInactiveMaskBranch()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResolvedSwitchNames.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var scalar = package.CreateExport("Scalar", "MaterialExpressionConstant", material, indexed: false);
            scalar.WriteProperty(new FloatProperty(0.5f, "R"));
            var mask = package.CreateExport("InactiveMask", "MaterialExpressionStaticComponentMaskParameter", material, indexed: false);
            mask.WriteProperty(Input("Input", mask.UIndex)); // Invalid cycle must remain inactive.
            var selector = package.CreateExport("Selector", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
            selector.WriteProperty(new NameProperty(new NameReference("ChooseMask", 2), "ParameterName"));
            selector.WriteProperty(new BoolProperty(true, "DefaultValue"));
            selector.WriteProperty(Input("A", mask.UIndex));
            selector.WriteProperty(Input("B", scalar.UIndex));
            material.WriteProperty(Input("Opacity", selector.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(scalar.UIndex), new(mask.UIndex), new(selector.UIndex)], "Expressions"));
            var parameters = (StaticParameterSet)Guid.Empty;
            parameters.StaticSwitchParameters =
            [
                new() { ParameterName = new NameReference("ChooseMask", 1), Value = true, bOverride = true },
                new() { ParameterName = new NameReference("CHOOSEMASK", 2), Value = false, bOverride = false, ExpressionGUID = Guid.NewGuid() },
                new() { ParameterName = new NameReference("ChooseMask", 2), Value = true, bOverride = true }
            ];
            var result = new LE3MaterialShaderCompiler(material, parameters).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual("\treturn float(0.50000000)", result.PropertyCode["Opacity"]);
            Assert.AreEqual(mask.UIndex, result.Graph.UnreachableExpressions.Single().UIndex);
            selector.WriteProperty(new NameProperty(new NameReference("ChooseMask", 3), "ParameterName"));
            var unmatched = new LE3MaterialShaderCompiler(material, parameters).AnalyzeGraph();
            Assert.IsFalse(unmatched.IsValid); // No full-name match selects authored A and finds the cycle.
        }

        [TestMethod]
        public void ResolvedCompilationInputsSnapshotParametersAndRejectMixedTranslations()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ResolvedPermutationTest.pcc", MEGame.LE3);
            var material = package.CreateExport("Parent", "Material", indexed: false);
            var instance = package.CreateExport("RequestedInstance", "MaterialInstanceConstant", indexed: false);
            instance.WriteProperty(new ObjectProperty(material.UIndex, "Parent"));
            var parameters = (StaticParameterSet)Guid.NewGuid();
            parameters.StaticSwitchParameters = [new() { ParameterName = "Feature", Value = false, bOverride = true }];
            parameters.StaticComponentMaskParameters = [new() { ParameterName = "Mask", R = true, A = true, bOverride = true }];
            parameters.NormalParameters = [new() { ParameterName = "Normal", CompressionSettings = 8, bOverride = true }];
            parameters.TerrainWeightParameters = [new() { ParameterName = "Weight", WeightmapIndex = 3, bOverride = true }];
            var input = new LE3MaterialShaderCompiler.ResolvedMaterialCompilationInput(material, parameters, instance);
            parameters.StaticSwitchParameters[0].Value = true;
            parameters.StaticComponentMaskParameters[0].R = false;
            parameters.NormalParameters[0].CompressionSettings = 1;
            parameters.TerrainWeightParameters[0].WeightmapIndex = 0;
            var snapshot = input.GetStaticParametersSnapshot();
            Assert.IsFalse(snapshot.StaticSwitchParameters[0].Value);
            Assert.IsTrue(snapshot.StaticSwitchParameters[0].bOverride);
            Assert.IsTrue(snapshot.StaticComponentMaskParameters[0].R);
            Assert.AreEqual((byte)8, snapshot.NormalParameters[0].CompressionSettings);
            Assert.AreEqual(3, snapshot.TerrainWeightParameters[0].WeightmapIndex);
            snapshot.StaticSwitchParameters[0].Value = true;
            Assert.IsFalse(input.GetStaticParametersSnapshot().StaticSwitchParameters[0].Value);
            Assert.AreSame(instance, input.RequestedMaterial);
            Assert.AreSame(material, input.BaseMaterial);
            var compiler = new LE3MaterialShaderCompiler(input);
            var translation = compiler.TranslateMaterialHlsl(MinimalTemplate());
            Assert.AreSame(input, translation.CompilationInput);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(
                "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight", "FLocalVertexFactory", out var environment));
            var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), [environment.CreateCandidate()]);
            Assert.IsTrue(plan.Success);
            Assert.AreSame(input, plan.CompilationInput);
            Assert.AreSame(input, plan.Jobs.Single().CompilationInput);
            // Same base export is not enough: another compiler may represent a different static permutation.
            var other = new LE3MaterialShaderCompiler(material, parameters);
            Assert.ThrowsExactly<ArgumentException>(() => other.BuildProvisionalCompileJob(translation,
                MaterialResource.Create(), environment.Target));
            Assert.ThrowsExactly<ArgumentException>(() => other.BuildProvisionalMaterialMacros(MaterialResource.Create(), translation));
            Assert.ThrowsExactly<ArgumentException>(() => new LE3MaterialShaderCompiler.ResolvedMaterialCompilationInput(material,
                requestedMaterialInstance: material));
        }

        [TestMethod]
        public void VertexColorUsageAndMacrosFollowSelectedPermutationNotStaleResource()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3VertexColorPermutationTest.pcc", MEGame.LE3);
            var material = package.CreateExport("Parent", "Material", indexed: false);
            var vertexColor = package.CreateExport("Color", "MaterialExpressionVertexColor", material, indexed: false);
            var constant = package.CreateExport("Constant", "MaterialExpressionConstant3Vector", material, indexed: false);
            var selector = package.CreateExport("Switch", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
            selector.WriteProperty(new NameProperty("UseColor", "ParameterName"));
            selector.WriteProperty(new BoolProperty(true, "DefaultValue"));
            selector.WriteProperty(MaskedInput("A", vertexColor.UIndex, r: true, g: true, b: true));
            selector.WriteProperty(Input("B", constant.UIndex));
            material.WriteProperty(Input("EmissiveColor", selector.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(vertexColor.UIndex), new(constant.UIndex), new(selector.UIndex)], "Expressions"));
            var compiler = new LE3MaterialShaderCompiler(material);
            var translation = compiler.TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
            Assert.IsTrue(translation.UsesVertexColor);
            Assert.AreEqual("\treturn Parameters.VertexColor.rgb", translation.PropertyCode["EmissiveColor"]);
            Assert.AreEqual("1", compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), translation)["MATERIAL_USES_VERTEX_COLOR"]);
            var parameters = (StaticParameterSet)Guid.Empty;
            parameters.StaticSwitchParameters = [new() { ParameterName = "UseColor", Value = false, bOverride = true }];
            var noColorCompiler = new LE3MaterialShaderCompiler(material, parameters);
            parameters.StaticSwitchParameters[0].Value = true; // constructor snapshot must remain false.
            var noColor = noColorCompiler.TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(noColor.Success, string.Join("; ", noColor.Errors));
            Assert.IsFalse(noColor.UsesVertexColor);
            var staleResource = MaterialResource.Create();
            staleResource.bUsesVertexColor = true;
            Assert.IsFalse(noColorCompiler.BuildProvisionalMaterialMacros(staleResource, noColor).ContainsKey("MATERIAL_USES_VERTEX_COLOR"));
            Assert.IsFalse(noColorCompiler.BuildProvisionalMaterialMacros(staleResource, noColor.Graph).ContainsKey("MATERIAL_USES_VERTEX_COLOR"));
            // The translator primitive also accepts vertex-stage usage.
            material.WriteProperty(new StructProperty("ColorMaterialInput", new PropertyCollection(), "EmissiveColor"));
            material.WriteProperty(MaskedInput("WorldPositionOffset", vertexColor.UIndex, r: true, g: true, b: true));
            var vertexStage = compiler.TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(vertexStage.Success, string.Join("; ", vertexStage.Errors));
            Assert.IsTrue(vertexStage.UsesVertexColor);
            Assert.AreEqual("\treturn Parameters.VertexColor.rgb", vertexStage.PropertyCode["WorldPositionOffset"]);
        }

        [TestMethod]
        public void MaterialEnvironmentReadsInheritedRawFlagsAndPositiveUnclampedWounds()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MaterialEnvironmentFlagsTest.pcc", MEGame.LE3);
            var parent = package.CreateExport("Parent", "Material", indexed: false);
            parent.WriteProperty(new IntProperty(2, "nNumWounds"));
            parent.WriteProperty(new BoolProperty(true, "bUsedWithGammaCorrection"));
            parent.WriteProperty(new BoolProperty(true, "TwoSidedSeparatePass"));
            var material = package.CreateExport("Child", "Material", indexed: false);
            material.Archetype = parent;
            var compiler = new LE3MaterialShaderCompiler(material);
            var resource = MaterialResource.Create();
            var macros = compiler.BuildProvisionalMaterialMacros(resource, compiler.AnalyzeGraph());
            Assert.AreEqual("2", macros["NUM_WOUNDS"]);
            Assert.AreEqual("1", macros["MATERIAL_USE_GAMMA_CORRECTION"]);
            Assert.AreEqual("1", macros["MATERIAL_TWOSIDED_SEPARATE_PASS"]);
            Assert.IsFalse(macros.ContainsKey("MATERIAL_TWOSIDED"));
            foreach (string blend in new[] { "BLEND_Opaque", "BLEND_Masked", "BLEND_Translucent", "BLEND_Additive",
                "BLEND_Modulate", "BLEND_SoftMasked", "BLEND_AlphaComposite" })
            {
                material.WriteProperty(new EnumProperty(blend, "EBlendMode", MEGame.LE3, "BlendMode"));
                macros = compiler.BuildProvisionalMaterialMacros(resource, compiler.AnalyzeGraph());
                Assert.AreEqual("1", macros["MATERIAL_TWOSIDED_SEPARATE_PASS"], blend);
            }
            foreach (int wounds in new[] { -1, 0, 1, 2, 3, int.MaxValue })
            {
                material.WriteProperty(new IntProperty(wounds, "nNumWounds"));
                macros = compiler.BuildProvisionalMaterialMacros(resource, compiler.AnalyzeGraph());
                Assert.AreEqual(wounds > 0, macros.ContainsKey("NUM_WOUNDS"));
                if (wounds > 0) Assert.AreEqual(wounds.ToString(System.Globalization.CultureInfo.InvariantCulture), macros["NUM_WOUNDS"]);
            }
            material.WriteProperty(new BoolProperty(false, "TwoSidedSeparatePass"));
            material.WriteProperty(new BoolProperty(false, "bUsedWithGammaCorrection"));
            material.WriteProperty(new IntProperty(0, "nNumWounds"));
            material.WriteProperty(new BoolProperty(true, "TwoSided"));
            macros = compiler.BuildProvisionalMaterialMacros(resource, compiler.AnalyzeGraph());
            Assert.AreEqual("1", macros["MATERIAL_TWOSIDED"]);
            Assert.IsFalse(macros.ContainsKey("MATERIAL_TWOSIDED_SEPARATE_PASS"));
            Assert.IsFalse(macros.ContainsKey("MATERIAL_USE_GAMMA_CORRECTION"));
            Assert.IsFalse(macros.ContainsKey("NUM_WOUNDS"));
        }

        [TestMethod]
        public void InitialTranslatorEmitsExactConstantArithmetic()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShaderTranslationTest.pcc", MEGame.LE3);
            var material = package.CreateExport("TestMaterial", "Material", indexed: false);
            var first = package.CreateExport("First", "MaterialExpressionConstant3Vector", material, indexed: false);
            var second = package.CreateExport("Second", "MaterialExpressionConstant3Vector", material, indexed: false);
            var scalar = package.CreateExport("Scalar", "MaterialExpressionConstant", material, indexed: false);
            var add = package.CreateExport("Add", "MaterialExpressionAdd", material, indexed: false);
            var multiply = package.CreateExport("Multiply", "MaterialExpressionMultiply", material, indexed: false);

            first.WriteProperty(new FloatProperty(1, "R"));
            first.WriteProperty(new FloatProperty(2, "G"));
            first.WriteProperty(new FloatProperty(3, "B"));
            second.WriteProperty(new FloatProperty(4, "R"));
            second.WriteProperty(new FloatProperty(5, "G"));
            second.WriteProperty(new FloatProperty(6, "B"));
            scalar.WriteProperty(new FloatProperty(0.5f, "R"));
            add.WriteProperty(Input("A", first.UIndex));
            add.WriteProperty(Input("B", second.UIndex));
            multiply.WriteProperty(Input("A", add.UIndex));
            multiply.WriteProperty(Input("B", scalar.UIndex));
            material.WriteProperty(Input("EmissiveColor", multiply.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>(
                [new(first.UIndex), new(second.UIndex), new(scalar.UIndex), new(add.UIndex), new(multiply.UIndex)],
                "Expressions"));

            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(
                "\treturn ((float3(1.00000000,2.00000000,3.00000000) + float3(4.00000000,5.00000000,6.00000000)) * float(0.50000000))",
                result.PropertyCode["EmissiveColor"]);
            StringAssert.Contains(result.MaterialHlsl, result.PropertyCode["EmissiveColor"]);
        }

        [TestMethod]
        [DataRow("Subtract", "-", false)]
        [DataRow("Subtract", "-", true)]
        [DataRow("Divide", "/", false)]
        [DataRow("Divide", "/", true)]
        public void OrderedArithmeticPreservesScalarVectorOperandOrder(string operation, string symbol, bool scalarFirst)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3OrderedArithmetic.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var vector = package.CreateExport("Vector", "MaterialExpressionConstant3Vector", material, indexed: false);
            vector.WriteProperty(new FloatProperty(1, "R"));
            vector.WriteProperty(new FloatProperty(2, "G"));
            vector.WriteProperty(new FloatProperty(3, "B"));
            var scalar = package.CreateExport("Scalar", "MaterialExpressionConstant", material, indexed: false);
            scalar.WriteProperty(new FloatProperty(0, "R")); // Do not invent HLSL denominator clamping.
            var arithmetic = package.CreateExport("Arithmetic", "MaterialExpression" + operation, material, indexed: false);
            arithmetic.WriteProperty(Input("A", scalarFirst ? scalar.UIndex : vector.UIndex));
            arithmetic.WriteProperty(Input("B", scalarFirst ? vector.UIndex : scalar.UIndex));
            material.WriteProperty(Input("EmissiveColor", arithmetic.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(vector.UIndex), new(scalar.UIndex), new(arithmetic.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            const string vectorCode = "float3(1.00000000,2.00000000,3.00000000)";
            const string scalarCode = "float(0.00000000)";
            Assert.AreEqual("\treturn (" + (scalarFirst ? scalarCode : vectorCode) + " " + symbol + " " +
                (scalarFirst ? vectorCode : scalarCode) + ")", result.PropertyCode["EmissiveColor"]);
            Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
            Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
        }

        [TestMethod]
        [DataRow("Add", "+")]
        [DataRow("Multiply", "*")]
        [DataRow("Subtract", "-")]
        [DataRow("Divide", "/")]
        public void OrderedArithmeticEmitsPixelAndVertexLocalsAfterConnectionMasks(string operation, string symbol)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ArithmeticLocals.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var color = package.CreateExport("Color", "MaterialExpressionVertexColor", material, indexed: false);
            var scalar = package.CreateExport("Scalar", "MaterialExpressionConstant", material, indexed: false);
            scalar.WriteProperty(new FloatProperty(0.5f, "R"));
            var arithmetic = package.CreateExport("Arithmetic", "MaterialExpression" + operation, material, indexed: false);
            arithmetic.WriteProperty(MaskedInput("A", color.UIndex, r: true, g: true, b: true));
            arithmetic.WriteProperty(Input("B", scalar.UIndex));
            material.WriteProperty(Input("EmissiveColor", arithmetic.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", arithmetic.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(color.UIndex), new(scalar.UIndex), new(arithmetic.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: true);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            string expression = operation == "Multiply" ? "(half(0.50000000) * Parameters.VertexColor.rgb)"
                : $"(Parameters.VertexColor.rgb {symbol} half(0.50000000))";
            Assert.AreEqual($"\thalf3 Local0 = {expression};\r\n\treturn Local0", result.PropertyCode["EmissiveColor"]);
            Assert.AreEqual($"\thalf3 Local1 = {expression};\r\n\treturn Local1", result.PropertyCode["WorldPositionOffset"]);
            Assert.IsTrue(result.UsesVertexColor);
        }

        [TestMethod]
        [DataRow("Add")]
        [DataRow("Multiply")]
        [DataRow("Subtract")]
        [DataRow("Divide")]
        public void ArithmeticRequiresConnectedInputsAndRejectsDifferentVectorWidths(string operation)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ArithmeticErrors.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = package.CreateExport("A", "MaterialExpressionConstant2Vector", material, indexed: false);
            var b = package.CreateExport("B", "MaterialExpressionConstant3Vector", material, indexed: false);
            var arithmetic = package.CreateExport("Arithmetic", "MaterialExpression" + operation, material, indexed: false);
            arithmetic.WriteProperty(new FloatProperty(5, "ConstA"));
            arithmetic.WriteProperty(new FloatProperty(6, "ConstB"));
            material.WriteProperty(Input("Distortion", arithmetic.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex), new(arithmetic.UIndex)], "Expressions"));
            Check("Missing " + operation + " input A");
            arithmetic.WriteProperty(Input("A", a.UIndex));
            Check("Missing " + operation + " input B");
            arithmetic.WriteProperty(Input("B", b.UIndex));
            Check("Arithmetic between Vector2 and Vector3 is undefined");
            void Check(string expectedError)
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
                Assert.IsFalse(result.Success);
                Assert.IsNull(result.MaterialHlsl);
                StringAssert.Contains(string.Join("; ", result.Errors), expectedError);
            }
        }

        [TestMethod]
        [DataRow("Add")]
        [DataRow("Multiply")]
        [DataRow("Subtract")]
        [DataRow("Divide")]
        public void DynamicUniformArithmeticProducesFoldedResourcesAndMasksStillUseHlsl(string operation)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3UniformArithmeticBoundary.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = package.CreateExport("A", "MaterialExpressionScalarParameter", material, indexed: false);
            a.WriteProperty(new NameProperty("Amount", "ParameterName"));
            var b = package.CreateExport("B", "MaterialExpressionConstant", material, indexed: false);
            var arithmetic = package.CreateExport("Arithmetic", "MaterialExpression" + operation, material, indexed: false);
            arithmetic.WriteProperty(Input("A", a.UIndex));
            arithmetic.WriteProperty(Input("B", b.UIndex));
            material.WriteProperty(Input("Opacity", arithmetic.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex), new(arithmetic.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual("\treturn UniformPixelScalars_0.y", result.PropertyCode["Opacity"]);
            Assert.AreEqual(2, result.UniformPixelScalarExpressions.Count);
            var folded = (MaterialUniformExpressionFoldedMath)result.UniformPixelScalarExpressions[1];
            Assert.AreSame(result.UniformPixelScalarExpressions[0], folded.A);
            var constant = (MaterialUniformExpressionConstant)folded.B;
            Assert.AreEqual((byte)0xf, constant.ValueType);
            Assert.AreEqual(new Vector4(), (Vector4)constant.Value);
            var expectedOp = operation switch
            {
                "Add" => MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Add,
                "Multiply" => MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Mul,
                "Subtract" => MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Sub,
                _ => MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Div
            };
            Assert.AreEqual(expectedOp, folded.Op);
            var context = new UniformExpressionRenderContext(new() { ["Amount"] = 3 }, new(), 0, 0, null);
            LinearColor evaluated = default;
            folded.GetNumberValue(context, ref evaluated);
            float expected = operation == "Multiply" ? 0 : operation == "Divide" ? 3 / 1e-5f : 3;
            Assert.AreEqual(new Vector4(expected, 0, 0, 0), (Vector4)evaluated);
            // A mask drops the uniform-expression object in LE3. Math then belongs in HLSL.
            arithmetic.WriteProperty(MaskedInput("A", a.UIndex, r: true));
            var masked = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(masked.Success, string.Join("; ", masked.Errors));
            Assert.AreEqual(1, masked.UniformPixelScalarExpressions.Count);
            Assert.IsFalse(masked.UniformPixelScalarExpressions.Any(p => p is MaterialUniformExpressionFoldedMath));
            StringAssert.Contains(masked.PropertyCode["Opacity"], "\tfloat Local0 = ");
        }

        [TestMethod]
        public void FoldedTreesInternOrderedChildrenAcrossPropertiesAndShaderStages()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FoldedIdentity.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = package.CreateExport("A", "MaterialExpressionVectorParameter", material, indexed: false);
            var b = package.CreateExport("B", "MaterialExpressionVectorParameter", material, indexed: false);
            a.WriteProperty(new NameProperty("A", "ParameterName"));
            b.WriteProperty(new NameProperty("B", "ParameterName"));
            var expressions = new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex)], "Expressions");
            var add = Math("Add", a, b, "Add");
            var sameAdd = Math("SameAdd", a, b, "Add");
            var reversedAdd = Math("ReversedAdd", b, a, "Add");
            var sub = Math("Sub", a, b, "Subtract");
            material.WriteProperty(MaskedInput("EmissiveColor", add.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("DiffuseColor", sameAdd.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("SpecularColor", reversedAdd.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("Normal", sub.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("WorldPositionOffset", sameAdd.UIndex, r: true, g: true, b: true));
            material.WriteProperty(expressions);
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(5, result.UniformPixelVectorExpressions.Count);
            Assert.AreEqual(3, result.UniformVertexVectorExpressions.Count);
            var folded = result.UniformPixelVectorExpressions.OfType<MaterialUniformExpressionFoldedMath>().ToArray();
            Assert.AreEqual(3, folded.Length);
            var plus = folded.Single(p => p.Op == MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Add &&
                ((MaterialUniformExpressionVectorParameter)p.A).ParameterName == new NameReference("A"));
            var reversed = folded.Single(p => p.Op == MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Add && !ReferenceEquals(p, plus));
            var minus = folded.Single(p => p.Op == MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Sub);
            Assert.AreSame(plus.A, reversed.B);
            Assert.AreSame(plus.B, reversed.A);
            Assert.AreSame(plus.A, minus.A);
            Assert.AreSame(plus.B, minus.B);
            Assert.AreEqual(result.PropertyCode["EmissiveColor"], result.PropertyCode["DiffuseColor"]);
            foreach (var uniform in result.UniformVertexVectorExpressions)
                Assert.IsTrue(result.UniformPixelVectorExpressions.Any(p => ReferenceEquals(p, uniform)));
            Assert.AreSame(plus, result.UniformVertexVectorExpressions[2]);
            CollectionAssert.AreEqual(new[] { "B", "A" }, result.UniformVertexVectorExpressions.Take(2)
                .Cast<MaterialUniformExpressionVectorParameter>().Select(p => p.ParameterName.Name).ToArray());
            Assert.AreEqual("\treturn UniformVertexVector_2.rgb", result.PropertyCode["WorldPositionOffset"]);

            ExportEntry Math(string name, ExportEntry left, ExportEntry right, string operation)
            {
                var node = package.CreateExport(name, "MaterialExpression" + operation, material, indexed: false);
                node.WriteProperty(Input("A", left.UIndex));
                node.WriteProperty(Input("B", right.UIndex));
                expressions.Add(new ObjectProperty(node.UIndex));
                return node;
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NestedFoldedScalarsPackByAccessAndRoundTripWithoutRetranslation(bool useHalf)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NestedFoldedScalars.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var expressions = new ArrayProperty<ObjectProperty>("Expressions");
            var a = Parameter("A");
            var b = Parameter("B");
            var c = Parameter("C");
            var add = Math("Add", a, b, "Add");
            var mul = Math("Mul", add, c, "Multiply");
            material.WriteProperty(Input("Opacity", mul.UIndex));
            material.WriteProperty(expressions);
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: useHalf);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual("\treturn UniformPixelScalars_1.x", result.PropertyCode["Opacity"]);
            Assert.AreEqual(5, result.UniformPixelScalarExpressions.Count);
            CollectionAssert.AreEqual(new[] { "B", "A", "C" }, result.UniformPixelScalarExpressions.Take(3)
                .Cast<MaterialUniformExpressionScalarParameter>().Select(p => p.ParameterName.Name).ToArray());
            var outer = (MaterialUniformExpressionFoldedMath)result.UniformPixelScalarExpressions[4];
            Assert.AreSame(result.UniformPixelScalarExpressions[3], outer.A);
            Assert.AreSame(result.UniformPixelScalarExpressions[2], outer.B);
            var inner = (MaterialUniformExpressionFoldedMath)outer.A;
            Assert.AreSame(result.UniformPixelScalarExpressions[1], inner.A);
            Assert.AreSame(result.UniformPixelScalarExpressions[0], inner.B);
            using var stream = new MemoryStream();
            outer.Serialize(new SerializingContainer(stream, package));
            stream.Position = 0;
            var reader = new SerializingContainer(stream, package, isLoading: true);
            var restored = (MaterialUniformExpressionFoldedMath)MaterialUniformExpression.Create(reader);
            restored.Serialize(reader);
            Assert.AreEqual(stream.Length, stream.Position);
            Assert.AreEqual(outer.Op, restored.Op);
            Assert.AreEqual(inner.Op, ((MaterialUniformExpressionFoldedMath)restored.A).Op);
            var context = new UniformExpressionRenderContext(new() { ["A"] = 2, ["B"] = 3, ["C"] = 4 }, new(), 0, 0, null);
            Check(20);
            context.ScalarParameterValues["A"] = 7;
            Check(40); // Runtime overrides change the tree's value, not its generated HLSL.

            void Check(float expected)
            {
                LinearColor originalValue = default, restoredValue = default;
                outer.GetNumberValue(context, ref originalValue);
                restored.GetNumberValue(context, ref restoredValue);
                Assert.AreEqual(new Vector4(expected, 0, 0, 0), (Vector4)originalValue);
                Assert.AreEqual((Vector4)originalValue, (Vector4)restoredValue);
            }
            ExportEntry Parameter(string name)
            {
                var node = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false);
                node.WriteProperty(new NameProperty(name, "ParameterName"));
                expressions.Add(new ObjectProperty(node.UIndex));
                return node;
            }
            ExportEntry Math(string name, ExportEntry left, ExportEntry right, string operation)
            {
                var node = package.CreateExport(name, "MaterialExpression" + operation, material, indexed: false);
                node.WriteProperty(Input("A", left.UIndex));
                node.WriteProperty(Input("B", right.UIndex));
                expressions.Add(new ObjectProperty(node.UIndex));
                return node;
            }
        }

        [TestMethod]
        [DataRow(2, false)]
        [DataRow(3, false)]
        [DataRow(4, false)]
        [DataRow(2, true)]
        [DataRow(3, true)]
        [DataRow(4, true)]
        public void FoldedVectorResultsKeepNativeWidthAndZeroScalarLanes(int width, bool useHalf)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FoldedVectorWidths.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var vector = package.CreateExport("Vector", "MaterialExpressionConstant" + width + "Vector", material, indexed: false);
            for (int i = 0; i < width; i++) vector.WriteProperty(new FloatProperty(i + 1, "RGBA"[i].ToString()));
            var scalar = package.CreateExport("Scalar", "MaterialExpressionScalarParameter", material, indexed: false);
            scalar.WriteProperty(new NameProperty("Amount", "ParameterName"));
            scalar.WriteProperty(new FloatProperty(2, "DefaultValue"));
            var mul = package.CreateExport("Mul", "MaterialExpressionMultiply", material, indexed: false);
            mul.WriteProperty(Input("A", vector.UIndex));
            mul.WriteProperty(Input("B", scalar.UIndex));
            string root = width == 2 ? "Distortion" : "EmissiveColor";
            material.WriteProperty(width == 4 ? MaskedInput(root, mul.UIndex, r: true, g: true, b: true) : Input(root, mul.UIndex));
            if (width == 3) material.WriteProperty(Input("WorldPositionOffset", mul.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(vector.UIndex), new(scalar.UIndex), new(mul.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: useHalf);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual("\treturn UniformPixelVector_0." + (width == 2 ? "rg" : "rgb"), result.PropertyCode[root]);
            Assert.AreEqual(1, result.UniformPixelVectorExpressions.Count);
            Assert.AreEqual(1, result.UniformPixelScalarExpressions.Count);
            var folded = (MaterialUniformExpressionFoldedMath)result.UniformPixelVectorExpressions[0];
            var constant = (MaterialUniformExpressionConstant)folded.A; // Uniform Mul must not swap scalar B first.
            Assert.AreEqual(width == 2 ? (byte)2 : width == 3 ? (byte)4 : (byte)8, constant.ValueType);
            Assert.AreSame(result.UniformPixelScalarExpressions[0], folded.B);
            Assert.AreEqual(width == 4 ? 4f : 0f, constant.Value.A);
            Assert.AreEqual(width >= 3 ? 3f : 0f, constant.Value.B);
            var context = new UniformExpressionRenderContext(new(), new(), 0, 0, null);
            LinearColor evaluated = default;
            folded.GetNumberValue(context, ref evaluated);
            Assert.AreEqual(new Vector4(2, 0, 0, 0), (Vector4)evaluated);
            context.ScalarParameterValues["Amount"] = 5;
            folded.GetNumberValue(context, ref evaluated);
            Assert.AreEqual(new Vector4(5, 0, 0, 0), (Vector4)evaluated);
            if (width == 3)
            {
                Assert.AreSame(folded, result.UniformVertexVectorExpressions.Single());
                Assert.AreSame(folded.B, result.UniformVertexScalarExpressions.Single());
                Assert.AreEqual("\treturn UniformVertexVector_0.rgb", result.PropertyCode["WorldPositionOffset"]);
            }
        }

        [TestMethod]
        [DataRow("Add", 8f, 10f, 12f, 14f)]
        [DataRow("Subtract", 4f, 6f, 8f, 10f)]
        [DataRow("Multiply", 12f, 16f, 20f, 24f)]
        [DataRow("Divide", 3f, 4f, 5f, 6f)]
        public void FoldedVectorArithmeticEvaluatesEveryLaneAndSerializesConstants(string operation, float r, float g, float b, float a)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FoldedVectorEvaluation.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var parameter = package.CreateExport("Parameter", "MaterialExpressionVectorParameter", material, indexed: false);
            parameter.WriteProperty(new NameProperty("Color", "ParameterName"));
            var constant = package.CreateExport("Constant", "MaterialExpressionConstant4Vector", material, indexed: false);
            foreach (char lane in "RGBA") constant.WriteProperty(new FloatProperty(2, lane.ToString()));
            var arithmetic = package.CreateExport("Arithmetic", "MaterialExpression" + operation, material, indexed: false);
            arithmetic.WriteProperty(Input("A", parameter.UIndex));
            arithmetic.WriteProperty(Input("B", constant.UIndex));
            material.WriteProperty(MaskedInput("EmissiveColor", arithmetic.UIndex, r: true, g: true, b: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(parameter.UIndex), new(constant.UIndex), new(arithmetic.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            var folded = (MaterialUniformExpressionFoldedMath)result.UniformPixelVectorExpressions[1];
            using var stream = new MemoryStream();
            folded.Serialize(new SerializingContainer(stream, package));
            stream.Position = 0;
            var reader = new SerializingContainer(stream, package, isLoading: true);
            var restored = MaterialUniformExpression.Create(reader);
            restored.Serialize(reader);
            Assert.AreEqual(stream.Length, stream.Position);
            var context = new UniformExpressionRenderContext(new(), new() { ["Color"] = new LinearColor(6, 8, 10, 12) }, 0, 0, null);
            LinearColor evaluated = default;
            restored.GetNumberValue(context, ref evaluated);
            Assert.AreEqual(new Vector4(r, g, b, a), (Vector4)evaluated);
        }

        [TestMethod]
        public void ConstantOnlySubtreesStayInlineButAreRetainedAndInternedInsideDynamicTrees()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FoldedConstantSubtrees.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var expressions = new ArrayProperty<ObjectProperty>("Expressions");
            var first = Tree("First");
            var second = Tree("Second");
            material.WriteProperty(Input("Opacity", first.UIndex));
            material.WriteProperty(Input("OpacityMask", second.UIndex));
            material.WriteProperty(expressions);
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(2, result.UniformPixelScalarExpressions.Count);
            Assert.AreEqual(result.PropertyCode["Opacity"], result.PropertyCode["OpacityMask"]);
            var outer = (MaterialUniformExpressionFoldedMath)result.UniformPixelScalarExpressions[1];
            var constantTree = (MaterialUniformExpressionFoldedMath)outer.B;
            Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Div, constantTree.Op);
            Assert.AreEqual((byte)0xf, ((MaterialUniformExpressionConstant)constantTree.A).ValueType);
            Assert.AreEqual(8f, ((MaterialUniformExpressionConstant)constantTree.A).Value.R);
            Assert.AreEqual(2f, ((MaterialUniformExpressionConstant)constantTree.B).Value.R);
            var context = new UniformExpressionRenderContext(new() { ["Amount"] = 3 }, new(), 0, 0, null);
            LinearColor evaluated = default;
            outer.GetNumberValue(context, ref evaluated);
            Assert.AreEqual(new Vector4(7, 0, 0, 0), (Vector4)evaluated);
            material.WriteProperty(Input("Opacity", first.GetProperty<StructProperty>("B").GetProp<ObjectProperty>("Expression").Value));
            material.RemoveProperty("OpacityMask");
            var inline = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(inline.Success, string.Join("; ", inline.Errors));
            Assert.AreEqual("\treturn (float(8.00000000) / float(2.00000000))", inline.PropertyCode["Opacity"]);
            Assert.AreEqual(0, inline.UniformPixelScalarExpressions.Count);

            ExportEntry Tree(string prefix)
            {
                var parameter = package.CreateExport(prefix + "Parameter", "MaterialExpressionScalarParameter", material, indexed: false);
                parameter.WriteProperty(new NameProperty("Amount", "ParameterName"));
                var a = package.CreateExport(prefix + "A", "MaterialExpressionConstant", material, indexed: false);
                a.WriteProperty(new FloatProperty(8, "R"));
                var b = package.CreateExport(prefix + "B", "MaterialExpressionConstant", material, indexed: false);
                b.WriteProperty(new FloatProperty(2, "R"));
                var div = package.CreateExport(prefix + "Div", "MaterialExpressionDivide", material, indexed: false);
                div.WriteProperty(Input("A", a.UIndex));
                div.WriteProperty(Input("B", b.UIndex));
                var add = package.CreateExport(prefix + "Add", "MaterialExpressionAdd", material, indexed: false);
                add.WriteProperty(Input("A", parameter.UIndex));
                add.WriteProperty(Input("B", div.UIndex));
                foreach (var node in new[] { parameter, a, b, div, add }) expressions.Add(new ObjectProperty(node.UIndex));
                return add;
            }
        }

        [TestMethod]
        public void FoldedDivisionUsesNativeSignedGuardIncludingNegativeZeroAndThreshold()
        {
            var numerator = new MaterialUniformExpressionConstant { Value = new LinearColor(1, 1, 1, 1) };
            var denominator = new MaterialUniformExpressionConstant();
            var folded = new MaterialUniformExpressionFoldedMath
            {
                A = numerator, B = denominator, Op = MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Div
            };
            var context = new UniformExpressionRenderContext(new(), new(), 0, 0, null);
            Check(new LinearColor(0, -0.0f, 1e-6f, -1e-6f), new Vector4(1 / 1e-5f, 1 / 1e-5f, 1 / 1e-5f, -1 / 1e-5f));
            Check(new LinearColor(1e-5f, -1e-5f, 2e-5f, -2e-5f), new Vector4(1 / 1e-5f, -1 / 1e-5f, 1 / 2e-5f, -1 / 2e-5f));
            void Check(LinearColor divisors, Vector4 expected)
            {
                denominator.Value = divisors;
                LinearColor evaluated = default;
                folded.GetNumberValue(context, ref evaluated);
                Assert.AreEqual(expected, (Vector4)evaluated);
            }
        }

        [TestMethod]
        [DataRow(1, 1)]
        [DataRow(1, 2)]
        [DataRow(2, 1)]
        [DataRow(1, 3)]
        [DataRow(3, 1)]
        [DataRow(2, 2)]
        public void AppendVectorConstantsPreserveWidthsAndOrderWithoutUniformSlots(int widthA, int widthB)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3AppendConstants.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = Constant("A", widthA, 1);
            var b = Constant("B", widthB, 10);
            var append = package.CreateExport("Append", "MaterialExpressionAppendVector", material, indexed: false);
            append.WriteProperty(Input("A", a.UIndex));
            append.WriteProperty(Input("B", b.UIndex));
            int width = widthA + widthB;
            string root = width == 2 ? "Distortion" : "EmissiveColor";
            material.WriteProperty(width == 4 ? MaskedInput(root, append.UIndex, r: true, g: true, b: true) : Input(root, append.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex), new(append.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                string type = half ? "half" : "float";
                Assert.AreEqual($"\treturn {type}{width}({Literal(type, widthA, 1)},{Literal(type, widthB, 10)})" +
                    (width == 4 ? ".rgb" : ""), result.PropertyCode[root]);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
                Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
            }
            ExportEntry Constant(string name, int count, int start)
            {
                var node = package.CreateExport(name, count == 1 ? "MaterialExpressionConstant" : "MaterialExpressionConstant" + count + "Vector", material, indexed: false);
                for (int i = 0; i < count; i++) node.WriteProperty(new FloatProperty(start + i, "RGBA"[i].ToString()));
                return node;
            }
            static string Literal(string type, int count, int start) => type + (count == 1 ? "" : count.ToString()) + "(" +
                string.Join(',', Enumerable.Range(start, count).Select(n => n + ".00000000")) + ")";
        }

        [TestMethod]
        [DataRow(1, 1)]
        [DataRow(1, 2)]
        [DataRow(2, 1)]
        [DataRow(1, 3)]
        [DataRow(3, 1)]
        [DataRow(2, 2)]
        public void AppendVectorNonuniformPathIsInlineInPixelAndVertexInputs(int widthA, int widthB)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3AppendInline.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var color = package.CreateExport("Color", "MaterialExpressionVertexColor", material, indexed: false);
            var b = package.CreateExport("B", widthB == 1 ? "MaterialExpressionConstant" : "MaterialExpressionConstant" + widthB + "Vector", material, indexed: false);
            for (int i = 0; i < widthB; i++) b.WriteProperty(new FloatProperty(i + 1, "RGBA"[i].ToString()));
            var append = package.CreateExport("Append", "MaterialExpressionAppendVector", material, indexed: false);
            append.WriteProperty(MaskedInput("A", color.UIndex, r: true, g: widthA > 1, b: widthA > 2));
            append.WriteProperty(Input("B", b.UIndex));
            int width = widthA + widthB;
            string root = width == 2 ? "Distortion" : "EmissiveColor";
            material.WriteProperty(width == 4 ? MaskedInput(root, append.UIndex, r: true, g: true, b: true) : Input(root, append.UIndex));
            material.WriteProperty(width == 3 ? Input("WorldPositionOffset", append.UIndex) : MaskedInput("WorldPositionOffset", append.UIndex, r: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(color.UIndex), new(b.UIndex), new(append.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                string type = half ? "half" : "float";
                string literal = type + (widthB == 1 ? "" : widthB.ToString()) + "(" +
                    string.Join(',', Enumerable.Range(1, widthB).Select(n => n + ".00000000")) + ")";
                string code = $"{type}{width}(Parameters.VertexColor.{"rgb"[..widthA]},{literal})";
                Assert.AreEqual("\treturn " + code + (width == 4 ? ".rgb" : ""), result.PropertyCode[root]);
                Assert.AreEqual("\treturn " + code + (width == 3 ? "" : ".r"), result.PropertyCode["WorldPositionOffset"]);
                Assert.IsTrue(result.UsesVertexColor);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
                Assert.AreEqual(0, result.UniformVertexVectorExpressions.Count);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NestedUniformAppendsDeduplicatePackAndRoundTripAcrossShaderStages(bool half)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3AppendTrees.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var expressions = new ArrayProperty<ObjectProperty>("Expressions");
            var a = Parameter("A", 2);
            var b = Parameter("B", 3);
            var c = Parameter("C", 4);
            var ab = Append("AB", a, b);
            var abc = Append("ABC", ab, c);
            var duplicate = Append("Duplicate", ab, c);
            var reversed = Append("Reversed", c, ab);
            material.WriteProperty(Input("EmissiveColor", abc.UIndex));
            material.WriteProperty(Input("DiffuseColor", duplicate.UIndex));
            material.WriteProperty(Input("SpecularColor", reversed.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", abc.UIndex));
            material.WriteProperty(expressions);
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual("\treturn UniformPixelVector_1.rgb", result.PropertyCode["EmissiveColor"]);
            Assert.AreEqual(result.PropertyCode["EmissiveColor"], result.PropertyCode["DiffuseColor"]);
            Assert.AreEqual("\treturn UniformPixelVector_2.rgb", result.PropertyCode["SpecularColor"]);
            Assert.AreEqual("\treturn UniformVertexVector_1.rgb", result.PropertyCode["WorldPositionOffset"]);
            Assert.AreEqual(3, result.UniformPixelVectorExpressions.Count);
            Assert.AreEqual(2, result.UniformVertexVectorExpressions.Count);
            CollectionAssert.AreEqual(new[] { "B", "A", "C" }, result.UniformPixelScalarExpressions
                .Cast<MaterialUniformExpressionScalarParameter>().Select(p => p.ParameterName.Name).ToArray());
            var outer = (MaterialUniformExpressionAppendVector)result.UniformPixelVectorExpressions[1];
            var inner = (MaterialUniformExpressionAppendVector)outer.A;
            Assert.AreEqual(2u, outer.NumComponentsA);
            Assert.AreEqual(1u, inner.NumComponentsA);
            Assert.AreSame(result.UniformPixelVectorExpressions[0], inner);
            Assert.AreSame(result.UniformPixelScalarExpressions[1], inner.A);
            Assert.AreSame(result.UniformPixelScalarExpressions[0], inner.B);
            Assert.AreSame(result.UniformPixelScalarExpressions[2], outer.B);
            for (int i = 0; i < 2; i++) Assert.AreSame(result.UniformPixelVectorExpressions[i], result.UniformVertexVectorExpressions[i]);
            var reverse = (MaterialUniformExpressionAppendVector)result.UniformPixelVectorExpressions[2];
            Assert.AreEqual(1u, reverse.NumComponentsA);
            Assert.AreSame(outer.B, reverse.A);
            Assert.AreSame(inner, reverse.B);
            using var stream = new MemoryStream();
            outer.Serialize(new SerializingContainer(stream, package));
            stream.Position = 0;
            var reader = new SerializingContainer(stream, package, isLoading: true);
            var restored = (MaterialUniformExpressionAppendVector)MaterialUniformExpression.Create(reader);
            restored.Serialize(reader);
            Assert.AreEqual(stream.Length, stream.Position);
            Assert.AreEqual(2u, restored.NumComponentsA);
            Assert.AreEqual(1u, ((MaterialUniformExpressionAppendVector)restored.A).NumComponentsA);
            var context = new UniformExpressionRenderContext(new(), new(), 0, 0, null);
            Check(new Vector4(2, 3, 4, 0));
            context.ScalarParameterValues["B"] = 8;
            Check(new Vector4(2, 8, 4, 0));
            LinearColor reversedValue = default;
            reverse.GetNumberValue(context, ref reversedValue);
            Assert.AreEqual(new Vector4(4, 2, 8, 0), (Vector4)reversedValue);
            void Check(Vector4 expected)
            {
                LinearColor value = default, restoredValue = default;
                outer.GetNumberValue(context, ref value);
                restored.GetNumberValue(context, ref restoredValue);
                Assert.AreEqual(expected, (Vector4)value);
                Assert.AreEqual(expected, (Vector4)restoredValue);
            }
            ExportEntry Parameter(string name, float defaultValue)
            {
                var node = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false);
                node.WriteProperty(new NameProperty(name, "ParameterName"));
                node.WriteProperty(new FloatProperty(defaultValue, "DefaultValue"));
                expressions.Add(new ObjectProperty(node.UIndex));
                return node;
            }
            ExportEntry Append(string name, ExportEntry left, ExportEntry right)
            {
                var node = package.CreateExport(name, "MaterialExpressionAppendVector", material, indexed: false);
                node.WriteProperty(Input("A", left.UIndex));
                node.WriteProperty(Input("B", right.UIndex));
                expressions.Add(new ObjectProperty(node.UIndex));
                return node;
            }
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        public void UniformAppendCopiesNativeFourLanesAndSerializesConstantTail(int widthA)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3AppendConstantTail.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var expressions = new ArrayProperty<ObjectProperty>("Expressions");
            ExportEntry prefix = null;
            for (int i = 0; i < widthA; i++)
            {
                var parameter = package.CreateExport("P" + i, "MaterialExpressionScalarParameter", material, indexed: false);
                parameter.WriteProperty(new NameProperty("P" + i, "ParameterName"));
                parameter.WriteProperty(new FloatProperty(i + 2, "DefaultValue"));
                expressions.Add(new ObjectProperty(parameter.UIndex));
                prefix = prefix == null ? parameter : Append("Prefix" + i, prefix, parameter);
            }
            int widthB = 4 - widthA;
            var tail = package.CreateExport("Tail", widthB == 1 ? "MaterialExpressionConstant" : "MaterialExpressionConstant" + widthB + "Vector", material, indexed: false);
            for (int i = 0; i < widthB; i++) tail.WriteProperty(new FloatProperty(10 + i, "RGBA"[i].ToString()));
            expressions.Add(new ObjectProperty(tail.UIndex));
            var root = Append("Root", prefix, tail);
            material.WriteProperty(MaskedInput("EmissiveColor", root.UIndex, r: true, g: true, b: true));
            material.WriteProperty(expressions);
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(widthA, result.UniformPixelVectorExpressions.Count);
            var outer = (MaterialUniformExpressionAppendVector)result.UniformPixelVectorExpressions.Last();
            Assert.AreEqual((uint)widthA, outer.NumComponentsA);
            var constant = (MaterialUniformExpressionConstant)outer.B;
            Assert.AreEqual(widthB == 1 ? (byte)0xf : widthB == 2 ? (byte)2 : (byte)4, constant.ValueType);
            using var stream = new MemoryStream();
            outer.Serialize(new SerializingContainer(stream, package));
            stream.Position = 0;
            var reader = new SerializingContainer(stream, package, isLoading: true);
            var restored = (MaterialUniformExpressionAppendVector)MaterialUniformExpression.Create(reader);
            restored.Serialize(reader);
            Assert.AreEqual(stream.Length, stream.Position);
            var context = new UniformExpressionRenderContext(new(), new(), 0, 0, null);
            LinearColor evaluated = default;
            restored.GetNumberValue(context, ref evaluated);
            var expected = Enumerable.Range(2, widthA).Concat(Enumerable.Range(10, widthB)).Select(v => (float)v).ToArray();
            Assert.AreEqual(new Vector4(expected[0], expected[1], expected[2], expected[3]), (Vector4)evaluated);
            ExportEntry Append(string name, ExportEntry left, ExportEntry right)
            {
                var node = package.CreateExport(name, "MaterialExpressionAppendVector", material, indexed: false);
                node.WriteProperty(Input("A", left.UIndex));
                node.WriteProperty(Input("B", right.UIndex));
                expressions.Add(new ObjectProperty(node.UIndex));
                return node;
            }
        }

        [TestMethod]
        public void AppendInputMaskDropsUniformTreeAndRetainsOnlyAccessedLeaf()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3AppendMask.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var parameter = package.CreateExport("Parameter", "MaterialExpressionScalarParameter", material, indexed: false);
            var constant = package.CreateExport("Constant", "MaterialExpressionConstant", material, indexed: false);
            var append = package.CreateExport("Append", "MaterialExpressionAppendVector", material, indexed: false);
            append.WriteProperty(Input("A", parameter.UIndex));
            append.WriteProperty(MaskedInput("B", constant.UIndex, r: true));
            material.WriteProperty(Input("Distortion", append.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(parameter.UIndex), new(constant.UIndex), new(append.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual("\treturn float2(UniformPixelScalars_0.x,float(0.00000000).r)", result.PropertyCode["Distortion"]);
            Assert.AreEqual(1, result.UniformPixelScalarExpressions.Count);
            Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
        }

        [TestMethod]
        public void AppendVectorRequiresBothConnectionsAndReportsCycles()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3AppendInputErrors.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var constant = package.CreateExport("Constant", "MaterialExpressionConstant", material, indexed: false);
            var append = package.CreateExport("Append", "MaterialExpressionAppendVector", material, indexed: false);
            material.WriteProperty(Input("Distortion", append.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(constant.UIndex), new(append.UIndex)], "Expressions"));
            Check("Missing AppendVector input A");
            append.WriteProperty(Input("A", constant.UIndex));
            Check("Missing AppendVector input B");
            append.WriteProperty(Input("B", append.UIndex));
            Check("cycle");
            void Check(string expected)
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
                Assert.IsFalse(result.Success);
                Assert.IsNull(result.MaterialHlsl);
                Assert.IsTrue(result.Errors.Any(e => e.Contains(expected, StringComparison.OrdinalIgnoreCase)));
            }
        }

        [TestMethod]
        [DataRow(1, 4)]
        [DataRow(2, 3)]
        [DataRow(3, 3)]
        [DataRow(4, 4)]
        public void AppendVectorRejectsUnknownOverwideResultInsteadOfTruncating(int widthA, int widthB)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3AppendWidthErrors.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = Constant("A", widthA);
            var b = Constant("B", widthB);
            var append = package.CreateExport("Append", "MaterialExpressionAppendVector", material, indexed: false);
            append.WriteProperty(Input("A", a.UIndex));
            append.WriteProperty(Input("B", b.UIndex));
            material.WriteProperty(MaskedInput("EmissiveColor", append.UIndex, r: true, g: true, b: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex), new(append.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.MaterialHlsl);
            StringAssert.Contains(string.Join("; ", result.Errors), $"AppendVector produces {widthA + widthB} components; only 2-4 are supported.");
            ExportEntry Constant(string name, int width) => package.CreateExport(name,
                width == 1 ? "MaterialExpressionConstant" : "MaterialExpressionConstant" + width + "Vector", material, indexed: false);
        }

        [TestMethod]
        [DataRow(1, 1)]
        [DataRow(2, 2)]
        [DataRow(3, 3)]
        [DataRow(4, 4)]
        [DataRow(1, 3)]
        [DataRow(3, 1)]
        [DataRow(2, 4)]
        [DataRow(4, 2)]
        public void ConstantDotPreservesOriginalOperandsEvenForUnequalWidths(int widthA, int widthB)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DotConstants.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = Constant("A", widthA, 1);
            var b = Constant("B", widthB, 10);
            var dot = package.CreateExport("Dot", "MaterialExpressionDotProduct", material, indexed: false);
            dot.WriteProperty(Input("A", a.UIndex));
            dot.WriteProperty(Input("B", b.UIndex));
            material.WriteProperty(Input("EmissiveColor", dot.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", dot.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex), new(dot.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                string type = half ? "half" : "float";
                string expected = $"\treturn dot({Literal(type, widthA, 1)},{Literal(type, widthB, 10)})";
                Assert.AreEqual(expected, result.PropertyCode["EmissiveColor"]);
                Assert.AreEqual(expected, result.PropertyCode["WorldPositionOffset"]);
                Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(0, result.UniformVertexScalarExpressions.Count);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
                Assert.AreEqual(0, result.UniformVertexVectorExpressions.Count);
            }
            ExportEntry Constant(string name, int count, int start)
            {
                var node = package.CreateExport(name, count == 1 ? "MaterialExpressionConstant" : "MaterialExpressionConstant" + count + "Vector", material, indexed: false);
                for (int i = 0; i < count; i++) node.WriteProperty(new FloatProperty(start + i, "RGBA"[i].ToString()));
                return node;
            }
            static string Literal(string type, int count, int start) => type + (count == 1 ? "" : count.ToString()) + "(" +
                string.Join(',', Enumerable.Range(start, count).Select(n => n + ".00000000")) + ")";
        }

        [TestMethod]
        [DataRow(1, 1)]
        [DataRow(2, 2)]
        [DataRow(3, 3)]
        [DataRow(4, 4)]
        [DataRow(2, 1)]
        [DataRow(3, 1)]
        [DataRow(4, 1)]
        [DataRow(1, 2)]
        [DataRow(1, 3)]
        [DataRow(1, 4)]
        [DataRow(2, 3)]
        [DataRow(3, 2)]
        [DataRow(2, 4)]
        [DataRow(4, 2)]
        [DataRow(3, 4)]
        [DataRow(4, 3)]
        public void NonuniformDotCoercesOnlyRightOperandAndEmitsScalarLocal(int widthA, int widthB)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DotInline.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var color = package.CreateExport("Color", "MaterialExpressionVertexColor", material, indexed: false);
            var b = package.CreateExport("B", widthB == 1 ? "MaterialExpressionConstant" : "MaterialExpressionConstant" + widthB + "Vector", material, indexed: false);
            for (int i = 0; i < widthB; i++) b.WriteProperty(new FloatProperty(i + 1, "RGBA"[i].ToString()));
            var dot = package.CreateExport("Dot", "MaterialExpressionDotProduct", material, indexed: false);
            dot.WriteProperty(MaskedInput("A", color.UIndex, r: true, g: widthA > 1, b: widthA > 2, a: widthA > 3));
            dot.WriteProperty(Input("B", b.UIndex));
            material.WriteProperty(Input("EmissiveColor", dot.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", dot.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(color.UIndex), new(b.UIndex), new(dot.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                if (widthA > 1 && widthB > 1 && widthA != widthB)
                {
                    Assert.IsFalse(result.Success);
                    Assert.IsNull(result.MaterialHlsl);
                    StringAssert.Contains(string.Join("; ", result.Errors), "DotProduct coercion failed");
                    continue;
                }
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                string type = half ? "half" : "float";
                string right = type + (widthB == 1 ? "" : widthB.ToString()) + "(" +
                    string.Join(',', Enumerable.Range(1, widthB).Select(n => n + ".00000000")) + ")";
                if (widthB == 1 && widthA > 1) right = $"{type}{widthA}({string.Join(',', Enumerable.Repeat(right, widthA))})";
                string expected = $"\t{type} Local0 = dot(Parameters.VertexColor.{"rgba"[..widthA]},{right});\r\n\treturn Local0";
                Assert.AreEqual(expected, result.PropertyCode["EmissiveColor"]);
                Assert.AreEqual(expected.Replace("Local0", "Local1"), result.PropertyCode["WorldPositionOffset"]);
                Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void UniformDotInternsOrderedTreesAcrossStagesAndBroadcastsAllFourLanes(bool half)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DotTrees.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var expressions = new ArrayProperty<ObjectProperty>("Expressions");
            var a = VectorParameter("A", 1);
            var b = VectorParameter("B", 10);
            var dot = Math("Dot", a, b, "DotProduct");
            var duplicate = Math("Duplicate", a, b, "DotProduct");
            var reversed = Math("Reversed", b, a, "DotProduct");
            var add = Math("Add", dot, duplicate, "Add");
            material.WriteProperty(Input("EmissiveColor", dot.UIndex));
            material.WriteProperty(Input("DiffuseColor", duplicate.UIndex));
            material.WriteProperty(Input("SpecularColor", reversed.UIndex));
            material.WriteProperty(Input("Opacity", add.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", dot.UIndex));
            material.WriteProperty(expressions);
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            CollectionAssert.AreEqual(new[] { "B", "A" }, result.UniformPixelVectorExpressions
                .Cast<MaterialUniformExpressionVectorParameter>().Select(p => p.ParameterName.Name).ToArray());
            Assert.AreEqual(3, result.UniformPixelScalarExpressions.Count);
            Assert.AreEqual(1, result.UniformVertexScalarExpressions.Count);
            Assert.AreEqual(2, result.UniformVertexVectorExpressions.Count);
            Assert.AreEqual("\treturn UniformPixelScalars_0.x", result.PropertyCode["EmissiveColor"]);
            Assert.AreEqual(result.PropertyCode["EmissiveColor"], result.PropertyCode["DiffuseColor"]);
            Assert.AreEqual("\treturn UniformPixelScalars_0.y", result.PropertyCode["SpecularColor"]);
            Assert.AreEqual("\treturn UniformPixelScalars_0.z", result.PropertyCode["Opacity"]);
            Assert.AreEqual("\treturn UniformVertexScalars_0.x", result.PropertyCode["WorldPositionOffset"]);
            var tree = (MaterialUniformExpressionFoldedMath)result.UniformPixelScalarExpressions[0];
            var reverseTree = (MaterialUniformExpressionFoldedMath)result.UniformPixelScalarExpressions[1];
            var addTree = (MaterialUniformExpressionFoldedMath)result.UniformPixelScalarExpressions[2];
            Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Dot, tree.Op);
            Assert.AreSame(result.UniformPixelVectorExpressions[1], tree.A);
            Assert.AreSame(result.UniformPixelVectorExpressions[0], tree.B);
            Assert.AreSame(tree.A, reverseTree.B);
            Assert.AreSame(tree.B, reverseTree.A);
            Assert.AreSame(tree, addTree.A);
            Assert.AreSame(tree, addTree.B);
            Assert.AreSame(tree, result.UniformVertexScalarExpressions[0]);
            using var stream = new MemoryStream();
            tree.Serialize(new SerializingContainer(stream, package));
            stream.Position = 0;
            var reader = new SerializingContainer(stream, package, isLoading: true);
            var restored = (MaterialUniformExpressionFoldedMath)MaterialUniformExpression.Create(reader);
            restored.Serialize(reader);
            Assert.AreEqual(stream.Length, stream.Position);
            Assert.AreEqual(tree.Op, restored.Op);
            var context = new UniformExpressionRenderContext(new(), new(), 0, 0, null);
            Check(120);
            context.VectorParameterValues["A"] = new LinearColor(2, 3, 4, 5);
            Check(166);
            void Check(float expected)
            {
                foreach (var candidate in new[] { tree, reverseTree, restored })
                {
                    LinearColor value = default;
                    candidate.GetNumberValue(context, ref value);
                    Assert.AreEqual(new Vector4(expected), (Vector4)value);
                }
            }
            ExportEntry VectorParameter(string name, int start)
            {
                var node = package.CreateExport(name, "MaterialExpressionVectorParameter", material, indexed: false);
                node.WriteProperty(new NameProperty(name, "ParameterName"));
                node.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
                {
                    new FloatProperty(start, "R"), new FloatProperty(start + 1, "G"),
                    new FloatProperty(start + 2, "B"), new FloatProperty(start + 3, "A")
                }, "DefaultValue", isImmutable: true));
                expressions.Add(new ObjectProperty(node.UIndex));
                return node;
            }
            ExportEntry Math(string name, ExportEntry left, ExportEntry right, string operation)
            {
                var node = package.CreateExport(name, "MaterialExpression" + operation, material, indexed: false);
                node.WriteProperty(Input("A", left.UIndex));
                node.WriteProperty(Input("B", right.UIndex));
                expressions.Add(new ObjectProperty(node.UIndex));
                return node;
            }
        }

        [TestMethod]
        public void DotRequiresBothConnectionsAndReportsCycles()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DotErrors.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var constant = package.CreateExport("Constant", "MaterialExpressionConstant", material, indexed: false);
            var dot = package.CreateExport("Dot", "MaterialExpressionDotProduct", material, indexed: false);
            material.WriteProperty(Input("EmissiveColor", dot.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(constant.UIndex), new(dot.UIndex)], "Expressions"));
            Check("Missing DotProduct input A");
            dot.WriteProperty(Input("A", constant.UIndex));
            Check("Missing DotProduct input B");
            dot.WriteProperty(Input("B", dot.UIndex));
            Check("cycle");
            void Check(string expected)
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
                Assert.IsFalse(result.Success);
                Assert.IsNull(result.MaterialHlsl);
                Assert.IsTrue(result.Errors.Any(e => e.Contains(expected, StringComparison.OrdinalIgnoreCase)));
            }
        }

        [TestMethod]
        [DataRow(false, 0)]
        [DataRow(true, 0)]
        [DataRow(false, 1)]
        [DataRow(true, 1)]
        [DataRow(false, 2)]
        [DataRow(true, 2)]
        public void DotScalarVectorUniformsKeepOriginalChildrenButMasksSelectLocalPath(bool scalarFirst, int maskedOperand)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DotMixed.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var scalar = package.CreateExport("Scalar", "MaterialExpressionScalarParameter", material, indexed: false);
            scalar.WriteProperty(new NameProperty("S", "ParameterName"));
            scalar.WriteProperty(new FloatProperty(2, "DefaultValue"));
            var vector = package.CreateExport("Vector", "MaterialExpressionVectorParameter", material, indexed: false);
            vector.WriteProperty(new NameProperty("V", "ParameterName"));
            vector.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
            {
                new FloatProperty(10, "R"), new FloatProperty(11, "G"),
                new FloatProperty(12, "B"), new FloatProperty(13, "A")
            }, "DefaultValue", isImmutable: true));
            var a = scalarFirst ? scalar : vector;
            var b = scalarFirst ? vector : scalar;
            var dot = package.CreateExport("Dot", "MaterialExpressionDotProduct", material, indexed: false);
            dot.WriteProperty(Connection("A", a, maskedOperand == 1));
            dot.WriteProperty(Connection("B", b, maskedOperand == 2));
            material.WriteProperty(Input("EmissiveColor", dot.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", dot.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(scalar.UIndex), new(vector.UIndex), new(dot.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                Assert.AreEqual(1, result.UniformPixelVectorExpressions.Count);
                Assert.AreEqual(1, result.UniformVertexVectorExpressions.Count);
                Assert.AreEqual(maskedOperand == 0 ? 2 : 1, result.UniformPixelScalarExpressions.Count);
                if (maskedOperand == 0)
                {
                    var folded = (MaterialUniformExpressionFoldedMath)result.UniformPixelScalarExpressions[1];
                    Assert.AreSame(scalarFirst ? result.UniformPixelScalarExpressions[0] : result.UniformPixelVectorExpressions[0], folded.A);
                    Assert.AreSame(scalarFirst ? result.UniformPixelVectorExpressions[0] : result.UniformPixelScalarExpressions[0], folded.B);
                    var context = new UniformExpressionRenderContext(new(), new(), 0, 0, null);
                    LinearColor evaluated = default;
                    folded.GetNumberValue(context, ref evaluated);
                    Assert.AreEqual(new Vector4(20), (Vector4)evaluated);
                    context.ScalarParameterValues["S"] = 3;
                    folded.GetNumberValue(context, ref evaluated);
                    Assert.AreEqual(new Vector4(30), (Vector4)evaluated);
                }
                else
                {
                    string type = half ? "half" : "float";
                    string left = scalarFirst ? "UniformPixelScalars_0.x" : "UniformPixelVector_0";
                    string right = scalarFirst ? "UniformPixelVector_0" : "UniformPixelScalars_0.x";
                    if (maskedOperand == 1) left += scalarFirst ? ".r" : ".rgba";
                    if (maskedOperand == 2) right += scalarFirst ? ".rgba" : ".r";
                    if (!scalarFirst) right = $"{type}4({string.Join(',', Enumerable.Repeat(right, 4))})";
                    Assert.AreEqual($"\t{type} Local0 = dot({left},{right});\r\n\treturn Local0", result.PropertyCode["EmissiveColor"]);
                    Assert.IsFalse(result.UniformPixelScalarExpressions.Any(p => p is MaterialUniformExpressionFoldedMath));
                }
            }
            StructProperty Connection(string name, ExportEntry node, bool mask) => !mask ? Input(name, node.UIndex)
                : MaskedInput(name, node.UIndex, r: true, g: node == vector, b: node == vector, a: node == vector);
        }

        [TestMethod]
        public void DotOfUniformAppendAndShortConstantUsesAllFourStoredLanesAndRoundTrips()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3AppendDot.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var scalar = package.CreateExport("Scalar", "MaterialExpressionScalarParameter", material, indexed: false);
            scalar.WriteProperty(new FloatProperty(2, "DefaultValue"));
            var tail = package.CreateExport("Tail", "MaterialExpressionConstant3Vector", material, indexed: false);
            var right = package.CreateExport("Right", "MaterialExpressionConstant3Vector", material, indexed: false);
            for (int i = 0; i < 3; i++)
            {
                tail.WriteProperty(new FloatProperty(10 + i, "RGB"[i].ToString()));
                right.WriteProperty(new FloatProperty(3 + i, "RGB"[i].ToString()));
            }
            var append = package.CreateExport("Append", "MaterialExpressionAppendVector", material, indexed: false);
            append.WriteProperty(Input("A", scalar.UIndex));
            append.WriteProperty(Input("B", tail.UIndex));
            var dot = package.CreateExport("Dot", "MaterialExpressionDotProduct", material, indexed: false);
            dot.WriteProperty(Input("A", append.UIndex));
            dot.WriteProperty(Input("B", right.UIndex));
            material.WriteProperty(Input("EmissiveColor", dot.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(scalar.UIndex), new(tail.UIndex), new(right.UIndex), new(append.UIndex), new(dot.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            var tree = (MaterialUniformExpressionFoldedMath)result.UniformPixelScalarExpressions[1];
            Assert.AreSame(result.UniformPixelVectorExpressions[0], tree.A);
            Assert.IsInstanceOfType<MaterialUniformExpressionAppendVector>(tree.A);
            Assert.AreEqual((byte)4, ((MaterialUniformExpressionConstant)tree.B).ValueType);
            using var stream = new MemoryStream();
            tree.Serialize(new SerializingContainer(stream, package));
            stream.Position = 0;
            var reader = new SerializingContainer(stream, package, isLoading: true);
            var restored = (MaterialUniformExpressionFoldedMath)MaterialUniformExpression.Create(reader);
            restored.Serialize(reader);
            Assert.AreEqual(stream.Length, stream.Position);
            LinearColor evaluated = default;
            restored.GetNumberValue(new UniformExpressionRenderContext(new(), new(), 0, 0, null), ref evaluated);
            Assert.AreEqual(new Vector4(101), (Vector4)evaluated);
        }

        [TestMethod]
        public void FoldedDotUsesNativeScalarReductionOrderInsteadOfPairwiseSimd()
        {
            var tree = new MaterialUniformExpressionFoldedMath
            {
                A = new MaterialUniformExpressionConstant { Value = new LinearColor(1e20f, 1, -1e20f, 1), ValueType = 8 },
                B = new MaterialUniformExpressionConstant { Value = new LinearColor(1), ValueType = 8 },
                Op = MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Dot
            };
            LinearColor evaluated = default;
            tree.GetNumberValue(new UniformExpressionRenderContext(new(), new(), 0, 0, null), ref evaluated);
            foreach (float lane in new[] { evaluated.R, evaluated.G, evaluated.B, evaluated.A })
                Assert.AreEqual(BitConverter.SingleToInt32Bits(1f), BitConverter.SingleToInt32Bits(lane));
        }

        [TestMethod]
        [DataRow(1, 1)]
        [DataRow(1, 2)]
        [DataRow(1, 3)]
        [DataRow(1, 4)]
        [DataRow(2, 1)]
        [DataRow(2, 2)]
        [DataRow(2, 3)]
        [DataRow(2, 4)]
        [DataRow(3, 1)]
        [DataRow(3, 2)]
        [DataRow(3, 3)]
        [DataRow(3, 4)]
        [DataRow(4, 1)]
        [DataRow(4, 2)]
        [DataRow(4, 3)]
        [DataRow(4, 4)]
        public void CrossConstantsCoerceToThreeComponentsWithoutFoldingOrTruncation(int widthA, int widthB)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CrossConstants.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = Constant("A", widthA, 1);
            var b = Constant("B", widthB, 10);
            var cross = package.CreateExport("Cross", "MaterialExpressionCrossProduct", material, indexed: false);
            cross.WriteProperty(Input("A", a.UIndex));
            cross.WriteProperty(Input("B", b.UIndex));
            material.WriteProperty(Input("EmissiveColor", cross.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", cross.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex), new(cross.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                if (widthA is not (1 or 3) || widthB is not (1 or 3))
                {
                    Assert.IsFalse(result.Success);
                    Assert.IsNull(result.MaterialHlsl);
                    StringAssert.Contains(string.Join("; ", result.Errors), "CrossProduct coercion to Vector3 failed");
                    continue;
                }
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                string type = half ? "half" : "float";
                string expected = $"\t{type}3 Local0 = cross({Literal(type, widthA, 1)},{Literal(type, widthB, 10)});\r\n\treturn Local0";
                Assert.AreEqual(expected, result.PropertyCode["EmissiveColor"]);
                Assert.AreEqual(expected.Replace("Local0", "Local1"), result.PropertyCode["WorldPositionOffset"]);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
                Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(0, result.UniformVertexVectorExpressions.Count);
                Assert.AreEqual(0, result.UniformVertexScalarExpressions.Count);
            }
            ExportEntry Constant(string name, int count, int start)
            {
                var node = package.CreateExport(name, count == 1 ? "MaterialExpressionConstant" : "MaterialExpressionConstant" + count + "Vector", material, indexed: false);
                for (int i = 0; i < count; i++) node.WriteProperty(new FloatProperty(start + i, "RGBA"[i].ToString()));
                return node;
            }
            static string Literal(string type, int count, int start) => count == 1
                ? $"{type}3({string.Join(',', Enumerable.Repeat($"{type}({start}.00000000)", 3))})"
                : $"{type}3({string.Join(',', Enumerable.Range(start, 3).Select(n => n + ".00000000"))})";
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void CrossScalarParametersAllocateRightThenLeftAndNeverProduceUniformTree(bool half)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CrossParameters.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = Parameter("A");
            var b = Parameter("B");
            var cross = package.CreateExport("Cross", "MaterialExpressionCrossProduct", material, indexed: false);
            cross.WriteProperty(Input("A", a.UIndex));
            cross.WriteProperty(Input("B", b.UIndex));
            material.WriteProperty(Input("EmissiveColor", cross.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", cross.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex), new(cross.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            CollectionAssert.AreEqual(new[] { "B", "A" }, result.UniformPixelScalarExpressions.Cast<MaterialUniformExpressionScalarParameter>().Select(p => p.ParameterName.Name).ToArray());
            Assert.AreEqual(2, result.UniformVertexScalarExpressions.Count);
            Assert.AreSame(result.UniformPixelScalarExpressions[0], result.UniformVertexScalarExpressions[0]);
            Assert.AreSame(result.UniformPixelScalarExpressions[1], result.UniformVertexScalarExpressions[1]);
            string type = half ? "half" : "float";
            string left = $"{type}3(UniformPixelScalars_0.y,UniformPixelScalars_0.y,UniformPixelScalars_0.y)";
            string right = $"{type}3(UniformPixelScalars_0.x,UniformPixelScalars_0.x,UniformPixelScalars_0.x)";
            string expected = $"\t{type}3 Local0 = cross({left},{right});\r\n\treturn Local0";
            Assert.AreEqual(expected, result.PropertyCode["EmissiveColor"]);
            Assert.AreEqual(expected.Replace("Local0", "Local1").Replace("UniformPixel", "UniformVertex"), result.PropertyCode["WorldPositionOffset"]);
            Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
            ExportEntry Parameter(string name)
            {
                var node = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false);
                node.WriteProperty(new NameProperty(name, "ParameterName"));
                return node;
            }
        }

        [TestMethod]
        public void CrossVectorParametersRequireExplicitRgbMasksAndPreserveVisitAccessOrder()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CrossMasks.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = Parameter("A");
            var b = Parameter("B");
            var cross = package.CreateExport("Cross", "MaterialExpressionCrossProduct", material, indexed: false);
            cross.WriteProperty(Input("A", a.UIndex));
            cross.WriteProperty(Input("B", b.UIndex));
            material.WriteProperty(Input("EmissiveColor", cross.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex), new(cross.UIndex)], "Expressions"));
            Assert.IsFalse(new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate()).Success);
            cross.WriteProperty(MaskedInput("A", a.UIndex, r: true, g: true, b: true));
            cross.WriteProperty(MaskedInput("B", b.UIndex, r: true, g: true, b: true));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            CollectionAssert.AreEqual(new[] { "A", "B" }, result.UniformPixelVectorExpressions.Cast<MaterialUniformExpressionVectorParameter>().Select(p => p.ParameterName.Name).ToArray());
            Assert.AreEqual("\tfloat3 Local0 = cross(UniformPixelVector_0.rgb,UniformPixelVector_1.rgb);\r\n\treturn Local0", result.PropertyCode["EmissiveColor"]);
            Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
            ExportEntry Parameter(string name)
            {
                var node = package.CreateExport(name, "MaterialExpressionVectorParameter", material, indexed: false);
                node.WriteProperty(new NameProperty(name, "ParameterName"));
                return node;
            }
        }

        [TestMethod]
        public void CrossRequiresBothConnectionsAndReportsCycles()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CrossErrors.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var constant = package.CreateExport("Constant", "MaterialExpressionConstant", material, indexed: false);
            var cross = package.CreateExport("Cross", "MaterialExpressionCrossProduct", material, indexed: false);
            material.WriteProperty(Input("EmissiveColor", cross.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(constant.UIndex), new(cross.UIndex)], "Expressions"));
            Check("Missing CrossProduct input A");
            cross.WriteProperty(Input("A", constant.UIndex));
            Check("Missing CrossProduct input B");
            cross.WriteProperty(Input("B", cross.UIndex));
            Check("cycle");
            void Check(string expected)
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
                Assert.IsFalse(result.Success);
                Assert.IsNull(result.MaterialHlsl);
                Assert.IsTrue(result.Errors.Any(e => e.Contains(expected, StringComparison.OrdinalIgnoreCase)));
            }
        }

        [TestMethod]
        [DataRow(1, 1)]
        [DataRow(2, 1)]
        [DataRow(3, 1)]
        [DataRow(4, 1)]
        [DataRow(3, 2)]
        [DataRow(3, 3)]
        [DataRow(3, 4)]
        public void PowerPreservesBaseTypeAndDoesNotNarrowVectorExponent(int baseWidth, int exponentWidth)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3PowerTypes.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = Constant("Base", baseWidth, 2);
            var b = Constant("Exponent", exponentWidth, 3);
            var power = package.CreateExport("Power", "MaterialExpressionPower", material, indexed: false);
            power.WriteProperty(Input("Base", a.UIndex));
            power.WriteProperty(Input("Exponent", b.UIndex));
            string root = baseWidth == 2 ? "Distortion" : "EmissiveColor";
            material.WriteProperty(baseWidth == 4 ? MaskedInput(root, power.UIndex, r: true, g: true, b: true) : Input(root, power.UIndex));
            material.WriteProperty(baseWidth is 1 or 3 ? Input("WorldPositionOffset", power.UIndex) : MaskedInput("WorldPositionOffset", power.UIndex, r: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex), new(power.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                string type = half ? "half" : "float";
                string resultType = type + (baseWidth == 1 ? "" : baseWidth.ToString());
                string declaration = $"\t{resultType} Local0 = pow({Literal(type, baseWidth, 2)},{Literal(type, exponentWidth, 3)});\r\n\treturn Local0";
                Assert.AreEqual(declaration + (baseWidth == 4 ? ".rgb" : ""), result.PropertyCode[root]);
                Assert.AreEqual(declaration.Replace("Local0", "Local1") + (baseWidth is 1 or 3 ? "" : ".r"), result.PropertyCode["WorldPositionOffset"]);
                Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
            }
            ExportEntry Constant(string name, int count, int start)
            {
                var node = package.CreateExport(name, count == 1 ? "MaterialExpressionConstant" : "MaterialExpressionConstant" + count + "Vector", material, indexed: false);
                for (int i = 0; i < count; i++) node.WriteProperty(new FloatProperty(start + i, "RGBA"[i].ToString()));
                return node;
            }
            static string Literal(string type, int count, int start) => type + (count == 1 ? "" : count.ToString()) + "(" +
                string.Join(',', Enumerable.Range(start, count).Select(n => n + ".00000000")) + ")";
        }

        [TestMethod]
        [DataRow(1f)]
        [DataRow(0.99995f)]
        [DataRow(1.00005f)]
        [DataRow(1.0001f)]
        [DataRow(1.0002f)]
        public void TypedConstantPowerExponentDoesNotTriggerLegacyRenderedTextIdentityShortcut(float exponentValue)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3PowerIdentity.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = package.CreateExport("Base", "MaterialExpressionScalarParameter", material, indexed: false);
            var b = package.CreateExport("Exponent", "MaterialExpressionConstant", material, indexed: false);
            b.WriteProperty(new FloatProperty(exponentValue, "R"));
            var power = package.CreateExport("Power", "MaterialExpressionPower", material, indexed: false);
            power.WriteProperty(Input("Base", a.UIndex));
            power.WriteProperty(Input("Exponent", b.UIndex));
            material.WriteProperty(Input("EmissiveColor", power.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex), new(power.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                string type = half ? "half" : "float";
                Assert.AreEqual($"\t{type} Local0 = pow(UniformPixelScalars_0.x,{type}({exponentValue.ToString("F8", System.Globalization.CultureInfo.InvariantCulture)}));\r\n\treturn Local0", result.PropertyCode["EmissiveColor"]);
                Assert.AreEqual(1, result.UniformPixelScalarExpressions.Count);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void PowerDynamicParametersAllocateExponentThenBaseAndNeverFold(bool half)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3PowerParameters.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = Parameter("Base", 2);
            var b = Parameter("Exponent", 1);
            var power = package.CreateExport("Power", "MaterialExpressionPower", material, indexed: false);
            power.WriteProperty(Input("Base", a.UIndex));
            power.WriteProperty(Input("Exponent", b.UIndex));
            material.WriteProperty(Input("EmissiveColor", power.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", power.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex), new(power.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            CollectionAssert.AreEqual(new[] { "Exponent", "Base" }, result.UniformPixelScalarExpressions.Cast<MaterialUniformExpressionScalarParameter>().Select(p => p.ParameterName.Name).ToArray());
            string type = half ? "half" : "float";
            string expected = $"\t{type} Local0 = pow(UniformPixelScalars_0.y,UniformPixelScalars_0.x);\r\n\treturn Local0";
            Assert.AreEqual(expected, result.PropertyCode["EmissiveColor"]);
            Assert.AreEqual(expected.Replace("Local0", "Local1").Replace("UniformPixel", "UniformVertex"), result.PropertyCode["WorldPositionOffset"]);
            Assert.AreSame(result.UniformPixelScalarExpressions[0], result.UniformVertexScalarExpressions[0]);
            Assert.AreSame(result.UniformPixelScalarExpressions[1], result.UniformVertexScalarExpressions[1]);
            ExportEntry Parameter(string name, float defaultValue)
            {
                var node = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false);
                node.WriteProperty(new NameProperty(name, "ParameterName"));
                node.WriteProperty(new FloatProperty(defaultValue, "DefaultValue"));
                return node;
            }
        }

        [TestMethod]
        public void PowerRequiresBothConnectionsEvenWhenModernDefaultPropertiesArePresent()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3PowerErrors.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var constant = package.CreateExport("Constant", "MaterialExpressionConstant", material, indexed: false);
            var power = package.CreateExport("Power", "MaterialExpressionPower", material, indexed: false);
            power.WriteProperty(new FloatProperty(2, "ConstExponent"));
            material.WriteProperty(Input("EmissiveColor", power.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(constant.UIndex), new(power.UIndex)], "Expressions"));
            Check("Missing Power Base input");
            power.WriteProperty(Input("Base", constant.UIndex));
            Check("Missing Power Exponent input");
            power.WriteProperty(Input("Exponent", power.UIndex));
            Check("cycle");
            void Check(string expected)
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
                Assert.IsFalse(result.Success);
                Assert.IsNull(result.MaterialHlsl);
                Assert.IsTrue(result.Errors.Any(e => e.Contains(expected, StringComparison.OrdinalIgnoreCase)));
            }
        }

        [TestMethod]
        public void PowerConstantFoldedExponentIsNotCpuEvaluatedForIdentityAndResultIsNonuniform()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3PowerFoldedExponent.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = package.CreateExport("Base", "MaterialExpressionScalarParameter", material, indexed: false);
            var two = package.CreateExport("Two", "MaterialExpressionConstant", material, indexed: false);
            two.WriteProperty(new FloatProperty(2, "R"));
            var one = package.CreateExport("One", "MaterialExpressionConstant", material, indexed: false);
            one.WriteProperty(new FloatProperty(1, "R"));
            var subtract = package.CreateExport("Subtract", "MaterialExpressionSubtract", material, indexed: false);
            subtract.WriteProperty(Input("A", two.UIndex));
            subtract.WriteProperty(Input("B", one.UIndex));
            var power = package.CreateExport("Power", "MaterialExpressionPower", material, indexed: false);
            power.WriteProperty(Input("Base", a.UIndex));
            power.WriteProperty(Input("Exponent", subtract.UIndex));
            var add = package.CreateExport("Add", "MaterialExpressionAdd", material, indexed: false);
            add.WriteProperty(Input("A", power.UIndex));
            add.WriteProperty(Input("B", one.UIndex));
            material.WriteProperty(Input("EmissiveColor", add.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(two.UIndex), new(one.UIndex), new(subtract.UIndex), new(power.UIndex), new(add.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual("\tfloat Local0 = pow(UniformPixelScalars_0.x,(float(2.00000000) - float(1.00000000)));\r\n" +
                "\tfloat Local1 = (Local0 + float(1.00000000));\r\n\treturn Local1", result.PropertyCode["EmissiveColor"]);
            Assert.AreEqual(1, result.UniformPixelScalarExpressions.Count);
            Assert.IsInstanceOfType<MaterialUniformExpressionScalarParameter>(result.UniformPixelScalarExpressions[0]);
        }

        [TestMethod]
        public void UniformClampEvaluatesInputAndBothBoundsIntoSeparateValues()
        {
            var tree = new MaterialUniformExpressionClamp
            {
                Input = new MaterialUniformExpressionConstant { Value = new LinearColor(-2, 0.5f, 2, 7), ValueType = 8 },
                Min = new MaterialUniformExpressionConstant { Value = new LinearColor(-1, 0, 1, 8), ValueType = 8 },
                Max = new MaterialUniformExpressionConstant { Value = new LinearColor(1, 1, 3, 9), ValueType = 8 }
            };
            LinearColor evaluated = default;
            tree.GetNumberValue(new UniformExpressionRenderContext(new(), new(), 0, 0, null), ref evaluated);
            Assert.AreEqual(new Vector4(-1, 0.5f, 2, 8), (Vector4)evaluated);
        }

        [TestMethod]
        public void CrossOfUniformAppendDropsUniformStatusBeforeFollowingDot()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CrossAppendDot.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var p = package.CreateExport("P", "MaterialExpressionScalarParameter", material, indexed: false);
            var tail = package.CreateExport("Tail", "MaterialExpressionConstant2Vector", material, indexed: false);
            var right = package.CreateExport("Right", "MaterialExpressionConstant3Vector", material, indexed: false);
            var scalar = package.CreateExport("Scalar", "MaterialExpressionConstant", material, indexed: false);
            scalar.WriteProperty(new FloatProperty(2, "R"));
            var append = Binary("Append", "AppendVector", p, tail);
            var cross = Binary("Cross", "CrossProduct", append, right);
            var dot = Binary("Dot", "DotProduct", cross, scalar);
            material.WriteProperty(Input("EmissiveColor", dot.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", dot.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(p.UIndex), new(tail.UIndex), new(right.UIndex), new(scalar.UIndex), new(append.UIndex), new(cross.UIndex), new(dot.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual("\tfloat3 Local0 = cross(UniformPixelVector_0.rgb,float3(0.00000000,0.00000000,0.00000000));\r\n" +
                "\tfloat Local1 = dot(Local0,float3(float(2.00000000),float(2.00000000),float(2.00000000)));\r\n\treturn Local1", result.PropertyCode["EmissiveColor"]);
            Assert.AreEqual(1, result.UniformPixelVectorExpressions.Count);
            Assert.AreEqual(1, result.UniformPixelScalarExpressions.Count);
            Assert.IsInstanceOfType<MaterialUniformExpressionAppendVector>(result.UniformPixelVectorExpressions[0]);
            Assert.IsInstanceOfType<MaterialUniformExpressionScalarParameter>(result.UniformPixelScalarExpressions[0]);
            Assert.AreSame(result.UniformPixelVectorExpressions[0], result.UniformVertexVectorExpressions[0]);
            ExportEntry Binary(string name, string operation, ExportEntry a, ExportEntry b)
            {
                var node = package.CreateExport(name, "MaterialExpression" + operation, material, indexed: false);
                node.WriteProperty(Input("A", a.UIndex));
                node.WriteProperty(Input("B", b.UIndex));
                return node;
            }
        }

        [TestMethod]
        public void PowerDoesNotReplaceNegativeBaseWithClampedPow()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3PowerNegative.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var a = package.CreateExport("Base", "MaterialExpressionConstant", material, indexed: false);
            a.WriteProperty(new FloatProperty(-2, "R"));
            var b = package.CreateExport("Exponent", "MaterialExpressionConstant", material, indexed: false);
            b.WriteProperty(new FloatProperty(0.5f, "R"));
            var power = package.CreateExport("Power", "MaterialExpressionPower", material, indexed: false);
            power.WriteProperty(Input("Base", a.UIndex));
            power.WriteProperty(Input("Exponent", b.UIndex));
            material.WriteProperty(Input("EmissiveColor", power.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(a.UIndex), new(b.UIndex), new(power.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual("\tfloat Local0 = pow(float(-2.00000000),float(0.50000000));\r\n\treturn Local0", result.PropertyCode["EmissiveColor"]);
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void ClampOptionalBoundsDispatchPackDeduplicateAndRoundTrip(bool hasMin, bool hasMax)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ClampBounds.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = Parameter("X", 5);
            var min = Parameter("L", 1);
            var max = Parameter("U", 3);
            var clamp = Clamp("Clamp");
            var duplicate = Clamp("Duplicate");
            material.WriteProperty(Input("EmissiveColor", clamp.UIndex));
            material.WriteProperty(Input("DiffuseColor", duplicate.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", clamp.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(min.UIndex), new(max.UIndex), new(clamp.UIndex), new(duplicate.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                string[] order = hasMin && hasMax ? ["U", "L", "X"] : hasMin ? ["L", "X"] : hasMax ? ["U", "X"] : ["X"];
                int count = order.Length + (hasMin || hasMax ? 1 : 0);
                Assert.AreEqual(count, result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(count, result.UniformVertexScalarExpressions.Count);
                CollectionAssert.AreEqual(order, result.UniformPixelScalarExpressions.Take(order.Length)
                    .Cast<MaterialUniformExpressionScalarParameter>().Select(p => p.ParameterName.Name).ToArray());
                Assert.AreEqual($"\treturn UniformPixelScalars_0.{"xyzw"[count - 1]}", result.PropertyCode["EmissiveColor"]);
                Assert.AreEqual(result.PropertyCode["EmissiveColor"], result.PropertyCode["DiffuseColor"]);
                for (int i = 0; i < count; i++) Assert.AreSame(result.UniformPixelScalarExpressions[i], result.UniformVertexScalarExpressions[i]);
                var tree = result.UniformPixelScalarExpressions.Last();
                Assert.AreEqual(hasMin && hasMax ? typeof(MaterialUniformExpressionClamp) : hasMin ? typeof(MaterialUniformExpressionMax) : hasMax ? typeof(MaterialUniformExpressionMin) : typeof(MaterialUniformExpressionScalarParameter), tree.GetType());
                using var stream = new MemoryStream();
                tree.Serialize(new SerializingContainer(stream, package));
                stream.Position = 0;
                var reader = new SerializingContainer(stream, package, isLoading: true);
                var restored = MaterialUniformExpression.Create(reader);
                restored.Serialize(reader);
                Assert.AreEqual(stream.Length, stream.Position);
                var context = new UniformExpressionRenderContext(new(), new(), 0, 0, null);
                Check(hasMax ? 3 : 5);
                context.ScalarParameterValues["X"] = -3;
                Check(hasMin ? 1 : -3);
                void Check(float expected)
                {
                    LinearColor value = default, value2 = default;
                    tree.GetNumberValue(context, ref value);
                    restored.GetNumberValue(context, ref value2);
                    Assert.AreEqual(new Vector4(expected, 0, 0, 0), (Vector4)value);
                    Assert.AreEqual((Vector4)value, (Vector4)value2);
                }
            }
            ExportEntry Parameter(string name, float defaultValue)
            {
                var node = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false);
                node.WriteProperty(new NameProperty(name, "ParameterName"));
                node.WriteProperty(new FloatProperty(defaultValue, "DefaultValue"));
                return node;
            }
            ExportEntry Clamp(string name)
            {
                var node = package.CreateExport(name, "MaterialExpressionClamp", material, indexed: false);
                node.WriteProperty(Input("Input", x.UIndex));
                if (hasMin) node.WriteProperty(Input("Min", min.UIndex));
                if (hasMax) node.WriteProperty(Input("Max", max.UIndex));
                node.WriteProperty(new FloatProperty(-100, "MinDefault"));
                node.WriteProperty(new FloatProperty(100, "MaxDefault"));
                return node;
            }
        }

        [TestMethod]
        [DataRow(1, 1)]
        [DataRow(1, 2)]
        [DataRow(1, 3)]
        [DataRow(1, 4)]
        [DataRow(2, 1)]
        [DataRow(2, 2)]
        [DataRow(2, 3)]
        [DataRow(2, 4)]
        [DataRow(3, 1)]
        [DataRow(3, 2)]
        [DataRow(3, 3)]
        [DataRow(3, 4)]
        [DataRow(4, 1)]
        [DataRow(4, 2)]
        [DataRow(4, 3)]
        [DataRow(4, 4)]
        public void ClampCoercesBoundsToInputTypeEvenForConstantUniforms(int width, int boundWidth)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ClampWidths.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = Constant("X", width, 2);
            var min = Constant("L", boundWidth, 0);
            var max = Constant("U", boundWidth, 10);
            var clamp = package.CreateExport("Clamp", "MaterialExpressionClamp", material, indexed: false);
            clamp.WriteProperty(Input("Input", x.UIndex));
            clamp.WriteProperty(Input("Min", min.UIndex));
            clamp.WriteProperty(Input("Max", max.UIndex));
            string root = width == 2 ? "Distortion" : "EmissiveColor";
            material.WriteProperty(width == 4 ? MaskedInput(root, clamp.UIndex, r: true, g: true, b: true) : Input(root, clamp.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(min.UIndex), new(max.UIndex), new(clamp.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                if (width > 1 && boundWidth > 1 && width != boundWidth)
                {
                    Assert.IsFalse(result.Success);
                    Assert.IsNull(result.MaterialHlsl);
                    StringAssert.Contains(string.Join("; ", result.Errors), "Clamp bound coercion failed");
                    continue;
                }
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                string type = half ? "half" : "float";
                string lower = Literal(type, boundWidth, 0), upper = Literal(type, boundWidth, 10);
                if (width > 1 && boundWidth == 1)
                {
                    lower = $"{type}{width}({string.Join(',', Enumerable.Repeat(lower, width))})";
                    upper = $"{type}{width}({string.Join(',', Enumerable.Repeat(upper, width))})";
                }
                Assert.AreEqual($"\treturn min(max({Literal(type, width, 2)},{lower}),{upper})" + (width == 4 ? ".rgb" : ""), result.PropertyCode[root]);
                Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
            }
            ExportEntry Constant(string name, int count, int start)
            {
                var node = package.CreateExport(name, count == 1 ? "MaterialExpressionConstant" : "MaterialExpressionConstant" + count + "Vector", material, indexed: false);
                for (int i = 0; i < count; i++) node.WriteProperty(new FloatProperty(start + i, "RGBA"[i].ToString()));
                return node;
            }
            static string Literal(string type, int count, int start) => type + (count == 1 ? "" : count.ToString()) + "(" +
                string.Join(',', Enumerable.Range(start, count).Select(n => n + ".00000000")) + ")";
        }

        [TestMethod]
        public void NativeClampReversedBoundsNanAndSignedZeroDoNotUseMathClamp()
        {
            var tree = new MaterialUniformExpressionClamp
            {
                Input = Constant(new LinearColor(0, 3, float.NaN, -0f)),
                Min = Constant(new LinearColor(2, 2, -5, 0f)),
                Max = Constant(new LinearColor(1, 1, 5, -0f))
            };
            LinearColor value = default;
            tree.GetNumberValue(new UniformExpressionRenderContext(new(), new(), 0, 0, null), ref value);
            Assert.AreEqual(new Vector4(2, 1, -5, -0f), (Vector4)value);
            Assert.AreEqual(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits(value.A));
            static MaterialUniformExpressionConstant Constant(LinearColor color) => new() { Value = color, ValueType = 8 };
        }

        [TestMethod]
        public void NativeMinMaxChooseSecondOperandForNanAndSignedZeroEquality()
        {
            var a = new MaterialUniformExpressionConstant { Value = new LinearColor(float.NaN, 2, -0f, 0f), ValueType = 8 };
            var b = new MaterialUniformExpressionConstant { Value = new LinearColor(1, float.NaN, 0f, -0f), ValueType = 8 };
            foreach (MaterialUniformExpressionBinaryOp tree in new MaterialUniformExpressionBinaryOp[]
            {
                new MaterialUniformExpressionMin { A = a, B = b }, new MaterialUniformExpressionMax { A = a, B = b }
            })
            {
                LinearColor value = default;
                tree.GetNumberValue(new UniformExpressionRenderContext(new(), new(), 0, 0, null), ref value);
                Assert.AreEqual(1f, value.R);
                Assert.IsTrue(float.IsNaN(value.G));
                Assert.AreEqual(0, BitConverter.SingleToInt32Bits(value.B));
                Assert.AreEqual(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits(value.A));
            }
        }

        [TestMethod]
        public void ClampRequiresInputAndReportsCycleThroughConnectedBound()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ClampErrors.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var constant = package.CreateExport("Constant", "MaterialExpressionConstant", material, indexed: false);
            var clamp = package.CreateExport("Clamp", "MaterialExpressionClamp", material, indexed: false);
            material.WriteProperty(Input("EmissiveColor", clamp.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(constant.UIndex), new(clamp.UIndex)], "Expressions"));
            var missing = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsFalse(missing.Success);
            CollectionAssert.Contains(missing.Errors.ToArray(), "Missing Clamp input");
            clamp.WriteProperty(Input("Input", constant.UIndex));
            clamp.WriteProperty(Input("Max", clamp.UIndex));
            var cycle = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsFalse(cycle.Success);
            Assert.IsTrue(cycle.Errors.Any(e => e.Contains("cycle", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void NonuniformClampUsesLocalOrPassthroughAndCoercesSelectedBounds(bool hasMin, bool hasMax)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ClampLocal.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = package.CreateExport("X", "MaterialExpressionVertexColor", material, indexed: false);
            var min = Parameter("Min");
            var max = Parameter("Max");
            var clamp = package.CreateExport("Clamp", "MaterialExpressionClamp", material, indexed: false);
            clamp.WriteProperty(MaskedInput("Input", x.UIndex, r: true, g: true, b: true));
            if (hasMin) clamp.WriteProperty(Input("Min", min.UIndex));
            if (hasMax) clamp.WriteProperty(Input("Max", max.UIndex));
            material.WriteProperty(Input("EmissiveColor", clamp.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", clamp.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(min.UIndex), new(max.UIndex), new(clamp.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                Assert.AreEqual((hasMin ? 1 : 0) + (hasMax ? 1 : 0), result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
                if (!hasMin && !hasMax)
                {
                    Assert.AreEqual("\treturn Parameters.VertexColor.rgb", result.PropertyCode["EmissiveColor"]);
                    Assert.AreEqual(result.PropertyCode["EmissiveColor"], result.PropertyCode["WorldPositionOffset"]);
                    continue;
                }
                string type = half ? "half" : "float";
                string upper = $"{type}3({string.Join(',', Enumerable.Repeat("UniformPixelScalars_0.x", 3))})";
                string lower = $"{type}3({string.Join(',', Enumerable.Repeat("UniformPixelScalars_0." + (hasMax ? 'y' : 'x'), 3))})";
                string code = hasMin && hasMax ? $"min(max(Parameters.VertexColor.rgb,{lower}),{upper})"
                    : hasMin ? $"max(Parameters.VertexColor.rgb,{lower})" : $"min(Parameters.VertexColor.rgb,{upper})";
                string expected = $"\t{type}3 Local0 = {code};\r\n\treturn Local0";
                Assert.AreEqual(expected, result.PropertyCode["EmissiveColor"]);
                Assert.AreEqual(expected.Replace("Local0", "Local1").Replace("UniformPixel", "UniformVertex"), result.PropertyCode["WorldPositionOffset"]);
            }
            ExportEntry Parameter(string name)
            {
                var node = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false);
                node.WriteProperty(new NameProperty(name, "ParameterName"));
                return node;
            }
        }

        [TestMethod]
        public void UniformVectorClampRetainsUnreplicatedScalarBoundsAndOrderedIdentity()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ClampVectorUniform.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = package.CreateExport("X", "MaterialExpressionVectorParameter", material, indexed: false);
            x.WriteProperty(new NameProperty("X", "ParameterName"));
            x.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
            {
                new FloatProperty(5, "R"), new FloatProperty(6, "G"), new FloatProperty(7, "B"), new FloatProperty(8, "A")
            }, "DefaultValue", isImmutable: true));
            var min = Parameter("L", 1);
            var max = Parameter("U", 3);
            var clamp = Clamp("Clamp", min, max);
            var duplicate = Clamp("Duplicate", min, max);
            var reversed = Clamp("Reversed", max, min);
            material.WriteProperty(MaskedInput("EmissiveColor", clamp.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("DiffuseColor", duplicate.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("SpecularColor", reversed.UIndex, r: true, g: true, b: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(min.UIndex), new(max.UIndex), new(clamp.UIndex), new(duplicate.UIndex), new(reversed.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(3, result.UniformPixelVectorExpressions.Count);
            Assert.AreEqual(2, result.UniformPixelScalarExpressions.Count);
            Assert.AreEqual("\treturn UniformPixelVector_1.rgb", result.PropertyCode["EmissiveColor"]);
            Assert.AreEqual(result.PropertyCode["EmissiveColor"], result.PropertyCode["DiffuseColor"]);
            Assert.AreEqual("\treturn UniformPixelVector_2.rgb", result.PropertyCode["SpecularColor"]);
            var tree = (MaterialUniformExpressionClamp)result.UniformPixelVectorExpressions[1];
            var reverseTree = (MaterialUniformExpressionClamp)result.UniformPixelVectorExpressions[2];
            Assert.AreSame(tree.Input, reverseTree.Input);
            Assert.AreSame(tree.Min, reverseTree.Max);
            Assert.AreSame(tree.Max, reverseTree.Min);
            var context = new UniformExpressionRenderContext(new(), new(), 0, 0, null);
            LinearColor value = default;
            tree.GetNumberValue(context, ref value);
            Assert.AreEqual(new Vector4(3, 0, 0, 0), (Vector4)value);
            context.VectorParameterValues["X"] = new LinearColor(-2, -3, 9, 8);
            tree.GetNumberValue(context, ref value);
            Assert.AreEqual(new Vector4(1, 0, 0, 0), (Vector4)value);
            ExportEntry Parameter(string name, float defaultValue)
            {
                var node = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false);
                node.WriteProperty(new NameProperty(name, "ParameterName"));
                node.WriteProperty(new FloatProperty(defaultValue, "DefaultValue"));
                return node;
            }
            ExportEntry Clamp(string name, ExportEntry low, ExportEntry high)
            {
                var node = package.CreateExport(name, "MaterialExpressionClamp", material, indexed: false);
                node.WriteProperty(Input("Input", x.UIndex));
                node.WriteProperty(Input("Min", low.UIndex));
                node.WriteProperty(Input("Max", high.UIndex));
                return node;
            }
        }

        [TestMethod]
        [DataRow("Abs", 1)]
        [DataRow("Abs", 2)]
        [DataRow("Abs", 3)]
        [DataRow("Abs", 4)]
        [DataRow("Floor", 1)]
        [DataRow("Floor", 2)]
        [DataRow("Floor", 3)]
        [DataRow("Floor", 4)]
        [DataRow("Ceil", 1)]
        [DataRow("Ceil", 2)]
        [DataRow("Ceil", 3)]
        [DataRow("Ceil", 4)]
        [DataRow("Frac", 1)]
        [DataRow("Frac", 2)]
        [DataRow("Frac", 3)]
        [DataRow("Frac", 4)]
        [DataRow("SquareRoot", 1)]
        [DataRow("SquareRoot", 2)]
        [DataRow("SquareRoot", 3)]
        [DataRow("SquareRoot", 4)]
        public void NumericUnaryPreservesConstantWidthAndConnectionMasksSelectLocalPath(string operation, int width)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3UnaryWidths.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = package.CreateExport("X", width == 1 ? "MaterialExpressionConstant" : "MaterialExpressionConstant" + width + "Vector", material, indexed: false);
            for (int i = 0; i < width; i++) x.WriteProperty(new FloatProperty(i - 1.25f, "RGBA"[i].ToString()));
            var unary = package.CreateExport("Unary", "MaterialExpression" + operation, material, indexed: false);
            unary.WriteProperty(Input("Input", x.UIndex));
            string root = width == 2 ? "Distortion" : "EmissiveColor";
            material.WriteProperty(width == 4 ? MaskedInput(root, unary.UIndex, r: true, g: true, b: true) : Input(root, unary.UIndex));
            material.WriteProperty(width is 1 or 3 ? Input("WorldPositionOffset", unary.UIndex) : MaskedInput("WorldPositionOffset", unary.UIndex, r: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(unary.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                string type = half ? "half" : "float";
                string valueType = type + (width == 1 ? "" : width.ToString());
                string literal = valueType + "(" + string.Join(',', Enumerable.Range(0, width).Select(i => (i - 1.25f).ToString("F8", System.Globalization.CultureInfo.InvariantCulture))) + ")";
                string op = operation == "SquareRoot" ? "sqrt" : operation.ToLowerInvariant();
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                Assert.AreEqual($"\treturn {op}({literal})" + (width == 4 ? ".rgb" : ""), result.PropertyCode[root]);
                Assert.AreEqual($"\treturn {op}({literal})" + (width is 1 or 3 ? "" : ".r"), result.PropertyCode["WorldPositionOffset"]);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
                Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
                unary.WriteProperty(MaskedInput("Input", x.UIndex, r: true, g: width > 1, b: width > 2, a: width > 3));
                var masked = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(masked.Success, string.Join("; ", masked.Errors));
                Assert.AreEqual($"\t{valueType} Local0 = {op}({literal}.{"rgba"[..width]});\r\n\treturn Local0" + (width == 4 ? ".rgb" : ""), masked.PropertyCode[root]);
                Assert.AreEqual($"\t{valueType} Local1 = {op}({literal}.{"rgba"[..width]});\r\n\treturn Local1" + (width is 1 or 3 ? "" : ".r"), masked.PropertyCode["WorldPositionOffset"]);
                unary.WriteProperty(Input("Input", x.UIndex));
            }
        }

        [TestMethod]
        [DataRow("Sine", 1)]
        [DataRow("Sine", 2)]
        [DataRow("Sine", 3)]
        [DataRow("Sine", 4)]
        [DataRow("Cosine", 1)]
        [DataRow("Cosine", 2)]
        [DataRow("Cosine", 3)]
        [DataRow("Cosine", 4)]
        public void TrigUniformReturnsScalarButMaskedLocalPreservesWidth(string operation, int width)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TrigWidths.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = package.CreateExport("X", width == 1 ? "MaterialExpressionConstant" : "MaterialExpressionConstant" + width + "Vector", material, indexed: false);
            for (int i = 0; i < width; i++) x.WriteProperty(new FloatProperty(i + 1, "RGBA"[i].ToString()));
            var node = package.CreateExport("Trig", "MaterialExpression" + operation, material, indexed: false);
            node.WriteProperty(Input("Input", x.UIndex));
            material.WriteProperty(Input("Opacity", node.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", node.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(node.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                string type = half ? "half" : "float";
                string valueType = type + (width == 1 ? "" : width.ToString());
                string literal = valueType + "(" + string.Join(',', Enumerable.Range(1, width).Select(i => i + ".00000000")) + ")";
                string scale = type + "(6.28318548)";
                string op = operation == "Sine" ? "sin" : "cos";
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                Assert.AreEqual($"\treturn {op}(({literal} * {scale}))", result.PropertyCode["Opacity"]);
                Assert.AreEqual(result.PropertyCode["Opacity"], result.PropertyCode["WorldPositionOffset"]);
                Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
                node.WriteProperty(MaskedInput("Input", x.UIndex, r: true, g: width > 1, b: width > 2, a: width > 3));
                material.WriteProperty(MaskedInput("Opacity", node.UIndex, r: true));
                material.WriteProperty(MaskedInput("WorldPositionOffset", node.UIndex, r: true));
                var masked = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(masked.Success, string.Join("; ", masked.Errors));
                string input = literal + "." + "rgba"[..width];
                string mul = width == 1 ? $"({input} * {scale})" : $"({scale} * {input})";
                StringAssert.Contains(masked.PropertyCode["Opacity"], $"\t{valueType} Local0 = {mul};\r\n\t{valueType} Local1 = {op}(Local0);");
                StringAssert.Contains(masked.PropertyCode["WorldPositionOffset"], $"\t{valueType} Local2 = {mul};\r\n\t{valueType} Local3 = {op}(Local2);");
                node.WriteProperty(Input("Input", x.UIndex));
                material.WriteProperty(Input("Opacity", node.UIndex));
                material.WriteProperty(Input("WorldPositionOffset", node.UIndex));
            }
        }

        [TestMethod]
        [DataRow("Sine", false)]
        [DataRow("Sine", true)]
        [DataRow("Cosine", false)]
        [DataRow("Cosine", true)]
        public void TrigUniformTreesDeduplicateFlagShareStagesAndSerialize(string operation, bool vector)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TrigUniforms.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = package.CreateExport("X", vector ? "MaterialExpressionVectorParameter" : "MaterialExpressionScalarParameter", material, indexed: false);
            x.WriteProperty(new NameProperty("X", "ParameterName"));
            if (vector) x.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
            {
                new FloatProperty(0.25f, "R"), new FloatProperty(5, "G"), new FloatProperty(7, "B"), new FloatProperty(9, "A")
            }, "DefaultValue", isImmutable: true));
            else x.WriteProperty(new FloatProperty(0.25f, "DefaultValue"));
            var primary = Node("Primary", operation);
            var duplicate = Node("Duplicate", operation);
            var other = Node("Other", operation == "Sine" ? "Cosine" : "Sine");
            material.WriteProperty(Input("EmissiveColor", primary.UIndex));
            material.WriteProperty(Input("DiffuseColor", duplicate.UIndex));
            material.WriteProperty(Input("Opacity", other.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", primary.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(primary.UIndex), new(duplicate.UIndex), new(other.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                int index = vector ? 0 : 2;
                Assert.AreEqual(vector ? 2 : 4, result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(vector ? 1 : 3, result.UniformVertexScalarExpressions.Count);
                Assert.AreEqual(vector ? 2 : 0, result.UniformPixelVectorExpressions.Count);
                var tree = (MaterialUniformExpressionSine)result.UniformPixelScalarExpressions[index];
                var opposite = (MaterialUniformExpressionSine)result.UniformPixelScalarExpressions[index + 1];
                Assert.AreSame(tree, result.UniformVertexScalarExpressions[index]);
                Assert.AreEqual(operation == "Cosine", tree.bIsCosine);
                Assert.AreNotEqual(tree.bIsCosine, opposite.bIsCosine);
                Assert.AreSame(tree.X, opposite.X);
                var folded = (MaterialUniformExpressionFoldedMath)tree.X;
                Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Mul, folded.Op);
                Assert.AreEqual(3.1415927f, ((MaterialUniformExpressionConstant)folded.B).Value.R);
                Assert.AreEqual(result.PropertyCode["EmissiveColor"], result.PropertyCode["DiffuseColor"]);
                Assert.AreEqual($"\treturn UniformPixelScalars_0.{"xyzw"[index]}", result.PropertyCode["EmissiveColor"]);
                using var stream = new MemoryStream();
                tree.Serialize(new SerializingContainer(stream, package));
                Assert.AreEqual(tree.bIsCosine ? 1 : 0, BitConverter.ToInt32(stream.ToArray(), (int)stream.Length - 4));
                stream.Position = 0;
                var reader = new SerializingContainer(stream, package, isLoading: true);
                var restored = (MaterialUniformExpressionSine)MaterialUniformExpression.Create(reader);
                restored.Serialize(reader);
                Assert.AreEqual(stream.Length, stream.Position);
                Assert.AreEqual(tree.bIsCosine, restored.bIsCosine);
                var context = new UniformExpressionRenderContext(new(), new(), 0, 0, null);
                Check(0.25f);
                if (vector) context.VectorParameterValues["X"] = new LinearColor(-0.5f, 20, 30, 40);
                else context.ScalarParameterValues["X"] = -0.5f;
                Check(-0.5f);
                void Check(float input)
                {
                    float expected = operation == "Cosine" ? MathF.Cos(input * 3.1415927f) : MathF.Sin(input * 3.1415927f);
                    foreach (var expression in new[] { tree, restored })
                    {
                        LinearColor output = new(10, 11, 12, 13);
                        expression.GetNumberValue(context, ref output);
                        Assert.AreEqual(new Vector4(expected, 11, 12, 13), (Vector4)output);
                    }
                }
            }
            ExportEntry Node(string name, string op)
            {
                var node = package.CreateExport(name, "MaterialExpression" + op, material, indexed: false);
                node.WriteProperty(Input("Input", x.UIndex));
                node.WriteProperty(new FloatProperty(2, "Period"));
                return node;
            }
        }

        [TestMethod]
        [DataRow(0f)]
        [DataRow(-1f)]
        [DataRow(float.NegativeInfinity)]
        [DataRow(float.NaN)]
        public void SineNonpositiveOrUnorderedPeriodSkipsScaleAndMultiply(float period)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SineUnscaled.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = package.CreateExport("X", "MaterialExpressionScalarParameter", material, indexed: false);
            x.WriteProperty(new FloatProperty(0.25f, "DefaultValue"));
            var node = package.CreateExport("Sine", "MaterialExpressionSine", material, indexed: false);
            node.WriteProperty(new FloatProperty(period, "Period"));
            node.WriteProperty(Input("Input", x.UIndex));
            material.WriteProperty(Input("Opacity", node.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(node.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(2, result.UniformPixelScalarExpressions.Count);
            var tree = (MaterialUniformExpressionSine)result.UniformPixelScalarExpressions[1];
            Assert.AreSame(result.UniformPixelScalarExpressions[0], tree.X);
            LinearColor output = default;
            tree.GetNumberValue(new UniformExpressionRenderContext(new(), new(), 0, 0, null), ref output);
            Assert.AreEqual(MathF.Sin(0.25f), output.R);
        }

        [TestMethod]
        [DataRow(0f)]
        [DataRow(-1f)]
        [DataRow(float.NegativeInfinity)]
        [DataRow(float.NaN)]
        public void CosineNonpositiveOrUnorderedPeriodFailsInsteadOfInventingConstantZero(float period)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CosineIndexZero.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = package.CreateExport("X", "MaterialExpressionConstant", material, indexed: false);
            var node = package.CreateExport("Cosine", "MaterialExpressionCosine", material, indexed: false);
            node.WriteProperty(new FloatProperty(period, "Period"));
            node.WriteProperty(Input("Input", x.UIndex));
            material.WriteProperty(Input("Opacity", node.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(node.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.MaterialHlsl);
            StringAssert.Contains(string.Join("; ", result.Errors), "native code-chunk index 0");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void SquareRootUniformPreservesChunkWidthButCpuWritesOnlyRed(bool vector)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SquareRootUniform.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = package.CreateExport("X", vector ? "MaterialExpressionVectorParameter" : "MaterialExpressionScalarParameter", material, indexed: false);
            x.WriteProperty(new NameProperty("X", "ParameterName"));
            if (vector) x.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
            {
                new FloatProperty(4, "R"), new FloatProperty(9, "G"), new FloatProperty(16, "B"), new FloatProperty(25, "A")
            }, "DefaultValue", isImmutable: true));
            else x.WriteProperty(new FloatProperty(4, "DefaultValue"));
            var primary = Node("Primary", x);
            var duplicate = Node("Duplicate", x);
            var nested = Node("Nested", primary);
            material.WriteProperty(Root("EmissiveColor", primary));
            material.WriteProperty(Root("DiffuseColor", duplicate));
            material.WriteProperty(Root("WorldPositionOffset", primary));
            material.WriteProperty(vector ? MaskedInput("Opacity", nested.UIndex, r: true) : Input("Opacity", nested.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(primary.UIndex), new(duplicate.UIndex), new(nested.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                var pixel = vector ? result.UniformPixelVectorExpressions : result.UniformPixelScalarExpressions;
                var vertex = vector ? result.UniformVertexVectorExpressions : result.UniformVertexScalarExpressions;
                Assert.AreEqual(3, pixel.Count);
                Assert.AreEqual(2, vertex.Count);
                Assert.AreSame(pixel[1], vertex[1]);
                Assert.AreEqual(result.PropertyCode["EmissiveColor"], result.PropertyCode["DiffuseColor"]);
                Assert.AreEqual(vector ? "\treturn UniformPixelVector_1.rgb" : "\treturn UniformPixelScalars_0.y", result.PropertyCode["EmissiveColor"]);
                var tree = (MaterialUniformExpressionSquareRoot)pixel[1];
                var nestedTree = (MaterialUniformExpressionSquareRoot)pixel[2];
                Assert.AreSame(pixel[0], tree.X);
                Assert.AreSame(tree, nestedTree.X);
                using var stream = new MemoryStream();
                nestedTree.Serialize(new SerializingContainer(stream, package));
                stream.Position = 0;
                var reader = new SerializingContainer(stream, package, isLoading: true);
                var restored = (MaterialUniformExpressionSquareRoot)MaterialUniformExpression.Create(reader);
                restored.Serialize(reader);
                Assert.AreEqual(stream.Length, stream.Position);
                var context = new UniformExpressionRenderContext(new(), new(), 0, 0, null);
                Check(4);
                if (vector) context.VectorParameterValues["X"] = new LinearColor(81, 100, 121, 144);
                else context.ScalarParameterValues["X"] = 81;
                Check(81);
                if (vector) context.VectorParameterValues["X"] = new LinearColor(-1, 9, 16, 25);
                else context.ScalarParameterValues["X"] = -1;
                LinearColor negative = default;
                tree.GetNumberValue(context, ref negative);
                Assert.IsTrue(float.IsNaN(negative.R));
                void Check(float red)
                {
                    LinearColor output = new(10, 11, 12, 13);
                    tree.GetNumberValue(context, ref output);
                    Assert.AreEqual(new Vector4(MathF.Sqrt(red), 11, 12, 13), (Vector4)output);
                    restored.GetNumberValue(context, ref output);
                    Assert.AreEqual(new Vector4(MathF.Sqrt(MathF.Sqrt(red)), 11, 12, 13), (Vector4)output);
                }
            }
            StructProperty Root(string name, ExportEntry node) => vector ? MaskedInput(name, node.UIndex, r: true, g: true, b: true) : Input(name, node.UIndex);
            ExportEntry Node(string name, ExportEntry child)
            {
                var node = package.CreateExport(name, "MaterialExpressionSquareRoot", material, indexed: false);
                node.WriteProperty(Input("Input", child.UIndex));
                return node;
            }
        }

        [TestMethod]
        public void ShippingExpressionDefaultsMatchTrigAndTimeFallbacks()
        {
            GlobalTest.Init();
            using var engine = MEPackageHandler.OpenMEPackage(Path.Combine(GlobalTest.GetTestMiniGamePath(MEGame.LE3), "BioGame", "CookedPCConsole", "Engine.pcc"));
            foreach (string operation in new[] { "Sine", "Cosine" })
            {
                var defaults = engine.Exports.Single(e => e.ObjectName.Name == "Default__MaterialExpression" + operation);
                Assert.AreEqual(1f, defaults.GetProperty<FloatProperty>("Period")?.Value);
            }
            var time = engine.Exports.Single(e => e.ObjectName.Name == "Default__MaterialExpressionTime");
            Assert.IsFalse(time.GetProperty<BoolProperty>("bIgnorePause")?.Value ?? false);
        }

        [TestMethod]
        [DataRow("Sine")]
        [DataRow("Cosine")]
        public void PositiveInfiniteTrigPeriodKeepsZeroScaleFoldedTree(string operation)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TrigInfinitePeriod.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = package.CreateExport("X", "MaterialExpressionScalarParameter", material, indexed: false);
            x.WriteProperty(new FloatProperty(2, "DefaultValue"));
            var node = package.CreateExport("Trig", "MaterialExpression" + operation, material, indexed: false);
            node.WriteProperty(Input("Input", x.UIndex));
            node.WriteProperty(new FloatProperty(float.PositiveInfinity, "Period"));
            material.WriteProperty(Input("Opacity", node.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(node.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(3, result.UniformPixelScalarExpressions.Count);
            var tree = (MaterialUniformExpressionSine)result.UniformPixelScalarExpressions[2];
            var folded = (MaterialUniformExpressionFoldedMath)tree.X;
            Assert.AreSame(result.UniformPixelScalarExpressions[0], folded.A);
            Assert.AreEqual(0f, ((MaterialUniformExpressionConstant)folded.B).Value.R);
            Assert.AreSame(folded, result.UniformPixelScalarExpressions[1]);
            LinearColor output = default;
            tree.GetNumberValue(new UniformExpressionRenderContext(new(), new(), 0, 0, null), ref output);
            Assert.AreEqual(operation == "Sine" ? 0f : 1f, output.R);
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        public void OneMinusUsesOrdinarySubtractForConstantsAndMaskedLocals(int width)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3OneMinusWidths.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = package.CreateExport("X", width == 1 ? "MaterialExpressionConstant" : "MaterialExpressionConstant" + width + "Vector", material, indexed: false);
            for (int i = 0; i < width; i++) x.WriteProperty(new FloatProperty(i + 2, "RGBA"[i].ToString()));
            var node = package.CreateExport("OneMinus", "MaterialExpressionOneMinus", material, indexed: false);
            node.WriteProperty(Input("Input", x.UIndex));
            material.WriteProperty(MaskedInput("Opacity", node.UIndex, r: true));
            material.WriteProperty(MaskedInput("WorldPositionOffset", node.UIndex, r: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(node.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                string type = half ? "half" : "float";
                string valueType = type + (width == 1 ? "" : width.ToString());
                string literal = valueType + "(" + string.Join(',', Enumerable.Range(2, width).Select(i => i + ".00000000")) + ")";
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                Assert.AreEqual($"\treturn ({type}(1.00000000) - {literal}).r", result.PropertyCode["Opacity"]);
                Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
                node.WriteProperty(MaskedInput("Input", x.UIndex, r: true, g: width > 1, b: width > 2, a: width > 3));
                var masked = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(masked.Success, string.Join("; ", masked.Errors));
                Assert.AreEqual($"\t{valueType} Local0 = ({type}(1.00000000) - {literal}.{"rgba"[..width]});\r\n\treturn Local0.r", masked.PropertyCode["Opacity"]);
                Assert.AreEqual($"\t{valueType} Local1 = ({type}(1.00000000) - {literal}.{"rgba"[..width]});\r\n\treturn Local1.r", masked.PropertyCode["WorldPositionOffset"]);
                node.WriteProperty(Input("Input", x.UIndex));
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void OneMinusInternsAsEquivalentSubtractWithoutReplicatingCpuConstant(bool vector)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3OneMinusUniform.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = package.CreateExport("X", vector ? "MaterialExpressionVectorParameter" : "MaterialExpressionScalarParameter", material, indexed: false);
            x.WriteProperty(new NameProperty("X", "ParameterName"));
            if (vector) x.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
            {
                new FloatProperty(2, "R"), new FloatProperty(3, "G"), new FloatProperty(4, "B"), new FloatProperty(5, "A")
            }, "DefaultValue", isImmutable: true));
            else x.WriteProperty(new FloatProperty(2, "DefaultValue"));
            var one = package.CreateExport("One", "MaterialExpressionConstant", material, indexed: false);
            one.WriteProperty(new FloatProperty(1, "R"));
            var node = package.CreateExport("OneMinus", "MaterialExpressionOneMinus", material, indexed: false);
            node.WriteProperty(Input("Input", x.UIndex));
            var subtract = package.CreateExport("Subtract", "MaterialExpressionSubtract", material, indexed: false);
            subtract.WriteProperty(Input("A", one.UIndex));
            subtract.WriteProperty(Input("B", x.UIndex));
            material.WriteProperty(Root("EmissiveColor", node));
            material.WriteProperty(Root("DiffuseColor", subtract));
            material.WriteProperty(Root("WorldPositionOffset", node));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(one.UIndex), new(node.UIndex), new(subtract.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            var pixel = vector ? result.UniformPixelVectorExpressions : result.UniformPixelScalarExpressions;
            var vertex = vector ? result.UniformVertexVectorExpressions : result.UniformVertexScalarExpressions;
            Assert.AreEqual(2, pixel.Count);
            Assert.AreEqual(2, vertex.Count);
            Assert.AreSame(pixel[1], vertex[1]);
            Assert.AreEqual(result.PropertyCode["EmissiveColor"], result.PropertyCode["DiffuseColor"]);
            var tree = (MaterialUniformExpressionFoldedMath)pixel[1];
            Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Sub, tree.Op);
            Assert.AreSame(pixel[0], tree.B);
            Assert.AreEqual(new Vector4(1, 0, 0, 0), (Vector4)((MaterialUniformExpressionConstant)tree.A).Value);
            using var stream = new MemoryStream();
            tree.Serialize(new SerializingContainer(stream, package));
            stream.Position = 0;
            var reader = new SerializingContainer(stream, package, isLoading: true);
            var restored = (MaterialUniformExpressionFoldedMath)MaterialUniformExpression.Create(reader);
            restored.Serialize(reader);
            Assert.AreEqual(stream.Length, stream.Position);
            var context = new UniformExpressionRenderContext(new(), new(), 0, 0, null);
            Check(vector ? new Vector4(-1, -3, -4, -5) : new Vector4(-1, 0, 0, 0));
            if (vector) context.VectorParameterValues["X"] = new LinearColor(6, 7, 8, 9);
            else context.ScalarParameterValues["X"] = 6;
            Check(vector ? new Vector4(-5, -7, -8, -9) : new Vector4(-5, 0, 0, 0));
            void Check(Vector4 expected)
            {
                foreach (var expression in new[] { tree, restored })
                {
                    LinearColor output = default;
                    expression.GetNumberValue(context, ref output);
                    Assert.AreEqual(expected, (Vector4)output);
                }
            }
            StructProperty Root(string name, ExportEntry root) => vector ? MaskedInput(name, root.UIndex, r: true, g: true, b: true) : Input(name, root.UIndex);
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public void TimeUniformsHonorPauseFlagInternByTypeShareStagesAndRoundTrip(bool ignorePause, bool explicitFlag)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TimeUniforms.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var primary = Time("Primary", ignorePause, explicitFlag);
            var duplicate = Time("Duplicate", ignorePause, true);
            var opposite = Time("Opposite", !ignorePause, true);
            material.WriteProperty(Input("EmissiveColor", primary.UIndex));
            material.WriteProperty(Input("DiffuseColor", duplicate.UIndex));
            material.WriteProperty(Input("Opacity", opposite.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", primary.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(primary.UIndex), new(duplicate.UIndex), new(opposite.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                Assert.IsTrue(result.UsesTime);
                Assert.AreEqual(2, result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(1, result.UniformVertexScalarExpressions.Count);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
                Assert.AreEqual("\treturn UniformPixelScalars_0.x", result.PropertyCode["EmissiveColor"]);
                Assert.AreEqual(result.PropertyCode["EmissiveColor"], result.PropertyCode["DiffuseColor"]);
                Assert.AreEqual("\treturn UniformPixelScalars_0.y", result.PropertyCode["Opacity"]);
                Assert.AreEqual("\treturn UniformVertexScalars_0.x", result.PropertyCode["WorldPositionOffset"]);
                var tree = result.UniformPixelScalarExpressions[0];
                Assert.AreSame(tree, result.UniformVertexScalarExpressions[0]);
                Assert.AreEqual(ignorePause ? typeof(MaterialUniformExpressionRealTime) : typeof(MaterialUniformExpressionTime), tree.GetType());
                Assert.AreNotEqual(tree.GetType(), result.UniformPixelScalarExpressions[1].GetType());
                Assert.IsFalse(tree.IsNotFrameDependent);
                using var stream = new MemoryStream();
                tree.Serialize(new SerializingContainer(stream, package));
                Assert.AreEqual(8L, stream.Length); // Name tag only: no per-time payload.
                stream.Position = 0;
                var reader = new SerializingContainer(stream, package, isLoading: true);
                var restored = MaterialUniformExpression.Create(reader);
                restored.Serialize(reader);
                Assert.AreEqual(stream.Length, stream.Position);
                Assert.AreEqual(tree.GetType(), restored.GetType());
                foreach (float time in new[] { 2f, -3f, float.PositiveInfinity, float.NaN, -0f })
                {
                    var context = new UniformExpressionRenderContext(new(), new(), time, time + 10, null);
                    foreach (var expression in new[] { tree, restored })
                    {
                        LinearColor output = new(20, 21, 22, 23);
                        expression.GetNumberValue(context, ref output);
                        Assert.AreEqual(BitConverter.SingleToInt32Bits(ignorePause ? time + 10 : time), BitConverter.SingleToInt32Bits(output.R));
                        Assert.AreEqual(new Vector3(21, 22, 23), new Vector3(output.G, output.B, output.A));
                    }
                }
            }
            ExportEntry Time(string name, bool realTime, bool writeFlag)
            {
                var node = package.CreateExport(name, "MaterialExpressionTime", material, indexed: false);
                if (writeFlag) node.WriteProperty(new BoolProperty(realTime, "bIgnorePause"));
                return node;
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TimeDrivenTrigRemainsFrameDependentAndUsesDynamicScalarSlots(bool ignorePause)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TimeTrig.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
            time.WriteProperty(new BoolProperty(ignorePause, "bIgnorePause"));
            var sine = package.CreateExport("Sine", "MaterialExpressionSine", material, indexed: false);
            sine.WriteProperty(Input("Input", time.UIndex));
            sine.WriteProperty(new FloatProperty(2, "Period"));
            material.WriteProperty(Input("Opacity", sine.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", sine.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(time.UIndex), new(sine.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.IsTrue(result.UsesTime);
            Assert.AreEqual(3, result.UniformPixelScalarExpressions.Count);
            Assert.AreEqual(3, result.UniformVertexScalarExpressions.Count);
            for (int i = 0; i < 3; i++) Assert.AreSame(result.UniformPixelScalarExpressions[i], result.UniformVertexScalarExpressions[i]);
            Assert.AreEqual("\treturn UniformPixelScalars_0.z", result.PropertyCode["Opacity"]);
            var tree = (MaterialUniformExpressionSine)result.UniformPixelScalarExpressions[2];
            Assert.IsFalse(tree.IsNotFrameDependent);
            foreach (float value in new[] { 0.25f, 0.5f })
            {
                var context = new UniformExpressionRenderContext(new(), new(), value, value + 0.5f, null);
                LinearColor output = default;
                tree.GetNumberValue(context, ref output);
                Assert.AreEqual(MathF.Sin((ignorePause ? value + 0.5f : value) * 3.1415927f), output.R);
            }
        }

        [TestMethod]
        public void TimeUsageFollowsSelectedStaticBranchNotUnreachableNodesOrPreviousTranslation()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3InactiveTime.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
            var constant = package.CreateExport("Constant", "MaterialExpressionConstant", material, indexed: false);
            var selector = package.CreateExport("Switch", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
            selector.WriteProperty(Input("A", time.UIndex));
            selector.WriteProperty(Input("B", constant.UIndex));
            selector.WriteProperty(new BoolProperty(true, "DefaultValue"));
            material.WriteProperty(Input("Opacity", selector.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(time.UIndex), new(constant.UIndex), new(selector.UIndex)], "Expressions"));
            var compiler = new LE3MaterialShaderCompiler(material);
            var active = compiler.TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(active.Success, string.Join("; ", active.Errors));
            Assert.IsTrue(active.UsesTime);
            selector.WriteProperty(new BoolProperty(false, "DefaultValue"));
            var inactive = compiler.TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(inactive.Success, string.Join("; ", inactive.Errors));
            Assert.IsFalse(inactive.UsesTime);
            Assert.AreEqual(0, inactive.UniformPixelScalarExpressions.Count);
        }

        [TestMethod]
        [DataRow("Abs", false)]
        [DataRow("Abs", true)]
        [DataRow("Floor", false)]
        [DataRow("Floor", true)]
        [DataRow("Ceil", false)]
        [DataRow("Ceil", true)]
        [DataRow("Frac", false)]
        [DataRow("Frac", true)]
        public void UnaryUniformsInternNestedTreesShareStagesAndRoundTrip(string operation, bool vector)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3UnaryUniforms.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var x = package.CreateExport("X", vector ? "MaterialExpressionVectorParameter" : "MaterialExpressionScalarParameter", material, indexed: false);
            x.WriteProperty(new NameProperty("X", "ParameterName"));
            if (vector) x.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
            {
                new FloatProperty(-1.25f, "R"), new FloatProperty(2.5f, "G"), new FloatProperty(-3.75f, "B"), new FloatProperty(4.25f, "A")
            }, "DefaultValue", isImmutable: true));
            else x.WriteProperty(new FloatProperty(-1.25f, "DefaultValue"));
            var primary = Unary("Primary", x);
            var duplicate = Unary("Duplicate", x);
            var nested = Unary("Nested", primary);
            material.WriteProperty(Connection("EmissiveColor", primary, vector));
            material.WriteProperty(Connection("DiffuseColor", duplicate, vector));
            material.WriteProperty(vector ? MaskedInput("Opacity", nested.UIndex, r: true) : Input("Opacity", nested.UIndex));
            material.WriteProperty(Connection("WorldPositionOffset", primary, vector));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(x.UIndex), new(primary.UIndex), new(duplicate.UIndex), new(nested.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                var pixel = vector ? result.UniformPixelVectorExpressions : result.UniformPixelScalarExpressions;
                var vertex = vector ? result.UniformVertexVectorExpressions : result.UniformVertexScalarExpressions;
                Assert.AreEqual(3, pixel.Count);
                Assert.AreEqual(2, vertex.Count);
                Assert.AreSame(pixel[0], vertex[0]);
                Assert.AreSame(pixel[1], vertex[1]);
                Assert.AreEqual(vector ? "\treturn UniformPixelVector_1.rgb" : "\treturn UniformPixelScalars_0.y", result.PropertyCode["EmissiveColor"]);
                Assert.AreEqual(result.PropertyCode["EmissiveColor"], result.PropertyCode["DiffuseColor"]);
                var tree = (MaterialUniformExpressionUnaryOp)pixel[1];
                var nestedTree = (MaterialUniformExpressionUnaryOp)pixel[2];
                Assert.AreSame(pixel[0], tree.X);
                Assert.AreSame(tree, nestedTree.X);
                Assert.AreEqual("FMaterialUniformExpression" + operation, tree.ExpressionType.Name);
                using var stream = new MemoryStream();
                nestedTree.Serialize(new SerializingContainer(stream, package));
                stream.Position = 0;
                var reader = new SerializingContainer(stream, package, isLoading: true);
                var restored = (MaterialUniformExpressionUnaryOp)MaterialUniformExpression.Create(reader);
                restored.Serialize(reader);
                Assert.AreEqual(stream.Length, stream.Position);
                Assert.AreEqual(tree.GetType(), restored.GetType());
                Assert.AreEqual(tree.GetType(), restored.X.GetType());
                var context = new UniformExpressionRenderContext(new(), new(), 0, 0, null);
                Check(vector ? new Vector4(-1.25f, 2.5f, -3.75f, 4.25f) : new Vector4(-1.25f, 0, 0, 0));
                if (vector) context.VectorParameterValues["X"] = new LinearColor(0.5f, -2.5f, 3, -4);
                else context.ScalarParameterValues["X"] = 0.5f;
                Check(vector ? new Vector4(0.5f, -2.5f, 3, -4) : new Vector4(0.5f, 0, 0, 0));
                void Check(Vector4 input)
                {
                    Vector4 expected = new(Eval(input.X), Eval(input.Y), Eval(input.Z), Eval(input.W));
                    LinearColor value = default;
                    tree.GetNumberValue(context, ref value);
                    Assert.AreEqual(expected, (Vector4)value);
                    restored.GetNumberValue(context, ref value);
                    Assert.AreEqual(new Vector4(Eval(expected.X), Eval(expected.Y), Eval(expected.Z), Eval(expected.W)), (Vector4)value);
                }
            }
            float Eval(float v) => operation switch { "Abs" => MathF.Abs(v), "Floor" => MathF.Floor(v), "Ceil" => MathF.Ceiling(v), _ => v - MathF.Floor(v) };
            ExportEntry Unary(string name, ExportEntry child)
            {
                var node = package.CreateExport(name, "MaterialExpression" + operation, material, indexed: false);
                node.WriteProperty(Input("Input", child.UIndex));
                return node;
            }
            static StructProperty Connection(string name, ExportEntry node, bool mask) => mask
                ? MaskedInput(name, node.UIndex, r: true, g: true, b: true) : Input(name, node.UIndex);
        }

        [TestMethod]
        [DataRow("Abs")]
        [DataRow("Floor")]
        [DataRow("Ceil")]
        [DataRow("Frac")]
        [DataRow("Sine")]
        [DataRow("Cosine")]
        [DataRow("SquareRoot")]
        [DataRow("OneMinus")]
        public void UnaryRequiresConnectedInputAndReportsCycles(string operation)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3UnaryErrors.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var unary = package.CreateExport("Unary", "MaterialExpression" + operation, material, indexed: false);
            material.WriteProperty(Input("EmissiveColor", unary.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(unary.UIndex)], "Expressions"));
            var missing = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsFalse(missing.Success);
            CollectionAssert.Contains(missing.Errors.ToArray(), operation == "SquareRoot" ? "Missing square root input" : operation == "OneMinus" ? "Missing 1-x input" : $"Missing {operation} input");
            unary.WriteProperty(Input("Input", unary.UIndex));
            var cycle = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsFalse(cycle.Success);
            Assert.IsTrue(cycle.Errors.Any(e => e.Contains("cycle", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        [DataRow("Floor", -1e-8f, 0f)]
        [DataRow("Ceil", 1e-8f, 0f)]
        [DataRow("Frac", -1e-8f, -1e-8f)]
        [DataRow("Floor", 1073741824f, -1073741824f)]
        [DataRow("Ceil", 1073741824f, 1073741824f)]
        [DataRow("Frac", 1073741824f, 2147483648f)]
        [DataRow("Floor", float.PositiveInfinity, -1073741824f)]
        [DataRow("Ceil", float.NegativeInfinity, 1073741824f)]
        [DataRow("Floor", float.NaN, -1073741824f)]
        [DataRow("Ceil", float.NaN, 1073741824f)]
        public void UnaryCpuUsesNativeFloatIntegerSequenceForEdgeValues(string operation, float input, float expected)
        {
            MaterialUniformExpressionUnaryOp tree = operation switch
            {
                "Floor" => new MaterialUniformExpressionFloor(), "Ceil" => new MaterialUniformExpressionCeil(), _ => new MaterialUniformExpressionFrac()
            };
            tree.X = new MaterialUniformExpressionConstant { Value = new LinearColor(input), ValueType = 8 };
            LinearColor value = default;
            tree.GetNumberValue(new UniformExpressionRenderContext(new(), new(), 0, 0, null), ref value);
            foreach (float lane in new[] { value.R, value.G, value.B, value.A })
                Assert.AreEqual(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(lane));
        }

        [TestMethod]
        public void UnaryCpuSignedZeroMatchesIntegerConversionAndAbsSignClear()
        {
            foreach (MaterialUniformExpressionUnaryOp tree in new MaterialUniformExpressionUnaryOp[]
            {
                new MaterialUniformExpressionAbs(), new MaterialUniformExpressionFloor(), new MaterialUniformExpressionCeil(), new MaterialUniformExpressionFrac()
            })
            {
                tree.X = new MaterialUniformExpressionConstant { Value = new LinearColor(-0f), ValueType = 8 };
                LinearColor value = default;
                tree.GetNumberValue(new UniformExpressionRenderContext(new(), new(), 0, 0, null), ref value);
                int bits = tree is MaterialUniformExpressionFrac ? unchecked((int)0x80000000) : 0;
                Assert.AreEqual(bits, BitConverter.SingleToInt32Bits(value.R));
                Assert.AreEqual(bits, BitConverter.SingleToInt32Bits(value.A));
            }
        }

        [TestMethod]
        [DataRow(2, false, false)]
        [DataRow(2, true, false)]
        [DataRow(3, false, false)]
        [DataRow(3, true, false)]
        [DataRow(4, false, false)]
        [DataRow(4, true, false)]
        [DataRow(2, false, true)]
        [DataRow(2, true, true)]
        [DataRow(3, false, true)]
        [DataRow(3, true, true)]
        [DataRow(4, false, true)]
        [DataRow(4, true, true)]
        public void LerpReplicatesScalarEndpointsAndCastsScalarAlphaExactly(int width, bool scalarFirst, bool useHalf)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3LerpCoercion.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var vector = package.CreateExport("Vector", "MaterialExpressionConstant" + width + "Vector", material, indexed: false);
            for (int i = 0; i < width; i++) vector.WriteProperty(new FloatProperty(i + 1, "RGBA"[i].ToString()));
            var scalar = package.CreateExport("Scalar", "MaterialExpressionConstant", material, indexed: false);
            scalar.WriteProperty(new FloatProperty(0.5f, "R"));
            var alpha = package.CreateExport("Alpha", "MaterialExpressionConstant", material, indexed: false);
            alpha.WriteProperty(new FloatProperty(0.25f, "R"));
            var lerp = package.CreateExport("Lerp", "MaterialExpressionLinearInterpolate", material, indexed: false);
            lerp.WriteProperty(Input("A", scalarFirst ? scalar.UIndex : vector.UIndex));
            lerp.WriteProperty(Input("B", scalarFirst ? vector.UIndex : scalar.UIndex));
            lerp.WriteProperty(Input("Alpha", alpha.UIndex));
            string root = width == 2 ? "Distortion" : "EmissiveColor";
            material.WriteProperty(width == 4 ? MaskedInput(root, lerp.UIndex, r: true, g: true, b: true) : Input(root, lerp.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(vector.UIndex), new(scalar.UIndex), new(alpha.UIndex), new(lerp.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: useHalf);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            string type = useHalf ? "half" : "float";
            string vectorCode = type + width + "(" + string.Join(',', Enumerable.Range(1, width).Select(i => i + ".00000000")) + ")";
            string replicated = type + width + "(" + string.Join(',', Enumerable.Repeat(type + "(0.50000000)", width)) + ")";
            Assert.AreEqual($"\t{type}{width} Local0 = lerp({(scalarFirst ? replicated : vectorCode)},{(scalarFirst ? vectorCode : replicated)},{type}({type}(0.25000000)));\r\n\treturn Local0" +
                (width == 4 ? ".rgb" : ""), result.PropertyCode[root]);
            Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
            Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ScalarLerpStillEmitsLocalWithoutExtraAlphaCast(bool useHalf)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ScalarLerp.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var scalar = package.CreateExport("Scalar", "MaterialExpressionConstant", material, indexed: false);
            scalar.WriteProperty(new FloatProperty(0.5f, "R"));
            var lerp = package.CreateExport("Lerp", "MaterialExpressionLinearInterpolate", material, indexed: false);
            foreach (string name in new[] { "A", "B", "Alpha" }) lerp.WriteProperty(Input(name, scalar.UIndex));
            material.WriteProperty(Input("Opacity", lerp.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(scalar.UIndex), new(lerp.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: useHalf);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            string type = useHalf ? "half" : "float";
            Assert.AreEqual($"\t{type} Local0 = lerp({type}(0.50000000),{type}(0.50000000),{type}(0.50000000));\r\n\treturn Local0", result.PropertyCode["Opacity"]);
        }

        [TestMethod]
        public void LerpUsesMatchingVectorAlphaAndNativeUniformAccessOrderInBothStages()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3UniformLerp.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var lerp = package.CreateExport("Lerp", "MaterialExpressionLinearInterpolate", material, indexed: false);
            var expressions = new ArrayProperty<ObjectProperty>([new(lerp.UIndex)], "Expressions");
            foreach (string name in new[] { "A", "B", "Alpha" })
            {
                var parameter = package.CreateExport(name, "MaterialExpressionVectorParameter", material, indexed: false);
                parameter.WriteProperty(new NameProperty(name, "ParameterName"));
                lerp.WriteProperty(Input(name, parameter.UIndex));
                expressions.Add(new ObjectProperty(parameter.UIndex));
            }
            material.WriteProperty(MaskedInput("EmissiveColor", lerp.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("WorldPositionOffset", lerp.UIndex, r: true, g: true, b: true));
            material.WriteProperty(expressions);
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual("\tfloat4 Local0 = lerp(UniformPixelVector_2,UniformPixelVector_1,UniformPixelVector_0);\r\n\treturn Local0.rgb", result.PropertyCode["EmissiveColor"]);
            Assert.AreEqual("\tfloat4 Local1 = lerp(UniformVertexVector_2,UniformVertexVector_1,UniformVertexVector_0);\r\n\treturn Local1.rgb", result.PropertyCode["WorldPositionOffset"]);
            CollectionAssert.AreEqual(new[] { "Alpha", "B", "A" }, result.UniformPixelVectorExpressions
                .Cast<MaterialUniformExpressionVectorParameter>().Select(p => p.ParameterName.Name).ToArray());
            for (int i = 0; i < 3; i++) Assert.AreSame(result.UniformPixelVectorExpressions[i], result.UniformVertexVectorExpressions[i]);
        }

        [TestMethod]
        public void LerpRequiresAllInputsAndRejectsIncompatibleEndpointsAndAlpha()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3LerpErrors.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var vector2 = package.CreateExport("Two", "MaterialExpressionConstant2Vector", material, indexed: false);
            var vector3 = package.CreateExport("Three", "MaterialExpressionConstant3Vector", material, indexed: false);
            var lerp = package.CreateExport("Lerp", "MaterialExpressionLinearInterpolate", material, indexed: false);
            foreach (string name in new[] { "ConstA", "ConstB", "ConstAlpha" }) lerp.WriteProperty(new FloatProperty(0.5f, name));
            material.WriteProperty(Input("Distortion", lerp.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(vector2.UIndex), new(vector3.UIndex), new(lerp.UIndex)], "Expressions"));
            Check("Missing LinearInterpolate input A");
            lerp.WriteProperty(Input("A", vector2.UIndex));
            Check("Missing LinearInterpolate input B");
            lerp.WriteProperty(Input("B", vector2.UIndex));
            Check("Missing LinearInterpolate input Alpha");
            lerp.WriteProperty(Input("Alpha", vector3.UIndex));
            Check("LinearInterpolate alpha coercion from Vector3 to float1 is not supported");
            lerp.WriteProperty(Input("B", vector3.UIndex));
            Check("Arithmetic between Vector2 and Vector3 is undefined");
            lerp.WriteProperty(Input("A", lerp.UIndex));
            Check("cycle");
            void Check(string error)
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
                Assert.IsFalse(result.Success);
                Assert.IsNull(result.MaterialHlsl);
                StringAssert.Contains(string.Join("; ", result.Errors).ToLowerInvariant(), error.ToLowerInvariant());
            }
        }

        [TestMethod]
        public void InitialTranslatorCreatesTextureCoordinateLocal()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShaderTexCoordTest.pcc", MEGame.LE3);
            var material = package.CreateExport("TestMaterial", "Material", indexed: false);
            var texCoord = package.CreateExport("UV2", "MaterialExpressionTextureCoordinate", material, indexed: false);
            texCoord.WriteProperty(new IntProperty(2, "CoordinateIndex"));
            material.WriteProperty(Input("Distortion", texCoord.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(texCoord.UIndex)], "Expressions"));

            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(3u, result.NumUserTexCoords);
            Assert.AreEqual("\tfloat2 Local0 = Parameters.TexCoords[2].xy;\r\n\tfloat2 Local1 = (float(1.00000000) * Local0);\r\n\treturn Local1", result.PropertyCode["Distortion"]);
        }

        [TestMethod]
        [DataRow(2f, 2f, false)]
        [DataRow(-2f, -2f, false)]
        [DataRow(0f, 0f, false)]
        [DataRow(2f, 3f, true)]
        [DataRow(-2f, 0.5f, true)]
        [DataRow(0f, 9e-9f, false)]
        [DataRow(0f, 1e-8f, false)]
        [DataRow(0f, 1.0000001e-8f, true)]
        [DataRow(0f, -1.0000001e-8f, true)]
        public void TextureCoordinateUsesNativeScalarOrVectorTilingThreshold(float u, float v, bool vectorTiling)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TexCoordTiling.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var uv = package.CreateExport("UV", "MaterialExpressionTextureCoordinate", material, indexed: false);
            uv.WriteProperty(new FloatProperty(u, "UTiling"));
            uv.WriteProperty(new FloatProperty(v, "VTiling"));
            material.WriteProperty(Input("Distortion", uv.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(uv.UIndex)], "Expressions"));
            foreach (bool half in new[] { false, true })
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                string type = half ? "half" : "float";
                string uCode = u.ToString("F8", System.Globalization.CultureInfo.InvariantCulture);
                string vCode = v.ToString("F8", System.Globalization.CultureInfo.InvariantCulture);
                string multiply = vectorTiling ? $"(Local0 * {type}2({uCode},{vCode}))" : $"({type}({uCode}) * Local0)";
                Assert.AreEqual($"\t{type}2 Local0 = Parameters.TexCoords[0].xy;\r\n\t{type}2 Local1 = {multiply};\r\n\treturn Local1", result.PropertyCode["Distortion"]);
                Assert.AreEqual(1u, result.NumUserTexCoords);
                Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
            }
        }

        [TestMethod]
        [DataRow(false, false, false)]
        [DataRow(true, false, false)]
        [DataRow(false, true, false)]
        [DataRow(true, true, false)]
        [DataRow(false, false, true)]
        [DataRow(true, false, true)]
        [DataRow(false, true, true)]
        [DataRow(true, true, true)]
        public void TextureCoordinateTilingFollowsUnmirrorAndRejectsVertexInputs(bool unmirrorU, bool unmirrorV, bool half)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TexCoordUnmirror.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var uv = package.CreateExport("UV", "MaterialExpressionTextureCoordinate", material, indexed: false);
            uv.WriteProperty(new IntProperty(2, "CoordinateIndex"));
            uv.WriteProperty(new FloatProperty(2, "UTiling"));
            uv.WriteProperty(new FloatProperty(3, "VTiling"));
            uv.WriteProperty(new BoolProperty(unmirrorU, "UnMirrorU"));
            uv.WriteProperty(new BoolProperty(unmirrorV, "UnMirrorV"));
            material.WriteProperty(Input("Distortion", uv.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(uv.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            string type = half ? "half" : "float";
            string coord = "Parameters.TexCoords[2].xy";
            string helper = unmirrorU && unmirrorV ? "UnMirrorUV" : unmirrorU ? "UnMirrorU" : "UnMirrorV";
            if (unmirrorU || unmirrorV) coord = $"{helper}({coord}, Parameters)";
            Assert.AreEqual($"\t{type}2 Local0 = {coord};\r\n\t{type}2 Local1 = (Local0 * {type}2(2.00000000,3.00000000));\r\n\treturn Local1", result.PropertyCode["Distortion"]);
            Assert.AreEqual(3u, result.NumUserTexCoords);
            Assert.AreEqual(unmirrorU || unmirrorV ? 1u : 0u, result.UsingTransforms);
            Assert.AreEqual(0, result.UniformVertexVectorExpressions.Count);
            Assert.AreEqual(0, result.UniformVertexScalarExpressions.Count);
            material.WriteProperty(MaskedInput("WorldPositionOffset", uv.UIndex, r: true));
            var vertex = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: half);
            Assert.IsFalse(vertex.Success);
            Assert.IsNull(vertex.MaterialHlsl);
            CollectionAssert.Contains(vertex.Errors.ToArray(), "Invalid node used in vertex shader input!");
        }

        [TestMethod]
        [DataRow("Material", false)]
        [DataRow("Material", true)]
        [DataRow("DecalMaterial", false)]
        [DataRow("DecalMaterial", true)]
        public void TextureCoordinateDecalFlagControlsRemapInitialCountAndFinalLimit(string materialClass, bool usedWithDecals)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TexCoordDecal.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", materialClass, indexed: false);
            material.WriteProperty(new BoolProperty(usedWithDecals, "bUsedWithDecals"));
            var compiler = new LE3MaterialShaderCompiler(material);
            var empty = compiler.TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(empty.Success);
            Assert.AreEqual(usedWithDecals ? 2u : 0u, empty.NumUserTexCoords);
            Assert.AreEqual(usedWithDecals, compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), empty).ContainsKey("MATERIAL_DECAL"));
            var uv = package.CreateExport("UV", "MaterialExpressionTextureCoordinate", material, indexed: false);
            uv.WriteProperty(new IntProperty(1, "CoordinateIndex"));
            material.WriteProperty(Input("Distortion", uv.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(uv.UIndex)], "Expressions"));
            var translation = compiler.TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
            Assert.AreEqual(2u, translation.NumUserTexCoords);
            StringAssert.Contains(translation.PropertyCode["Distortion"], $"Parameters.TexCoords[{(usedWithDecals ? 0 : 1)}].xy");
            for (int flags = 0; flags < 4; flags++)
            {
                uv.WriteProperty(new BoolProperty((flags & 1) != 0, "UnMirrorU"));
                uv.WriteProperty(new BoolProperty((flags & 2) != 0, "UnMirrorV"));
                var mirrored = compiler.TranslateMaterialHlsl(MinimalTemplate());
                Assert.IsTrue(mirrored.Success, string.Join("; ", mirrored.Errors));
                string coordinate = $"Parameters.TexCoords[{(usedWithDecals ? 0 : 1)}].xy";
                string helper = flags == 3 ? "UnMirrorUV" : flags == 1 ? "UnMirrorU" : "UnMirrorV";
                StringAssert.Contains(mirrored.PropertyCode["Distortion"], flags == 0 ? coordinate : $"{helper}({coordinate}, Parameters)");
                Assert.AreEqual(flags == 0 ? 0u : 1u, mirrored.UsingTransforms);
                Assert.AreEqual(2u, mirrored.NumUserTexCoords);
            }
            uv.WriteProperty(new IntProperty(2, "CoordinateIndex"));
            var excess = compiler.TranslateMaterialHlsl(MinimalTemplate());
            Assert.AreEqual(!usedWithDecals, excess.Success);
            Assert.AreEqual(3u, excess.NumUserTexCoords);
            if (usedWithDecals)
            {
                Assert.IsNull(excess.MaterialHlsl);
                CollectionAssert.Contains(excess.Errors.ToArray(), "Decal materials can only use UV0 and UV1, currently setup to use 3 UV sets.");
            }
        }

        [TestMethod]
        [DataRow(-1, 0u, "float2(0,0)")]
        [DataRow(-2, uint.MaxValue, "UnMirrorU(Parameters.TexCoords[4294967294].xy, Parameters)")]
        [DataRow(int.MaxValue, 2147483648u, "UnMirrorU(Parameters.TexCoords[2147483647].xy, Parameters)")]
        public void TextureCoordinateRetainsNativeUnsignedIndexArithmeticAndZeroCountFallback(int index, uint count, string coordinate)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TexCoordIndexBits.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var uv = package.CreateExport("UV", "MaterialExpressionTextureCoordinate", material, indexed: false);
            uv.WriteProperty(new IntProperty(index, "CoordinateIndex"));
            uv.WriteProperty(new BoolProperty(true, "UnMirrorU"));
            material.WriteProperty(Input("Distortion", uv.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(uv.UIndex)], "Expressions"));
            var compiler = new LE3MaterialShaderCompiler(material);
            var result = compiler.TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: true);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(count, result.NumUserTexCoords);
            Assert.AreEqual(count == 0 ? 0u : 1u, result.UsingTransforms);
            Assert.AreEqual($"\thalf2 Local0 = {coordinate};\r\n\thalf2 Local1 = (half(1.00000000) * Local0);\r\n\treturn Local1", result.PropertyCode["Distortion"]);
            Assert.AreEqual(count != 0, compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), result).ContainsKey("WORLD_COORDS"));
            // These pathological indices reproduce translation; no claim that D3D accepts the enormous arrays.
        }

        [TestMethod]
        public void TextureCoordinateCountIsMonotonicAcrossPropertiesBeforeZeroFallbackSelection()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TexCoordCountOrder.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var first = package.CreateExport("First", "MaterialExpressionTextureCoordinate", material, indexed: false);
            first.WriteProperty(new IntProperty(3, "CoordinateIndex"));
            var second = package.CreateExport("Second", "MaterialExpressionTextureCoordinate", material, indexed: false);
            second.WriteProperty(new IntProperty(-1, "CoordinateIndex"));
            second.WriteProperty(new BoolProperty(true, "UnMirrorV"));
            material.WriteProperty(MaskedInput("EmissiveColor", first.UIndex, r: true));
            material.WriteProperty(Input("Distortion", second.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(first.UIndex), new(second.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(4u, result.NumUserTexCoords);
            Assert.AreEqual(1u, result.UsingTransforms);
            StringAssert.Contains(result.PropertyCode["Distortion"], "UnMirrorV(Parameters.TexCoords[4294967295].xy, Parameters)");
        }

        [TestMethod]
        public void TextureCoordinateUnmirrorMacrosFollowResolvedBranchesWithoutMutatingResource()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TexCoordWorldUsage.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var uv = package.CreateExport("UV", "MaterialExpressionTextureCoordinate", material, indexed: false);
            uv.WriteProperty(new BoolProperty(true, "UnMirrorV"));
            uv.WriteProperty(new FloatProperty(2, "VTiling"));
            var constant = package.CreateExport("Constant", "MaterialExpressionConstant2Vector", material, indexed: false);
            var selector = package.CreateExport("Selector", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
            selector.WriteProperty(new NameProperty("Unmirror", "ParameterName"));
            selector.WriteProperty(new BoolProperty(true, "DefaultValue"));
            selector.WriteProperty(Input("A", uv.UIndex));
            selector.WriteProperty(Input("B", constant.UIndex));
            material.WriteProperty(Input("Distortion", selector.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(uv.UIndex), new(constant.UIndex), new(selector.UIndex)], "Expressions"));
            var parameters = (StaticParameterSet)Guid.Empty;
            parameters.StaticSwitchParameters = [new() { ParameterName = "Unmirror", Value = false, bOverride = false }];
            var resource = MaterialResource.Create();
            foreach (bool selected in new[] { false, true })
            {
                parameters.StaticSwitchParameters[0].Value = selected;
                var compiler = new LE3MaterialShaderCompiler(material, parameters);
                var translation = compiler.TranslateMaterialHlsl(MinimalTemplate());
                Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
                Assert.AreEqual(selected ? 1u : 0u, translation.UsingTransforms);
                Assert.AreEqual(selected, compiler.BuildProvisionalMaterialMacros(resource, translation).ContainsKey("WORLD_COORDS"));
                Assert.AreEqual(selected, compiler.BuildProvisionalMaterialMacros(resource, translation.Graph).ContainsKey("WORLD_COORDS"));
                Assert.AreEqual(0u, resource.UsingTransforms);
            }
            // Existing resource transform requirements remain additive in this provisional API.
            resource.UsingTransforms = 2;
            parameters.StaticSwitchParameters[0].Value = false;
            var inheritedCompiler = new LE3MaterialShaderCompiler(material, parameters);
            Assert.AreEqual("1", inheritedCompiler.BuildProvisionalMaterialMacros(resource,
                inheritedCompiler.TranslateMaterialHlsl(MinimalTemplate()))["WORLD_COORDS"]);
            Assert.AreEqual(2u, resource.UsingTransforms);
        }

        [TestMethod]
        public void InitialTranslatorEmitsVectorAndScalarComponentMasks()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShaderMaskTest.pcc", MEGame.LE3);
            var material = package.CreateExport("TestMaterial", "Material", indexed: false);
            var vector = package.CreateExport("Vector", "MaterialExpressionConstant4Vector", material, indexed: false);
            var scalar = package.CreateExport("Scalar", "MaterialExpressionConstant", material, indexed: false);
            vector.WriteProperty(new FloatProperty(1, "R"));
            vector.WriteProperty(new FloatProperty(2, "G"));
            vector.WriteProperty(new FloatProperty(3, "B"));
            vector.WriteProperty(new FloatProperty(4, "A"));
            scalar.WriteProperty(new FloatProperty(0.5f, "R"));
            material.WriteProperty(MaskedInput("EmissiveColor", vector.UIndex, r: true, b: true, a: true));
            material.WriteProperty(MaskedInput("Distortion", scalar.UIndex, r: true, g: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(vector.UIndex), new(scalar.UIndex)], "Expressions"));

            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());

            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual("\treturn float4(1.00000000,2.00000000,3.00000000,4.00000000).rba",
                result.PropertyCode["EmissiveColor"]);
            Assert.AreEqual("\treturn float(0.50000000).rr", result.PropertyCode["Distortion"]);
        }

        [TestMethod]
        [DataRow("MaterialExpressionComponentMask", false)]
        [DataRow("MaterialExpressionStaticComponentMaskParameter", false)]
        [DataRow("MaterialExpressionStaticComponentMaskParameter", true)]
        public void ExplicitComponentMasksEmitAllChannelCombinationsInline(string expressionClass, bool useStaticParameter)
        {
            GlobalTest.Init();
            for (int bits = 1; bits < 16; bits++)
            {
                using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MaskNode.pcc", MEGame.LE3);
                var material = package.CreateExport("Material", "Material", indexed: false);
                var color = package.CreateExport("Color", "MaterialExpressionVertexColor", material, indexed: false);
                var mask = package.CreateExport("Mask", expressionClass, material, indexed: false);
                mask.WriteProperty(Input("Input", color.UIndex));
                string swizzle = "";
                for (int channel = 0; channel < 4; channel++)
                {
                    bool enabled = (bits & (1 << channel)) != 0;
                    mask.WriteProperty(new BoolProperty(enabled, (expressionClass.Contains("Static") ? "Default" : "") + "RGBA"[channel]));
                    if (enabled) swizzle += "rgba"[channel];
                }
                string root = swizzle.Length == 2 ? "Distortion" : swizzle.Length == 1 ? "Opacity" : "EmissiveColor";
                material.WriteProperty(swizzle.Length == 4
                    ? MaskedInput(root, mask.UIndex, r: true, g: true, b: true)
                    : Input(root, mask.UIndex));
                material.WriteProperty(new ArrayProperty<ObjectProperty>([new(color.UIndex), new(mask.UIndex)], "Expressions"));
                var parameters = (StaticParameterSet)Guid.Empty;
                if (useStaticParameter)
                {
                    // The entry must supply all channels, rather than combine with defaults.
                    foreach (char channel in "RGBA") mask.WriteProperty(new BoolProperty(false, "Default" + channel));
                    parameters.StaticComponentMaskParameters = [new()
                    {
                        ParameterName = NameReference.None, R = (bits & 1) != 0, G = (bits & 2) != 0,
                        B = (bits & 4) != 0, A = (bits & 8) != 0, bOverride = false
                    }];
                }
                var result = new LE3MaterialShaderCompiler(material, parameters).TranslateMaterialHlsl(MinimalTemplate());
                Assert.IsTrue(result.Success, bits + ": " + string.Join("; ", result.Errors));
                Assert.AreEqual("\treturn Parameters.VertexColor." + swizzle + (swizzle.Length == 4 ? ".rgb" : ""), result.PropertyCode[root]);
                Assert.IsTrue(result.UsesVertexColor);
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
            }
        }

        [TestMethod]
        [DataRow("MaterialExpressionComponentMask")]
        [DataRow("MaterialExpressionStaticComponentMaskParameter")]
        public void MaskNodesApplyInputMaskFirstAndSupportScalarReplicationInVertexStage(string expressionClass)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MaskInputOrder.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var color = package.CreateExport("Color", "MaterialExpressionVertexColor", material, indexed: false);
            var scalar = package.CreateExport("Scalar", "MaterialExpressionConstant", material, indexed: false);
            scalar.WriteProperty(new FloatProperty(0.5f, "R"));
            string prefix = expressionClass.Contains("Static") ? "Default" : "";
            var mask = package.CreateExport("Mask", expressionClass, material, indexed: false);
            mask.WriteProperty(MaskedInput("Input", color.UIndex, r: true, b: true, a: true));
            mask.WriteProperty(new BoolProperty(true, prefix + "G"));
            mask.WriteProperty(new BoolProperty(true, prefix + "B"));
            var scalarMask = package.CreateExport("ScalarMask", expressionClass, material, indexed: false);
            scalarMask.WriteProperty(Input("Input", scalar.UIndex));
            scalarMask.WriteProperty(new BoolProperty(true, prefix + "G"));
            scalarMask.WriteProperty(new BoolProperty(true, prefix + "B"));
            scalarMask.WriteProperty(new BoolProperty(true, prefix + "A"));
            material.WriteProperty(Input("Distortion", mask.UIndex));
            material.WriteProperty(Input("WorldPositionOffset", scalarMask.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(color.UIndex), new(scalar.UIndex), new(mask.UIndex), new(scalarMask.UIndex)], "Expressions"));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: true);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual("\treturn Parameters.VertexColor.rba.gb", result.PropertyCode["Distortion"]);
            Assert.AreEqual("\treturn half(0.50000000).rrr", result.PropertyCode["WorldPositionOffset"]);
        }

        [TestMethod]
        [DataRow("MaterialExpressionComponentMask")]
        [DataRow("MaterialExpressionStaticComponentMaskParameter")]
        public void ExplicitMaskFailuresRemainErrorsAndCyclesFailBeforeTranslation(string expressionClass)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MaskFailure.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var mask = package.CreateExport("Mask", expressionClass, material, indexed: false);
            var vector = package.CreateExport("Vector", "MaterialExpressionConstant2Vector", material, indexed: false);
            material.WriteProperty(Input("Opacity", mask.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(mask.UIndex), new(vector.UIndex)], "Expressions"));
            Check("Missing ComponentMask input");
            mask.WriteProperty(Input("Input", vector.UIndex));
            Check("Couldn't determine result type of component mask 0000");
            mask.WriteProperty(new BoolProperty(true, expressionClass.Contains("Static") ? "DefaultA" : "A"));
            Check("Not enough components");
            mask.WriteProperty(Input("Input", mask.UIndex));
            var cycle = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsFalse(cycle.Success);
            Assert.IsFalse(cycle.Graph.IsValid);
            Assert.IsTrue(cycle.Errors.Any(e => e.Contains("cycle", StringComparison.OrdinalIgnoreCase)));

            void Check(string error)
            {
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
                Assert.IsFalse(result.Success);
                Assert.IsNull(result.MaterialHlsl);
                StringAssert.Contains(string.Join("; ", result.Errors), error);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void StaticMasksUseResolvedValuesWithoutFilteringOverrideFlags(bool enabled)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3StaticMaskBoundary.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var vector = package.CreateExport("Color", "MaterialExpressionVertexColor", material, indexed: false);
            var mask = package.CreateExport("Mask", "MaterialExpressionStaticComponentMaskParameter", material, indexed: false);
            mask.WriteProperty(new NameProperty("SelectChannels", "ParameterName"));
            mask.WriteProperty(new BoolProperty(true, "DefaultR"));
            mask.WriteProperty(Input("Input", vector.UIndex));
            material.WriteProperty(Input("Opacity", mask.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(vector.UIndex), new(mask.UIndex)], "Expressions"));
            var parameters = (StaticParameterSet)Guid.Empty;
            parameters.NormalParameters = [new() { ParameterName = "OtherNormal", bOverride = true, CompressionSettings = 12 }];
            var authored = new LE3MaterialShaderCompiler(material, parameters).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(authored.Success, string.Join("; ", authored.Errors));
            Assert.AreEqual("\treturn Parameters.VertexColor.r", authored.PropertyCode["Opacity"]);
            foreach (string name in new[] { "SelectChannels", "DifferentParameter" })
            {
                parameters.StaticComponentMaskParameters = [new()
                {
                    ParameterName = name, G = true, bOverride = enabled, ExpressionGUID = Guid.NewGuid()
                }];
                var resolved = new LE3MaterialShaderCompiler(material, parameters).TranslateMaterialHlsl(MinimalTemplate());
                Assert.IsTrue(resolved.Success, string.Join("; ", resolved.Errors));
                Assert.AreEqual("\treturn Parameters.VertexColor." + (name == "SelectChannels" ? "g" : "r"), resolved.PropertyCode["Opacity"]);
            }
        }

        [TestMethod]
        public void StaticMasksMatchFullNamesTakeFirstDuplicateAndSnapshotValues()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3StaticMaskNames.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var vector = package.CreateExport("Color", "MaterialExpressionVertexColor", material, indexed: false);
            var mask = package.CreateExport("Mask", "MaterialExpressionStaticComponentMaskParameter", material, indexed: false);
            mask.WriteProperty(new NameProperty(new NameReference("Channels", 2), "ParameterName"));
            mask.WriteProperty(new BoolProperty(true, "DefaultR"));
            mask.WriteProperty(Input("Input", vector.UIndex));
            material.WriteProperty(Input("Opacity", mask.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(vector.UIndex), new(mask.UIndex)], "Expressions"));
            var parameters = (StaticParameterSet)Guid.Empty;
            parameters.StaticComponentMaskParameters =
            [
                new() { ParameterName = new NameReference("Channels", 1), R = true, bOverride = true },
                new() { ParameterName = new NameReference("CHANNELS", 2), B = true, bOverride = false, ExpressionGUID = Guid.NewGuid() },
                new() { ParameterName = new NameReference("Channels", 2), A = true, bOverride = true }
            ];
            var compiler = new LE3MaterialShaderCompiler(material, parameters);
            parameters.StaticComponentMaskParameters[1].B = false;
            parameters.StaticComponentMaskParameters[1].G = true;
            var selected = compiler.TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(selected.Success, string.Join("; ", selected.Errors));
            Assert.AreEqual("\treturn Parameters.VertexColor.b", selected.PropertyCode["Opacity"]);
            var fresh = new LE3MaterialShaderCompiler(material, parameters).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(fresh.Success, string.Join("; ", fresh.Errors));
            Assert.AreEqual("\treturn Parameters.VertexColor.g", fresh.PropertyCode["Opacity"]);
            // No matching number must fall back to the authored mask, not the first name string.
            mask.WriteProperty(new NameProperty(new NameReference("Channels", 3), "ParameterName"));
            var unmatched = compiler.TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(unmatched.Success, string.Join("; ", unmatched.Errors));
            Assert.AreEqual("\treturn Parameters.VertexColor.r", unmatched.PropertyCode["Opacity"]);
        }

        [TestMethod]
        public void StaticMaskResolvedEmptyAndOutOfRangeChannelsStillFail()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3StaticMaskInvalid.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var vector = package.CreateExport("Vector", "MaterialExpressionConstant2Vector", material, indexed: false);
            var mask = package.CreateExport("Mask", "MaterialExpressionStaticComponentMaskParameter", material, indexed: false);
            mask.WriteProperty(new BoolProperty(true, "DefaultR"));
            mask.WriteProperty(Input("Input", vector.UIndex));
            material.WriteProperty(Input("Opacity", mask.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(vector.UIndex), new(mask.UIndex)], "Expressions"));
            var parameters = (StaticParameterSet)Guid.Empty;
            parameters.StaticComponentMaskParameters = [new() { ParameterName = NameReference.None }];
            var empty = new LE3MaterialShaderCompiler(material, parameters).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsFalse(empty.Success);
            Assert.IsNull(empty.MaterialHlsl);
            StringAssert.Contains(string.Join("; ", empty.Errors), "Couldn't determine result type of component mask 0000");
            parameters.StaticComponentMaskParameters[0].A = true;
            var invalidWidth = new LE3MaterialShaderCompiler(material, parameters).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsFalse(invalidWidth.Success);
            Assert.IsNull(invalidWidth.MaterialHlsl);
            StringAssert.Contains(string.Join("; ", invalidWidth.Errors), "Not enough components");
        }

        [TestMethod]
        public void ParameterExpressionsUseStageSpecificDeduplicatedUniformArrays()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShaderParameterTest.pcc", MEGame.LE3);
            var material = package.CreateExport("TestMaterial", "Material", indexed: false);
            var vector = package.CreateExport("Vector", "MaterialExpressionVectorParameter", material, indexed: false);
            vector.WriteProperty(new NameProperty("Tint", "ParameterName"));
            vector.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
            {
                new FloatProperty(1, "R"), new FloatProperty(2, "G"),
                new FloatProperty(3, "B"), new FloatProperty(4, "A")
            }, "DefaultValue", isImmutable: true));
            var duplicateVector = package.CreateExport("DuplicateVector", "MaterialExpressionVectorParameter", material,
                indexed: false);
            duplicateVector.WriteProperty(new NameProperty("Tint", "ParameterName"));
            duplicateVector.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
            {
                new FloatProperty(1, "R"), new FloatProperty(2, "G"),
                new FloatProperty(3, "B"), new FloatProperty(4, "A")
            }, "DefaultValue", isImmutable: true));

            var scalars = Enumerable.Range(0, 5)
                .Select(index => package.CreateExport($"Scalar{index}", "MaterialExpressionScalarParameter", material,
                    indexed: false))
                .ToArray();
            for (int i = 0; i < scalars.Length; i++)
            {
                scalars[i].WriteProperty(new NameProperty($"Value{i}", "ParameterName"));
                scalars[i].WriteProperty(new FloatProperty(i + 0.5f, "DefaultValue"));
            }

            material.WriteProperty(MaskedInput("EmissiveColor", vector.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("DiffuseColor", duplicateVector.UIndex, r: true, g: true, b: true));
            material.WriteProperty(Input("DiffusePower", scalars[0].UIndex));
            material.WriteProperty(Input("SpecularPower", scalars[1].UIndex));
            material.WriteProperty(Input("Opacity", scalars[2].UIndex));
            material.WriteProperty(Input("OpacityMask", scalars[3].UIndex));
            material.WriteProperty(Input("TwoSidedLightingMask", scalars[4].UIndex));
            material.WriteProperty(MaskedInput("WorldPositionOffset", vector.UIndex, r: true, g: true, b: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>(
                [new(vector.UIndex), new(duplicateVector.UIndex), .. scalars.Select(scalar => new ObjectProperty(scalar.UIndex))],
                "Expressions"));

            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());

            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(1, result.UniformPixelVectorExpressions.Count);
            Assert.AreEqual(5, result.UniformPixelScalarExpressions.Count);
            Assert.AreEqual(1, result.UniformVertexVectorExpressions.Count);
            Assert.AreEqual(0, result.UniformVertexScalarExpressions.Count);
            Assert.AreSame(result.UniformPixelVectorExpressions[0], result.UniformVertexVectorExpressions[0]);
            Assert.AreEqual("\treturn UniformPixelVector_0.rgb", result.PropertyCode["EmissiveColor"]);
            Assert.AreEqual("\treturn UniformPixelScalars_0.x", result.PropertyCode["DiffusePower"]);
            Assert.AreEqual("\treturn UniformPixelScalars_0.y", result.PropertyCode["SpecularPower"]);
            Assert.AreEqual("\treturn UniformPixelScalars_0.z", result.PropertyCode["Opacity"]);
            Assert.AreEqual("\treturn UniformPixelScalars_0.w", result.PropertyCode["OpacityMask"]);
            Assert.AreEqual("\treturn UniformPixelScalars_1.x", result.PropertyCode["TwoSidedLightingMask"]);
            Assert.AreEqual("\treturn UniformVertexVector_0.rgb", result.PropertyCode["WorldPositionOffset"]);
            StringAssert.Contains(result.MaterialHlsl, "float4 UniformPixelVector_0;\r\n");
            StringAssert.Contains(result.MaterialHlsl, "float4 UniformPixelScalars_0;\r\n");
            StringAssert.Contains(result.MaterialHlsl, "float4 UniformPixelScalars_1;\r\n");
            StringAssert.Contains(result.MaterialHlsl, "float4 UniformVertexVector_0;\r\n");
        }

        [TestMethod]
        public void TextureSamplesBuildDeduplicated2DAndCubeUniforms()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShaderTextureTest.pcc", MEGame.LE3);
            var material = package.CreateExport("TestMaterial", "Material", indexed: false);
            var texture2D = package.CreateExport("Default2D", "Texture2D", indexed: false);
            var textureCube = package.CreateExport("DefaultCube", "TextureCube", indexed: false);
            var first2D = package.CreateExport("First2D", "MaterialExpressionTextureSampleParameter2D", material,
                indexed: false);
            var duplicate2D = package.CreateExport("Duplicate2D", "MaterialExpressionTextureSampleParameter2D", material,
                indexed: false);
            var cube = package.CreateExport("Cube", "MaterialExpressionTextureSampleParameterCube", material,
                indexed: false);
            var cubeCoordinates = package.CreateExport("CubeCoordinates", "MaterialExpressionConstant3Vector", material,
                indexed: false);

            foreach (var expression in new[] { first2D, duplicate2D })
            {
                expression.WriteProperty(new NameProperty("DiffuseMap", "ParameterName"));
                expression.WriteProperty(new ObjectProperty(texture2D.UIndex, "Texture"));
            }
            cube.WriteProperty(new NameProperty("EnvironmentMap", "ParameterName"));
            cube.WriteProperty(new ObjectProperty(textureCube.UIndex, "Texture"));
            cube.WriteProperty(Input("Coordinates", cubeCoordinates.UIndex));
            cubeCoordinates.WriteProperty(new FloatProperty(1, "R"));
            cubeCoordinates.WriteProperty(new FloatProperty(2, "G"));
            cubeCoordinates.WriteProperty(new FloatProperty(3, "B"));

            material.WriteProperty(MaskedInput("EmissiveColor", first2D.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("DiffuseColor", duplicate2D.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("SpecularColor", cube.UIndex, r: true, g: true, b: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>(
                [new(first2D.UIndex), new(duplicate2D.UIndex), new(cube.UIndex), new(cubeCoordinates.UIndex)],
                "Expressions"));

            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());

            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
            Assert.AreEqual(1, result.UniformCubeTextureExpressions.Count);
            Assert.AreEqual(2, result.UniformExpressionTextures.Count);
            Assert.AreEqual(texture2D.UIndex, result.UniformExpressionTextures[0]);
            Assert.AreEqual(textureCube.UIndex, result.UniformExpressionTextures[1]);
            Assert.AreEqual(0, result.Uniform2DTextureExpressions[0].TextureIndex);
            Assert.AreEqual(1, result.UniformCubeTextureExpressions[0].TextureIndex);
            Assert.AreSame(result.Uniform2DTextureExpressions[0], result.Uniform2DTextureExpressions.Single());
            StringAssert.Contains(result.MaterialHlsl, "SAMPLER2D( Texture2D_0 );\r\n");
            StringAssert.Contains(result.MaterialHlsl, "SAMPLERCUBE( TextureCube_0 );\r\n");
            StringAssert.Contains(result.PropertyCode["EmissiveColor"], "tex2D(Texture2D_0,Local0)");
            StringAssert.Contains(result.PropertyCode["DiffuseColor"], "tex2D(Texture2D_0,Local4)");
            StringAssert.Contains(result.PropertyCode["EmissiveColor"],
                "float4 Local2 = (Local1 * float4(1.00000000,1.00000000,1.00000000,1.00000000));\r\n");
            StringAssert.Contains(result.PropertyCode["EmissiveColor"],
                "float4 Local3 = (Local2 + float4(0.00000000,0.00000000,0.00000000,0.00000000));\r\n");
            StringAssert.Contains(result.PropertyCode["SpecularColor"],
                "texCUBE(TextureCube_0,float3(1.00000000,2.00000000,3.00000000))");
        }

        [TestMethod]
        public void ImportedTextureReadsResolvedUnpackProperties()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShaderImportedTextureTest.pcc", MEGame.LE3);
            using var texturePackage = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShaderTextureSource.pcc", MEGame.LE3);
            var material = package.CreateExport("TestMaterial", "Material", indexed: false);
            var texture = texturePackage.CreateExport("External2D", "Texture2D", indexed: false);
            var importedTexture = new ImportEntry(package, 0, "External2D")
            {
                ClassName = "Texture2D", PackageFile = "LE3ShaderTextureSource"
            };
            package.AddImport(importedTexture);
            var sample = package.CreateExport("Sample", "MaterialExpressionTextureSample", material, indexed: false);
            sample.WriteProperty(new ObjectProperty(importedTexture.UIndex, "Texture"));
            material.WriteProperty(MaskedInput("EmissiveColor", sample.UIndex, r: true, g: true, b: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(sample.UIndex)], "Expressions"));

            var unresolved = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsFalse(unresolved.Success);
            StringAssert.Contains(string.Join("; ", unresolved.Errors), "supply a resolved LE3 texture export");

            var resolved = new LE3MaterialShaderCompiler(material, resolveTextureImport: _ => texture)
                .TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(resolved.Success, string.Join("; ", resolved.Errors));
            Assert.AreEqual(importedTexture.UIndex, resolved.UniformExpressionTextures[0]);

            for (int component = 0; component < 3; component++)
                texture.WriteProperty(new FloatProperty(-1, "UnpackMin") { StaticArrayIndex = component });
            var affine = new LE3MaterialShaderCompiler(material, resolveTextureImport: _ => texture)
                .TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(affine.Success, string.Join("; ", affine.Errors));
            StringAssert.Contains(affine.PropertyCode["EmissiveColor"],
                "float4 Local2 = (Local1 * float4(2.00000000,2.00000000,2.00000000,1.00000000));\r\n");
            StringAssert.Contains(affine.PropertyCode["EmissiveColor"],
                "float4 Local3 = (Local2 + float4(-1.00000000,-1.00000000,-1.00000000,0.00000000));\r\n");
        }

        [TestMethod]
        public void TextureMetadataCompilesImportsWithoutResolvingAnyTextureExport()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MetadataImports.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var texture = new ImportEntry(package, 0, "ExternalNormal") { ClassName = "Texture2D", PackageFile = "UnloadedDonor" };
            var otherTexture = new ImportEntry(package, 0, "OtherNormal") { ClassName = "Texture2D", PackageFile = "UnloadedDonor" };
            var cube = new ImportEntry(package, 0, "ExternalCube") { ClassName = "TextureCube", PackageFile = "UnloadedDonor" };
            package.AddImport(texture);
            package.AddImport(otherTexture);
            package.AddImport(cube);
            var first = Sample("First", texture);
            var duplicate = Sample("Duplicate", texture);
            var other = Sample("Other", otherTexture);
            var cubeSample = package.CreateExport("CubeSample", "MaterialExpressionTextureSampleParameterCube", material, indexed: false);
            cubeSample.WriteProperty(new NameProperty("Environment", "ParameterName"));
            cubeSample.WriteProperty(new ObjectProperty(cube.UIndex, "Texture"));
            var coordinates = package.CreateExport("Coordinates", "MaterialExpressionConstant3Vector", material, indexed: false);
            coordinates.WriteProperty(new FloatProperty(1, "R"));
            cubeSample.WriteProperty(Input("Coordinates", coordinates.UIndex));
            material.WriteProperty(MaskedInput("Normal", first.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("DiffuseColor", duplicate.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("SpecularColor", other.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("EmissiveColor", cubeSample.UIndex, r: true, g: true, b: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(first.UIndex), new(duplicate.UIndex),
                new(other.UIndex), new(cubeSample.UIndex), new(coordinates.UIndex)], "Expressions"));
            var normalMetadata = new LE3MaterialShaderCompiler.TextureCompilationMetadata(
                LE3MaterialShaderCompiler.TextureDimension.Texture2D, "TC_NormalmapBC5", new Vector4(-1, -1, -1, 0));
            int calls = 0;
            var compiler = new LE3MaterialShaderCompiler(material,
                resolveTextureImport: _ => throw new AssertFailedException("Metadata path must not resolve a texture export."),
                getTextureMetadata: entry =>
                {
                    calls++;
                    Assert.AreSame(package, entry.FileRef);
                    return entry == cube ? new(LE3MaterialShaderCompiler.TextureDimension.TextureCube) : normalMetadata;
                });
            var result = compiler.TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(3, calls); // one read per distinct original reference, not per sample.
            Assert.AreEqual(2, result.Uniform2DTextureExpressions.Count); // identical metadata is not texture identity.
            Assert.AreEqual(1, result.UniformCubeTextureExpressions.Count);
            CollectionAssert.AreEquivalent(new[] { texture.UIndex, otherTexture.UIndex, cube.UIndex }, result.UniformExpressionTextures.ToArray());
            StringAssert.Contains(result.PropertyCode["Normal"], "BiasNormalizeNormalMap_BC5");
            StringAssert.Contains(result.PropertyCode["EmissiveColor"], "texCUBE(TextureCube_0");
            Assert.AreEqual("SurfaceNormal", ((MaterialUniformExpressionTextureParameter)result.Uniform2DTextureExpressions[0]).ParameterName.Name);
            normalMetadata = new(LE3MaterialShaderCompiler.TextureDimension.Texture2D, "TC_NormalmapBC7", new Vector4(-1, -1, -1, 0));
            var half = compiler.TranslateMaterialHlsl(MinimalTemplate(), useHalfTypes: true);
            Assert.IsTrue(half.Success, string.Join("; ", half.Errors));
            Assert.AreEqual(6, calls); // a fresh translation obtains fresh metadata.
            StringAssert.Contains(half.PropertyCode["Normal"], "h4tex2D");
            StringAssert.Contains(half.PropertyCode["Normal"], "BiasNormalizeNormalMap_BC7");

            ExportEntry Sample(string name, IEntry reference)
            {
                var sample = package.CreateExport(name, "MaterialExpressionTextureSampleParameterNormal", material, indexed: false);
                sample.WriteProperty(new ObjectProperty(reference.UIndex, "Texture"));
                sample.WriteProperty(new NameProperty("SurfaceNormal", "ParameterName"));
                return sample;
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void MetadataOnlyTranslationMatchesLoadedTexturesWithTextureExportDataOmitted(bool compress)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MetadataPartialLoad.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var texture = package.CreateExport("Normal", "Texture2D", indexed: false);
            var cube = package.CreateExport("Cube", "TextureCube", indexed: false);
            texture.WriteProperty(new EnumProperty("TC_NormalmapBC5", "TextureCompressionSettings", MEGame.LE3, "CompressionSettings"));
            texture.WriteProperty(new FloatProperty(-0.25f, "UnpackMin") { StaticArrayIndex = 1 });
            texture.WriteProperty(new FloatProperty(0.5f, "UnpackMax") { StaticArrayIndex = 3 });
            cube.WriteProperty(new FloatProperty(2f, "UnpackMax") { StaticArrayIndex = 0 });
            var normal = package.CreateExport("NormalSample", "MaterialExpressionTextureSampleParameterNormal", material, indexed: false);
            normal.WriteProperty(new NameProperty("NormalMode", "ParameterName"));
            normal.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
            var environment = package.CreateExport("CubeSample", "MaterialExpressionTextureSampleParameterCube", material, indexed: false);
            environment.WriteProperty(new NameProperty("Environment", "ParameterName"));
            environment.WriteProperty(new ObjectProperty(cube.UIndex, "Texture"));
            var coordinates = package.CreateExport("Coordinates", "MaterialExpressionConstant3Vector", material, indexed: false);
            coordinates.WriteProperty(new FloatProperty(1, "R"));
            environment.WriteProperty(Input("Coordinates", coordinates.UIndex));
            material.WriteProperty(MaskedInput("Normal", normal.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("DiffuseColor", normal.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("EmissiveColor", environment.UIndex, r: true, g: true, b: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(normal.UIndex), new(environment.UIndex), new(coordinates.UIndex)], "Expressions"));
            var parameters = (StaticParameterSet)Guid.Empty;
            parameters.NormalParameters = [new() { ParameterName = "NormalMode", CompressionSettings = 12, bOverride = true }];
            var baseline = new LE3MaterialShaderCompiler(material, parameters).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(baseline.Success, string.Join("; ", baseline.Errors));
            var metadata = new System.Collections.Generic.Dictionary<int, LE3MaterialShaderCompiler.TextureCompilationMetadata>
            {
                [texture.UIndex] = LE3MaterialShaderCompiler.TextureCompilationMetadata.FromExport(texture),
                [cube.UIndex] = LE3MaterialShaderCompiler.TextureCompilationMetadata.FromExport(cube)
            };
            // Only serialize an in-memory synthetic fixture; no installed package is saved.
            using var stream = package.SaveToStream(compress);
            stream.Position = 0;
            using var partial = MEPackageHandler.UnsafePartialLoadFromStream(stream, "LE3MetadataPartialLoad.pcc",
                export => export.ClassName is not ("Texture2D" or "TextureCube"));
            var partialTexture = partial.GetUExport(texture.UIndex);
            var partialCube = partial.GetUExport(cube.UIndex);
            Assert.IsFalse(partialTexture.IsDataLoaded());
            Assert.IsFalse(partialCube.IsDataLoaded());
            var unresolved = new LE3MaterialShaderCompiler(partial.GetUExport(material.UIndex), parameters)
                .TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsFalse(unresolved.Success);
            StringAssert.Contains(string.Join("; ", unresolved.Errors), "unloaded texture");
            int reads = 0;
            var input = new LE3MaterialShaderCompiler.ResolvedMaterialCompilationInput(partial.GetUExport(material.UIndex), parameters);
            var compiler = new LE3MaterialShaderCompiler(input, getTextureMetadata: entry =>
            {
                reads++;
                Assert.AreSame(partial, entry.FileRef);
                return metadata[entry.UIndex];
            });
            var result = compiler.TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(2, reads);
            Assert.AreEqual(baseline.MaterialHlsl, result.MaterialHlsl);
            CollectionAssert.AreEqual(baseline.UniformExpressionTextures.ToArray(), result.UniformExpressionTextures.ToArray());
            CollectionAssert.AreEqual(baseline.Uniform2DTextureExpressions.Select(e => e.TextureIndex).ToArray(),
                result.Uniform2DTextureExpressions.Select(e => e.TextureIndex).ToArray());
            CollectionAssert.AreEqual(baseline.UniformCubeTextureExpressions.Select(e => e.TextureIndex).ToArray(),
                result.UniformCubeTextureExpressions.Select(e => e.TextureIndex).ToArray());
            Assert.AreSame(input, result.CompilationInput);
            StringAssert.Contains(result.PropertyCode["Normal"], "BiasNormalizeNormalMap_BC7");
            Assert.IsFalse(partialTexture.IsDataLoaded());
            Assert.IsFalse(partialCube.IsDataLoaded());
            Assert.ThrowsExactly<InvalidOperationException>(() => LE3MaterialShaderCompiler.TextureCompilationMetadata.FromExport(partialTexture));
        }

        [TestMethod]
        [DataRow("Texture2D", false)]
        [DataRow("Texture2D", true)]
        [DataRow("TextureCube", false)]
        [DataRow("TextureCube", true)]
        public void SuppliedTextureMetadataFailsClosedForMissingWrongDimensionAndUnsupportedCompression(string className, bool imported)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3InvalidMetadata.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            IEntry texture;
            if (imported)
            {
                var import = new ImportEntry(package, 0, "ExternalTexture") { ClassName = className, PackageFile = "UnloadedDonor" };
                package.AddImport(import);
                texture = import;
            }
            else texture = package.CreateExport("LoadedTexture", className, indexed: false);
            var sample = package.CreateExport("Sample", "MaterialExpressionTextureSample", material, indexed: false);
            sample.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
            material.WriteProperty(MaskedInput("EmissiveColor", sample.UIndex, r: true, g: true, b: true));
            material.WriteProperty(MaskedInput("DiffuseColor", sample.UIndex, r: true, g: true, b: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(sample.UIndex)], "Expressions"));
            Check(null, "no compilation metadata");
            var dimension = className == "TextureCube" ? LE3MaterialShaderCompiler.TextureDimension.TextureCube : LE3MaterialShaderCompiler.TextureDimension.Texture2D;
            var wrongDimension = className == "TextureCube" ? LE3MaterialShaderCompiler.TextureDimension.Texture2D : LE3MaterialShaderCompiler.TextureDimension.TextureCube;
            Check(new(wrongDimension), "requires " + className + " metadata");
            Check(new(dimension, "TC_Unsupported"), "CompressionSettings=TC_Unsupported");

            void Check(LE3MaterialShaderCompiler.TextureCompilationMetadata metadata, string expectedError)
            {
                int reads = 0;
                var result = new LE3MaterialShaderCompiler(material,
                    resolveTextureImport: _ => throw new AssertFailedException("An authoritative provider must not fall back to loading exports."),
                    getTextureMetadata: _ => { reads++; return metadata; })
                    .TranslateMaterialHlsl(MinimalTemplate());
                Assert.IsFalse(result.Success);
                Assert.IsNull(result.MaterialHlsl);
                Assert.AreEqual(1, reads);
                StringAssert.Contains(string.Join("; ", result.Errors), expectedError);
            }
        }

        [TestMethod]
        public void StaticSelectionDoesNotReadInactiveTextureMetadata()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3InactiveMetadata.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var texture = new ImportEntry(package, 0, "External") { ClassName = "Texture2D", PackageFile = "UnloadedDonor" };
            package.AddImport(texture);
            var sample = package.CreateExport("Sample", "MaterialExpressionTextureSample", material, indexed: false);
            sample.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
            var constant = package.CreateExport("Default", "MaterialExpressionConstant3Vector", material, indexed: false);
            var selector = package.CreateExport("Selector", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
            selector.WriteProperty(new NameProperty("UseTexture", "ParameterName"));
            selector.WriteProperty(new BoolProperty(true, "DefaultValue"));
            selector.WriteProperty(MaskedInput("A", sample.UIndex, r: true, g: true, b: true));
            selector.WriteProperty(Input("B", constant.UIndex));
            material.WriteProperty(Input("EmissiveColor", selector.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(sample.UIndex), new(constant.UIndex), new(selector.UIndex)], "Expressions"));
            var parameters = (StaticParameterSet)Guid.Empty;
            parameters.StaticSwitchParameters = [new() { ParameterName = "UseTexture", Value = false, bOverride = true }];
            var result = new LE3MaterialShaderCompiler(material, parameters,
                getTextureMetadata: _ => throw new AssertFailedException("Inactive texture branch must not read metadata."))
                .TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        }

        [TestMethod]
        public void TextureMetadataPropertyAdapterSnapshotsAllChannelsAndDefaults()
        {
            var defaults = LE3MaterialShaderCompiler.TextureCompilationMetadata.FromProperties(
                LE3MaterialShaderCompiler.TextureDimension.TextureCube, new PropertyCollection());
            Assert.AreEqual("TC_Default", defaults.CompressionSettings);
            Assert.AreEqual(Vector4.Zero, defaults.UnpackMin);
            Assert.AreEqual(Vector4.One, defaults.UnpackMax);
            var properties = new PropertyCollection
            {
                new EnumProperty("TC_NormalmapBC5", "TextureCompressionSettings", MEGame.LE3, "CompressionSettings")
            };
            for (int i = 0; i < 4; i++)
            {
                properties.Add(new FloatProperty(i - 2, "UnpackMin") { StaticArrayIndex = i });
                properties.Add(new FloatProperty(i + 2, "UnpackMax") { StaticArrayIndex = i });
            }
            var snapshot = LE3MaterialShaderCompiler.TextureCompilationMetadata.FromProperties(
                LE3MaterialShaderCompiler.TextureDimension.Texture2D, properties);
            properties.GetProp<FloatProperty>("UnpackMin", 0).Value = 99;
            Assert.AreEqual(new Vector4(-2, -1, 0, 1), snapshot.UnpackMin);
            Assert.AreEqual(new Vector4(2, 3, 4, 5), snapshot.UnpackMax);
            Assert.AreEqual("TC_NormalmapBC5", snapshot.CompressionSettings);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LE3MaterialShaderCompiler.TextureCompilationMetadata(
                (LE3MaterialShaderCompiler.TextureDimension)99));
            Assert.ThrowsExactly<ArgumentException>(() => new LE3MaterialShaderCompiler.TextureCompilationMetadata(
                LE3MaterialShaderCompiler.TextureDimension.Texture2D, ""));
        }

        [TestMethod]
        public void NormalTextureUsesCompressionHelperAndResolvedStaticOverride()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShaderNormalTextureTest.pcc", MEGame.LE3);
            var material = package.CreateExport("TestMaterial", "Material", indexed: false);
            var texture = package.CreateExport("Normal2D", "Texture2D", indexed: false);
            texture.WriteProperty(new EnumProperty("TC_NormalmapBC5", "TextureCompressionSettings", MEGame.LE3,
                "CompressionSettings"));
            for (int component = 0; component < 3; component++)
                texture.WriteProperty(new FloatProperty(-1, "UnpackMin") { StaticArrayIndex = component });

            var sample = package.CreateExport("NormalSample", "MaterialExpressionTextureSampleParameterNormal", material,
                indexed: false);
            sample.WriteProperty(new NameProperty("NormalMode", "ParameterName"));
            sample.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
            sample.WriteProperty(new BoolProperty(false, "bDecompressNormalMap"));
            material.WriteProperty(MaskedInput("Normal", sample.UIndex, r: true, g: true, b: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(sample.UIndex)], "Expressions"));

            var authored = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(authored.Success, string.Join("; ", authored.Errors));
            StringAssert.Contains(authored.PropertyCode["Normal"], "BiasNormalizeNormalMap_BC5(Local1)");
            var parameters = (StaticParameterSet)Guid.Empty;
            parameters.NormalParameters =
            [
                new StaticParameterSet.NormalParameter
                {
                    ParameterName = "NormalMode", CompressionSettings = 12, bOverride = false, ExpressionGUID = Guid.NewGuid()
                }
            ];
            var inherited = new LE3MaterialShaderCompiler(material, parameters).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(inherited.Success, string.Join("; ", inherited.Errors));
            StringAssert.Contains(inherited.PropertyCode["Normal"],
                "float4 Local2 = BiasNormalizeNormalMap_BC7(Local1);\r\n");
            Assert.IsFalse(inherited.PropertyCode["Normal"].Contains("Local3", StringComparison.Ordinal));

            parameters.NormalParameters[0].bOverride = true;
            var overridden = new LE3MaterialShaderCompiler(material, parameters).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(overridden.Success, string.Join("; ", overridden.Errors));
            StringAssert.Contains(overridden.PropertyCode["Normal"],
                "float4 Local2 = BiasNormalizeNormalMap_BC7(Local1);\r\n");

            for (int component = 0; component < 3; component++)
                texture.WriteProperty(new FloatProperty(0, "UnpackMin") { StaticArrayIndex = component });
            var noncanonical = new LE3MaterialShaderCompiler(material, parameters).TranslateMaterialHlsl(MinimalTemplate());
            Assert.IsTrue(noncanonical.Success, string.Join("; ", noncanonical.Errors));
            StringAssert.Contains(noncanonical.PropertyCode["Normal"],
                "float4 Local3 = (Local2 + float4(1.00000000,1.00000000,1.00000000,0.00000000));\r\n");
            StringAssert.Contains(noncanonical.PropertyCode["Normal"],
                "float4 Local4 = (Local3 * float4(0.50000000,0.50000000,0.50000000,1.00000000));\r\n");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NormalStaticBindingsMatchFullNamesUseFirstEntryAndKeepNoneFallback(bool enabled)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NormalStaticBinding.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var texture = package.CreateExport("NormalTexture", "Texture2D", indexed: false);
            var sample = package.CreateExport("NormalSample", "MaterialExpressionTextureSampleParameterNormal", material, indexed: false);
            sample.WriteProperty(new NameProperty(new NameReference("NormalMode", 2), "ParameterName"));
            sample.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
            material.WriteProperty(MaskedInput("Normal", sample.UIndex, r: true, g: true, b: true));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(sample.UIndex)], "Expressions"));
            var metadata = new LE3MaterialShaderCompiler.TextureCompilationMetadata(
                LE3MaterialShaderCompiler.TextureDimension.Texture2D, "TC_NormalmapBC5", new Vector4(-1, -1, -1, 0), Vector4.One);
            var parameters = (StaticParameterSet)Guid.Empty;
            parameters.NormalParameters =
            [
                new() { ParameterName = new NameReference("NormalMode", 1), CompressionSettings = 1, bOverride = true },
                new() { ParameterName = new NameReference("NORMALMODE", 2), CompressionSettings = 12, bOverride = enabled, ExpressionGUID = Guid.NewGuid() },
                new() { ParameterName = new NameReference("NormalMode", 2), CompressionSettings = 3, bOverride = true }
            ];
            Check("BC7");
            parameters.NormalParameters[1].CompressionSettings = 250;
            var invalid = Translate();
            Assert.IsFalse(invalid.Success);
            Assert.IsNull(invalid.MaterialHlsl);
            StringAssert.Contains(string.Join("; ", invalid.Errors), "TC_#250");
            sample.WriteProperty(new NameProperty(new NameReference("NormalMode", 3), "ParameterName"));
            Check("BC5");
            sample.WriteProperty(new NameProperty(NameReference.None, "ParameterName"));
            parameters.NormalParameters = [new() { ParameterName = NameReference.None, CompressionSettings = 12, bOverride = true }];
            Check("BC5"); // Native NAME_None fallback calls ordinary TextureSample, ignoring the bound normal entry.

            LE3MaterialShaderCompiler.TranslationResult Translate() => new LE3MaterialShaderCompiler(material, parameters,
                getTextureMetadata: _ => metadata).TranslateMaterialHlsl(MinimalTemplate());
            void Check(string helper)
            {
                var result = Translate();
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                StringAssert.Contains(result.PropertyCode["Normal"], "BiasNormalizeNormalMap_" + helper + "(Local1)");
                Assert.AreEqual("TC_NormalmapBC5", metadata.CompressionSettings);
                Assert.AreEqual(sample.GetProperty<NameProperty>("ParameterName").Value == NameReference.None,
                    result.Uniform2DTextureExpressions.Single() is not MaterialUniformExpressionTextureParameter);
            }
        }

        [TestMethod]
        public void ProvisionalMacrosIncludeMeasuredDecalWorldCoordinatesRule()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShaderMacroTest.pcc", MEGame.LE3);
            var material = package.CreateExport("TestDecal", "DecalMaterial", indexed: false);
            var compiler = new LE3MaterialShaderCompiler(material);
            var graph = compiler.AnalyzeGraph();
            var macros = compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), graph);

            Assert.AreEqual("1", macros["MATERIALBLENDING_SOLID"]);
            Assert.AreEqual("1", macros["MATERIAL_LIGHTINGMODEL_PHONG"]);
            Assert.AreEqual("1", macros["MATERIAL_DECAL"]);
            Assert.AreEqual("1", macros["WORLD_COORDS"]);
        }

        [TestMethod]
        public void TranslationRejectsInvalidGraphBeforeRecursiveCompilation()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShaderCycleTest.pcc", MEGame.LE3);
            var material = package.CreateExport("TestMaterial", "Material", indexed: false);
            var constant = package.CreateExport("Constant", "MaterialExpressionConstant", material, indexed: false);
            var add = package.CreateExport("Cycle", "MaterialExpressionAdd", material, indexed: false);
            add.WriteProperty(Input("A", add.UIndex));
            add.WriteProperty(Input("B", constant.UIndex));
            material.WriteProperty(Input("EmissiveColor", add.UIndex));
            material.WriteProperty(new ArrayProperty<ObjectProperty>([new(constant.UIndex), new(add.UIndex)], "Expressions"));

            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(MinimalTemplate());

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.MaterialHlsl);
            Assert.IsTrue(result.Errors.Any(error => error.Contains("cycle", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public void CompileJobPreservesRegistrationMetadataAndRejectsMacroConflicts()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CompilePlanTest.pcc", MEGame.LE3);
            var material = package.CreateExport("TestMaterial", "Material", indexed: false);
            var compiler = new LE3MaterialShaderCompiler(material);
            var translation = compiler.TranslateMaterialHlsl(MinimalTemplate());
            var target = new LE3MaterialShaderCompiler.ShaderCompileTarget(
                "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight",
                "BasePassPixelShader",
                "Main",
                LE3MaterialShaderCompiler.ShaderStage.Pixel,
                LE3MaterialShaderCompiler.CapturedDefaultCompileFlags,
                "FLocalVertexFactory",
                "LocalVertexFactory");

            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), target,
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["PIXELSHADER"] = "1",
                    ["MATERIALBLENDING_SOLID"] = "1"
                });

            Assert.AreSame(target, job.Target);
            Assert.AreEqual("ps_5_0", job.Target.Profile);
            Assert.AreEqual(0x9000u, job.Target.CompileFlags);
            Assert.AreSame(translation.MaterialHlsl, job.MaterialHlsl);
            Assert.AreEqual("1", job.Macros["SM5_PROFILE"]);
            Assert.AreEqual("1", job.Macros["PIXELSHADER"]);
            Assert.AreEqual("1", job.Macros["MATERIAL_LIGHTINGMODEL_PHONG"]);

            Assert.ThrowsExactly<InvalidOperationException>(() =>
                compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), target,
                    new System.Collections.Generic.Dictionary<string, string> { ["MATERIALBLENDING_SOLID"] = "0" }));
        }

        [TestMethod]
        [DataRow("TBasePassPixelShaderFDirectionalLightMapTexturePolicyNoSkyLight", true)]
        [DataRow("TBasePassPixelShaderFDirectionalLightMapTexturePolicySkyLight", true)]
        [DataRow("TBasePassVertexShaderFDirectionalLightMapTexturePolicyFNoDensityPolicy", true)]
        [DataRow("TBasePassPixelShaderFSimpleLightMapTexturePolicyNoSkyLight", false)]
        [DataRow("TBasePassPixelShaderFSimpleLightMapTexturePolicySkyLight", false)]
        [DataRow("TBasePassVertexShaderFSimpleLightMapTexturePolicyFNoDensityPolicy", false)]
        [DataRow("TBasePassPixelShaderFSimpleVertexLightMapPolicyNoSkyLight", false)]
        [DataRow("TBasePassPixelShaderFSimpleVertexLightMapPolicySkyLight", false)]
        [DataRow("TBasePassVertexShaderFSimpleVertexLightMapPolicyFNoDensityPolicy", false)]
        [DataRow("TLightVertexShaderFDirectionalLightPolicyFShadowTexturePolicy", false)]
        [DataRow("TLightPixelShaderFDirectionalLightPolicyFShadowTexturePolicy", false)]
        [DataRow("TLightPixelShaderFPointLightPolicyFShadowTexturePolicy", false)]
        [DataRow("TLightVertexShaderFSpotLightPolicyFShadowTexturePolicy", false)]
        [DataRow("TLightPixelShaderFSpotLightPolicyFShadowTexturePolicy", false)]
        public void TextureLightmapShouldCacheUsesMaterialAndFactoryCapabilities(string shader, bool directional)
        {
            var ordinary = new LE3MaterialShaderCompiler.ShouldCacheMaterialContext(false, false, false,
                false, false, false, false, false);
            for (int bits = 0; bits < 32; bits++)
            {
                bool unlit = (bits & 1) != 0, supports = (bits & 2) != 0,
                    used = (bits & 4) != 0, special = (bits & 8) != 0, simpleOnly = (bits & 16) != 0;
                var material = ordinary with { IsUnlit = unlit, UsedWithStaticLighting = used,
                    IsSpecialEngineMaterial = special, RequiresSimpleStaticLighting = simpleOnly };
                var factory = new LE3MaterialShaderCompiler.ShouldCacheVertexFactoryContext(supports);
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(shader,
                    material, "FLocalVertexFactory", factory, out bool caches));
                Assert.AreEqual(!unlit && supports && (used || special) && (!directional || !simpleOnly), caches,
                    $"{shader}, predicates {bits}");
            }
            Assert.IsFalse(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(shader,
                ordinary, "FLocalVertexFactory", out bool unresolved));
            Assert.IsFalse(unresolved); // A name must not silently stand in for capability evidence.
            if (directional)
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(shader,
                    ordinary with { RequiresSimpleStaticLighting = true }, out bool rejected));
                Assert.IsFalse(rejected); // Native directional early veto needs no factory query.
            }
        }

        [TestMethod]
        [DataRow("TBasePassPixelShaderFDirectionalLightMapTexturePolicyNoSkyLight", false, false)]
        [DataRow("TBasePassPixelShaderFDirectionalLightMapTexturePolicySkyLight", false, true)]
        [DataRow("TBasePassVertexShaderFDirectionalLightMapTexturePolicyFNoDensityPolicy", false, false)]
        [DataRow("TBasePassPixelShaderFSimpleLightMapTexturePolicyNoSkyLight", true, false)]
        [DataRow("TBasePassPixelShaderFSimpleLightMapTexturePolicySkyLight", true, true)]
        [DataRow("TBasePassVertexShaderFSimpleLightMapTexturePolicyFNoDensityPolicy", true, false)]
        public void TextureLightmapEnvironmentsKeepExactCapturedPolicyDefines(string shader, bool simple, bool sky)
        {
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(shader,
                "FLocalVertexFactory", out var environment));
            var macros = environment.CreateCandidate().EnvironmentMacros;
            bool vertex = environment.Target.Stage == LE3MaterialShaderCompiler.ShaderStage.Vertex;
            string[] expected = new[] { "SM5_PROFILE", "SUPPORTS_DEPTH_TEXTURES", "NUM_SAMPLES",
                "NUM_LIGHTMAP_COEFFICIENTS", vertex ? "VERTEXSHADER" : "PIXELSHADER" }
                .Concat(simple ? ["SIMPLE_TEXTURE_LIGHTMAP"] : Array.Empty<string>())
                .Concat(vertex || !simple ? ["TEXTURE_LIGHTMAP"] : Array.Empty<string>())
                .Concat(vertex ? ["FOGVOLUMEDENSITY_NONE"] : Array.Empty<string>())
                .Concat(sky ? ["ENABLE_SKY_LIGHT"] : Array.Empty<string>()).ToArray();
            CollectionAssert.AreEquivalent(expected, macros.Keys.ToArray());
            Assert.AreEqual(simple ? "1" : "2", macros["NUM_LIGHTMAP_COEFFICIENTS"]);
            Assert.IsTrue(macros.Where(m => m.Key != "NUM_LIGHTMAP_COEFFICIENTS").All(m => m.Value == "1"));
            Assert.AreEqual(vertex ? "BasePassVertexShader" : "BasePassPixelShader", environment.Target.SourceFileName);
            Assert.AreEqual(0x9000u, environment.Target.CompileFlags);
Assert.AreEqual(LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.ShippingBytecode,
                environment.CompileFlagsEvidence);
            Assert.IsTrue(environment.MacroDefinitions.All(m => m.Evidence ==
                LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.ShaderClientReconstruction));
        }

        [TestMethod]
        [DataRow("TBasePassPixelShaderFSimpleVertexLightMapPolicyNoSkyLight", false, false)]
        [DataRow("TBasePassPixelShaderFSimpleVertexLightMapPolicySkyLight", true, false)]
        [DataRow("TBasePassVertexShaderFSimpleVertexLightMapPolicyFNoDensityPolicy", false, true)]
        public void SimpleVertexLightmapEnvironmentsAndInstancedFactoryVetoAreExplicit(string shader, bool sky, bool vertex)
        {
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(shader,
                "FLocalVertexFactory", out var environment));
            var macros = environment.CreateCandidate().EnvironmentMacros;
            string[] expected = new[] { "SM5_PROFILE", "SUPPORTS_DEPTH_TEXTURES", "NUM_SAMPLES",
                "NUM_LIGHTMAP_COEFFICIENTS", "SIMPLE_VERTEX_LIGHTMAP", vertex ? "VERTEXSHADER" : "PIXELSHADER" }
                .Concat(vertex ? ["FOGVOLUMEDENSITY_NONE"] : Array.Empty<string>())
                .Concat(sky ? ["ENABLE_SKY_LIGHT"] : Array.Empty<string>()).ToArray();
            CollectionAssert.AreEquivalent(expected, macros.Keys.ToArray());
            Assert.IsTrue(macros.Values.All(v => v == "1"));
            Assert.AreEqual(0x9000u, environment.Target.CompileFlags);
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.ShippingBytecode,
                environment.CompileFlagsEvidence);
            var material = new LE3MaterialShaderCompiler.ShouldCacheMaterialContext(false, false, false,
                false, false, false, false, true);
            var factory = new LE3MaterialShaderCompiler.ShouldCacheVertexFactoryContext(true);
            Assert.IsFalse(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(shader, material,
                null, factory, out bool unresolved));
            Assert.IsFalse(unresolved);
            foreach (string name in new[] { "FInstancedStaticMeshVertexFactory", "OtherFInstancedStaticMeshVertexFactory" })
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(shader, material,
                    name, factory, out bool caches));
                Assert.IsFalse(caches);
            }
            // Native substring matching is case-sensitive; no invented case-folding policy.
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(shader, material,
                "finstancedstaticmeshvertexfactory", factory, out bool lowerCase));
            Assert.IsTrue(lowerCase);
        }

        [TestMethod]
        [DataRow("TLightVertexShaderFDirectionalLightPolicyFShadowTexturePolicy", true, true)]
        [DataRow("TLightPixelShaderFDirectionalLightPolicyFShadowTexturePolicy", true, false)]
        [DataRow("TLightPixelShaderFPointLightPolicyFShadowTexturePolicy", false, false)]
        [DataRow("TLightVertexShaderFSpotLightPolicyFShadowTexturePolicy", false, true)]
        [DataRow("TLightPixelShaderFSpotLightPolicyFShadowTexturePolicy", false, false)]
        public void ShadowTextureEnvironmentsKeepExactDefinesAndShippingFlags(string shader, bool directional, bool vertex)
        {
            foreach (string factory in new[] { "FLocalVertexFactory", "FGPUSkinVertexFactory" })
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(shader, factory, out var environment));
                var macros = environment.CreateCandidate().EnvironmentMacros;
                string[] expected = new[] { "SM5_PROFILE", "SUPPORTS_DEPTH_TEXTURES", "NUM_SAMPLES",
                    "NUM_LIGHTMAP_COEFFICIENTS", "SIMPLE_VERTEX_LIGHTMAP", "STATICLIGHTING_TEXTUREMASK",
                    vertex ? "VERTEXSHADER" : "PIXELSHADER" }
                    .Concat(directional ? ["ENABLE_DISTANCE_SHADOW_FADING"] : Array.Empty<string>())
                    .Concat(factory == "FGPUSkinVertexFactory" ? ["QUAT_SKINNING"] : Array.Empty<string>()).ToArray();
                CollectionAssert.AreEquivalent(expected, macros.Keys.ToArray());
                Assert.AreEqual("2", macros["NUM_LIGHTMAP_COEFFICIENTS"]);
                Assert.IsTrue(macros.Where(m => m.Key is not ("NUM_LIGHTMAP_COEFFICIENTS" or "QUAT_SKINNING"))
                    .All(m => m.Value == "1"));
                Assert.AreEqual(vertex ? 0x9000u : 0x9400u, environment.Target.CompileFlags);
                Assert.AreEqual(LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.ShippingBytecode,
                    environment.CompileFlagsEvidence);
                Assert.AreEqual(LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.ShaderClientReconstruction,
                    environment.MacroDefinitions.Single(m => m.Name == "STATICLIGHTING_TEXTUREMASK").Evidence);
            }
        }

        [TestMethod]
        public void KnownShaderTypeShouldCacheCallbacksUseRecoveredMaterialPredicates()
        {
            var ordinary = new LE3MaterialShaderCompiler.ShouldCacheMaterialContext(
                IsUnlit: false,
                IsMasked: false,
                IsSpecialEngineMaterial: false,
                UsesLitTranslucencyDepthPass: false,
                UsesSurfaceTranslucencyDepthPass: false,
                CastLitTranslucencyShadowAsMasked: false,
                HasWorldPositionOffset: false,
                UsedWithStaticLighting: false);

            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight", ordinary, out bool noSkyLight));
            Assert.IsTrue(noSkyLight);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "TBasePassPixelShaderFNoLightMapPolicySkyLight", ordinary with { IsUnlit = true }, out bool skyLight));
            Assert.IsFalse(skyLight);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "TDepthOnlySolidPixelShader", ordinary with { IsMasked = true }, out bool depthOnly));
            Assert.IsTrue(depthOnly);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "TShadowDepthVertexShader<ShadowDepth_OutputDepth>",
                ordinary with { HasWorldPositionOffset = true }, out bool shadowVertex));
            Assert.IsTrue(shadowVertex);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "TShadowDepthPixelShaderFALSETRUE", ordinary, out bool shadowPixel));
            Assert.IsFalse(shadowPixel);

            Assert.IsFalse(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "FUnrecoveredShaderType", ordinary, out bool unknown));
            Assert.IsFalse(unknown);
        }

        [TestMethod]
        public void ShouldCacheContextUsesStoredMaskAndLE3DepthEligibility()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShouldCacheContextTest.pcc", MEGame.LE3);
            var material = package.CreateExport("TestMaterial", "Material", indexed: false);
            var offset = package.CreateExport("Offset", "MaterialExpressionConstant3Vector", material, indexed: false);
            material.WriteProperty(new EnumProperty("MLM_Unlit", "EMaterialLightingModel", MEGame.LE3,
                "LightingModel"));
            material.WriteProperty(new EnumProperty("BLEND_Translucent", "EBlendMode", MEGame.LE3, "BlendMode"));
            material.WriteProperty(new BoolProperty(true, "bIsMasked"));
            material.WriteProperty(new BoolProperty(true, "bUsedAsSpecialEngineMaterial"));
            material.WriteProperty(new BoolProperty(true, "bUsedWithStaticLighting"));
            material.WriteProperty(new BoolProperty(true, "bCastLitTranslucencyShadowAsMasked"));
            material.WriteProperty(new BoolProperty(true, "bUseLitTranslucencyDepthPass"));
            material.WriteProperty(new BoolProperty(true, "bUseLitTranslucencyPostRenderDepthPass"));
            material.WriteProperty(new BoolProperty(true, "bUseSurfaceTranslucencyDepthPass"));
            material.WriteProperty(new BoolProperty(true, "bUsedWithFogVolumes"));
            material.WriteProperty(Input("WorldPositionOffset", offset.UIndex));

            var compiler = new LE3MaterialShaderCompiler(material);
            var unlit = compiler.BuildShouldCacheMaterialContext();
            Assert.IsTrue(unlit.IsUnlit);
            Assert.IsTrue(unlit.IsMasked);
            Assert.IsTrue(unlit.IsSpecialEngineMaterial);
            Assert.IsTrue(unlit.UsedWithStaticLighting);
            Assert.IsTrue(unlit.CastLitTranslucencyShadowAsMasked);
            Assert.IsTrue(unlit.HasWorldPositionOffset);
            Assert.IsFalse(unlit.UsesLitTranslucencyDepthPass);
            Assert.IsTrue(unlit.UsesLitTranslucencyDepthPostpass);
            Assert.IsTrue(unlit.UsesSurfaceTranslucencyDepthPass);
            Assert.IsTrue(unlit.UsedWithFogVolumes);
            Assert.AreEqual(EBlendMode.BLEND_Translucent, unlit.BlendMode);

            material.WriteProperty(new EnumProperty("MLM_Phong", "EMaterialLightingModel", MEGame.LE3,
                "LightingModel"));
            material.WriteProperty(new EnumProperty("BLEND_SoftMasked", "EBlendMode", MEGame.LE3, "BlendMode"));
            Assert.IsFalse(compiler.BuildShouldCacheMaterialContext().UsesLitTranslucencyDepthPass);
            Assert.IsFalse(compiler.BuildShouldCacheMaterialContext().UsesLitTranslucencyDepthPostpass);
            material.WriteProperty(new EnumProperty("BLEND_Translucent", "EBlendMode", MEGame.LE3, "BlendMode"));
            Assert.IsTrue(compiler.BuildShouldCacheMaterialContext().UsesLitTranslucencyDepthPass);

            var decal = package.CreateExport("TestDecal", "DecalMaterial", indexed: false);
            var decalCompiler = new LE3MaterialShaderCompiler(decal);
            Assert.IsTrue(decalCompiler.BuildShouldCacheMaterialContext().UsedWithStaticLighting);
            decal.WriteProperty(new BoolProperty(false, "bUsedWithStaticLighting"));
            Assert.IsFalse(decalCompiler.BuildShouldCacheMaterialContext().UsedWithStaticLighting);
        }

        [TestMethod]
        public void DepthAndShadowSelectionDistinguishesPostpassWpoAndPositionOnlyStreams()
        {
            var ordinary = new LE3MaterialShaderCompiler.ShouldCacheMaterialContext(
                false, false, false, false, false, false, false, false);

            // The LE3 depth VS includes +0x48; the solid PS does not. Unlit does not suppress postpass.
            var postpass = ordinary with { IsUnlit = true, UsesLitTranslucencyDepthPostpass = true };
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "TDepthOnlyVertexShader<0>", postpass, out bool caches));
            Assert.IsTrue(caches);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "TDepthOnlySolidPixelShader", postpass, out caches));
            Assert.IsFalse(caches);

            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "FTranslucencyPostRenderDepthPixelShader", postpass, out caches));
            Assert.IsTrue(caches);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "FTranslucencyPostRenderDepthPixelShader", ordinary with { BlendMode = EBlendMode.BLEND_SoftMasked }, out caches));
            Assert.IsTrue(caches);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "FTranslucencyPostRenderDepthPixelShader", ordinary, out caches));
            Assert.IsFalse(caches);

            var wpo = ordinary with { HasWorldPositionOffset = true };
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "TShadowDepthVertexShader<ShadowDepth_OutputDepthToColor>", wpo, out caches));
            Assert.IsTrue(caches); // SP_SM5 is among the native color-output callback's accepted platforms.
            foreach (string shader in new[] { "TShadowDepthPixelShaderTRUEFALSE", "TShadowDepthPixelShaderFALSEFALSE" })
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(shader, wpo, out caches));
                Assert.IsFalse(caches);
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(shader,
                    ordinary with { CastLitTranslucencyShadowAsMasked = true }, out caches));
                Assert.IsTrue(caches);
            }

            var special = ordinary with { IsSpecialEngineMaterial = true };
            foreach (string vf in new[] { "FLocalVertexFactory", "FInstancedStaticMeshVertexFactory", "FSplineMeshVertexFactory" })
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                    "TDepthOnlyVertexShader<1>", special, vf, out caches));
                Assert.IsTrue(caches);
            }
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "TDepthOnlyVertexShader<1>", special, "FGPUSkinVertexFactory", out caches));
            Assert.IsFalse(caches);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "TDepthOnlyVertexShader<1>", ordinary with { IsMasked = true }, "FLocalVertexFactory", out caches));
            Assert.IsFalse(caches);
            Assert.IsFalse(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "TDepthOnlyVertexShader<1>", special, out caches));
        }

        [TestMethod]
        public void NoLightmapFogSelectionRequiresBlendAndGpuSkinUsageButNoDensityDoesNot()
        {
            var opaque = new LE3MaterialShaderCompiler.ShouldCacheMaterialContext(
                true, false, false, false, false, false, false, false);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", opaque, out bool caches));
            Assert.IsTrue(caches);
            foreach (string density in new[] { "FConstantDensityPolicy", "FLinearHalfspaceDensityPolicy", "FSphereDensityPolicy" })
            {
                string shader = $"TBasePassVertexShaderFNoLightMapPolicy{density}";
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                    shader, opaque, "FLocalVertexFactory", out caches));
                Assert.IsFalse(caches);
                var translucent = opaque with { BlendMode = EBlendMode.BLEND_Translucent };
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                    shader, translucent, "FLocalVertexFactory", out caches));
                Assert.IsTrue(caches);
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                    shader, translucent, "FGPUSkinVertexFactory", out caches));
                Assert.IsFalse(caches);
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                    shader, translucent with { UsedWithFogVolumes = true }, "FGPUSkinVertexFactory", out caches));
                Assert.IsTrue(caches);
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(
                    shader, translucent with { BlendMode = EBlendMode.BLEND_Modulate }, "FLocalVertexFactory", out caches));
                Assert.IsFalse(caches);
                Assert.IsFalse(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(shader, translucent, out caches));
            }
        }

        [TestMethod]
        public void CompilePlanSelectsKnownPermutationsAndKeepsPerTargetFlagsAndEnvironment()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CompilePlanSelectionTest.pcc", MEGame.LE3);
            var material = package.CreateExport("TestMaterial", "Material", indexed: false);
            material.WriteProperty(new EnumProperty("MLM_Unlit", "EMaterialLightingModel", MEGame.LE3, "LightingModel"));
            var compiler = new LE3MaterialShaderCompiler(material);
            var translation = compiler.TranslateMaterialHlsl(MinimalTemplate());
            var noSky = new LE3MaterialShaderCompiler.ShaderCompileTarget(
                "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight", "BasePassPixelShader", "Main",
                LE3MaterialShaderCompiler.ShaderStage.Pixel, 0x9400, "FLocalVertexFactory", "LocalVertexFactory");
            var sky = noSky with { ShaderTypeName = "TBasePassPixelShaderFNoLightMapPolicySkyLight" };
            var depth = noSky with { ShaderTypeName = "TDepthOnlySolidPixelShader", SourceFileName = "DepthOnlyPixelShader" };
            var vf = new LE3MaterialShaderCompiler.ShouldCacheVertexFactoryContext(false);
            var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(),
            [
                new(noSky, new System.Collections.Generic.Dictionary<string, string> { ["NUM_SAMPLES"] = "1" }, vf),
                new(sky, VertexFactoryContext: vf),
                new(depth, VertexFactoryContext: vf)
            ]);
            Assert.IsTrue(plan.Success, string.Join(Environment.NewLine, plan.Errors));
            Assert.AreEqual(1, plan.Jobs.Count);
            Assert.AreEqual(2, plan.ExcludedTargets.Count);
            Assert.AreSame(noSky, plan.Jobs[0].Target);
            Assert.AreEqual(0x9400u, plan.Jobs[0].Target.CompileFlags);
            Assert.AreEqual("1", plan.Jobs[0].Macros["NUM_SAMPLES"]);
            Assert.AreEqual("1", plan.Jobs[0].Macros["MATERIAL_LIGHTINGMODEL_UNLIT"]);
            Assert.AreEqual(3, plan.Decisions.Count);
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderCompileDisposition.Eligible, plan.Decisions[0].Disposition);
            Assert.IsTrue(plan.Decisions.Skip(1).All(d =>
                d.Disposition == LE3MaterialShaderCompiler.ShaderCompileDisposition.Excluded &&
                d.Gate == LE3MaterialShaderCompiler.ShaderCompileGate.ShaderTypeShouldCache));
        }

        [TestMethod]
        public void CompilePlanWithholdsPartialJobsForUnknownCallbacksAndConflictingMetadata()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CompilePlanFailureTest.pcc", MEGame.LE3);
            var compiler = new LE3MaterialShaderCompiler(package.CreateExport("TestMaterial", "Material", indexed: false));
            var translation = compiler.TranslateMaterialHlsl(MinimalTemplate());
            var known = new LE3MaterialShaderCompiler.ShaderCompileTarget(
                "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight", "BasePassPixelShader", "Main",
                LE3MaterialShaderCompiler.ShaderStage.Pixel, 0x9000, "FLocalVertexFactory", "LocalVertexFactory");
            var vf = new LE3MaterialShaderCompiler.ShouldCacheVertexFactoryContext(true);
            LE3MaterialShaderCompiler.ShaderCompileCandidate[] failures =
            [
                new(known with { ShaderTypeName = "UnknownShader" }, VertexFactoryContext: vf),
                new(known with { VertexFactoryTypeName = "UnknownVertexFactory" }, VertexFactoryContext: vf),
                new(known), // Missing VF registration context.
                new(known, VertexFactoryContext: vf), // Duplicate permutation.
                new(known with { ShaderTypeName = "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy",
                    Stage = (LE3MaterialShaderCompiler.ShaderStage)99 }, VertexFactoryContext: vf),
                new(known with { ShaderTypeName = "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy",
                    Stage = LE3MaterialShaderCompiler.ShaderStage.Vertex, SourceFileName = "BasePassVertexShader" },
                    new System.Collections.Generic.Dictionary<string, string> { ["SM5_PROFILE"] = "0" }, vf)
            ];
            foreach (var failure in failures)
            {
                var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(),
                    [new(known, VertexFactoryContext: vf), failure]);
                Assert.IsFalse(plan.Success);
                Assert.AreEqual(0, plan.Jobs.Count);
                Assert.AreEqual(1, plan.Errors.Count, string.Join(Environment.NewLine, plan.Errors));
                Assert.AreEqual(2, plan.Decisions.Count);
                Assert.AreEqual(LE3MaterialShaderCompiler.ShaderCompileDisposition.Eligible, plan.Decisions[0].Disposition);
                Assert.AreEqual(LE3MaterialShaderCompiler.ShaderCompileDisposition.Unresolved, plan.Decisions[1].Disposition);
                Assert.IsFalse(string.IsNullOrWhiteSpace(plan.Decisions[1].Reason));
            }
            var unused = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(),
                [new(known, VertexFactoryContext: vf with { UsedWithMaterials = false })]);
            Assert.IsTrue(unused.Success);
            Assert.AreEqual(0, unused.Jobs.Count);
            Assert.AreEqual(1, unused.ExcludedTargets.Count);
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderCompileGate.VertexFactoryUsage, unused.Decisions.Single().Gate);
        }

        [TestMethod]
        public void PermutationComparisonReportsExactMissingUnexpectedAndNonMeshIdentities()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3PermutationInventory.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            material.WriteProperty(new EnumProperty("MLM_Unlit", "EMaterialLightingModel", MEGame.LE3, "LightingModel"));
            var compiler = new LE3MaterialShaderCompiler(material);
            var plan = compiler.BuildKnownSubsetCompilePlan(compiler.TranslateMaterialHlsl(MinimalTemplate()), MaterialResource.Create());
            Assert.IsTrue(plan.Success, string.Join(Environment.NewLine, plan.Errors));
            var expected = plan.Jobs.Select(j => new ShaderPermutationIdentity(j.Target.ShaderTypeName, j.Target.VertexFactoryTypeName)).ToArray();
            var exact = LE3MaterialShaderCompiler.ComparePlannedPermutations(plan, expected.Reverse().ToArray());
            Assert.IsTrue(exact.PlanSucceeded);
            Assert.IsTrue(exact.MatchesExpectedPermutations);
            Assert.IsFalse(exact.IsCompleteShaderSet);
            Assert.AreEqual(plan.Jobs.Count, exact.ExpectedPermutationCount);
            Assert.AreEqual(plan.Jobs.Count, exact.PlannedPermutationCount);

            var replaced = expected.ToArray();
            var omitted = replaced[0];
            var nonMesh = new ShaderPermutationIdentity(omitted.ShaderTypeName);
            replaced[0] = nonMesh;
            var difference = LE3MaterialShaderCompiler.ComparePlannedPermutations(plan, replaced);
            Assert.IsFalse(difference.MatchesExpectedPermutations);
            Assert.AreEqual(0, difference.Errors.Count);
            CollectionAssert.AreEqual(new[] { nonMesh }, difference.MissingPermutations.ToArray());
            CollectionAssert.AreEqual(new[] { omitted }, difference.UnexpectedPermutations.ToArray());
            replaced[0] = new ShaderPermutationIdentity("UnrecoveredShader", "UnrecoveredFactory");
            var unknown = LE3MaterialShaderCompiler.ComparePlannedPermutations(plan, replaced);
            Assert.AreEqual(replaced[0], unknown.MissingPermutations.Single());
            Assert.AreEqual(omitted, unknown.UnexpectedPermutations.Single());
            // Results snapshot the inventory and preserve unknown exact names.
            replaced[0] = omitted;
            Assert.AreEqual("UnrecoveredShader", unknown.MissingPermutations.Single().ShaderTypeName);
            replaced[0] = omitted with { ShaderTypeName = omitted.ShaderTypeName.ToLowerInvariant() };
            Assert.IsFalse(LE3MaterialShaderCompiler.ComparePlannedPermutations(plan, replaced).MatchesExpectedPermutations);
        }

        [TestMethod]
        [DataRow("duplicate")]
        [DataRow("null")]
        [DataRow("shader")]
        [DataRow("factory")]
        public void PermutationComparisonRejectsMalformedOrDuplicateExpectedInventories(string error)
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MalformedInventory.pcc", MEGame.LE3);
            var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
            var plan = compiler.BuildKnownSubsetCompilePlan(compiler.TranslateMaterialHlsl(MinimalTemplate()), MaterialResource.Create());
            Assert.IsTrue(plan.Success, string.Join(Environment.NewLine, plan.Errors));
            var expected = plan.Jobs.Select(j => new ShaderPermutationIdentity(j.Target.ShaderTypeName, j.Target.VertexFactoryTypeName)).ToList();
            expected.Add(error switch
            {
                "duplicate" => expected[0],
                "null" => null,
                "shader" => new(" ", "FLocalVertexFactory"),
                _ => new("UnknownShader", "")
            });
            var comparison = LE3MaterialShaderCompiler.ComparePlannedPermutations(plan, expected);
            Assert.IsFalse(comparison.MatchesExpectedPermutations);
            Assert.AreEqual(1, comparison.Errors.Count);
            Assert.AreEqual(plan.Jobs.Count, comparison.ExpectedPermutationCount);
            Assert.AreEqual(0, comparison.MissingPermutations.Count);
            Assert.AreEqual(0, comparison.UnexpectedPermutations.Count);
        }

        [TestMethod]
        public void PermutationComparisonCannotUseEligibleDecisionsFromAFailedPlan()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FailedPlanInventory.pcc", MEGame.LE3);
            var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(
                "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight", "FLocalVertexFactory", out var environment));
            var known = environment.CreateCandidate();
            var unknown = known with { Target = known.Target with { ShaderTypeName = "UnrecoveredShader" } };
            var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(MinimalTemplate()),
                MaterialResource.Create(), [known, unknown]);
            Assert.IsFalse(plan.Success);
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderCompileDisposition.Eligible, plan.Decisions[0].Disposition);
            var identity = new ShaderPermutationIdentity(known.Target.ShaderTypeName, known.Target.VertexFactoryTypeName);
            var comparison = LE3MaterialShaderCompiler.ComparePlannedPermutations(plan, [identity]);
            Assert.IsFalse(comparison.PlanSucceeded);
            Assert.IsFalse(comparison.MatchesExpectedPermutations);
            Assert.AreEqual(1, comparison.Errors.Count);
            Assert.AreEqual(0, comparison.PlannedPermutationCount);
            Assert.AreEqual(identity, comparison.MissingPermutations.Single());
            Assert.IsFalse(LE3MaterialShaderCompiler.ComparePlannedPermutations(plan, []).MatchesExpectedPermutations);
        }

        [TestMethod]
        public void PermutationComparisonHandlesEmptyExcludedPlanWithoutClaimingCompleteness()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ExcludedInventory.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            material.WriteProperty(new EnumProperty("MLM_Unlit", "EMaterialLightingModel", MEGame.LE3, "LightingModel"));
            var compiler = new LE3MaterialShaderCompiler(material);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(
                "TBasePassPixelShaderFNoLightMapPolicySkyLight", "FLocalVertexFactory", out var environment));
            var candidate = environment.CreateCandidate();
            var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(MinimalTemplate()), MaterialResource.Create(), [candidate]);
            Assert.IsTrue(plan.Success);
            Assert.AreEqual(1, plan.ExcludedTargets.Count);
            var empty = LE3MaterialShaderCompiler.ComparePlannedPermutations(plan, []);
            Assert.IsFalse(new LE3MaterialShaderCompiler.ShaderPermutationComparison().MatchesExpectedPermutations);
            Assert.IsTrue(empty.PlanSucceeded);
            Assert.IsTrue(empty.MatchesExpectedPermutations);
            Assert.IsFalse(empty.IsCompleteShaderSet);
            var excluded = new ShaderPermutationIdentity(candidate.Target.ShaderTypeName, candidate.Target.VertexFactoryTypeName);
            var missing = LE3MaterialShaderCompiler.ComparePlannedPermutations(plan, [excluded]);
            Assert.IsFalse(missing.MatchesExpectedPermutations);
            Assert.AreEqual(excluded, missing.MissingPermutations.Single());
            Assert.ThrowsExactly<ArgumentNullException>(() => LE3MaterialShaderCompiler.ComparePlannedPermutations(null, []));
            Assert.ThrowsExactly<ArgumentNullException>(() => LE3MaterialShaderCompiler.ComparePlannedPermutations(plan, null));
        }

        [TestMethod]
        [DataRow("FHitMaskVertexShader")]
        [DataRow("FHitMaskPixelShader")]
        public void HitMaskSelectionOnlyUsesSpecialEngineMaterial(string shader)
        {
            var ordinary = new LE3MaterialShaderCompiler.ShouldCacheMaterialContext(false, false, false, false, false, false, false, false);
            foreach (EBlendMode blend in Enum.GetValues<EBlendMode>())
            foreach (bool special in new[] { false, true })
            foreach (bool masked in new[] { false, true })
            foreach (bool unlit in new[] { false, true })
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(shader,
                    ordinary with { BlendMode = blend, IsSpecialEngineMaterial = special, IsMasked = masked, IsUnlit = unlit,
                        UsedWithDecals = true, HasWorldPositionOffset = true }, out bool caches));
                Assert.AreEqual(special, caches);
            }
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(shader, "FLocalVertexFactory", out var environment));
            Assert.AreEqual("Main", environment.Target.EntryPoint);
            Assert.AreEqual("1", environment.CreateCandidate().EnvironmentMacros["SIMPLE_VERTEX_LIGHTMAP"]);
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.ShaderClientReconstruction, environment.CompileFlagsEvidence);
            Assert.AreEqual(0x9000u, environment.Target.CompileFlags);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public void AoMeshCacheUsesSpecialMaterialAndIndependentPreviousPositionCapability(int permutation)
        {
            string name = $"TAOMeshVertexShader<{permutation}>";
            var ordinary = new LE3MaterialShaderCompiler.ShouldCacheMaterialContext(false, false, false, false, false, false, false, false);
            foreach (bool special in new[] { false, true })
            foreach (bool precise in new[] { false, true })
            {
                var vf = new LE3MaterialShaderCompiler.ShouldCacheVertexFactoryContext(false, SupportsDynamicLighting: false,
                    SupportsPrecisePreviousPosition: precise);
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(name,
                    ordinary with { IsSpecialEngineMaterial = special, UsedWithDecals = true, IsUnlit = true }, "FLocalVertexFactory", vf, out bool caches));
                Assert.AreEqual(special && (permutation == 0 || precise), caches);
            }
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(name, "FLocalVertexFactory", out var local));
            Assert.AreEqual(permutation == 0 ? "MeshAOMaskVertexMain" : "MeshHistoryUpdateVertexMain", local.Target.EntryPoint);
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.ShaderClientReconstruction, local.CompileFlagsEvidence);
            Assert.AreEqual("1", local.CreateCandidate().EnvironmentMacros["SIMPLE_VERTEX_LIGHTMAP"]);
            Assert.AreEqual(0x9000u, local.Target.CompileFlags);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownVertexFactoryRegistration("FLocalVertexFactory", out var factory));
            Assert.AreEqual(true, factory.SupportsPrecisePreviousPosition);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownVertexFactoryRegistration("FGPUSkinVertexFactory", out factory));
            Assert.AreEqual(false, factory.SupportsPrecisePreviousPosition);
        }

        [TestMethod]
        [DataRow("FFogVolumeApplyVertexShader")]
        [DataRow("FFogVolumeApplyPixelShader")]
        public void FogApplySelectionOnlyUsesFogVolumeUsage(string name)
        {
            var ordinary = new LE3MaterialShaderCompiler.ShouldCacheMaterialContext(false, false, false, false, false, false, false, false);
            foreach (bool fog in new[] { false, true })
            foreach (bool special in new[] { false, true })
            foreach (EBlendMode blend in Enum.GetValues<EBlendMode>())
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(name,
                    ordinary with { UsedWithFogVolumes = fog, IsSpecialEngineMaterial = special, BlendMode = blend,
                        IsUnlit = true, UsedWithDecals = true, HasWorldPositionOffset = true }, out bool caches));
                Assert.AreEqual(fog, caches);
            }
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(name, "FLocalVertexFactory", out var environment));
            Assert.AreEqual("Main", environment.Target.EntryPoint);
            Assert.AreEqual(name[1..], environment.Target.SourceFileName);
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.ShaderClientReconstruction, environment.CompileFlagsEvidence);
            Assert.IsFalse(environment.CreateCandidate().EnvironmentMacros.ContainsKey("SIMPLE_VERTEX_LIGHTMAP"));
        }

        [TestMethod]
        [DataRow("FConeDensityPolicy", "ConeDensityMain", true)]
        [DataRow("FConeDensityPolicy", "ConeDensityMain", false)]
        [DataRow("FConstantDensityPolicy", "ConstantDensityMain", true)]
        [DataRow("FConstantDensityPolicy", "ConstantDensityMain", false)]
        [DataRow("FLinearHalfspaceDensityPolicy", "LinearHalfspaceDensityMain", true)]
        [DataRow("FLinearHalfspaceDensityPolicy", "LinearHalfspaceDensityMain", false)]
        [DataRow("FSphereDensityPolicy", "SphericalDensityMain", true)]
        [DataRow("FSphereDensityPolicy", "SphericalDensityMain", false)]
        public void FogIntegralSelectionPreservesDisabledConeAndStoredUsage(string policy, string pixelEntry, bool vertex)
        {
            string name = $"TFogIntegral{(vertex ? "Vertex" : "Pixel")}Shader<{policy}>";
            var ordinary = new LE3MaterialShaderCompiler.ShouldCacheMaterialContext(false, false, false, false, false, false, false, false);
            foreach (bool fog in new[] { false, true })
            foreach (bool special in new[] { false, true })
            foreach (string vf in new[] { "FLocalVertexFactory", "FGPUSkinVertexFactory", null })
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(name,
                    ordinary with { UsedWithFogVolumes = fog, IsSpecialEngineMaterial = special, UsedWithDecals = true,
                        IsUnlit = true, HasWorldPositionOffset = true }, vf, out bool caches));
                Assert.AreEqual(fog && policy != "FConeDensityPolicy", caches);
            }
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(name, "FLocalVertexFactory", out var environment));
            Assert.AreEqual(vertex ? "Main" : pixelEntry, environment.Target.EntryPoint);
            Assert.AreEqual(vertex ? "FogIntegralVertexShader" : "FogIntegralPixelShader", environment.Target.SourceFileName);
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.ShaderClientReconstruction, environment.CompileFlagsEvidence);
            Assert.IsFalse(environment.CreateCandidate().EnvironmentMacros.ContainsKey("SIMPLE_VERTEX_LIGHTMAP"));
        }

        [TestMethod]
        public void AoHistoryUnknownPreviousPositionCapabilityWithholdsPartialPlan()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3UnknownAoCapability.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            material.WriteProperty(new BoolProperty(true, "bUsedAsSpecialEngineMaterial"));
            var compiler = new LE3MaterialShaderCompiler(material);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment("FHitMaskPixelShader", "FLocalVertexFactory", out var hit));
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment("TAOMeshVertexShader<1>", "FLocalVertexFactory", out var ao));
            var unknown = ao.CreateCandidate() with { VertexFactoryContext = new(true, SupportsDynamicLighting: true) };
            var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(MinimalTemplate()), MaterialResource.Create(), [hit.CreateCandidate(), unknown]);
            Assert.IsFalse(plan.Success); Assert.AreEqual(0, plan.Jobs.Count);
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderCompileDisposition.Unresolved, plan.Decisions[1].Disposition);
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderCompileGate.ShaderTypeShouldCache, plan.Decisions[1].Gate);
        }

        [TestMethod]
        public void AccumulatedDistortionSelectionReadsStoredStateAndOneLayerExclusion()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DistortionCacheTest.pcc", MEGame.LE3);
            var material = package.CreateExport("Distorted", "Material", indexed: false);
            material.WriteProperty(new EnumProperty("BLEND_Translucent", "EBlendMode", MEGame.LE3, "BlendMode"));
            material.WriteProperty(new BoolProperty(true, "bUsesDistortion"));
            var compiler = new LE3MaterialShaderCompiler(material);
            var context = compiler.BuildShouldCacheMaterialContext();
            Assert.IsTrue(context.IsDistorted);
            foreach (string name in new[]
            {
                "TDistortionMeshVertexShader<FDistortMeshAccumulatePolicy>",
                "TDistortionMeshPixelShader<FDistortMeshAccumulatePolicy>"
            })
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownShaderTypeRegistration(name, out var registration));
                Assert.AreEqual(name.Contains("Vertex") ? "DistortAccumulateVertexShader" : "DistortAccumulatePixelShader",
                    registration.SourceFileName);
                foreach (var blend in new[] { EBlendMode.BLEND_Translucent, EBlendMode.BLEND_Additive,
                    EBlendMode.BLEND_Modulate, EBlendMode.BLEND_AlphaComposite })
                {
                    Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(name,
                        context with { BlendMode = blend }, out bool caches));
                    Assert.IsTrue(caches);
                }
                foreach (var blend in new[] { EBlendMode.BLEND_Opaque, EBlendMode.BLEND_Masked, EBlendMode.BLEND_SoftMasked })
                {
                    Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(name,
                        context with { BlendMode = blend }, out bool caches));
                    Assert.IsFalse(caches);
                }
            }
            material.WriteProperty(new BoolProperty(true, "bUseOneLayerDistortion"));
            Assert.IsFalse(compiler.BuildShouldCacheMaterialContext().IsDistorted);
            material.WriteProperty(new BoolProperty(false, "bUseOneLayerDistortion"));
            material.WriteProperty(new BoolProperty(false, "bUsesDistortion"));
            var offset = package.CreateExport("Offset", "MaterialExpressionConstant2Vector", material, indexed: false);
            material.WriteProperty(Input("Distortion", offset.UIndex));
            Assert.IsFalse(compiler.BuildShouldCacheMaterialContext().IsDistorted);
        }

        [TestMethod]
        public void GpuSkinSelectionUsesUsageOrSpecialEngineWithAnUnconditionalDecalVeto()
        {
            var ordinary = new LE3MaterialShaderCompiler.ShouldCacheMaterialContext(
                false, false, false, false, false, false, false, false);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownVertexFactoryRegistration("FGPUSkinVertexFactory", out var factory));
            Assert.AreEqual("GpuSkinVertexFactory", factory.SourceFileName);
            Assert.IsFalse(factory.SupportsStaticLighting);
            Assert.IsTrue(factory.UsedWithMaterials);
            var vf = factory.CreateCacheContext();
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownVertexFactoryShouldCache(factory.Name, ordinary, vf, out bool caches));
            Assert.IsFalse(caches);
            foreach (var accepted in new[]
            {
                ordinary with { UsedWithSkeletalMesh = true },
                ordinary with { UsedWithFracturedMeshes = true },
                ordinary with { IsSpecialEngineMaterial = true }
            })
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownVertexFactoryShouldCache(factory.Name,
                    accepted with { IsUnlit = true }, vf, out caches));
                Assert.IsTrue(caches);
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownVertexFactoryShouldCache(factory.Name,
                    accepted with { UsedWithDecals = true }, vf, out caches));
                Assert.IsFalse(caches);
            }

            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DecalUsageTest.pcc", MEGame.LE3);
            var decal = package.CreateExport("Decal", "DecalMaterial", indexed: false);
            var compiler = new LE3MaterialShaderCompiler(decal);
            var defaults = compiler.BuildShouldCacheMaterialContext();
            Assert.IsTrue(defaults.UsedWithSkeletalMesh);
            Assert.IsTrue(defaults.UsedWithFracturedMeshes);
            Assert.IsTrue(defaults.UsedWithDecals);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownVertexFactoryShouldCache(factory.Name, defaults, vf, out caches));
            Assert.IsFalse(caches);
            // The resource getters read flags directly, so an explicit property can override class defaults.
            decal.WriteProperty(new BoolProperty(false, "bUsedWithDecals"));
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownVertexFactoryShouldCache(factory.Name,
                compiler.BuildShouldCacheMaterialContext(), vf, out caches));
            Assert.IsTrue(caches);
        }

        [TestMethod]
        public void KnownPlannerInventoryReproducesCapturedGlassCandidateSubset()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3GlassSelectionSnapshotTest.pcc", MEGame.LE3);
            var material = package.CreateExport("GlassState", "Material", indexed: false);
            // This property snapshot and expected membership are independently read from the
            // decooked glass material/native capture. The synthetic graph isolates selection.
            material.WriteProperty(new EnumProperty("MLM_Unlit", "EMaterialLightingModel", MEGame.LE3, "LightingModel"));
            material.WriteProperty(new EnumProperty("BLEND_Translucent", "EBlendMode", MEGame.LE3, "BlendMode"));
            foreach (string flag in new[] { "bUsesDistortion", "bUsedWithSkeletalMesh", "bUsedWithStaticLighting",
                "bUseLitTranslucencyPostRenderDepthPass", "bCastLitTranslucencyShadowAsMasked" })
            {
                material.WriteProperty(new BoolProperty(true, flag));
            }
            var compiler = new LE3MaterialShaderCompiler(material);
            var translation = compiler.TranslateMaterialHlsl(MinimalTemplate());
            var shaders = LE3MaterialShaderCompiler.GetKnownShaderTypeRegistrations();
            var factories = LE3MaterialShaderCompiler.GetKnownVertexFactoryRegistrations();
            Assert.AreEqual(162, shaders.Count);
            Assert.AreEqual(11, factories.Count);
            var candidates = factories.SelectMany(factory => shaders.Where(s => s.IsMeshMaterial).Select(shader =>
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(shader.Name, factory.Name, out var environment));
                return environment.CreateCandidate();
            })).Concat(shaders.Where(s => !s.IsMeshMaterial).Select(shader =>
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(shader.Name, null, out var environment));
                return environment.CreateCandidate();
            })).ToArray();
            var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), candidates);
            Assert.IsTrue(plan.Success, string.Join(Environment.NewLine, plan.Errors));
            Assert.AreEqual(1732, plan.Decisions.Count);
            Assert.AreEqual(25, plan.Jobs.Count);
            Assert.AreEqual(1707, plan.ExcludedTargets.Count);
            string[] common =
            [
                "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight",
                "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy",
                "TDepthOnlyVertexShader<0>",
                "FTranslucencyPostRenderDepthPixelShader",
                "TDistortionMeshVertexShader<FDistortMeshAccumulatePolicy>",
                "TDistortionMeshPixelShader<FDistortMeshAccumulatePolicy>",
                "TShadowDepthVertexShader<ShadowDepth_OutputDepth>",
                "TShadowDepthVertexShader<ShadowDepth_PerspectiveCorrect>",
                "TShadowDepthVertexShader<ShadowDepth_OutputDepthToColor>",
                "TShadowDepthPixelShaderTRUEFALSE",
                "TShadowDepthPixelShaderFALSEFALSE"
            ];
            string[] localOnly =
            [
                "TBasePassVertexShaderFNoLightMapPolicyFConstantDensityPolicy",
                "TBasePassVertexShaderFNoLightMapPolicyFLinearHalfspaceDensityPolicy",
                "TBasePassVertexShaderFNoLightMapPolicyFSphereDensityPolicy"
            ];
            CollectionAssert.AreEquivalent(common.Concat(localOnly).ToArray(), plan.Jobs
                .Where(j => j.Target.VertexFactoryTypeName == "FLocalVertexFactory").Select(j => j.Target.ShaderTypeName).ToArray());
            CollectionAssert.AreEquivalent(common, plan.Jobs
                .Where(j => j.Target.VertexFactoryTypeName == "FGPUSkinVertexFactory").Select(j => j.Target.ShaderTypeName).ToArray());
            var expectedInventory = common.Concat(localOnly).Select(name => new ShaderPermutationIdentity(name, "FLocalVertexFactory"))
                .Concat(common.Select(name => new ShaderPermutationIdentity(name, "FGPUSkinVertexFactory"))).ToArray();
            var comparison = LE3MaterialShaderCompiler.ComparePlannedPermutations(plan, expectedInventory);
            Assert.IsTrue(comparison.MatchesExpectedPermutations);
            Assert.AreEqual(25, comparison.ExpectedPermutationCount);
            Assert.IsFalse(comparison.IsCompleteShaderSet);
            Assert.IsTrue(plan.Decisions.Where(d => d.Disposition == LE3MaterialShaderCompiler.ShaderCompileDisposition.Excluded)
                .All(d => d.Gate == LE3MaterialShaderCompiler.ShaderCompileGate.ShaderTypeShouldCache ||
                    d.Gate == LE3MaterialShaderCompiler.ShaderCompileGate.VertexFactoryShouldCache &&
                    d.Target.VertexFactoryTypeName is "FGPUSkinMorphVertexFactory" or "FInstancedStaticMeshVertexFactory" or
                        "FGPUSkinDecalVertexFactory" or "FLocalDecalVertexFactory" or "FGPUSkinMorphDecalVertexFactory" or "FSplineMeshVertexFactory" or
                        "FParticleInstancedMeshVertexFactory" or "FLocalVertexFactoryApex" or "FGPUSkinVertexFactoryApex"));
            Assert.IsTrue(plan.Jobs.Where(j => j.Target.VertexFactoryTypeName == "FGPUSkinVertexFactory")
                .All(j => j.Macros["QUAT_SKINNING"] == "0"));
        }

        [TestMethod]
        public void KnownPermutationEnvironmentsPreserveMacroAndFlagEvidence()
        {
            int shippingFlagTypes = 0;
            foreach (var shader in LE3MaterialShaderCompiler.GetKnownShaderTypeRegistrations().Where(s => s.IsMeshMaterial))
            foreach (var factory in LE3MaterialShaderCompiler.GetKnownVertexFactoryRegistrations())
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(shader.Name, factory.Name, out var environment));
                if (factory.Name == "FLocalVertexFactory" && environment.CompileFlagsEvidence ==
                    LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.ShippingBytecode) shippingFlagTypes++;
                var candidate = environment.CreateCandidate();
                Assert.AreEqual(shader.Name, candidate.Target.ShaderTypeName);
                Assert.AreEqual(factory.SourceFileName, candidate.Target.VertexFactoryShaderFileName);
                Assert.AreEqual(factory.SupportsStaticLighting, candidate.VertexFactoryContext.SupportsStaticLighting);
                Assert.AreEqual(factory.SupportsDynamicLighting, candidate.VertexFactoryContext.SupportsDynamicLighting);
                Assert.AreEqual(factory.SupportsPrecisePreviousPosition, candidate.VertexFactoryContext.SupportsPrecisePreviousPosition);
                Assert.AreEqual("1", candidate.EnvironmentMacros["SM5_PROFILE"]);
                Assert.AreEqual("1", candidate.EnvironmentMacros["SUPPORTS_DEPTH_TEXTURES"]);
                Assert.AreEqual("1", candidate.EnvironmentMacros["NUM_SAMPLES"]);
                bool simpleTexture = shader.Name is "TBasePassPixelShaderFSimpleLightMapTexturePolicyNoSkyLight" or
                    "TLightMapDensityVertexShader<FSimpleLightMapTexturePolicy>" or
                    "TLightMapDensityPixelShader<FSimpleLightMapTexturePolicy>" or
                    "TBasePassPixelShaderFSimpleLightMapTexturePolicySkyLight" or
                    "TBasePassVertexShaderFSimpleLightMapTexturePolicyFNoDensityPolicy" or
                    "TBasePassPixelShaderFSimpleVertexLightMapPolicyNoSkyLight" or
                    "TBasePassPixelShaderFSimpleVertexLightMapPolicySkyLight" or
                    "TBasePassVertexShaderFSimpleVertexLightMapPolicyFNoDensityPolicy" or
                    "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFNoDensityPolicy" or
                    "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFConstantDensityPolicy" or
                    "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFLinearHalfspaceDensityPolicy" or
                    "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFSphereDensityPolicy" or
                    "TBasePassVertexShaderFSimpleLightMapTexturePolicyFLinearHalfspaceDensityPolicy" or
                    "TBasePassVertexShaderFSimpleLightMapTexturePolicyFSphereDensityPolicy" or
                    "TBasePassVertexShaderFSimpleLightMapTexturePolicyFConstantDensityPolicy" or
                    "TBasePassVertexShaderFSimpleVertexLightMapPolicyFLinearHalfspaceDensityPolicy" or
                    "TBasePassVertexShaderFSimpleVertexLightMapPolicyFSphereDensityPolicy" or
                    "TBasePassVertexShaderFSimpleVertexLightMapPolicyFConstantDensityPolicy" or
                    "TBasePassVertexShaderFSimpleLightMapTexturePolicyFConeDensityPolicy" or
                    "TBasePassVertexShaderFSimpleVertexLightMapPolicyFConeDensityPolicy" or
                    "TBasePassPixelShaderFCustomSimpleVertexLightMapPolicySkyLight" or
                    "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFNoDensityPolicy" or
                    "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicyNoSkyLight" or
                    "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicySkyLight";
                Assert.AreEqual(simpleTexture ? "1" : "2", candidate.EnvironmentMacros["NUM_LIGHTMAP_COEFFICIENTS"]);
                string stageMacro = shader.Stage == LE3MaterialShaderCompiler.ShaderStage.Vertex ? "VERTEXSHADER" : "PIXELSHADER";
                Assert.AreEqual("1", candidate.EnvironmentMacros[stageMacro]);
                Assert.IsFalse(candidate.EnvironmentMacros.ContainsKey(stageMacro == "VERTEXSHADER" ? "PIXELSHADER" : "VERTEXSHADER"));
                bool lightPixel = shader.Stage == LE3MaterialShaderCompiler.ShaderStage.Pixel && shader.Name.StartsWith("TLightPixelShader", StringComparison.Ordinal);
                Assert.AreEqual(lightPixel ? 0x9400u : 0x9000u, candidate.Target.CompileFlags);
                if (factory.Name is "FGPUSkinVertexFactory" or "FGPUSkinMorphVertexFactory" or "FGPUSkinDecalVertexFactory" or "FGPUSkinMorphDecalVertexFactory")
                {
                    Assert.AreEqual("0", candidate.EnvironmentMacros["QUAT_SKINNING"]);
                    Assert.AreEqual(LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.LE3RegistrationCallback,
                        environment.MacroDefinitions.Single(m => m.Name == "QUAT_SKINNING").Evidence);
                }
                else Assert.IsFalse(candidate.EnvironmentMacros.ContainsKey("QUAT_SKINNING"));
                Assert.AreEqual(environment.MacroDefinitions.Count,
                    environment.MacroDefinitions.Select(m => m.Name).Distinct(StringComparer.Ordinal).Count());
            }
            Assert.AreEqual(96, shippingFlagTypes);
            Assert.IsFalse(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment("UnknownShader", "FLocalVertexFactory", out var unknown));
            Assert.IsNull(unknown);
            Assert.IsFalse(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(
                "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight", "UnknownVF", out unknown));
            Assert.IsNull(unknown);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(
                "TLightPixelShaderFSpotLightPolicyFNoStaticShadowingPolicy", "FLocalVertexFactory", out var light));
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.ShippingBytecode, light.CompileFlagsEvidence);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(
                "TDistortionMeshPixelShader<FDistortMeshAccumulatePolicy>", "FGPUSkinVertexFactory", out var distortion));
Assert.AreEqual(LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.ShippingBytecode, distortion.CompileFlagsEvidence);
            Assert.AreEqual(6, distortion.MacroDefinitions.Count);
        }

        [TestMethod]
        public void KnownFogAndScreenDoorEnvironmentsUseExactPolicyDefines()
        {
            string[] fogPolicies = ["FNoDensityPolicy", "FConstantDensityPolicy", "FLinearHalfspaceDensityPolicy", "FSphereDensityPolicy"];
            string[] fogDefines = ["FOGVOLUMEDENSITY_NONE", "FOGVOLUMEDENSITY_CONSTANT", "FOGVOLUMEDENSITY_LINEARHALFSPACE", "FOGVOLUMEDENSITY_SPHEREDENSITY"];
            for (int i = 0; i < fogPolicies.Length; i++)
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(
                    "TBasePassVertexShaderFNoLightMapPolicy" + fogPolicies[i], "FLocalVertexFactory", out var environment));
                var fog = environment.MacroDefinitions.Where(m => m.Name.StartsWith("FOGVOLUMEDENSITY_", StringComparison.Ordinal)).Single();
                Assert.AreEqual(fogDefines[i], fog.Name);
                Assert.AreEqual("1", fog.Definition);
                Assert.AreEqual(i == 0 ? LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.ShaderClientReconstruction :
                    LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.LE3RegistrationCallback, fog.Evidence);
            }
            foreach (string perspective in new[] { "TRUE", "FALSE" })
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(
                    "TShadowDepthPixelShader" + perspective + "TRUE", "FGPUSkinVertexFactory", out var environment));
                var macros = environment.CreateCandidate().EnvironmentMacros;
                Assert.AreEqual("1", macros["MATERIAL_USE_SCREEN_DOOR_FADE"]);
                Assert.AreEqual("1", macros["SIMPLE_VERTEX_LIGHTMAP"]);
                Assert.AreEqual(perspective == "TRUE", macros.ContainsKey("PERSPECTIVE_CORRECT_DEPTH"));
            }
        }

        [TestMethod]
        public void KnownEnvironmentsPlanMaskedLitMaterialAndSupplyPolicyDefines()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MaskedEnvironmentTest.pcc", MEGame.LE3);
            var material = package.CreateExport("MaskedLit", "Material", indexed: false);
            material.WriteProperty(new EnumProperty("BLEND_Masked", "EBlendMode", MEGame.LE3, "BlendMode"));
            material.WriteProperty(new BoolProperty(true, "bIsMasked"));
            material.WriteProperty(new BoolProperty(true, "TwoSided"));
            material.WriteProperty(new BoolProperty(true, "bUsedWithStaticLighting"));
            var compiler = new LE3MaterialShaderCompiler(material);
            var candidates = LE3MaterialShaderCompiler.GetKnownVertexFactoryRegistrations().SelectMany(factory =>
                LE3MaterialShaderCompiler.GetKnownShaderTypeRegistrations().Where(s => s.IsMeshMaterial).Select(shader =>
                {
                    Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(shader.Name, factory.Name, out var environment));
                    return environment.CreateCandidate();
                })).ToArray();
            var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(MinimalTemplate()), MaterialResource.Create(), candidates);
            Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors));
            Assert.AreEqual(73, plan.Jobs.Count);
            Assert.AreEqual(1654, plan.ExcludedTargets.Count);
            Assert.IsTrue(plan.Jobs.All(j => j.Target.VertexFactoryTypeName == "FLocalVertexFactory"));
            Assert.IsTrue(plan.Jobs.All(j => j.Macros["MATERIALBLENDING_MASKED"] == "1" &&
                j.Macros["MATERIAL_LIGHTINGMODEL_PHONG"] == "1" && j.Macros["MATERIAL_TWOSIDED"] == "1"));
            Assert.AreEqual("1", plan.Jobs.Single(j => j.Target.ShaderTypeName ==
                "TBasePassPixelShaderFNoLightMapPolicySkyLight").Macros["ENABLE_SKY_LIGHT"]);
            Assert.AreEqual("1", plan.Jobs.Single(j => j.Target.ShaderTypeName ==
                "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy").Macros["FOGVOLUMEDENSITY_NONE"]);
            Assert.AreEqual("1", plan.Jobs.Single(j => j.Target.ShaderTypeName ==
                "TShadowDepthPixelShaderTRUEFALSE").Macros["PERSPECTIVE_CORRECT_DEPTH"]);
            Assert.AreEqual(14, plan.Jobs.Count(j => j.Target.CompileFlags == 0x9400));
        }

        [TestMethod]
        public void RecoveredRegistrationUsesExactSourceAndRejectsConflictingPlanMetadata()
        {
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownShaderTypeRegistration(
                "TDepthOnlyVertexShader<1>", out var registration));
            Assert.AreEqual("PositionOnlyDepthVertexShader", registration.SourceFileName);
            Assert.AreEqual("Main", registration.EntryPoint);
            Assert.IsTrue(registration.IsMeshMaterial);
            var target = registration.CreateTarget(0x9400, "FLocalVertexFactory", "LocalVertexFactory");
            Assert.AreEqual("vs_5_0", target.Profile);
            Assert.AreEqual(0x9400u, target.CompileFlags);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownShaderTypeRegistration(
                "TLightPixelShaderFSpotLightPolicyFNoStaticShadowingPolicy", out registration));
            Assert.AreEqual("SpotLightPixelShader", registration.SourceFileName);
            Assert.IsFalse(LE3MaterialShaderCompiler.TryGetKnownShaderTypeRegistration("Unknown", out registration));
            Assert.IsNull(registration);

            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CompilePlanMetadataTest.pcc", MEGame.LE3);
            var compiler = new LE3MaterialShaderCompiler(package.CreateExport("TestMaterial", "Material", indexed: false));
            var translation = compiler.TranslateMaterialHlsl(MinimalTemplate());
            var vf = new LE3MaterialShaderCompiler.ShouldCacheVertexFactoryContext(true);
            foreach (var invalid in new[]
            {
                target with { SourceFileName = "DepthOnlyVertexShader" },
                target with { EntryPoint = "WrongEntry" },
                target with { Stage = LE3MaterialShaderCompiler.ShaderStage.Pixel },
                target with { VertexFactoryShaderFileName = "WrongFactory" },
                target with { VertexFactoryTypeName = null, VertexFactoryShaderFileName = null }
            })
            {
                var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(),
                    [new(invalid, VertexFactoryContext: invalid.VertexFactoryTypeName == null ? null : vf)]);
                Assert.IsFalse(plan.Success);
                Assert.AreEqual(0, plan.Jobs.Count);
                StringAssert.Contains(plan.Errors.Single(), "registration");
            }
        }

        [TestMethod]
        public void ProvisionalDepthMacrosUseInheritedPropertiesAndRecoveredEligibility()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DepthMacroTest.pcc", MEGame.LE3);
            var parent = package.CreateExport("ParentMaterial", "Material", indexed: false);
            parent.WriteProperty(new EnumProperty("MLM_Unlit", "EMaterialLightingModel", MEGame.LE3, "LightingModel"));
            parent.WriteProperty(new EnumProperty("BLEND_Translucent", "EBlendMode", MEGame.LE3, "BlendMode"));
            parent.WriteProperty(new BoolProperty(true, "bUseLitTranslucencyDepthPass"));
            parent.WriteProperty(new BoolProperty(true, "bUseLitTranslucencyPostRenderDepthPass"));
            parent.WriteProperty(new BoolProperty(true, "bUseSurfaceTranslucencyDepthPass"));
            parent.WriteProperty(new BoolProperty(true, "bCastLitTranslucencyShadowAsMasked"));
            var material = package.CreateExport("ChildMaterial", "Material", indexed: false);
            material.Archetype = parent;
            var compiler = new LE3MaterialShaderCompiler(material);
            var macros = compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), compiler.AnalyzeGraph());
            Assert.AreEqual("1", macros["MATERIALBLENDING_TRANSLUCENT"]);
            Assert.AreEqual("1", macros["MATERIAL_LIGHTINGMODEL_UNLIT"]);
            Assert.IsFalse(macros.ContainsKey("MATERIAL_LIT_TRANSLUCENCY_PREPASS"));
            Assert.AreEqual("1", macros["MATERIAL_LIT_TRANSLUCENCY_DEPTH_POSTPASS"]);
            Assert.AreEqual("1", macros["MATERIAL_TRANSLUCENT_SURFACE_PREPASS"]);
            Assert.AreEqual("1", macros["MATERIAL_CAST_LIT_TRANSLUCENCY_SHADOW_AS_MASKED"]);
            material.WriteProperty(new EnumProperty("BLEND_Opaque", "EBlendMode", MEGame.LE3, "BlendMode"));
            macros = compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), compiler.AnalyzeGraph());
            Assert.IsFalse(macros.ContainsKey("MATERIAL_LIT_TRANSLUCENCY_DEPTH_POSTPASS"));
            Assert.AreEqual("1", macros["MATERIAL_TRANSLUCENT_SURFACE_PREPASS"]);
            Assert.AreEqual("1", macros["MATERIAL_CAST_LIT_TRANSLUCENCY_SHADOW_AS_MASKED"]);
        }

        [TestMethod]
        public void LocalVertexFactoryAcceptsOrdinaryResourcesRegardlessOfLightingUsage()
        {
            var material = new LE3MaterialShaderCompiler.ShouldCacheMaterialContext(
                IsUnlit: false,
                IsMasked: false,
                IsSpecialEngineMaterial: false,
                UsesLitTranslucencyDepthPass: false,
                UsesSurfaceTranslucencyDepthPass: false,
                CastLitTranslucencyShadowAsMasked: false,
                HasWorldPositionOffset: false,
                UsedWithStaticLighting: true);
            var vertexFactory = new LE3MaterialShaderCompiler.ShouldCacheVertexFactoryContext(
                SupportsStaticLighting: true);

            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownVertexFactoryShouldCache(
                "FLocalVertexFactory", material, vertexFactory, out bool shouldCache));
            Assert.IsTrue(shouldCache);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownVertexFactoryShouldCache(
                "FLocalVertexFactory", material with { IsUnlit = true }, vertexFactory, out shouldCache));
            Assert.IsTrue(shouldCache);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownVertexFactoryShouldCache(
                "FLocalVertexFactory", material, vertexFactory with { SupportsStaticLighting = false }, out shouldCache));
            Assert.IsTrue(shouldCache);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownVertexFactoryShouldCache(
                "FLocalVertexFactory", material with { UsedWithStaticLighting = false }, vertexFactory, out shouldCache));
            Assert.IsTrue(shouldCache);
            Assert.IsFalse(LE3MaterialShaderCompiler.TryEvaluateKnownVertexFactoryShouldCache(
                "FUnknownVertexFactory", material, vertexFactory, out shouldCache));
            Assert.IsFalse(shouldCache);
        }

        [TestMethod]
        public void PreparedCompileJobCompilesWithVirtualMaterialAndVertexFactoryIncludes()
        {
            GlobalTest.Init();
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3D3DCompileTest.pcc", MEGame.LE3);
            var material = package.CreateExport("TestMaterial", "Material", indexed: false);
            var compiler = new LE3MaterialShaderCompiler(material);
            var translation = compiler.TranslateMaterialHlsl("/*\n" + MinimalTemplate() +
                "*/\nfloat4 MaterialColor() { return float4(0.25, 0.5, 0.75, 1.0); }\n");
            var target = new LE3MaterialShaderCompiler.ShaderCompileTarget(
                "TestPixelShader", "TestPixelShader", "Main",
                LE3MaterialShaderCompiler.ShaderStage.Pixel,
                LE3MaterialShaderCompiler.CapturedDefaultCompileFlags,
                "FTestVertexFactory", "TestVertexFactory");
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), target,
                new System.Collections.Generic.Dictionary<string, string> { ["PIXELSHADER"] = "1" });

            string shaderDirectory = Path.Combine(Path.GetTempPath(), "LE3MaterialShaderCompilerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(shaderDirectory);
            try
            {
                File.WriteAllText(Path.Combine(shaderDirectory, "TestPixelShader.usf"),
                    "#include \"Material.usf\"\n#include \"VertexFactory.usf\"\n" +
                    "cbuffer TestConstants : register(b3) { float4 TestColor; };\n" +
                    "Texture2D TestTexture : register(t4);\nSamplerState TestTextureSampler : register(s2);\n" +
                    "Texture2D TestTextures[2] : register(t6);\nSamplerState TestTexturesSampler[2] : register(s3);\n" +
                    "float4 Main(float4 position : SV_Position) : SV_Target { " +
                    "return MaterialColor() + VertexFactoryColor() + TestColor + TestTexture.Sample(TestTextureSampler, position.xy) " +
                    "+ TestTextures[0].Sample(TestTexturesSampler[0], position.xy) " +
                    "+ TestTextures[1].Sample(TestTexturesSampler[1], position.xy); }\n");
                File.WriteAllText(Path.Combine(shaderDirectory, "TestVertexFactory.usf"),
                    "float4 VertexFactoryColor() { return float4(0.0, 0.0, 0.0, 0.0); }\n");

                var result = compiler.CompileShader(job, shaderDirectory);

                Assert.IsTrue(result.Success, result.Diagnostics);
                Assert.AreSame(job.CompilationInput, result.CompilationInput);
                Assert.AreSame(job.Target, result.Target);
                CollectionAssert.AreEqual(new byte[] { (byte)'D', (byte)'X', (byte)'B', (byte)'C' }, result.Bytecode[..4]);
                Assert.IsTrue(result.InstructionCount > 0);
                var constant = result.ParameterBindings.Single(binding => binding.Name == "TestColor");
                Assert.AreEqual(LE3MaterialShaderCompiler.ShaderParameterBindingKind.Constant, constant.Kind);
                Assert.AreEqual((ushort)3, constant.BufferIndex);
                Assert.AreEqual((ushort)0, constant.BaseIndex);
                Assert.AreEqual((ushort)16, constant.Size);
                var texture = result.ParameterBindings.Single(binding => binding.Name == "TestTexture");
                Assert.AreEqual(LE3MaterialShaderCompiler.ShaderParameterBindingKind.Resource, texture.Kind);
                Assert.AreEqual((ushort)4, texture.BaseIndex);
                Assert.AreEqual((ushort)1, texture.Size);
                Assert.AreEqual((ushort)2, texture.SamplerIndex);
                var textures = result.ParameterBindings.Single(binding => binding.Name == "TestTextures");
                Assert.AreEqual((ushort)6, textures.BaseIndex);
                Assert.AreEqual((ushort)2, textures.Size);
                Assert.AreEqual((ushort)3, textures.SamplerIndex);

                File.WriteAllText(Path.Combine(shaderDirectory, "TestPixelShader.usf"), "this is not valid HLSL");
                var failed = compiler.CompileShader(job, shaderDirectory);
                Assert.IsFalse(failed.Success);
                Assert.AreSame(job.CompilationInput, failed.CompilationInput);
                Assert.AreSame(job.Target, failed.Target);
                Assert.IsFalse(string.IsNullOrWhiteSpace(failed.Diagnostics));
                Assert.AreEqual(0, failed.InstructionCount);
            }
            finally
            {
                Directory.Delete(shaderDirectory, recursive: true);
            }
        }

        private static string MinimalTemplate() =>
            "%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n";

        [TestMethod]
        [DataRow("Directional", true, false)]
        [DataRow("Directional", false, false)]
        [DataRow("Point", true, false)]
        [DataRow("Point", false, false)]
        [DataRow("Spot", true, false)]
        [DataRow("Spot", false, false)]
        [DataRow("Directional", true, true)]
        [DataRow("Directional", false, true)]
        [DataRow("Point", true, true)]
        [DataRow("Point", false, true)]
        [DataRow("Spot", true, true)]
        [DataRow("Spot", false, true)]
        public void StaticShadowLightPoliciesUseDistinctNativeGatesAndExactDefines(string light, bool vertex, bool sdf)
        {
            string policy = sdf ? "FSignedDistanceFieldShadowTexturePolicy" : "FShadowVertexBufferPolicy";
            string shader = $"TLight{(vertex ? "Vertex" : "Pixel")}ShaderF{light}LightPolicy{policy}";
            var context = new LE3MaterialShaderCompiler.ShouldCacheMaterialContext(false, false, false, false, false, false, false, true);
            var factory = new LE3MaterialShaderCompiler.ShouldCacheVertexFactoryContext(true);
            Check(context, factory, true);
            Check(context with { IsUnlit = true }, factory, false);
            Check(context with { UsedWithStaticLighting = false }, factory, false);
            Check(context with { UsedWithStaticLighting = false, IsSpecialEngineMaterial = true }, factory, true);
            Check(context, factory with { SupportsStaticLighting = false }, false);
            Check(context with { IsTerrainMaterial = true }, factory, sdf);
            Assert.IsFalse(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(shader, context, "FLocalVertexFactory",
                null, out bool unresolved));
            Assert.IsFalse(unresolved);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryGetKnownPermutationEnvironment(shader, "FLocalVertexFactory", out var environment));
            var candidate = environment.CreateCandidate();
            Assert.AreEqual(vertex ? light + "LightVertexShader" : light + "LightPixelShader", candidate.Target.SourceFileName);
            Assert.AreEqual("Main", candidate.Target.EntryPoint);
            Assert.AreEqual(vertex ? 0x9000u : 0x9400u, candidate.Target.CompileFlags);
            Assert.AreEqual(LE3MaterialShaderCompiler.ShaderEnvironmentEvidence.ShippingBytecode, environment.CompileFlagsEvidence);
            Assert.AreEqual("1", candidate.EnvironmentMacros["SIMPLE_VERTEX_LIGHTMAP"]);
            Assert.AreEqual("2", candidate.EnvironmentMacros["NUM_LIGHTMAP_COEFFICIENTS"]);
            Assert.AreEqual(light == "Directional", candidate.EnvironmentMacros.ContainsKey("ENABLE_DISTANCE_SHADOW_FADING"));
            Assert.AreEqual(sdf, candidate.EnvironmentMacros.ContainsKey("STATICLIGHTING_SIGNEDDISTANCEFIELD"));
            Assert.AreEqual(sdf, candidate.EnvironmentMacros.ContainsKey("STATICLIGHTING_TEXTUREMASK"));
            Assert.AreEqual(!sdf, candidate.EnvironmentMacros.ContainsKey("STATICLIGHTING_VERTEXMASK"));

            void Check(LE3MaterialShaderCompiler.ShouldCacheMaterialContext materialContext,
                LE3MaterialShaderCompiler.ShouldCacheVertexFactoryContext vf, bool expected)
            {
                Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownShaderTypeShouldCache(shader, materialContext,
                    "FLocalVertexFactory", vf, out bool actual));
                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void LocalVertexFactoryRejectsExplicitTerrainPredicate()
        {
            var context = new LE3MaterialShaderCompiler.ShouldCacheMaterialContext(false, false, false, false, false, false, false, true,
                IsTerrainMaterial: true);
            Assert.IsTrue(LE3MaterialShaderCompiler.TryEvaluateKnownVertexFactoryShouldCache("FLocalVertexFactory", context,
                new(true), out bool cache));
            Assert.IsFalse(cache);
        }

        private static StructProperty Input(string name, int expression) =>
            new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression, "Expression") }, name);

        private static StructProperty MaskedInput(string name, int expression, bool r = false, bool g = false,
            bool b = false, bool a = false) =>
            new("ExpressionInput", new PropertyCollection
            {
                new ObjectProperty(expression, "Expression"),
                new BoolProperty(true, "Mask"),
                new BoolProperty(r, "MaskR"),
                new BoolProperty(g, "MaskG"),
                new BoolProperty(b, "MaskB"),
                new BoolProperty(a, "MaskA")
            }, name);
    }
}
