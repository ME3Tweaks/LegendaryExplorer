using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Helpers
{
    /// <summary>
    /// An Unreal Engine 3 GUID
    /// </summary>
    public class FGuid
    {
        public readonly int A, B, C, D;
        /// <summary>
        /// The export that contains this FGuid. Only used for identifying the source of this FGuid
        /// </summary>
        public ExportEntry export;

        public FGuid(StructProperty guid)
        {
            if (guid.StructType != "Guid")
            {
                throw new Exception("Can't parse non-guid struct with UnrealGUID");
            }

            A = guid.GetProp<IntProperty>("A");
            B = guid.GetProp<IntProperty>("B");
            C = guid.GetProp<IntProperty>("C");
            D = guid.GetProp<IntProperty>("D");
        }

        public FGuid(Guid guid)
        {
            var ba = guid.ToByteArray();
            A = BitConverter.ToInt32(ba, 0);
            B = BitConverter.ToInt32(ba, 4);
            C = BitConverter.ToInt32(ba, 8);
            D = BitConverter.ToInt32(ba, 12);
        }

        public static bool operator ==(FGuid b1, FGuid b2)
        {
            if (b1 is null)
                return b2 is null;

            return b1.Equals(b2);
        }

        public static bool operator !=(FGuid b1, FGuid b2) => !(b1 == b2);

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;
            FGuid other = (FGuid)obj;
            return other.A == A && other.B == B && other.C == C && other.D == D;
        }

        public override int GetHashCode() => (A, B, C, D).GetHashCode();

        public override string ToString()
        {
            return ToGuid().ToString();
        }

        public Guid ToGuid()
        {
            var ms = new MemoryStream(16);
            ms.WriteInt32(A);
            ms.WriteInt32(B);
            ms.WriteInt32(C);
            ms.WriteInt32(D);
            return new Guid(ms.GetBuffer());
        }
    }
}
