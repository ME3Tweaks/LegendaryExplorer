using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer.Pathfinding_Editor
{
    class SharedPathfinding
    {
        public static void GenerateNewRandomGUID(IExportEntry export)
        {
            StructProperty guidProp = export.GetProperty<StructProperty>("NavGuid");
            if (guidProp != null)
            {
                Random rnd = new Random();
                IntProperty A = guidProp.GetProp<IntProperty>("A");
                IntProperty B = guidProp.GetProp<IntProperty>("B");
                IntProperty C = guidProp.GetProp<IntProperty>("C");
                IntProperty D = guidProp.GetProp<IntProperty>("D");
                byte[] data = export.Data;

                WriteMem(data, (int)A.Offset, BitConverter.GetBytes(rnd.Next()));
                WriteMem(data, (int)B.Offset, BitConverter.GetBytes(rnd.Next()));
                WriteMem(data, (int)C.Offset, BitConverter.GetBytes(rnd.Next()));
                WriteMem(data, (int)D.Offset, BitConverter.GetBytes(rnd.Next()));
                export.Data = data;
            }
        }

        /// <summary>
        /// Writes the buffer to the memory array starting at position pos
        /// </summary>
        /// <param name="memory">Memory array to overwrite onto</param>
        /// <param name="pos">Position to start writing at</param>
        /// <param name="buff">byte array to write, in order</param>
        /// <returns>Modified memory</returns>
        public static byte[] WriteMem(byte[] memory, int pos, byte[] buff)
        {
            for (int i = 0; i < buff.Length; i++)
                memory[pos + i] = buff[i];

            return memory;
        }

        /// <summary>
        /// Fetches the NavGuid object as a UnrealGUID
        /// </summary>
        /// <param name="export"></param>
        /// <returns></returns>
        public UnrealGUID GetNavGUID(IExportEntry export)
        {
            StructProperty navGuid = export.GetProperty<StructProperty>("NavGuid");
            if (navGuid != null)
            {
                UnrealGUID guid = GetGUIDFromStruct(navGuid);
                guid.export = export;
                return guid;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Fetches an UnrealGUID object from a GUID struct.
        /// </summary>
        /// <param name="guidStruct"></param>
        /// <returns></returns>
        public UnrealGUID GetGUIDFromStruct(StructProperty guidStruct)
        {
            int a = guidStruct.GetProp<IntProperty>("A");
            int b = guidStruct.GetProp<IntProperty>("B");
            int c = guidStruct.GetProp<IntProperty>("C");
            int d = guidStruct.GetProp<IntProperty>("D");
            UnrealGUID guid = new UnrealGUID();
            guid.A = a;
            guid.B = b;
            guid.C = c;
            guid.D = d;
            return guid;
        }
    }

    public class UnrealGUID
    {
        public int A, B, C, D, levelListIndex;
        public IExportEntry export;

        public override bool Equals(Object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;
            UnrealGUID other = (UnrealGUID)obj;
            return other.A == A && other.B == B && other.C == C && other.D == D;
        }

        //public override int GetHashCode()
        //{
        //    return x ^ y;
        //}

        public override string ToString()
        {
            return A.ToString() + " " + B.ToString() + " " + C.ToString() + " " + D.ToString();
        }
    }
}
