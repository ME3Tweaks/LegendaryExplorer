namespace LegendaryExplorer.Themes {
    public enum ThemeType {
        Dark,
        Light,
    }

    public static class ThemeTypeExtension {
        public static string GetName(this ThemeType type) {
            switch (type) {
                case ThemeType.Light:
                    // return "Light"; // Doesn't currently exist
                case ThemeType.Dark:
                    return "SoftDark";
                default:
                    return null;
            }
        }
    }
}