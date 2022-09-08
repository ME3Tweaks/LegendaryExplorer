namespace LegendaryExplorer.GameInterop.InteropTargets
{
    public record InteropModInfo(string InteropModName)
    {
        private static int InteropModVersion = 9;
        public bool CanUseCamPath { get; init; } = false;
        public bool CanUseAnimViewer { get; init; } = false;
        public string LiveEditorFilename { get; init; }
        public int Version { get; init; } = InteropModVersion;
    }
}