using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3MaterialShaderCompoundTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NestedCoordinateTextureAccessOrdersSamplerSlotsSeparatelyFromGlobalTextureIdentity(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NestedTexture.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var outerTexture = package.CreateExport("OuterTexture", "Texture2D", indexed: false);
        var innerTexture = package.CreateExport("InnerTexture", "Texture2D", indexed: false);
        var inner = package.CreateExport("Inner", "MaterialExpressionTextureSample", material, indexed: false);
        inner.WriteProperty(new ObjectProperty(innerTexture.UIndex, "Texture"));
        var outer = package.CreateExport("Outer", "MaterialExpressionTextureSample", material, indexed: false);
        outer.WriteProperty(new ObjectProperty(outerTexture.UIndex, "Texture"));
        var coordinates = Input("Coordinates", inner.UIndex);
        coordinates.Properties.Add(new IntProperty(1, "Mask"));
        coordinates.Properties.Add(new IntProperty(1, "MaskR")); coordinates.Properties.Add(new IntProperty(1, "MaskG"));
        outer.WriteProperty(coordinates);
        material.WriteProperty(Input("Normal", outer.UIndex)); material.WriteProperty(Input("DiffuseColor", outer.UIndex));
        var reads = new System.Collections.Generic.List<int>();
        var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: texture =>
        {
            reads.Add(texture.UIndex);
            return new(LE3MaterialShaderCompiler.TextureDimension.Texture2D, "TC_Default", System.Numerics.Vector4.Zero, System.Numerics.Vector4.One);
        }).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        CollectionAssert.AreEqual(new[] { outerTexture.UIndex, innerTexture.UIndex }, reads.ToArray());
        CollectionAssert.AreEqual(reads.ToArray(), result.UniformExpressionTextures.ToArray());
        CollectionAssert.AreEqual(new[] { 1, 0 }, result.Uniform2DTextureExpressions.Select(t => t.TextureIndex).ToArray());
        foreach (string root in new[] { "Normal", "DiffuseColor" })
        {
            string code = result.PropertyCode[root];
            string sample = half ? "h4tex2D" : "tex2D";
            Assert.IsTrue(code.IndexOf(sample + "(Texture2D_0,", StringComparison.Ordinal) < code.IndexOf(sample + "(Texture2D_1,", StringComparison.Ordinal));
        }
        // Memory-created exports have loaded empty data; the callback supplies all metadata.
    }

    [TestMethod]
    [DataRow("Constant", false)]
    [DataRow("Constant", true)]
    [DataRow("Constant3Vector", false)]
    [DataRow("Constant3Vector", true)]
    [DataRow("ScalarParameter", false)]
    [DataRow("ScalarParameter", true)]
    [DataRow("VectorParameter", false)]
    [DataRow("VectorParameter", true)]
    [DataRow("ObjectRadius", false)]
    [DataRow("ObjectRadius", true)]
    [DataRow("ObjectOrientation", false)]
    [DataRow("ObjectOrientation", true)]
    public void ConstantClampUsesNativeDefaultsCoercionAndUniformTreesInBothStages(string inputKind, bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ConstantClamp.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var input = package.CreateExport("Input", "MaterialExpression" + inputKind, material, indexed: false);
        input.WriteProperty(new NameProperty("Input", "ParameterName"));
        var clamp = package.CreateExport("Clamp", "MaterialExpressionConstantClamp", material, indexed: false);
        clamp.WriteProperty(Input("Input", input.UIndex));
        material.WriteProperty(Input("DiffuseColor", clamp.UIndex));
        material.WriteProperty(Input("WorldPositionOffset", clamp.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string type = half ? "half" : "float";
        string code = result.PropertyCode["DiffuseColor"];
        bool parameter = inputKind.EndsWith("Parameter", StringComparison.Ordinal);
        bool nonuniform = inputKind.StartsWith("Object", StringComparison.Ordinal);
        bool vector = inputKind is "Constant3Vector" or "VectorParameter" or "ObjectOrientation";
        if (!parameter)
        {
            StringAssert.Contains(code, "min(max(");
            string boundType = type + (vector ? "3" : "");
            string zeros = vector ? string.Join(',', Enumerable.Repeat(type + "(0.00000000)", 3)) : "0.00000000";
            string ones = vector ? string.Join(',', Enumerable.Repeat(type + "(1.00000000)", 3)) : "1.00000000";
            StringAssert.Contains(code, boundType + "(" + zeros + ")");
            StringAssert.Contains(code, boundType + "(" + ones + ")");
        }
        if (parameter)
        {
            var pixel = vector ? result.UniformPixelVectorExpressions : result.UniformPixelScalarExpressions;
            var vertex = vector ? result.UniformVertexVectorExpressions : result.UniformVertexScalarExpressions;
            Assert.AreEqual(2, pixel.Count);
            Assert.AreEqual(2, vertex.Count);
            var tree = (MaterialUniformExpressionClamp)pixel[1];
            Assert.AreSame(pixel[0], tree.Input);
            Assert.AreSame(tree, vertex[1]);
            Assert.AreEqual(0f, ((MaterialUniformExpressionConstant)tree.Min).Value.R);
            Assert.AreEqual(1f, ((MaterialUniformExpressionConstant)tree.Max).Value.R);
            Assert.AreEqual(0f, ((MaterialUniformExpressionConstant)tree.Max).Value.G, "CPU bound children are not replicated vector constants.");
            using var stream = new System.IO.MemoryStream();
            tree.Serialize(new SerializingContainer(stream, package));
            stream.Position = 0;
            var reader = new SerializingContainer(stream, package, isLoading: true);
            var restored = (MaterialUniformExpressionClamp)MaterialUniformExpression.Create(reader);
            restored.Serialize(reader);
            Assert.AreEqual(stream.Length, stream.Position);
        }
        else Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
        Assert.AreEqual(nonuniform, code.Contains("Local0"));
        Assert.AreEqual(0u, result.UsingTransforms);
    }

    [TestMethod]
    [DataRow(-2f, 3f)]
    [DataRow(3f, -2f)]
    public void ConstantClampPreservesCustomAndReversedBounds(float min, float max)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ConstantClampBounds.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var input = package.CreateExport("Input", "MaterialExpressionScalarParameter", material, indexed: false);
        input.WriteProperty(new NameProperty("Input", "ParameterName"));
        var clamp = package.CreateExport("Clamp", "MaterialExpressionConstantClamp", material, indexed: false);
        clamp.WriteProperty(Input("Input", input.UIndex));
        clamp.WriteProperty(new FloatProperty(min, "Min")); clamp.WriteProperty(new FloatProperty(max, "Max"));
        material.WriteProperty(Input("Opacity", clamp.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        var tree = (MaterialUniformExpressionClamp)result.UniformPixelScalarExpressions[1];
        Assert.AreEqual(min, ((MaterialUniformExpressionConstant)tree.Min).Value.R);
        Assert.AreEqual(max, ((MaterialUniformExpressionConstant)tree.Max).Value.R);
        LinearColor value = default;
        tree.GetNumberValue(new UniformExpressionRenderContext(new() { ["Input"] = 10f }, new(), 0, 0, null), ref value);
        Assert.AreEqual(MathF.Min(MathF.Max(10, min), max), value.R);
    }

    [TestMethod]
    public void ConstantClampMissingInputFailsWithoutInventedDefaults()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ConstantClampMissing.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var clamp = package.CreateExport("Clamp", "MaterialExpressionConstantClamp", material, indexed: false);
        material.WriteProperty(Input("Opacity", clamp.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Errors.Single(), ".Input is not connected.");
    }

    [TestMethod]
    [DataRow("Game", false)]
    [DataRow("Game", true)]
    [DataRow("Real", false)]
    [DataRow("Real", true)]
    [DataRow("Nonuniform", false)]
    [DataRow("Nonuniform", true)]
    [DataRow("Constant", false)]
    [DataRow("Constant", true)]
    public void PannerPreservesTimeSpeedOrderAndUniformOnlyPeriodicHints(string clock, bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3Panner.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var panner = package.CreateExport("Panner", "MaterialExpressionPanner", material, indexed: false);
        panner.WriteProperty(new FloatProperty(2, "SpeedX")); panner.WriteProperty(new FloatProperty(3, "SpeedY"));
        if (clock != "Game")
        {
            var time = package.CreateExport("Time", clock switch { "Real" => "MaterialExpressionTime", "Nonuniform" => "MaterialExpressionObjectRadius", _ => "MaterialExpressionConstant" }, material, indexed: false);
            if (clock == "Real") time.WriteProperty(new BoolProperty(true, "bIgnorePause"));
            if (clock == "Constant") time.WriteProperty(new FloatProperty(2.25f, "R"));
            panner.WriteProperty(Input("Time", time.UIndex));
        }
        material.WriteProperty(Input("Distortion", panner.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string type = half ? "half" : "float";
        string code = result.PropertyCode["Distortion"];
        Assert.IsFalse(code.Contains("frac("));
        Assert.AreEqual(1u, result.NumUserTexCoords);
        Assert.AreEqual(0u, result.UsingTransforms);
        Assert.AreEqual(clock is "Game" or "Real", result.UsesTime);
        if (clock is "Game" or "Real")
        {
            Assert.AreEqual($"\t{type}2 Local0 = Parameters.TexCoords[0].xy;\r\n\t{type}2 Local1 = (UniformPixelVector_0.rg + Local0);\r\n\treturn Local1", code);
            Assert.AreEqual(5, result.UniformPixelScalarExpressions.Count);
            Assert.AreEqual(clock == "Real" ? typeof(MaterialUniformExpressionRealTime) : typeof(MaterialUniformExpressionTime), result.UniformPixelScalarExpressions[0].GetType());
            var appended = (MaterialUniformExpressionAppendVector)result.UniformPixelVectorExpressions.Single();
            Assert.AreSame(result.UniformPixelScalarExpressions[3], appended.B);
            Assert.AreSame(result.UniformPixelScalarExpressions[4], appended.A);
            var x = (MaterialUniformExpressionPeriodic)appended.A;
            var y = (MaterialUniformExpressionPeriodic)appended.B;
            Assert.AreSame(result.UniformPixelScalarExpressions[1], x.X);
            Assert.AreSame(result.UniformPixelScalarExpressions[2], y.X);
            Assert.IsFalse(x.IsNotFrameDependent);
            foreach (var (hint, speed) in new[] { (x, 2f), (y, 3f) })
            {
                var product = (MaterialUniformExpressionFoldedMath)hint.X;
                Assert.AreSame(result.UniformPixelScalarExpressions[0], product.A);
                Assert.AreEqual(speed, ((MaterialUniformExpressionConstant)product.B).Value.R);
                Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Mul, product.Op);
            }
            using var stream = new System.IO.MemoryStream();
            appended.Serialize(new SerializingContainer(stream, package));
            stream.Position = 0;
            var reader = new SerializingContainer(stream, package, isLoading: true);
            var restored = (MaterialUniformExpressionAppendVector)MaterialUniformExpression.Create(reader);
            restored.Serialize(reader);
            Assert.AreEqual(stream.Length, stream.Position);
            var context = new UniformExpressionRenderContext(new(), new(), 1.25f, 2.25f, null);
            LinearColor before = default, after = default;
            appended.GetNumberValue(context, ref before); restored.GetNumberValue(context, ref after);
            Assert.AreEqual(0.5f, before.R); Assert.AreEqual(0.75f, before.G);
            Assert.AreEqual(before, after);
        }
        else if (clock == "Nonuniform")
        {
            Assert.AreEqual($"\t{type} Local0 = (ObjectWorldPositionAndRadius.w * {type}(2.00000000));\r\n" +
                $"\t{type} Local1 = (ObjectWorldPositionAndRadius.w * {type}(3.00000000));\r\n" +
                $"\t{type}2 Local2 = Parameters.TexCoords[0].xy;\r\n" +
                $"\t{type}2 Local3 = ({type}2(Local0,Local1) + Local2);\r\n\treturn Local3", code);
            Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
        }
        else
        {
            Assert.AreEqual($"\t{type}2 Local0 = Parameters.TexCoords[0].xy;\r\n" +
                $"\t{type}2 Local1 = ({type}2(({type}(2.25000000) * {type}(2.00000000)),({type}(2.25000000) * {type}(3.00000000))) + Local0);\r\n\treturn Local1", code);
            Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PannerZeroDefaultsRetainDeduplicatedTimeAndRejectImplicitVertexUvs(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3PannerDefaults.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var panner = package.CreateExport("Panner", "MaterialExpressionPanner", material, indexed: false);
        material.WriteProperty(Input("Distortion", panner.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsTrue(result.UsesTime);
        Assert.AreEqual(3, result.UniformPixelScalarExpressions.Count);
        var appended = (MaterialUniformExpressionAppendVector)result.UniformPixelVectorExpressions.Single();
        Assert.AreSame(appended.A, appended.B);
        Assert.AreEqual(0f, ((MaterialUniformExpressionConstant)((MaterialUniformExpressionFoldedMath)((MaterialUniformExpressionPeriodic)appended.A).X).B).Value.R);
        material.WriteProperty(Input("WorldPositionOffset", panner.UIndex));
        var rejected = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        CollectionAssert.AreEqual(new[] { "Invalid node used in vertex shader input!" }, rejected.Errors.ToArray());
        var coordinate = package.CreateExport("Coordinate", "MaterialExpressionConstant2Vector", material, indexed: false);
        panner.WriteProperty(Input("Coordinate", coordinate.UIndex));
        var both = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(both.Success, string.Join("; ", both.Errors));
        Assert.AreEqual(0u, both.NumUserTexCoords);
        Assert.AreEqual(3, both.UniformVertexScalarExpressions.Count);
        for (int i = 0; i < 3; i++) Assert.AreSame(both.UniformPixelScalarExpressions[i], both.UniformVertexScalarExpressions[i]);
    }

    [TestMethod]
    [DataRow("MaterialExpressionObjectOrientation")]
    [DataRow("MaterialExpressionConstant2Vector")]
    public void PannerDoesNotSilentlyCoerceVectorTime(string timeClass)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3PannerVectorTime.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var panner = package.CreateExport("Panner", "MaterialExpressionPanner", material, indexed: false);
        var time = package.CreateExport("Time", timeClass, material, indexed: false);
        panner.WriteProperty(Input("Time", time.UIndex)); material.WriteProperty(Input("Distortion", panner.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("AppendVector produces 6") || e.Contains("Arithmetic between Vector4 and Vector2")));
    }

    [TestMethod]
    [DataRow(-0.0000000001f, -0.0000000001f)]
    [DataRow(-1.25f, 0.75f)]
    [DataRow(2.25f, 0.25f)]
    [DataRow(2147483648f, 3221225472f)]
    public void PeriodicUniformUsesNativeFloorIncludingCancellationAndOverflow(float input, float expected)
    {
        var constant = new MaterialUniformExpressionConstant { Value = new LinearColor(input, input, input, input) };
        var hint = new MaterialUniformExpressionPeriodic { X = constant };
        LinearColor value = default;
        hint.GetNumberValue(new UniformExpressionRenderContext(new(), new(), 0, 0, null), ref value);
        Assert.AreEqual(new LinearColor(expected, expected, expected, expected), value);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void BumpOffsetPreservesNativeOrderDefaultsAndHeightNarrowing(bool vectorHeight, bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3BumpOffset.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var height = package.CreateExport("Height", vectorHeight ? "MaterialExpressionObjectOrientation" : "MaterialExpressionConstant", material, indexed: false);
        height.WriteProperty(new FloatProperty(1, "R"));
        var bump = package.CreateExport("Bump", "MaterialExpressionBumpOffset", material, indexed: false);
        bump.WriteProperty(Input("Height", height.UIndex));
        material.WriteProperty(Input("Distortion", bump.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        if (vectorHeight)
        {
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Errors.Single().Contains("arithmetic between native numeric types 0x2 and 0x1 is undefined."));
            return;
        }
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string type = half ? "half" : "float";
        string code = $"\t{type}2 Local0 = Parameters.TexCoords[0].xy;\r\n";
        code += $"\t{type}2 Local1 = ((({type}(0.05000000) * {type}(1.00000000)) + {type}(-0.02500000)) * Parameters.TangentCameraVector.rg);\r\n" +
                $"\t{type}2 Local2 = (Local1 + Local0);\r\n\treturn Local2";
        Assert.AreEqual(code, result.PropertyCode["Distortion"]);
        Assert.AreEqual(1u, result.NumUserTexCoords);
        Assert.AreEqual(0u, result.UsingTransforms);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BumpOffsetUsesSuppliedCoordinatesAndFoldedScalarUniform(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3BumpOffsetUniform.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var height = package.CreateExport("Height", "MaterialExpressionScalarParameter", material, indexed: false);
        height.WriteProperty(new NameProperty("Height", "ParameterName"));
        var coordinate = package.CreateExport("Coordinate", "MaterialExpressionConstant2Vector", material, indexed: false);
        coordinate.WriteProperty(new FloatProperty(2, "R")); coordinate.WriteProperty(new FloatProperty(3, "G"));
        var bump = package.CreateExport("Bump", "MaterialExpressionBumpOffset", material, indexed: false);
        bump.WriteProperty(Input("Height", height.UIndex)); bump.WriteProperty(Input("Coordinate", coordinate.UIndex));
        bump.WriteProperty(new FloatProperty(0.25f, "HeightRatio")); bump.WriteProperty(new FloatProperty(2, "ReferencePlane"));
        material.WriteProperty(Input("Distortion", bump.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string type = half ? "half" : "float";
        Assert.AreEqual($"\t{type}2 Local0 = (UniformPixelScalars_0.z * Parameters.TangentCameraVector.rg);\r\n" +
            $"\t{type}2 Local1 = (Local0 + {type}2(2.00000000,3.00000000));\r\n\treturn Local1", result.PropertyCode["Distortion"]);
        Assert.AreEqual(3, result.UniformPixelScalarExpressions.Count);
        var sum = (MaterialUniformExpressionFoldedMath)result.UniformPixelScalarExpressions[2];
        Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Add, sum.Op);
        var product = (MaterialUniformExpressionFoldedMath)sum.A;
        Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Mul, product.Op);
        Assert.AreEqual(0.25f, ((MaterialUniformExpressionConstant)product.A).Value.R);
        Assert.IsInstanceOfType<MaterialUniformExpressionScalarParameter>(product.B);
        Assert.AreEqual(-0.5f, ((MaterialUniformExpressionConstant)sum.B).Value.R);
        Assert.AreEqual(0u, result.NumUserTexCoords);
    }

    [TestMethod]
    public void BumpOffsetMissingHeightAndVertexCameraUseFailClosed()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3BumpOffsetErrors.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var bump = package.CreateExport("Bump", "MaterialExpressionBumpOffset", material, indexed: false);
        material.WriteProperty(Input("WorldPositionOffset", bump.UIndex));
        CollectionAssert.AreEqual(new[] { "Missing Height input" }, new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()).Errors.ToArray());
        var height = package.CreateExport("Height", "MaterialExpressionConstant", material, indexed: false);
        bump.WriteProperty(Input("Height", height.UIndex));
        foreach (bool supplied in new[] { false, true })
        {
            if (supplied) bump.WriteProperty(Input("Coordinate", height.UIndex));
            var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
            Assert.IsFalse(result.Success);
            CollectionAssert.AreEqual(new[] { "Invalid node used in vertex shader input!" }, result.Errors.ToArray());
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BumpOffsetMaskedVectorHeightIsBroadcastScalarAndCoordinatesAreVisitedFirst(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3BumpOffsetMasked.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var height = package.CreateExport("Height", "MaterialExpressionVectorParameter", material, indexed: false);
        height.WriteProperty(new NameProperty("Height", "ParameterName"));
        var coordinate = package.CreateExport("Coordinate", "MaterialExpressionVectorParameter", material, indexed: false);
        coordinate.WriteProperty(new NameProperty("Coordinate", "ParameterName"));
        var bump = package.CreateExport("Bump", "MaterialExpressionBumpOffset", material, indexed: false);
        var heightInput = Input("Height", height.UIndex);
        heightInput.Properties.Add(new IntProperty(1, "Mask"));
        heightInput.Properties.Add(new IntProperty(1, "MaskA"));
        bump.WriteProperty(heightInput);
        var coordinateInput = Input("Coordinate", coordinate.UIndex);
        coordinateInput.Properties.Add(new IntProperty(1, "Mask"));
        coordinateInput.Properties.Add(new IntProperty(1, "MaskR"));
        coordinateInput.Properties.Add(new IntProperty(1, "MaskG"));
        bump.WriteProperty(coordinateInput);
        material.WriteProperty(Input("Distortion", bump.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string type = half ? "half" : "float";
        Assert.AreEqual($"\t{type} Local0 = ({type}(0.05000000) * UniformPixelVector_1.a);\r\n" +
            $"\t{type} Local1 = (Local0 + {type}(-0.02500000));\r\n" +
            $"\t{type}2 Local2 = (Local1 * Parameters.TangentCameraVector.rg);\r\n" +
            $"\t{type}2 Local3 = (Local2 + UniformPixelVector_0.rg);\r\n\treturn Local3", result.PropertyCode["Distortion"]);
        CollectionAssert.AreEqual(new[] { "Coordinate", "Height" }, result.UniformPixelVectorExpressions
            .Cast<MaterialUniformExpressionVectorParameter>().Select(p => p.ParameterName.Name).ToArray());
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
        Assert.AreEqual(0u, result.NumUserTexCoords);
    }

    [TestMethod]
    public void BumpOffsetDoesNotInventHeightRatioInputAndRejectsCycles()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3BumpOffsetCycle.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var height = package.CreateExport("Height", "MaterialExpressionConstant", material, indexed: false);
        var bump = package.CreateExport("Bump", "MaterialExpressionBumpOffset", material, indexed: false);
        bump.WriteProperty(Input("Height", height.UIndex));
        material.WriteProperty(Input("Distortion", bump.UIndex));
        // This field does not exist in LE3: it must not replace the scalar HeightRatio property.
        bump.WriteProperty(Input("HeightRatioInput", height.UIndex));
        var translated = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(translated.Success);
        StringAssert.Contains(translated.PropertyCode["Distortion"], "float(0.05000000)");
        bump.WriteProperty(Input("Height", bump.UIndex));
        var cycle = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsFalse(cycle.Success);
        Assert.IsTrue(cycle.Errors.Any(e => e.Contains("cycle", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void FresnelUsesNativeNormalDefaultExponentAndFourOrderedLocals(bool suppliedNormal, bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3Fresnel.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("Fresnel", "MaterialExpressionFresnel", material, indexed: false);
        if (suppliedNormal)
        {
            var normal = package.CreateExport("Normal", "MaterialExpressionObjectOrientation", material, indexed: false);
            node.WriteProperty(Input("Normal", normal.UIndex));
            node.WriteProperty(new FloatProperty(5, "Exponent"));
        }
        material.WriteProperty(Input("EmissiveColor", node.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string type = half ? "half" : "float";
        string normalCode = suppliedNormal ? "ObjectOrientation" : type + "3(0.00000000,0.00000000,1.00000000)";
        Assert.AreEqual($"\t{type} Local0 = dot({normalCode},Parameters.TangentCameraVector);\r\n" +
            $"\t{type} Local1 = max({type}(0.00000000),Local0);\r\n" +
            $"\t{type} Local2 = ({type}(1.00000000) - Local1);\r\n" +
            $"\t{type} Local3 = pow(Local2,{type}({(suppliedNormal ? "5" : "3")}.00000000));\r\n\treturn Local3",
            result.PropertyCode["EmissiveColor"]);
        Assert.AreEqual(0u, result.UsingTransforms);
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count + result.UniformPixelScalarExpressions.Count);
        material.WriteProperty(Input("WorldPositionOffset", node.UIndex));
        var rejected = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsFalse(rejected.Success);
        CollectionAssert.AreEqual(new[] { "Invalid node used in vertex shader input!" }, rejected.Errors.ToArray());
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    public void DesaturationUsesNativeWeightsOptionalPercentAndBothFrequencies(bool nonuniform, bool percent, bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3Desaturation.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var color = package.CreateExport("Color", nonuniform ? "MaterialExpressionObjectOrientation" : "MaterialExpressionConstant3Vector", material, indexed: false);
        if (!nonuniform) foreach (string channel in new[] { "R", "G", "B" }) color.WriteProperty(new FloatProperty(1, channel));
        var node = package.CreateExport("Desaturation", "MaterialExpressionDesaturation", material, indexed: false);
        node.WriteProperty(Input("Input", color.UIndex));
        if (percent)
        {
            var parameter = package.CreateExport("Percent", "MaterialExpressionScalarParameter", material, indexed: false);
            parameter.WriteProperty(new NameProperty("Percent", "ParameterName"));
            parameter.WriteProperty(new FloatProperty(0.5f, "DefaultValue"));
            node.WriteProperty(Input("Percent", parameter.UIndex));
        }
        material.WriteProperty(Input("EmissiveColor", node.UIndex));
        material.WriteProperty(Input("WorldPositionOffset", node.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string type = half ? "half" : "float";
        string colorCode = nonuniform ? "ObjectOrientation" : type + "3(1.00000000,1.00000000,1.00000000)";
        string dot = $"dot({colorCode},{type}3(0.30000001,0.58999997,0.11000000))";
        if (!percent && !nonuniform)
        {
            Assert.AreEqual("\treturn " + dot, result.PropertyCode["EmissiveColor"]);
            Assert.AreEqual(result.PropertyCode["EmissiveColor"], result.PropertyCode["WorldPositionOffset"]);
        }
        else
        {
            StringAssert.Contains(result.PropertyCode["EmissiveColor"], dot);
            if (percent)
            {
                StringAssert.Contains(result.PropertyCode["EmissiveColor"], "lerp(");
                StringAssert.Contains(result.PropertyCode["EmissiveColor"], "UniformPixelScalars_0.x");
                StringAssert.Contains(result.PropertyCode["WorldPositionOffset"], "UniformVertexScalars_0.x");
                Assert.AreEqual(1, result.UniformPixelScalarExpressions.Count);
                Assert.AreSame(result.UniformPixelScalarExpressions[0], result.UniformVertexScalarExpressions[0]);
            }
        }
        Assert.AreEqual(0u, result.UsingTransforms);
    }

    [TestMethod]
    public void DesaturationCustomWeightsIgnoreAlphaAndMissingInputHasExactDiagnostic()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DesaturationWeights.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("Desaturation", "MaterialExpressionDesaturation", material, indexed: false);
        material.WriteProperty(Input("Opacity", node.UIndex));
        var missing = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        CollectionAssert.AreEqual(new[] { "Missing Desaturation input" }, missing.Errors.ToArray());
        var color = package.CreateExport("Color", "MaterialExpressionObjectOrientation", material, indexed: false);
        node.WriteProperty(Input("Input", color.UIndex));
        node.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
        {
            new FloatProperty(1, "R"), new FloatProperty(2, "G"), new FloatProperty(3, "B"), new FloatProperty(999, "A")
        }, "LuminanceFactors", isImmutable: true));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success);
        Assert.AreEqual("\tfloat Local0 = dot(ObjectOrientation,float3(1.00000000,2.00000000,3.00000000));\r\n\treturn Local0", result.PropertyCode["Opacity"]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MaterialRootForceCastTruncatesPadsAndRetainsUniformSourceLayout(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3RootCasts.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var parameter = package.CreateExport("Color", "MaterialExpressionVectorParameter", material, indexed: false);
        parameter.WriteProperty(new NameProperty("Color", "ParameterName"));
        material.WriteProperty(Input("DiffuseColor", parameter.UIndex));
        material.WriteProperty(Input("Opacity", parameter.UIndex));
        material.WriteProperty(Input("Distortion", parameter.UIndex));
        material.WriteProperty(Input("WorldPositionOffset", parameter.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual("\treturn UniformPixelVector_0.rgb", result.PropertyCode["DiffuseColor"]);
        Assert.AreEqual("\treturn UniformPixelVector_0.r", result.PropertyCode["Opacity"]);
        Assert.AreEqual("\treturn UniformPixelVector_0.rg", result.PropertyCode["Distortion"]);
        Assert.AreEqual("\treturn UniformVertexVector_0.rgb", result.PropertyCode["WorldPositionOffset"]);
        Assert.AreEqual(1, result.UniformPixelVectorExpressions.Count);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
        Assert.AreSame(result.UniformPixelVectorExpressions[0], result.UniformVertexVectorExpressions[0]);
        var two = package.CreateExport("Two", "MaterialExpressionConstant2Vector", material, indexed: false);
        two.WriteProperty(new FloatProperty(1, "R")); two.WriteProperty(new FloatProperty(2, "G"));
        material.WriteProperty(Input("DiffuseColor", two.UIndex));
        var padded = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        string type = half ? "half" : "float";
        Assert.AreEqual($"\treturn {type}3({type}2(1.00000000,2.00000000),0)", padded.PropertyCode["DiffuseColor"]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DesaturationDynamicUniformColorKeepsFoldedDotAndSharedStageTree(bool percent)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DesaturationUniform.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var color = package.CreateExport("Color", "MaterialExpressionConstant3Vector", material, indexed: false);
        foreach (string channel in new[] { "R", "G", "B" }) color.WriteProperty(new FloatProperty(1, channel));
        var parameter = package.CreateExport("Offset", "MaterialExpressionScalarParameter", material, indexed: false);
        parameter.WriteProperty(new NameProperty("Offset", "ParameterName"));
        var sum = package.CreateExport("Sum", "MaterialExpressionAdd", material, indexed: false);
        sum.WriteProperty(Input("A", color.UIndex)); sum.WriteProperty(Input("B", parameter.UIndex));
        var node = package.CreateExport("Desaturation", "MaterialExpressionDesaturation", material, indexed: false);
        node.WriteProperty(Input("Input", sum.UIndex));
        if (percent) node.WriteProperty(Input("Percent", parameter.UIndex));
        material.WriteProperty(Input("EmissiveColor", node.UIndex));
        material.WriteProperty(Input("WorldPositionOffset", node.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        var dot = result.UniformPixelScalarExpressions.OfType<MaterialUniformExpressionFoldedMath>()
            .Single(e => e.Op == MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Dot);
        Assert.IsInstanceOfType<MaterialUniformExpressionFoldedMath>(dot.A);
        var weights = (MaterialUniformExpressionConstant)dot.B;
        Assert.AreEqual(4, weights.ValueType);
        Assert.AreEqual(0.3f, weights.Value.R);
        Assert.AreEqual(0.59f, weights.Value.G);
        Assert.AreEqual(0.11f, weights.Value.B);
        Assert.AreEqual(0f, weights.Value.A);
        Assert.IsTrue(result.UniformVertexScalarExpressions.Any(e => ReferenceEquals(e, dot)));
        if (percent) StringAssert.Contains(result.PropertyCode["EmissiveColor"], "lerp(");
        else Assert.IsFalse(result.PropertyCode["EmissiveColor"].Contains("Local"));
        using var stream = new System.IO.MemoryStream();
        dot.Serialize(new SerializingContainer(stream, package));
        stream.Position = 0;
        var reader = new SerializingContainer(stream, package, isLoading: true);
        var restored = (MaterialUniformExpressionFoldedMath)MaterialUniformExpression.Create(reader);
        restored.Serialize(reader);
        Assert.AreEqual(stream.Length, stream.Position);
        Assert.AreEqual(dot.Op, restored.Op);
        Assert.AreEqual(weights.Value.G, ((MaterialUniformExpressionConstant)restored.B).Value.G);
    }

    [TestMethod]
    [DataRow(1, false)]
    [DataRow(2, false)]
    [DataRow(3, false)]
    [DataRow(4, false)]
    [DataRow(1, true)]
    [DataRow(2, true)]
    [DataRow(3, true)]
    [DataRow(4, true)]
    public void RootCastWidthMatrixMatchesNativeNumericIntersectionAndZeroPadding(int width, bool nonuniform)
    {
        GlobalTest.Init();
        foreach (bool half in new[] { false, true })
        {
            using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3RootCastWidths.pcc", MEGame.LE3);
            var material = package.CreateExport("Material", "Material", indexed: false);
            var node = package.CreateExport("Source", nonuniform ? "MaterialExpressionVertexColor" : width == 1
                ? "MaterialExpressionConstant" : "MaterialExpressionConstant" + width + "Vector", material, indexed: false);
            foreach (string channel in new[] { "R", "G", "B", "A" }.Take(width)) node.WriteProperty(new FloatProperty(1, channel));
            string type = half ? "half" : "float";
            string source = nonuniform ? "Parameters.VertexColor." + "rgba"[..width]
                : type + (width == 1 ? "" : width.ToString()) + "(" + string.Join(',', Enumerable.Repeat("1.00000000", width)) + ")";
            foreach (var (property, target) in new[] { ("Opacity", 1), ("Distortion", 2), ("DiffuseColor", 3), ("WorldPositionOffset", 3) })
            {
                var input = Input(property, node.UIndex);
                if (nonuniform)
                {
                    input.Properties.Add(new BoolProperty(true, "Mask"));
                    foreach (string channel in new[] { "MaskR", "MaskG", "MaskB", "MaskA" }.Take(width))
                        input.Properties.Add(new BoolProperty(true, channel));
                }
                material.WriteProperty(input);
                string expected = source;
                if (width != 1 && width != target)
                    expected = width > target ? source + "." + "rgba"[..target]
                        : type + target + "(" + source + string.Concat(Enumerable.Repeat(",0", target - width)) + ")";
                var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                Assert.AreEqual("\treturn " + expected, result.PropertyCode[property], property);
            }
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(-1)]
    [DataRow(2)]
    public void SerializedIntExpressionInputMasksUseNativeNonzeroSemantics(int mask)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3InputIntMask.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("Color", "MaterialExpressionVertexColor", material, indexed: false);
        var input = Input("DiffuseColor", node.UIndex);
        input.Properties.Add(new IntProperty(mask, "Mask"));
        input.Properties.Add(new IntProperty(-3, "MaskR"));
        input.Properties.Add(new IntProperty(0, "MaskG"));
        input.Properties.Add(new IntProperty(2, "MaskB"));
        input.Properties.Add(new IntProperty(0, "MaskA"));
        material.WriteProperty(input);
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(mask == 0 ? "\treturn Parameters.VertexColor.rgb" : "\treturn float3(Parameters.VertexColor.rb,0)", result.PropertyCode["DiffuseColor"]);
    }

    [TestMethod]
    [DataRow("Mask")]
    [DataRow("MaskR")]
    public void MalformedExpressionInputMaskTypesFailWithDiagnostics(string invalidField)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3InputMalformedMask.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("Color", "MaterialExpressionVertexColor", material, indexed: false);
        var input = Input("DiffuseColor", node.UIndex);
        if (invalidField != "Mask") input.Properties.Add(new IntProperty(1, "Mask"));
        input.Properties.Add(new FloatProperty(1, invalidField));
        material.WriteProperty(input);
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("DiffuseColor." + invalidField + " must be an integer or boolean mask flag.")));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void NormalmapHqMetadataAndStaticOverrideUseAffineOnlyEvenForCanonicalBounds(bool overridden, bool canonical)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NormalHq.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var texture = package.CreateExport("Texture", "Texture2D", indexed: false);
        var node = package.CreateExport("Sample", "MaterialExpressionTextureSampleParameterNormal", material, indexed: false);
        node.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        node.WriteProperty(new NameProperty("Normal", "ParameterName"));
        material.WriteProperty(Input("Normal", node.UIndex));
        var parameters = (StaticParameterSet)Guid.Empty;
        if (overridden) parameters.NormalParameters = [new() { ParameterName = "Normal", CompressionSettings = 10, bOverride = false }];
        var min = canonical ? new System.Numerics.Vector4(-1, -1, -1, 0) : new System.Numerics.Vector4(0.25f);
        var max = System.Numerics.Vector4.One;
        var metadata = new LE3MaterialShaderCompiler.TextureCompilationMetadata(LE3MaterialShaderCompiler.TextureDimension.Texture2D,
            overridden ? "TC_NormalmapBC5" : "TC_NormalmapHQ", min, max);
        foreach (bool half in new[] { false, true })
        {
            int reads = 0;
            var result = new LE3MaterialShaderCompiler(material, parameters, getTextureMetadata: _ => { reads++; return metadata; })
                .TranslateMaterialHlsl(Template(), half);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            string type = half ? "half" : "float";
            string code = result.PropertyCode["Normal"];
            Assert.IsFalse(code.Contains("BiasNormalizeNormalMap"));
            StringAssert.Contains(code, (half ? "h4tex2D" : "tex2D") + "(Texture2D_0,");
            StringAssert.Contains(code, canonical ? $"{type}4 Local2 = (Local1 * {type}4(2.00000000,2.00000000,2.00000000,1.00000000));"
                : $"{type}4 Local2 = (Local1 * {type}4(0.75000000,0.75000000,0.75000000,0.75000000));");
            StringAssert.Contains(code, canonical ? $"{type}4 Local3 = (Local2 + {type}4(-1.00000000,-1.00000000,-1.00000000,0.00000000));"
                : $"{type}4 Local3 = (Local2 + {type}4(0.25000000,0.25000000,0.25000000,0.25000000));");
            StringAssert.Contains(code, "return Local3.rgb");
            Assert.AreEqual(1, reads);
            Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
            Assert.AreEqual(overridden ? "TC_NormalmapBC5" : "TC_NormalmapHQ", metadata.CompressionSettings);
        }
    }

    private static StructProperty Input(string name, int expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression, "Expression") }, name);
    private static string Template() => "/*\n%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n*/";
}
