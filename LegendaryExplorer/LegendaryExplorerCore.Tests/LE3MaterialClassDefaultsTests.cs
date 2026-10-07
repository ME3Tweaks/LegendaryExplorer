using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3MaterialClassDefaultsTests
{
    private static readonly string[] ColorInputs = ["DiffuseColor", "SpecularColor"];

    [TestMethod]
    public void MiniEngineMaterialAndDecalMaterialCdosHaveDisconnectedColorInputDefaults()
    {
        GlobalTest.Init();
        string enginePath = Path.Combine(GlobalTest.GetTestMiniGamePath(MEGame.LE3), "BioGame", "CookedPCConsole", "Engine.pcc");
        using var engine = MEPackageHandler.OpenMEPackage(enginePath);

        AssertCdoColorDefaults(engine.Exports.Single(e => e.ObjectName.Name == "Default__Material"));
        AssertCdoColorDefaults(engine.Exports.Single(e => e.ObjectName.Name == "Default__DecalMaterial"));
    }

    [TestMethod]
    public void OptionalInstalledEngineInspectionReadsOnlySelectedCdosAndLeavesTexturesUnloaded()
    {
        string configuredGame = Environment.GetEnvironmentVariable("LE3_CLASS_DEFAULTS_GAME");
        if (string.IsNullOrWhiteSpace(configuredGame))
            Assert.Inconclusive("Set LE3_CLASS_DEFAULTS_GAME to an LE3 game root or Engine.pcc to inspect installed defaults.");

        string enginePath = File.Exists(configuredGame)
            ? configuredGame
            : Path.Combine(configuredGame, "BioGame", "CookedPCConsole", "Engine.pcc");
        Assert.IsTrue(File.Exists(enginePath), $"Engine.pcc was not found at {enginePath}");

        GlobalTest.Init();
        using var reader = LE3TextureMetadataReader.Open(enginePath);
        var textureExports = reader.Package.Exports
            .Where(e => e.ClassName?.StartsWith("Texture", StringComparison.Ordinal) == true)
            .ToArray();
        Assert.IsTrue(textureExports.All(e => !e.IsDataLoaded()), "Engine.pcc texture exports must start unloaded.");

        var materialHeader = reader.Package.Exports.Single(e => e.ObjectName.Name == "Default__Material");
        var decalHeader = reader.Package.Exports.Single(e => e.ObjectName.Name == "Default__DecalMaterial");
        var material = LoadDefaultChain(materialHeader);
        var decal = LoadDefaultChain(decalHeader);
        AssertCdoColorDefaults(material);
        AssertCdoColorDefaults(decal);

        string reportDirectory = Environment.GetEnvironmentVariable("LE3_CLASS_DEFAULTS_REPORT_DIR");
        LE3ParityInspection.Write(reportDirectory, "installed-defaults", material);
        LE3ParityInspection.Write(reportDirectory, "installed-defaults", decal);
        var vectorParameter = LoadDefaultChain(reader.Package.Exports.Single(e => e.ObjectName.Name == "Default__MaterialExpressionVectorParameter"));
        var vectorDefault = vectorParameter.GetPropertiesIncludingInherited().GetProp<StructProperty>("DefaultValue");
        Assert.IsNotNull(vectorDefault);
        Assert.AreEqual(0f, vectorDefault.GetProp<FloatProperty>("R").Value);
        Assert.AreEqual(0f, vectorDefault.GetProp<FloatProperty>("G").Value);
        Assert.AreEqual(0f, vectorDefault.GetProp<FloatProperty>("B").Value);
        Assert.AreEqual(1f, vectorDefault.GetProp<FloatProperty>("A").Value);
        LE3ParityInspection.Write(reportDirectory, "installed-defaults", vectorParameter);

        Assert.IsTrue(textureExports.All(e => !e.IsDataLoaded()), "Inspecting material class defaults must not load texture exports.");

        ExportEntry LoadDefaultChain(ExportEntry header)
        {
            var visited = new HashSet<int>();
            var result = reader.Package.LoadExport(header.UIndex);
            for (var current = result; current != null && visited.Add(current.UIndex); current = current.Archetype as ExportEntry)
            {
                Assert.IsTrue(current.IsDefaultObject, "Only the material class-default archetype chain may be loaded.");
                reader.Package.LoadExport(current.UIndex);
            }
            return result;
        }
    }

    private static void AssertCdoColorDefaults(ExportEntry cdo)
    {
        Assert.IsTrue(cdo.IsDefaultObject, $"{cdo.InstancedFullPath} must be a class default object.");
        foreach (string inputName in ColorInputs)
        {
            StructProperty input = cdo.GetPropertiesIncludingInherited().GetProp<StructProperty>(inputName);
            Assert.IsNotNull(input, $"{cdo.InstancedFullPath}.{inputName} must resolve through the CDO inheritance chain.");
            Assert.IsFalse(input.GetProp<BoolProperty>("UseConstant")?.Value ?? true,
                $"{cdo.InstancedFullPath}.{inputName}.UseConstant must resolve to false.");
            Assert.AreEqual(0, input.GetProp<ObjectProperty>("Expression")?.Value ?? int.MinValue,
                $"{cdo.InstancedFullPath}.{inputName}.Expression must resolve to null.");

            StructProperty constant = input.GetProp<StructProperty>("Constant");
            Assert.IsNotNull(constant, $"{cdo.InstancedFullPath}.{inputName}.Constant must resolve through the CDO inheritance chain.");
            Assert.AreEqual((byte)128, GetColorChannel(constant, "R"));
            Assert.AreEqual((byte)128, GetColorChannel(constant, "G"));
            Assert.AreEqual((byte)128, GetColorChannel(constant, "B"));
            Assert.AreEqual((byte)0, GetColorChannel(constant, "A"));
        }
    }

    private static byte GetColorChannel(StructProperty color, string channelName)
    {
        ByteProperty channel = color.GetProp<ByteProperty>(channelName);
        Assert.IsNotNull(channel, $"Stored color channel {channelName} must be present.");
        return channel.Value;
    }
}
