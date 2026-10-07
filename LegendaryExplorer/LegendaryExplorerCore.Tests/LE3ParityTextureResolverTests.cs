using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3ParityTextureResolverTests
{
    [TestMethod]
    public void PrimaryDecookedExportWinsAndDoesNotQueryDonorDatabase()
    {
        GlobalTest.Init();
        var primary = Package(ResolverFixture.DecookedPath("OriginalPackage.pcc"), p => Texture(p, "Texture", "Texture2D", "TC_Primary"));
        int donorQueries = 0;
        using var fixture = new ResolverFixture(primary);
        using var resolver = fixture.CreateResolver(_ => { donorQueries++; return Array.Empty<string>(); });
        using var source = MEPackageHandler.CreateMemoryEmptyPackage("Source.pcc", MEGame.LE3);
        var import = TextureImport(source, "OriginalPackage", "Texture", "Texture2D");

        var metadata = resolver.Get(import);

        AssertMetadata(metadata, TextureDimension.Texture2D, "TC_Primary");
        Assert.AreEqual(0, donorQueries);
        AssertImportUnchanged(import, source, "OriginalPackage.Texture");
    }

    [TestMethod]
    public void MissingPrimaryFindsDonorByOriginalFullObjectPath()
    {
        GlobalTest.Init();
        var donor = Package("CookedContainer.pcc", p =>
        {
            var unrelated = p.CreateExport("DifferentPackage", "Package", indexed: false);
            Texture(p, "Texture", "Texture2D", "TC_WrongNameOnly", unrelated);
            var originalRoot = p.CreateExport("OriginalPackage", "Package", indexed: false);
            Texture(p, "Texture", "Texture2D", "TC_ExactFullPath", originalRoot);
        });
        using var fixture = new ResolverFixture(donor);
        using var resolver = fixture.CreateResolver(path =>
        {
            Assert.AreEqual("OriginalPackage.Texture", path);
            return new[] { donor.Path };
        });
        using var source = MEPackageHandler.CreateMemoryEmptyPackage("Source.pcc", MEGame.LE3);
        var import = TextureImport(source, "OriginalPackage", "Texture", "Texture2D");

        var metadata = resolver.Get(import);

        AssertMetadata(metadata, TextureDimension.Texture2D, "TC_ExactFullPath");
        AssertImportUnchanged(import, source, "OriginalPackage.Texture");
    }

    [TestMethod]
    public void PrimaryMissingExportFallsBackToFullPathDonor()
    {
        GlobalTest.Init();
        var primary = Package(ResolverFixture.DecookedPath("OriginalPackage.pcc"), p => Texture(p, "SomethingElse", "Texture2D", "TC_Unrelated"));
        var donor = Package("Donor.pcc", p => NestedTexture(p, "OriginalPackage", "Texture", "TC_Donor"));
        using var fixture = new ResolverFixture(primary, donor);
        using var resolver = fixture.CreateResolver(_ => new[] { donor.Path });
        using var source = MEPackageHandler.CreateMemoryEmptyPackage("Source.pcc", MEGame.LE3);
        var import = TextureImport(source, "OriginalPackage", "Texture", "Texture2D");

        AssertMetadata(resolver.Get(import), TextureDimension.Texture2D, "TC_Donor");
    }

    [TestMethod]
    public void UnreadableAndMissingExportDonorsDoNotStopLaterCandidates()
    {
        GlobalTest.Init();
        string brokenPath = ResolverFixture.FullPath("Broken.pcc");
        string noExportPath = ResolverFixture.FullPath("NoExport.pcc");
        var noExport = Package(noExportPath, p => Texture(p, "Other", "Texture2D", "TC_Unrelated"));
        var valid = Package("ValidDonor.pcc", p => NestedTexture(p, "OriginalPackage", "Texture", "TC_Valid"));
        using var fixture = new ResolverFixture(noExport, valid);
        var opened = new List<string>();
        using var resolver = fixture.CreateResolver(_ => new[] { brokenPath, brokenPath, noExport.Path, valid.Path },
            exists: _ => true,
            open: path =>
            {
                opened.Add(path);
                if (path.Equals(brokenPath, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Synthetic unreadable package");
                return fixture.Open(path);
            });
        using var source = MEPackageHandler.CreateMemoryEmptyPackage("Source.pcc", MEGame.LE3);
        var import = TextureImport(source, "OriginalPackage", "Texture", "Texture2D");

        AssertMetadata(resolver.Get(import), TextureDimension.Texture2D, "TC_Valid");
        Assert.AreEqual(1, opened.Count(path => path.Equals(brokenPath, StringComparison.OrdinalIgnoreCase)), "Duplicate failed paths should be tried once.");
        Assert.AreEqual(1, opened.Count(path => path.Equals(noExport.Path, StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void MissingPackageAndMissingExportsFailClosed()
    {
        GlobalTest.Init();
        var unrelated = Package("Unrelated.pcc", p => Texture(p, "Other", "Texture2D", "TC_Unrelated"));
        using var fixture = new ResolverFixture(unrelated);
        using var resolver = fixture.CreateResolver(_ => new[] { ResolverFixture.FullPath("NotPresent.pcc"), unrelated.Path });
        using var source = MEPackageHandler.CreateMemoryEmptyPackage("Source.pcc", MEGame.LE3);
        var import = TextureImport(source, "OriginalPackage", "Texture", "Texture2D");

        Assert.IsNull(resolver.Get(import), "Unavailable or nonmatching packages must not produce default texture metadata.");
    }

    [TestMethod]
    [DataRow("TextureCube", "Texture2D")]
    [DataRow("Texture2D", "TextureCube")]
    public void IncompatibleDonorClassFailsClosed(string donorClass, string requestedClass)
    {
        GlobalTest.Init();
        var donor = Package("WrongClass.pcc", p => NestedTexture(p, "OriginalPackage", "Texture", "TC_WrongDimension", donorClass));
        using var fixture = new ResolverFixture(donor);
        using var resolver = fixture.CreateResolver(_ => new[] { donor.Path });
        using var source = MEPackageHandler.CreateMemoryEmptyPackage("Source.pcc", MEGame.LE3);
        var import = TextureImport(source, "OriginalPackage", "Texture", requestedClass);

        Assert.IsNull(resolver.Get(import), "A class or dimension mismatch must not fall back to TC_Default or the import's apparent shape.");
    }

    [TestMethod]
    public void SharedDonorPathIsCachedCaseInsensitivelyAndMetadataIsReused()
    {
        GlobalTest.Init();
        var donor = Package("SharedDonor.pcc", p => NestedTexture(p, "OriginalPackage", "Texture", "TC_Shared"));
        using var fixture = new ResolverFixture(donor);
        int opens = 0;
        using var resolver = fixture.CreateResolver(_ => new[] { donor.Path.ToUpperInvariant() }, open: path => { opens++; return fixture.Open(path); });
        using var sourceA = MEPackageHandler.CreateMemoryEmptyPackage("SourceA.pcc", MEGame.LE3);
        using var sourceB = MEPackageHandler.CreateMemoryEmptyPackage("SourceB.pcc", MEGame.LE3);
        var importA = TextureImport(sourceA, "OriginalPackage", "Texture", "Texture2D");
        var importB = TextureImport(sourceB, "OriginalPackage", "Texture", "Texture2D");

        var first = resolver.Get(importA);
        var second = resolver.Get(importB);

        AssertMetadata(first, TextureDimension.Texture2D, "TC_Shared");
        Assert.AreSame(first, second);
        Assert.AreEqual(1, opens, "The same full path with different casing must share one reader.");
    }

    [TestMethod]
    public void DonorMetadataReadLeavesLargeTextureExportUnloaded()
    {
        GlobalTest.Init();
        var donor = Package("LargeDonor.pcc", p =>
        {
            var texture = NestedTexture(p, "OriginalPackage", "Texture", "TC_Large");
            AppendPayload(texture, 2 * 1024 * 1024);
        });
        using var fixture = new ResolverFixture(donor);
        LE3TextureMetadataReader donorReader = null;
        using var resolver = fixture.CreateResolver(_ => new[] { donor.Path }, open: path => donorReader = fixture.Open(path));
        using var source = MEPackageHandler.CreateMemoryEmptyPackage("Source.pcc", MEGame.LE3);
        var import = TextureImport(source, "OriginalPackage", "Texture", "Texture2D");

        AssertMetadata(resolver.Get(import), TextureDimension.Texture2D, "TC_Large");
        var loadedTexture = donorReader.Package.FindExport("OriginalPackage.Texture");
        Assert.IsNotNull(loadedTexture);
        Assert.IsFalse(loadedTexture.IsDataLoaded(), "Metadata lookup must not load the appended texture binary payload.");
        AssertImportUnchanged(import, source, "OriginalPackage.Texture");
    }

    [TestMethod]
    public void FailedPackageLookupIsCachedByFullPath()
    {
        GlobalTest.Init();
        using var fixture = new ResolverFixture();
        int existenceChecks = 0;
        using var resolver = fixture.CreateResolver(_ => new[] { ResolverFixture.FullPath("Absent.pcc") },
            exists: path =>
            {
                if (path.Equals(ResolverFixture.FullPath("Absent.pcc"), StringComparison.OrdinalIgnoreCase)) existenceChecks++;
                return false;
            });
        using var source = MEPackageHandler.CreateMemoryEmptyPackage("Source.pcc", MEGame.LE3);
        var import = TextureImport(source, "OriginalPackage", "Texture", "Texture2D");

        Assert.IsNull(resolver.Get(import));
        Assert.IsNull(resolver.Get(import));
        Assert.AreEqual(1, existenceChecks, "The same unavailable full path should not be probed repeatedly.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FlipBookScaleReadsStoredImageCountsWithoutLoadingTexturePayload(bool primary)
    {
        GlobalTest.Init();
        string path = primary ? ResolverFixture.DecookedPath("OriginalPackage.pcc") : "FlipBookContainer.pcc";
        var donor = Package(path, package =>
        {
            var texture = primary ? Texture(package, "Texture", "TextureFlipBook", "TC_Default")
                : NestedTexture(package, "OriginalPackage", "Texture", "TC_Default", "TextureFlipBook");
            texture.WriteProperty(new IntProperty(4, "HorizontalImages"));
            texture.WriteProperty(new IntProperty(8, "VerticalImages"));
            AppendPayload(texture, 2 * 1024 * 1024);
        });
        using var fixture = new ResolverFixture(donor);
        LE3TextureMetadataReader reader = null;
        using var resolver = fixture.CreateResolver(_ => new[] { donor.Path }, open: candidate => reader = fixture.Open(candidate));
        using var source = MEPackageHandler.CreateMemoryEmptyPackage("Source.pcc", MEGame.LE3);
        var import = TextureImport(source, "OriginalPackage", "Texture", "TextureFlipBook");
        var metadata = resolver.Get(import);
        AssertMetadata(metadata, TextureDimension.Texture2D, "TC_Default");
        Assert.AreEqual(new System.Numerics.Vector2(.25f, .125f), metadata.FlipBookScale);
        Assert.IsTrue(reader.Package.Exports.Where(export => export.IsA("Texture2D")).All(export => !export.IsDataLoaded()));
        AssertImportUnchanged(import, source, "OriginalPackage.Texture");
    }

    private static void AssertMetadata(TextureCompilationMetadata metadata, TextureDimension dimension, string compression)
    {
        Assert.IsNotNull(metadata);
        Assert.AreEqual(dimension, metadata.Dimension);
        Assert.AreEqual(compression, metadata.CompressionSettings);
    }

    private static void AssertImportUnchanged(ImportEntry import, IMEPackage originalPackage, string fullPath)
    {
        Assert.AreSame(originalPackage, import.FileRef);
        Assert.AreEqual(fullPath, import.InstancedFullPath);
        Assert.AreEqual("OriginalPackage", import.PackageFile);
        Assert.IsInstanceOfType(import.Parent, typeof(ImportEntry));
    }

    private static ImportEntry TextureImport(IMEPackage package, string originalPackage, string name, string className)
    {
        var root = new ImportEntry(package, 0, originalPackage) { ClassName = "Package", PackageFile = originalPackage };
        package.AddImport(root);
        var texture = new ImportEntry(package, root, name) { ClassName = className, PackageFile = originalPackage };
        package.AddImport(texture);
        return texture;
    }

    private static PackageFixture Package(string name, Action<IMEPackage> populate = null)
    {
        var package = MEPackageHandler.CreateMemoryEmptyPackage(name, MEGame.LE3);
        try
        {
            populate?.Invoke(package);
            using var bytes = package.SaveToStream(false);
            return new PackageFixture(name, bytes.ToArray());
        }
        finally
        {
            package.Dispose();
        }
    }

    private static ExportEntry NestedTexture(IMEPackage package, string parentName, string name, string compression, string className = "Texture2D")
    {
        var parent = package.CreateExport(parentName, "Package", indexed: false);
        return Texture(package, name, className, compression, parent);
    }

    private static ExportEntry Texture(IMEPackage package, string name, string className, string compression, ExportEntry parent = null)
    {
        var texture = package.CreateExport(name, className, parent, indexed: false);
        texture.WriteProperty(new EnumProperty(compression, "TextureCompressionSettings", MEGame.LE3, "CompressionSettings"));
        texture.WriteProperty(new FloatProperty(-2, "UnpackMin") { StaticArrayIndex = 0 });
        texture.WriteProperty(new FloatProperty(3, "UnpackMax") { StaticArrayIndex = 3 });
        return texture;
    }

    private static void AppendPayload(ExportEntry texture, int payloadSize)
    {
        int propertyLength = texture.DataSize;
        var data = new byte[propertyLength + payloadSize];
        texture.DataReadOnly.CopyTo(data);
        new Random(13).NextBytes(data.AsSpan(propertyLength));
        texture.Data = data;
    }

    private sealed record PackageFixture(string Path, byte[] Bytes);

    private sealed class ResolverFixture : IDisposable
    {
        private readonly Dictionary<string, byte[]> packages = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, LE3TextureMetadataReader> openedReaders = new(StringComparer.OrdinalIgnoreCase);
        private readonly string folder = ResolverFolder;
        private static string ResolverFolder => Path.Combine(Path.GetTempPath(), "LE3ParityTextureResolverTests");

        public ResolverFixture(params PackageFixture[] packageFixtures)
        {
            foreach (var package in packageFixtures) packages[Path.GetFullPath(package.Path)] = package.Bytes;
        }

        public static string FullPath(string path) => Path.GetFullPath(path);
        public static string DecookedPath(string fileName) => Path.GetFullPath(Path.Combine(ResolverFolder, fileName));

        public LE3ShippingParityHarness.TextureResolver CreateResolver(Func<string, IEnumerable<string>> donors,
            Func<string, bool> exists = null, Func<string, LE3TextureMetadataReader> open = null)
        {
            return new LE3ShippingParityHarness.TextureResolver(folder, donors,
                exists ?? (path => packages.ContainsKey(Path.GetFullPath(path))),
                open ?? Open);
        }

        public LE3TextureMetadataReader Open(string path)
        {
            if (!packages.TryGetValue(Path.GetFullPath(path), out var bytes)) throw new FileNotFoundException("No in-memory fixture registered.", path);
            var reader = new LE3TextureMetadataReader(new MemoryStream(bytes, writable: false), path, leaveOpen: false);
            openedReaders[Path.GetFullPath(path)] = reader;
            return reader;
        }

        public void Dispose()
        {
            foreach (var reader in openedReaders.Values) reader.Dispose();
            openedReaders.Clear();
        }
    }
}
