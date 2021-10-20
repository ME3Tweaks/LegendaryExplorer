using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class Bio2DABinary : ObjectBinary
    {
        public bool IsIndexed;
        public OrderedMultiValueDictionary<int, Cell> Cells;
        public List<NameReference> ColumnNames;

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.IsLoading)
            {
                IsIndexed = !sc.ms.ReadBoolInt();
                sc.ms.Skip(-4);
            }
            else
            {
                //If there are no cells, IsIndexed has to be true, since there is no way to differentiate between 
                //a cell count of zero and the extra zero that is present when IsIndexed is true.
                IsIndexed |= Cells.IsEmpty();
            }

            if (IsIndexed)
            {
                sc.SerializeConstInt(0);
            }

            int cellIndex = 0;
            //If IsIndexed, the index needs to be read and written, so just use the normal Serialize for ints.
            //If it's not indexed, we don't need to write anything, but the Dictionary still needs to be populated with a value
            sc.Serialize(ref Cells, IsIndexed ? SCExt.Serialize : (SerializingContainer2 sc2, ref int idx) => idx = cellIndex++, SCExt.Serialize);
            if (!IsIndexed)
            {
                sc.SerializeConstInt(0);
            }

            int count = ColumnNames?.Count ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                int index = 0;
                ColumnNames = new List<NameReference>(count);
                for (int i = 0; i < count; i++)
                {
                    NameReference tmp = default;
                    sc.Serialize(ref tmp);
                    sc.Serialize(ref index);
                    ColumnNames.Add(tmp);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    NameReference tmp = ColumnNames[i];
                    sc.Serialize(ref tmp);
                    sc.Serialize(ref i);
                }
            }
        }

        public static Bio2DABinary Create()
        {
            return new()
            {
                Cells = new OrderedMultiValueDictionary<int, Cell>(),
                ColumnNames = new List<NameReference>()
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game) => ColumnNames.Select((n, i) => (n, $"{ColumnNames}[{i}]")).ToList();

        public enum DataType : byte
        {
            INT = 0,
            NAME = 1,
            FLOAT = 2
        }

        public class Cell
        {

            public DataType Type;
            public int IntValue;
            public NameReference NameValue;
            public float FloatValue;
        }
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref Bio2DABinary.Cell cell)
        {
            if (sc.IsLoading)
            {
                cell = new Bio2DABinary.Cell
                {
                    Type = (Bio2DABinary.DataType)sc.ms.ReadByte()
                };
            }
            else
            {
                sc.ms.Writer.WriteByte((byte)cell.Type);
            }
            switch (cell.Type)
            {
                case Bio2DABinary.DataType.INT:
                    sc.Serialize(ref cell.IntValue);
                    break;
                case Bio2DABinary.DataType.NAME:
                    sc.Serialize(ref cell.NameValue);
                    break;
                case Bio2DABinary.DataType.FLOAT:
                    sc.Serialize(ref cell.FloatValue);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}