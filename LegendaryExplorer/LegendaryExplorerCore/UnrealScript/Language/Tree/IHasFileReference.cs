namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public interface IHasFileReference
    {
        public string Name { get; }
        public string FilePath { get; }
        public int UIndex { get; }
    }
}