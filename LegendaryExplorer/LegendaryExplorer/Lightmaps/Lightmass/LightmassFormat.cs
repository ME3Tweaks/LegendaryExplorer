using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace LegendaryExplorer.Lightmaps.Lightmass;

/// <summary>
/// The UE3 Lightmass exchange format (IB Tools/UnrealLightmass/Public/*Export.h), as UDK-2015-02's UnrealLightmass.exe reads
/// it. Channel version GUIDs are UDK's: they match IB's ImportExport.h except LM_MATERIAL_VERSION.
/// </summary>
public static class LightmassFormat
{
    // ImportExport.h channel versions (LM_COMPRESS_OUTPUT_DATA). GUIDs are written as UE3 FGuid A, B, C, D.
    public static readonly Guid SceneVersion = FromUe3(0xfe50c828, 0x56ee4777, 0xbcbc6c90, 0x0a5426d3);
    public static readonly Guid StaticMeshVersion = FromUe3(0x581b2a30, 0xa75b4611, 0x967a7389, 0xf386fb24);
    /// <summary>UDK-2015-02's material version (IB's is c8b3cd18 3039416b 92f7e7ef 30551703); the layout is assumed to be IB's.</summary>
    public static readonly Guid MaterialVersion = FromUe3(0x18286259, 0x5b4742c7, 0xb23fd922, 0x131b2675);
    public static readonly Guid TextureMappingVersion = FromUe3(0x0d6d0c6c, 0x84d14c54, 0xa0c4773e, 0x3dc9cdd2);
    public static readonly Guid VertexMappingVersion = FromUe3(0x6bd8f62d, 0x896f453c, 0xa9465252, 0x0b2f9d4a);
    public static readonly Guid TerrainVersion = FromUe3(0x2f7c902b, 0x7b7d4f2d, 0xa53774c5, 0x04301938);

    /// <summary>Task GUIDs Lightmass knows by value: volume lighting samples and mesh area light data (ImportExport.h).</summary>
    public static readonly Guid PrecomputedVolumeLightingGuid = FromUe3(0xce97c5c3, 0xab614fd3, 0xb2da55c0, 0xe6c33fb4);
    public static readonly Guid MeshAreaLightDataGuid = FromUe3(0xe11f4760, 0xfa454d2b, 0xa090c388, 0x33326646);

    public const string SceneExtension = "scenegz";
    public const string StaticMeshExtension = "meshgz";
    public const string MaterialExtension = "mtrlgz";
    public const string TerrainExtension = "terrgz";
    public const string TextureMappingExtension = "tmap";
    public const string VertexMappingExtension = "vmap";

    // SwarmDefines.h channel flags.
    public const int ChannelPersistent = 0x1, ChannelJobOnly = 0x2, ChannelRead = 0x10, ChannelWrite = 0x20, ChannelCompressed = 0x20000;
    public const int SceneChannelFlags = ChannelJobOnly | ChannelWrite | ChannelCompressed;
    public const int PersistentChannelFlags = ChannelPersistent | ChannelWrite | ChannelCompressed;
    public const int ResultChannelFlags = ChannelJobOnly | ChannelRead;

    /// <summary>A System.Guid with the same 16 bytes as the UE3 FGuid (A, B, C, D), which is also how LEC reads package GUIDs.</summary>
    public static Guid FromUe3(uint a, uint b, uint c, uint d)
    {
        var bytes = new byte[16];
        BitConverter.TryWriteBytes(bytes.AsSpan(0), a);
        BitConverter.TryWriteBytes(bytes.AsSpan(4), b);
        BitConverter.TryWriteBytes(bytes.AsSpan(8), c);
        BitConverter.TryWriteBytes(bytes.AsSpan(12), d);
        return new Guid(bytes);
    }

    public static (uint A, uint B, uint C, uint D) ToUe3(Guid g)
    {
        var bytes = g.ToByteArray();
        return (BitConverter.ToUInt32(bytes, 0), BitConverter.ToUInt32(bytes, 4), BitConverter.ToUInt32(bytes, 8), BitConverter.ToUInt32(bytes, 12));
    }

    /// <summary>UE3 FGuid::String(): the four parts as %08X, which is how Lightmass's command line takes the scene GUID.</summary>
    public static string Ue3String(Guid g)
    {
        var (a, b, c, d) = ToUe3(g);
        return $"{a:X8}{b:X8}{c:X8}{d:X8}";
    }

    /// <summary>The inverse of <see cref="Ue3String"/> (how the Swarm bridge sends task GUIDs), or null if it isn't 32 hex digits.</summary>
    public static Guid? ParseUe3String(string hex)
    {
        if (hex is not { Length: 32 }) return null;
        var parts = new uint[4];
        for (int i = 0; i < 4; i++)
            if (!uint.TryParse(hex.AsSpan(i * 8, 8), System.Globalization.NumberStyles.AllowHexSpecifier, null, out parts[i])) return null;
        return FromUe3(parts[0], parts[1], parts[2], parts[3]);
    }

    /// <summary>CreateChannelName (ImportExport.h): version first, then GUID, then extension.</summary>
    public static string ChannelName(Guid guid, Guid version, string extension) => $"{Ue3String(version)}.{Ue3String(guid)}.{extension}";

    /// <summary>A GUID derived from a string, for content that should keep its identity (and Swarm cache entry) across bakes.</summary>
    public static Guid HashGuid(string key)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash.AsSpan(0, 16));
    }
}

/// <summary>
/// Writes Lightmass's #pragma pack(1) structs. FVector4, FMatrix and FBox are __declspec(align(16)) types, which MSVC keeps aligned
/// even inside packed structs, so every struct containing one is padded to 16 (offsets from an MSVC build of the IB headers).
/// </summary>
public sealed class LightmassWriter(Stream stream) : IDisposable
{
    readonly CountingStream counter = new(stream);
    BinaryWriter writer => field ??= new BinaryWriter(counter, Encoding.Unicode, leaveOpen: true);

    /// <summary>Bytes written so far (BinaryWriter passes primitives straight through, so no flush is needed; the output, e.g. a compression stream, needn't be seekable).</summary>
    public long Position => counter.Count;

    public void Int(int v) => writer.Write(v);
    public void UInt(uint v) => writer.Write(v);
    public void Bool(bool v) => writer.Write(v ? 1 : 0);
    public void Float(float v) => writer.Write(v);
    public void Double(double v) => writer.Write(v);
    public void Byte(byte v) => writer.Write(v);
    public void Bytes(ReadOnlySpan<byte> v) => writer.Write(v);
    public void UShort(ushort v) => writer.Write(v);
    public void Guid(Guid g) => writer.Write(g.ToByteArray());
    public void Vector2(Vector2 v) { writer.Write(v.X); writer.Write(v.Y); }
    public void Vector4(Vector3 v, float w) { writer.Write(v.X); writer.Write(v.Y); writer.Write(v.Z); writer.Write(w); }
    public void Vector4(Vector4 v) { writer.Write(v.X); writer.Write(v.Y); writer.Write(v.Z); writer.Write(v.W); }
    /// <summary>FLinearColor: R, G, B, A floats.</summary>
    public void LinearColor(Vector3 rgb, float a = 1f) => Vector4(rgb, a);
    /// <summary>FColor in memory order B, G, R, A.</summary>
    public void Color(byte r, byte g, byte b, byte a) { writer.Write(b); writer.Write(g); writer.Write(r); writer.Write(a); }

    /// <summary>FMatrix M[row][column]; UE3 and System.Numerics both use row vectors (translation in the fourth row).</summary>
    public void Matrix(Matrix4x4 m)
    {
        Vector4(new Vector4(m.M11, m.M12, m.M13, m.M14)); Vector4(new Vector4(m.M21, m.M22, m.M23, m.M24));
        Vector4(new Vector4(m.M31, m.M32, m.M33, m.M34)); Vector4(new Vector4(m.M41, m.M42, m.M43, m.M44));
    }

    /// <summary>FBox: Min and Max as FVector4 (W 1, as UE3's Copy(FVector) writes it).</summary>
    public void Box(Vector3 min, Vector3 max) { Vector4(min, 1f); Vector4(max, 1f); }

    public void Zero(int count) { for (int i = 0; i < count; i++) writer.Write((byte)0); }

    /// <summary>Pads with zeros to the given offset from <paramref name="structStart"/>, and checks a struct didn't overrun it.</summary>
    public void PadTo(long structStart, int offset)
    {
        long pad = structStart + offset - Position;
        if (pad < 0) throw new InvalidOperationException($"Lightmass struct overran offset {offset} by {-pad} bytes");
        Zero((int)pad);
    }

    /// <summary>A length-prefixed UTF-16 string without terminator (UE3's user and level names).</summary>
    public void LengthPrefixedString(string s)
    {
        writer.Write(s.Length);
        writer.Write(Encoding.Unicode.GetBytes(s));
    }

    public void Dispose() => writer.Dispose();

    /// <summary>Passes writes through and counts them.</summary>
    sealed class CountingStream(Stream inner) : Stream
    {
        public long Count;
        public override void Write(byte[] buffer, int offset, int count) { inner.Write(buffer, offset, count); Count += count; }
        public override void Write(ReadOnlySpan<byte> buffer) { inner.Write(buffer); Count += buffer.Length; }
        public override void WriteByte(byte value) { inner.WriteByte(value); Count++; }
        public override void Flush() => inner.Flush();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => Count; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
