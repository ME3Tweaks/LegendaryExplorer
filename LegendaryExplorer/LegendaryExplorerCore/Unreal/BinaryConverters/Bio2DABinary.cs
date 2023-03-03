using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.Collections;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class Bio2DABinary : ObjectBinary
    {
        public bool IsIndexed;
        public UMultiMap<int, Bio2DACell> Cells; //TODO: Make this a UMap
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
                IsIndexed |= Cells.Count == 0;
            }

            if (IsIndexed)
            {
                sc.SerializeConstInt(0);
            }

            int cellIndex = 0;
            //If IsIndexed, the index needs to be read and written, so just use the normal Serialize for ints.
            //If it's not indexed, we don't need to write anything, but the Dictionary still needs to be populated with a value
            sc.Serialize(ref Cells, IsIndexed ? SCExt.Serialize : (SerializingContainer2 _, ref int idx) => idx = cellIndex++, SCExt.Serialize);
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
                Cells = new(),
                ColumnNames = new List<NameReference>()
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game) => ColumnNames.Select((n, i) => (n, $"{ColumnNames}[{i}]")).ToList();


        //public enum DataType : byte
        //{
        //    INT = 0,
        //    NAME = 1,
        //    FLOAT = 2
        //}

        //public class Cell
        //{

        //    public DataType Type;
        //    public int IntValue;
        //    public NameReference NameValue;
        //    public float FloatValue;
        //}
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref Bio2DACell cell)
        {
            if (sc.IsLoading)
            {
                cell = new Bio2DACell
                {
                    Type = (Bio2DACell.Bio2DADataType)sc.ms.ReadByte()
                };
            }
            else
            {
                if (cell.Type == Bio2DACell.Bio2DADataType.TYPE_NULL) return; // DO NOT SERIALIZE!
                sc.ms.Writer.WriteByte((byte)cell.Type);
            }
            switch (cell.Type)
            {
                case Bio2DACell.Bio2DADataType.TYPE_INT:
                    int intV = cell.IntValue;
                    sc.Serialize(ref intV);
                    if (sc.IsLoading)
                        cell.IntValue = intV;
                    break;
                case Bio2DACell.Bio2DADataType.TYPE_NAME:
                    NameReference nameV = cell.NameValue;
                    sc.Serialize(ref nameV);
                    if (sc.IsLoading)
                        cell.NameValue = nameV;
                    break;
                case Bio2DACell.Bio2DADataType.TYPE_FLOAT:
                    float floatV = cell.FloatValue;
                    sc.Serialize(ref floatV);
                    if (sc.IsLoading)
                        cell.FloatValue = floatV;
                    break;
                case Bio2DACell.Bio2DADataType.TYPE_NULL:
                    // THIS CELL TYPE IS NOT SERIALIZED AND IS LEC INTERNAL ONLY (so it cell can be populated)
                    if (sc.IsLoading)
                        throw new Exception("Malformed 2DA: NULL cell was written at some point in the past!");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}