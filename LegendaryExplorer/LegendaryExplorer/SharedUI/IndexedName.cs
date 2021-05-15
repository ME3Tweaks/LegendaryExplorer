using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.SharedUI
{
    /// <summary>
    /// Class that holds a Name, and the index of the name in the name table
    /// </summary>
    public class IndexedName
    {
        public int Index { get; set; }
        public NameReference Name { get; set; }

        public IndexedName(int index, NameReference name)
        {
            Index = index;
            Name = name;
        }

        public override bool Equals(object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || GetType() != obj.GetType())
            {
                return false;
            }
            else
            {
                IndexedName other = (IndexedName)obj;
                return Index == other.Index && Name == other.Name;
            }
        }
    }
}
