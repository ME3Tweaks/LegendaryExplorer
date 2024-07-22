using System;

namespace Be.Windows.Forms
{
    /* TODO: Something to consider: if this class, as well as IByteProvider, were moved to LEC, we could avoid creating a copy of an export's Data array every time an export is displayed in a hex box,
     * by having a GetByteProvider method on ExportEntry. Moving IByteProvider to LEC would make Be.Windows.Forms.HexBox dependent on LEC though, which seems gross.
     * Might be worth it though, since it would avoid a lot of allocations, in PackEd especially.
     */

    /// <summary>
    /// Can be used like a <see cref="DynamicByteProvider"/>, but is optimized for the case where it is never modified
    /// </summary>
    public class ReadOptimizedByteProvider : IByteProvider
    {
        /// <summary>
        /// Contains information about changes.
        /// </summary>
        private bool _hasChanges;

        private bool IsUsingReadOnlyArray;

        private byte[] Bytes;
        private int realLength;

        public long Length => realLength;

        /// <summary>
        /// <paramref name="bytes"/> will never be modified. Any modifications will be made to a copy.
        /// </summary>
        /// <param name="bytes"></param>
        public ReadOptimizedByteProvider(byte[] bytes)
        {
            Bytes = bytes;
            realLength = bytes.Length;
            IsUsingReadOnlyArray = true;
        }

        public ReadOptimizedByteProvider()
        {
            Bytes = [];
        }

        public ReadOnlySpan<byte> Span => Bytes.AsSpan(0, realLength);

        /// <summary>
        /// Reads a byte from the byte collection.
        /// </summary>
        /// <param name="index">the index of the byte to read</param>
        /// <returns>the byte</returns>
        public byte ReadByte(long index) => Bytes[(int)index];

        /// <summary>
        /// Write a byte into the byte collection.
        /// </summary>
        /// <param name="index">the index of the byte to write.</param>
        /// <param name="value">the byte</param>
        public void WriteByte(long index, byte value)
        {
            if (IsUsingReadOnlyArray)
            {
                CopyToNewArray();
            }

            Bytes[(int)index] = value;
            OnChanged();
        }

        /// <summary>
        /// Writes bytes into the byte collection.
        /// </summary>
        /// <param name="index">the index of the bytes to write.</param>
        /// <param name="values">the bytes</param>
        public void WriteBytes(long index, byte[] values)
        {
            if (IsUsingReadOnlyArray)
            {
                CopyToNewArray();
            }
            Buffer.BlockCopy(values, 0, Bytes, (int)index, values.Length);
            OnChanged();
        }

        public void InsertBytes(long index, byte[] bs)
        {
            int newLength = realLength + bs.Length;
            int insertionIndex = (int)index;
            if (IsUsingReadOnlyArray || newLength > Bytes.Length)
            {
                var newCapacity = Math.Max(Bytes.Length * 2, newLength);
                var tmp = new byte[newCapacity];
                Buffer.BlockCopy(Bytes, 0, tmp, 0, insertionIndex);
                Buffer.BlockCopy(bs, 0, tmp, insertionIndex, bs.Length);
                Buffer.BlockCopy(Bytes, insertionIndex, tmp, insertionIndex + bs.Length, realLength - insertionIndex);
                Bytes = tmp;
                IsUsingReadOnlyArray = false;
            }
            else
            {
                Buffer.BlockCopy(Bytes, insertionIndex, Bytes, insertionIndex + bs.Length, realLength - insertionIndex);
                Buffer.BlockCopy(bs, 0, Bytes, insertionIndex, bs.Length);
            }
            realLength = newLength;
            OnLengthChanged();
        }

        public void DeleteBytes(long index, long length)
        {
            if (IsUsingReadOnlyArray)
            {
                CopyToNewArray();
            }
            int lenToDelete = (int)length;
            int deletionRangeBeginOffset = (int)index;
            int deletionRangeEndOffset = deletionRangeBeginOffset + lenToDelete;
            Buffer.BlockCopy(Bytes, deletionRangeEndOffset, Bytes, deletionRangeBeginOffset, realLength - deletionRangeEndOffset);
            realLength -= lenToDelete;
            OnLengthChanged();
        }

        /// <summary>
        /// <paramref name="bytes"/> will never be modified. All modifications will be made to a copy.
        /// </summary>
        /// <param name="bytes"></param>
        public void ReplaceBytes(byte[] bytes)
        {
            int oldLength = realLength;
            Bytes = bytes;
            realLength = bytes.Length;
            IsUsingReadOnlyArray = true;
            if (oldLength == realLength)
            {
                OnChanged();
            }
            else
            {
                OnLengthChanged();
            }
        }

        public void Clear()
        {
            int oldLength = realLength;
            Bytes = Array.Empty<byte>();
            realLength = 0;
            OnLengthChanged();
            if (oldLength == realLength)
            {
                OnChanged();
            }
            else
            {
                OnLengthChanged();
            }
        }

        private void CopyToNewArray()
        {
            var tmp = new byte[realLength];
            Buffer.BlockCopy(Bytes, 0, tmp, 0, realLength);
            Bytes = tmp;
            IsUsingReadOnlyArray = false;
        }
        void OnChanged()
        {
            _hasChanges = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        void OnLengthChanged()
        {
            LengthChanged?.Invoke(this, EventArgs.Empty);
            OnChanged();
        }

        public event EventHandler LengthChanged;

        public event EventHandler Changed;
        public bool HasChanges() => _hasChanges;

        public void ApplyChanges() => _hasChanges = false;
        public bool SupportsWriteByte() => true;

        public bool SupportsInsertBytes() => true;

        public bool SupportsDeleteBytes() => true;
    }
}
