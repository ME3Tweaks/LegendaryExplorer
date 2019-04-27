using ME3Explorer.Packages;

namespace ME3Explorer
{
    internal interface IExportLoader
    {
        IExportEntry CurrentLoadedExport { get; set; }
        void LoadExport(IExportEntry exportEntry);
        void UnloadExport();
        void PopOut();
    }
}