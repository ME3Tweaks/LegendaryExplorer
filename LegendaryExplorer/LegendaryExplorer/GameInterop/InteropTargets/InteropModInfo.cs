namespace LegendaryExplorer.GameInterop.InteropTargets
{
    public record InteropModInfo
    {
        private static int InteropModVersion = 9;
        public string InteropModName { get; }
        public bool CanUseLLE { get; }
        public bool CanUseCamPath { get; init; } = false;
        public bool CanUseAnimViewer { get; }
        public string LiveEditorFilename { get; init; }
        public int Version { get; init; } = InteropModVersion;

        public InteropModInfo(string interopModName, bool canUseLLE, bool canUseAnimViewer = false)
        {
            InteropModName = interopModName;
            CanUseLLE = canUseLLE;
        }
    }
}