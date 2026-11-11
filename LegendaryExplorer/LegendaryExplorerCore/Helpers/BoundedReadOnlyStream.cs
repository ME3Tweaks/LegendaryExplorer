using System;
using System.IO;

namespace LegendaryExplorerCore.Helpers;

/// <summary>
/// A read-only, relative-position view of a seekable stream. Reads and seeks cannot leave the
/// specified range. Disposing the view leaves its source open. Not safe for concurrent source use.
/// </summary>
internal sealed class BoundedReadOnlyStream : Stream
{
    private readonly Stream source;
    private readonly long start;
    private readonly long length;
    private long position;
    private bool disposed;

    public BoundedReadOnlyStream(Stream source, long start, long length)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead || !source.CanSeek) throw new ArgumentException("A readable, seekable stream is required.", nameof(source));
        if (start < 0 || start > source.Length) throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0 || length > source.Length - start) throw new ArgumentOutOfRangeException(nameof(length));
        this.source = source;
        this.start = start;
        this.length = length;
    }

    public override bool CanRead => !disposed;
    public override bool CanSeek => !disposed;
    public override bool CanWrite => false;
    public override long Length { get { ThrowIfDisposed(); return length; } }
    public override long Position
    {
        get { ThrowIfDisposed(); return position; }
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Read(buffer.AsSpan(offset, count));
    }

    public override int ReadByte()
    {
        Span<byte> buffer = stackalloc byte[1];
        return Read(buffer) == 0 ? -1 : buffer[0];
    }

    public override int Read(Span<byte> buffer)
    {
        ThrowIfDisposed();
        int count = (int)Math.Min(buffer.Length, length - position);
        if (count == 0) return 0;
        source.Position = start + position;
        int read = source.Read(buffer[..count]);
        position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();
        long next = checked(origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => position + offset,
            SeekOrigin.End => length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        });
        if (next < 0 || next > length) throw new IOException("Cannot seek outside the bounded stream.");
        return position = next;
    }

    public override void Flush() => ThrowIfDisposed();
    public override void SetLength(long value) => throw new NotSupportedException("This stream is read-only.");
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("This stream is read-only.");
    protected override void Dispose(bool disposing) { disposed = true; base.Dispose(disposing); }
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}
