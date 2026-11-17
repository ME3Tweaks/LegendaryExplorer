using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3TextureMetadataReaderTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReadsAndCachesPropertiesWithoutLoadingLargeTexturePayloads(bool compress)
    {
        GlobalTest.Init();
        using var fixture = MEPackageHandler.CreateMemoryEmptyPackage("MetadataReader.pcc", MEGame.LE3);
        var texture = fixture.CreateExport("Normal", "Texture2D", indexed: false);
        var cube = fixture.CreateExport("Environment", "TextureCube", indexed: false);
        texture.WriteProperty(new EnumProperty("TC_NormalmapBC5", "TextureCompressionSettings", MEGame.LE3, "CompressionSettings"));
        texture.WriteProperty(new FloatProperty(-1, "UnpackMin") { StaticArrayIndex = 1 });
        cube.WriteProperty(new FloatProperty(2, "UnpackMax") { StaticArrayIndex = 3 });
        AppendPayload(texture);
        AppendPayload(cube);
        using var bytes = fixture.SaveToStream(compress);
        bytes.Position = 0;
        using var counted = new CountingStream(bytes);
        using var reader = new LE3TextureMetadataReader(counted, "MetadataReader.pcc");
        Assert.IsTrue(reader.Package.Exports.All(e => !e.IsDataLoaded()));
        counted.BytesRead = 0;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var metadata = reader.Read(texture.UIndex);
        var cubeMetadata = reader.Read(cube.InstancedFullPath);
        long phaseAllocation = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.AreEqual(LE3MaterialShaderCompiler.TextureDimension.Texture2D, metadata.Dimension);
        Assert.AreEqual("TC_NormalmapBC5", metadata.CompressionSettings);
        Assert.AreEqual(new Vector4(0, -1, 0, 0), metadata.UnpackMin);
        Assert.AreEqual(LE3MaterialShaderCompiler.TextureDimension.TextureCube, cubeMetadata.Dimension);
        Assert.AreEqual(new Vector4(1, 1, 1, 2), cubeMetadata.UnpackMax);
        Assert.IsTrue(phaseAllocation < 1024 * 1024, $"Metadata allocated {phaseAllocation} bytes for 4 MiB of texture payload.");
        if (!compress) Assert.IsTrue(counted.BytesRead < 1024, $"Metadata read {counted.BytesRead} uncompressed bytes.");
        Assert.IsTrue(reader.Package.Exports.All(e => !e.IsDataLoaded()));
        long readCount = counted.BytesRead;
        Assert.AreSame(metadata, reader.Read(reader.Package.GetUExport(texture.UIndex)));
        Assert.AreSame(cubeMetadata, reader.Read(cube.UIndex));
        Assert.AreEqual(readCount, counted.BytesRead);
        Assert.ThrowsExactly<ArgumentException>(() => reader.Read(texture)); // another package, same UIndex.
        Assert.ThrowsExactly<ArgumentException>(() => reader.Read(-1));
        Assert.ThrowsExactly<KeyNotFoundException>(() => reader.Read("Missing"));

        static void AppendPayload(ExportEntry export)
        {
            int propertiesLength = export.DataSize;
            var data = new byte[propertiesLength + 2 * 1024 * 1024];
            export.DataReadOnly.CopyTo(data);
            new Random(17).NextBytes(data.AsSpan(propertiesLength));
            export.Data = data;
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReaderMetadataFeedsCompilerWhileTextureDataRemainsUnloaded(bool compress)
    {
        GlobalTest.Init();
        using var fixture = MEPackageHandler.CreateMemoryEmptyPackage("MetadataTranslation.pcc", MEGame.LE3);
        var material = fixture.CreateExport("Material", "Material", indexed: false);
        var texture = fixture.CreateExport("Normal", "Texture2D", indexed: false);
        texture.WriteProperty(new FloatProperty(-1, "UnpackMin") { StaticArrayIndex = 2 });
        var sample = fixture.CreateExport("Sample", "MaterialExpressionTextureSampleParameter2D", material, indexed: false);
        sample.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        sample.WriteProperty(new NameProperty("Albedo", "ParameterName"));
        material.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection
        {
            new ObjectProperty(sample.UIndex, "Expression"), new BoolProperty(true, "Mask"),
            new BoolProperty(true, "MaskR"), new BoolProperty(true, "MaskG"), new BoolProperty(true, "MaskB")
        }, "EmissiveColor"));
        material.WriteProperty(new ArrayProperty<ObjectProperty>([new(sample.UIndex)], "Expressions"));
        const string template = "%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n";
        var baseline = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(template);
        Assert.IsTrue(baseline.Success);
        using var bytes = fixture.SaveToStream(compress);
        bytes.Position = 0;
        using var reader = new LE3TextureMetadataReader(bytes, "MetadataTranslation.pcc");
        var loadedMaterial = reader.Package.LoadExport(material.UIndex);
        reader.Package.LoadExport(sample.UIndex);
        var result = new LE3MaterialShaderCompiler(loadedMaterial, getTextureMetadata: entry => reader.Read(entry.UIndex))
            .TranslateMaterialHlsl(template);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(baseline.MaterialHlsl, result.MaterialHlsl);
        CollectionAssert.AreEqual(baseline.UniformExpressionTextures.ToArray(), result.UniformExpressionTextures.ToArray());
        Assert.IsFalse(reader.Package.GetUExport(texture.UIndex).IsDataLoaded());
    }

    [TestMethod]
    public void ExportStreamIsBoundedReadOnlyAndSupportsInterleavedViews()
    {
        GlobalTest.Init();
        using var fixture = MEPackageHandler.CreateMemoryEmptyPackage("BoundedExport.pcc", MEGame.LE3);
        var one = fixture.CreateExport("One", "Texture2D", indexed: false);
        var two = fixture.CreateExport("Two", "Texture2D", indexed: false);
        one.WriteProperty(new FloatProperty(12, "UnpackMax"));
        two.WriteProperty(new FloatProperty(34, "UnpackMax"));
        using var bytes = fixture.SaveToStream(false);
        bytes.Position = 0;
        using var package = MEPackageHandler.UnsafeLazyLoadFromStream(bytes, "BoundedExport.pcc");
        using var first = package.OpenExportDataStream(package.GetUExport(one.UIndex));
        using var second = package.OpenExportDataStream(package.GetUExport(two.UIndex));
        Assert.AreEqual(one.DataSize, first.Length);
        first.Position = 4;
        second.Position = 8;
        var firstPart = new byte[8];
        var secondPart = new byte[8];
        first.ReadExactly(firstPart);
        second.ReadExactly(secondPart);
        CollectionAssert.AreEqual(one.DataReadOnly.Slice(4, 8).ToArray(), firstPart);
        CollectionAssert.AreEqual(two.DataReadOnly.Slice(8, 8).ToArray(), secondPart);
        Assert.AreEqual(first.Length - 1, first.Seek(-1, SeekOrigin.End));
        Assert.AreEqual(1, first.Read(new byte[20]));
        Assert.AreEqual(0, first.Read(new byte[20]));
        Assert.ThrowsExactly<IOException>(() => first.Seek(1, SeekOrigin.End));
        Assert.ThrowsExactly<IOException>(() => first.Seek(-1, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => first.Write(new byte[1], 0, 1));
        Assert.ThrowsExactly<ArgumentException>(() => package.OpenExportDataStream(one));
        first.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => first.ReadByte());
        Assert.IsTrue(bytes.CanRead);
        Assert.IsFalse(package.GetUExport(one.UIndex).IsDataLoaded());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MalformedTexturePropertiesCannotBecomeDefaultMetadata(bool compress)
    {
        GlobalTest.Init();
        using var fixture = MEPackageHandler.CreateMemoryEmptyPackage("BrokenMetadata.pcc", MEGame.LE3);
        var texture = fixture.CreateExport("Texture", "Texture2D", indexed: false);
        texture.WriteProperty(new FloatProperty(2, "UnpackMax"));
        var data = texture.Data;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(data.Length - 8), int.MaxValue);
        texture.Data = data;
        using var bytes = fixture.SaveToStream(compress);
        bytes.Position = 0;
        using var reader = new LE3TextureMetadataReader(bytes, "BrokenMetadata.pcc");
        Assert.ThrowsExactly<InvalidDataException>(() => reader.Read(texture.UIndex));
        Assert.IsFalse(reader.Package.GetUExport(texture.UIndex).IsDataLoaded());
    }

    [TestMethod]
    public void ReaderRejectsWrongPropertyTypesAndNonTextureExports()
    {
        GlobalTest.Init();
        using var fixture = MEPackageHandler.CreateMemoryEmptyPackage("WrongMetadata.pcc", MEGame.LE3);
        var texture = fixture.CreateExport("Texture", "Texture2D", indexed: false);
        var material = fixture.CreateExport("Material", "Material", indexed: false);
        texture.WriteProperty(new IntProperty(1, "UnpackMin"));
        using var bytes = fixture.SaveToStream(false);
        bytes.Position = 0;
        using var reader = new LE3TextureMetadataReader(bytes, "WrongMetadata.pcc");
        Assert.ThrowsExactly<InvalidDataException>(() => reader.Read(texture.UIndex));
        Assert.ThrowsExactly<ArgumentException>(() => reader.Read(material.UIndex));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReaderHonorsStreamOwnershipAndRejectsUseAfterDispose(bool leaveOpen)
    {
        GlobalTest.Init();
        using var fixture = MEPackageHandler.CreateMemoryEmptyPackage("ReaderLifetime.pcc", MEGame.LE3);
        var texture = fixture.CreateExport("Texture", "Texture2D", indexed: false);
        using var bytes = fixture.SaveToStream(false);
        bytes.Position = 0;
        var reader = new LE3TextureMetadataReader(bytes, "ReaderLifetime.pcc", leaveOpen);
        reader.Read(texture.UIndex);
        reader.Dispose();
        reader.Dispose();
        Assert.AreEqual(leaveOpen, bytes.CanRead);
        Assert.ThrowsExactly<ObjectDisposedException>(() => reader.Read(texture.UIndex));
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = reader.Package);
    }

    [TestMethod]
    public void ReaderRejectsNonLE3AndDisposesOwnedSourceOnOpenFailure()
    {
        GlobalTest.Init();
        using var fixture = MEPackageHandler.CreateMemoryEmptyPackage("ME2Metadata.pcc", MEGame.ME2);
        using var bytes = fixture.SaveToStream(false);
        bytes.Position = 0;
        Assert.ThrowsExactly<NotSupportedException>(() => new LE3TextureMetadataReader(bytes, "ME2Metadata.pcc", leaveOpen: false));
        Assert.IsFalse(bytes.CanRead);
    }

    private sealed class CountingStream(Stream source) : Stream
    {
        public long BytesRead;
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => source.CanSeek;
        public override bool CanWrite => false;
        public override long Length => source.Length;
        public override long Position { get => source.Position; set => source.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer) { int read = source.Read(buffer); BytesRead += read; return read; }
        public override long Seek(long offset, SeekOrigin origin) => source.Seek(offset, origin);
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
