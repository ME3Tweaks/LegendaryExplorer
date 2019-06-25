using ME3Explorer.Packages;

namespace ME3Explorer
{
    internal interface IExportLoader
    {
        ExportEntry CurrentLoadedExport { get; set; }
        void LoadExport(ExportEntry exportEntry);
        void UnloadExport();
        void PopOut();
    }
}