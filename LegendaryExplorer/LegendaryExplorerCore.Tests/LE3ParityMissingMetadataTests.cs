using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3ParityMissingMetadataTests
{
    [TestMethod]
    public void RepeatedMissingImportHasOneInventoryEntryAndTwoOutcomes()
    {
        GlobalTest.Init();
        using var source = MEPackageHandler.CreateMemoryEmptyPackage("RequestingSource.pcc", MEGame.LE3);
        var import = TextureImport(source, "OriginalPackage", "Texture", "Texture2D");
        using var resolver = CreateResolver(_ => Array.Empty<string>());

        Assert.IsNull(resolver.Get(import));
        Assert.IsNull(resolver.Get(import));

        string request = resolver.MissingMetadataRequests.Single();
        StringAssert.Contains(request, "OriginalPackage.Texture (Texture2D)");
        StringAssert.Contains(request, "requested by RequestingSource.pcc");
        Assert.AreEqual(2, resolver.Outcomes["import: no authoritative metadata donor"]);
    }

    [TestMethod]
    public void MissingImportsAreAllListedInOrdinalIgnoreCaseOrder()
    {
        GlobalTest.Init();
        using var source = MEPackageHandler.CreateMemoryEmptyPackage("RequestingSource.pcc", MEGame.LE3);
        using var resolver = CreateResolver(_ => Array.Empty<string>());
        string[] paths = { "Zulu.Texture", "alpha.Texture", "Mike.Texture", "bravo.Texture" };

        foreach (string path in paths)
        {
            var import = TextureImport(source, path[..path.IndexOf('.')], path[(path.IndexOf('.') + 1)..], "Texture2D");
            Assert.IsNull(resolver.Get(import));
        }

        CollectionAssert.AreEqual(paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
            resolver.MissingMetadataRequests.Select(request => request[..request.IndexOf(" (Texture2D)", StringComparison.Ordinal)]).ToArray());
        Assert.AreEqual(4, resolver.MissingMetadataRequests.Count);
    }

    [TestMethod]
    public void CandidatePackagePathsAreCanonicalSortedAndUnavailablePathsAreNotOpened()
    {
        GlobalTest.Init();
        using var source = MEPackageHandler.CreateMemoryEmptyPackage("RequestingSource.pcc", MEGame.LE3);
        var import = TextureImport(source, "OriginalPackage", "Texture", "Texture2D");
        string zulu = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "zulu-donor.pcc"));
        string alpha = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "alpha-donor.pcc"));
        int openCalls = 0;
        using var resolver = CreateResolver(_ => new[] { zulu, alpha, zulu.ToUpperInvariant() },
            exists: _ => false,
            open: _ => { openCalls++; throw new InvalidOperationException("Unavailable paths must not be opened."); });

        Assert.IsNull(resolver.Get(import));

        string request = resolver.MissingMetadataRequests.Single();
        string primary = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "LE3ParityMissingMetadataTests", "OriginalPackage.pcc"));
        string expectedCandidates = string.Join(", ", new[] { alpha, primary, zulu }.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        StringAssert.Contains(request, "candidate packages: " + expectedCandidates);
        Assert.AreEqual(0, openCalls);
    }

    [TestMethod]
    public void DonorLookupExceptionsPreserveFullImportPathAndAreCappedAtThree()
    {
        GlobalTest.Init();
        using var source = MEPackageHandler.CreateMemoryEmptyPackage("RequestingSource.pcc", MEGame.LE3);
        using var resolver = CreateResolver(_ => throw new InvalidDataException("Synthetic donor lookup failure"));
        string[] fullPaths = { "Alpha.Texture", "Bravo.Texture", "Charlie.Texture", "Delta.Texture" };

        foreach (string path in fullPaths)
        {
            var import = TextureImport(source, path[..path.IndexOf('.')], path[(path.IndexOf('.') + 1)..], "Texture2D");
            Assert.IsNull(resolver.Get(import));
        }

        Assert.AreEqual(3, resolver.MetadataErrors.Count);
        for (int i = 0; i < 3; i++)
        {
            StringAssert.Contains(resolver.MetadataErrors[i], fullPaths[i]);
            StringAssert.Contains(resolver.MetadataErrors[i], "Synthetic donor lookup failure");
        }
        Assert.IsFalse(resolver.MetadataErrors.Any(error => error.Contains("Delta.Texture", StringComparison.Ordinal)));
    }

    private static ImportEntry TextureImport(IMEPackage package, string originalPackage, string name, string className)
    {
        var root = new ImportEntry(package, 0, originalPackage) { ClassName = "Package", PackageFile = originalPackage };
        package.AddImport(root);
        var texture = new ImportEntry(package, root, name) { ClassName = className, PackageFile = originalPackage };
        package.AddImport(texture);
        return texture;
    }

    private static LE3ShippingParityHarness.TextureResolver CreateResolver(
        Func<string, IEnumerable<string>> donors,
        Func<string, bool> exists = null,
        Func<string, LE3TextureMetadataReader> open = null)
    {
        string folder = Path.Combine(Path.GetTempPath(), "LE3ParityMissingMetadataTests");
        return new LE3ShippingParityHarness.TextureResolver(folder, donors, exists ?? (_ => false), open);
    }
}
