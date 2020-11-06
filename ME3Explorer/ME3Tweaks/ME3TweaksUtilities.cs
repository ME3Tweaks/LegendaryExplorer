using Microsoft.Win32;

namespace ME3Explorer.ME3Tweaks
{
    /// <summary>
    /// Subset of the Utilities class that is commonly used in ME3Tweaks programs
    /// </summary>
    internal static class ME3TweaksUtilities
    {
        /// <summary>
        /// Gets a string value from the registry from the specified key and value name.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        internal static string GetRegistrySettingString(string key, string name)
        {
            return (string)Registry.GetValue(key, name, null);
        }
    }
}
