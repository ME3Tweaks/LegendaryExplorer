using System;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3ParityInspectionTests
{
    [TestMethod]
    public void DisabledInspectionDoesNotAccessInputs() => LE3ParityInspection.Write(null, null, null);

    [TestMethod]
    public void InspectionSeparatesHlslAndRecordsReferencesWithoutMutatingTheMaterial()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3Inspection.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var expression = package.CreateExport("Value", "MaterialExpressionScalarParameter", material, indexed: false);
        expression.WriteProperty(new NameProperty("Opacity", "ParameterName"));
        material.WriteProperty(new ObjectProperty(expression.UIndex, "ProbeReference"));
        byte[] before = material.Data.ToArray();
        string directory = Directory.CreateTempSubdirectory("LE3Inspection_").FullName;
        try
        {
            LE3ParityInspection.Write(directory, "decooked", material, new InspectionDetail());
            var json = JObject.Parse(File.ReadAllText(Directory.GetFiles(directory, "*.json").Single()));
            var reference = ((JArray)json["properties"]).Single(p => (string)p["name"] == "ProbeReference");
            Assert.AreEqual(expression.InstancedFullPath, (string)reference["resolvedPath"]);
            Assert.AreEqual(expression.ClassName, (string)reference["resolvedClass"]);
            Assert.AreEqual(expression.UIndex, (int)reference["uindex"]);
            Assert.AreEqual(1, ((JArray)json["materialExpressions"]).Count);
            Assert.AreEqual("float probe;", File.ReadAllText(Directory.GetFiles(directory, "*.usf").Single()));
            Assert.IsNull(json["details"][nameof(InspectionDetail)]["Graph"]);
            CollectionAssert.AreEqual(before, material.Data);
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public void InspectionNeverLoadsAHeaderOnlyTexturePayload()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3InspectionTexture.pcc", MEGame.LE3);
        var texture = package.CreateExport("Texture", "Texture2D", indexed: false);
        texture.WriteProperty(new IntProperty(64, "SizeX"));
        texture.Data = texture.Data.Concat(new byte[2 * 1024 * 1024]).ToArray();
        using var serialized = package.SaveToStream(false);
        using var reader = new LE3TextureMetadataReader(new MemoryStream(serialized.ToArray()), "LE3InspectionTexture.pcc", leaveOpen: false);
        var header = reader.Package.FindExport("Texture");
        Assert.IsFalse(header.IsDataLoaded());
        string directory = Directory.CreateTempSubdirectory("LE3InspectionTexture_").FullName;
        try
        {
            LE3ParityInspection.Write(directory, "decooked", header);
            Assert.IsFalse(header.IsDataLoaded());
            var json = JObject.Parse(File.ReadAllText(Directory.GetFiles(directory, "*.json").Single()));
            Assert.AreEqual(0, ((JArray)json["properties"]).Count);
            Assert.AreEqual(0, ((JArray)json["effectiveProperties"]).Count);
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class InspectionDetail
    {
        public string MaterialHlsl => "float probe;";
        public object Graph => throw new InvalidOperationException("Graph inspection must stay excluded.");
    }
}
