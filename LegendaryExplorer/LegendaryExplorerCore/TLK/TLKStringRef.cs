using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace LegendaryExplorerCore.TLK
{
    /// <summary>
    /// An entry in a tlk.
    /// </summary>
    [DebuggerDisplay("TLKStringRef {StringID} {Data}")]
    public class TLKStringRef : INotifyPropertyChanged, IEquatable<TLKStringRef>, IComparable
    {
        /// <summary>
        /// The StringRef ID this corresponds to
        /// </summary>
        public int StringID { get; set; }
           
        /// <summary>
        /// The string corresponding to <see cref="StringID"/>
        /// </summary>
        public string Data { get; set; }
            
        /// <summary>
        /// Only applicable to ME1/LE1. If equal to 1, this is a valid string ref.
        /// </summary>
        public int Flags { get; private init; }

        /// <summary>
        /// Only applicable to ME1/LE1. Index into the raw strings array. Only meaningful during compression/decompression.
        /// </summary>
        internal int Index { get; set; }

        /// <summary>
        /// The offset into the compressed bit array this string should start being read from during decompression. Only applicable in ME2/3/LE2/3
        /// </summary>
        internal int BitOffset
        {
            get => Flags;
            //use same variable to save memory as flags is not used in me2/3, but bitoffset is.
            private init => Flags = value;
        }

        /// <summary>
        /// Version of <see cref="StringID"/> only used during compression.
        /// </summary>
        internal int CalculatedID => StringID >= 0 ? StringID : -(int.MinValue - StringID);

        /// <summary>
        /// Zero-terminated string that will be written to compressed file. Only used during compression.
        /// </summary>
        internal string ASCIIData
        {
            get
            {
                if (Data == null)
                {
                    return "-1\0";
                }
                if (Data.EndsWith("\0", StringComparison.Ordinal))
                {
                    return Data;
                }
                return Data + '\0';
            }
        }

        /// <summary>
        /// Creates a new <see cref="TLKStringRef"/> by reading from a <see cref="BinaryReader"/>
        /// </summary>
        /// <param name="r"></param>
        /// <param name="me1">Is this from an ME1/LE1 tlk</param>
        internal TLKStringRef(BinaryReader r, bool me1)
        {
            StringID = r.ReadInt32();
            if (me1)
            {
                Flags = r.ReadInt32();
                Index = r.ReadInt32();
            }
            else
            {
                BitOffset = r.ReadInt32();
            }
        }

        /// <summary>
        /// Creates an new <see cref="TLKStringRef"/>
        /// </summary>
        /// <param name="id">The StringRef ID</param>
        /// <param name="data">The string</param>
        /// <param name="flags">Only applicable to ME1/LE1. If equal to 1, this is a valid string ref.</param>
        /// <param name="index">Only applicable to ME1/LE1. Index into the raw strings array. Only meaningful during compression</param>
        public TLKStringRef(int id, string data, int flags = 1, int index = -1)
        {
            StringID = id;
            Flags = flags;
            Data = data;
            Index = index;
        }

        public bool Equals(TLKStringRef other)
        {
            return other is not null && StringID == other.StringID && ASCIIData == other.ASCIIData && Flags == other.Flags /*&& Index == other.Index*/;
        }

        /// <summary>
        /// For sorting
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int CompareTo(object obj)
        {
            var entry = (TLKStringRef)obj;
            return Index.CompareTo(entry.Index);
        }
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}