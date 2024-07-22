using System;

namespace LegendaryExplorerCore.Packages
{
    /// <summary>
    /// Indicates a game in the Mass Effect series
    /// </summary>
    /// <remarks><see cref="MEGameExtensions">Extension Methods</see></remarks>
    public enum MEGame
    {
        /// <summary>Unknown game</summary>
        /// <remarks>Used when a game is not able to be determined</remarks>
        Unknown = 0,
        /// <summary>Mass Effect (2007)</summary>
        ME1 = 1,
        /// <summary>Mass Effect 2 (2010)</summary>
        ME2 = 2,
        /// <summary>Mass Effect 3 (2012)</summary>
        ME3 = 3,
        /// <summary>Mass Effect: Legendary Edition</summary>
        LE1 = 4,
        /// <summary>Mass Effect 2: Legendary Edition</summary>
        LE2 = 5,
        /// <summary>Mass Effect 3: Legendary Edition</summary>
        LE3 = 6,
        /// <summary>Unreal Development Kit</summary>
        UDK = 7,

        /// <summary>Mass Effect Legendary Edition Launcher</summary>
        /// <remarks>
        /// Not an actual game, but used for identifying Legendary Edition game directories.
        /// Do not change this number. It's so we can add entries before it without messing up any existing items.
        /// </remarks>
        LELauncher = 100,
    }

    /// <summary>
    /// Extension methods for the <see cref="MEGame"/> enum
    /// </summary>
    public static class MEGameExtensions
    {
        /// <summary>
        /// Is game part of Legendary Edition (not including UDK)
        /// </summary>
        /// <param name="game">Input game</param>
        /// <returns>True if game is LE</returns>
        public static bool IsLEGame(this MEGame game) => game is MEGame.LE1 or MEGame.LE2 or MEGame.LE3;

        /// <summary>
        /// Is game part of original trilogy (not including UDK)
        /// </summary>
        /// <param name="game">Input game</param>
        /// <returns>True if game is OT</returns>
        public static bool IsOTGame(this MEGame game) => game is MEGame.ME1 or MEGame.ME2 or MEGame.ME3;

        /// <summary>
        /// Is game a Mass Effect game (original trilogy or Legendary Edition)
        /// </summary>
        /// <param name="game">Input game</param>
        /// <returns>True if game is OT</returns>
        public static bool IsMEGame(this MEGame game) => game is MEGame.ME1 or MEGame.ME2 or MEGame.ME3 or MEGame.LE1 or MEGame.LE2 or MEGame.LE3;

        /// <summary>
        /// Is game a version of Mass Effect 1
        /// </summary>
        /// <param name="game">Input game</param>
        /// <returns>True if Game 1</returns>
        public static bool IsGame1(this MEGame game) => game is MEGame.ME1 or MEGame.LE1;

        /// <summary>
        /// Is game a version of Mass Effect 2
        /// </summary>
        /// <param name="game">Input game</param>
        /// <returns>True if Game 2</returns>
        public static bool IsGame2(this MEGame game) => game is MEGame.ME2 or MEGame.LE2;

        /// <summary>
        /// Is game a version of Mass Effect 3
        /// </summary>
        /// <param name="game">Input game</param>
        /// <returns>True if Game 3</returns>
        public static bool IsGame3(this MEGame game) => game is MEGame.ME3 or MEGame.LE3;

        /// <summary>
        /// Turns an MEGame to it's OT variant, if game is LE
        /// </summary>
        /// <remarks>This method can return LELauncher</remarks>
        /// <param name="game">Input game</param>
        /// <returns>Game that will not be LE</returns>
        public static MEGame ToOTVersion(this MEGame game)
        {
            if (game == MEGame.LE1) return MEGame.ME1;
            if (game == MEGame.LE2) return MEGame.ME2;
            if (game == MEGame.LE3) return MEGame.ME3;
            return game;
        }

        /// <summary>
        /// Turns an MEGame to it's LE variant, if game is OT
        /// </summary>
        /// <param name="game">Input game</param>
        /// <returns>Game that will not be OT</returns>
        public static MEGame ToLEVersion(this MEGame game)
        {
            if (game == MEGame.ME1) return MEGame.LE1;
            if (game == MEGame.ME2) return MEGame.LE2;
            if (game == MEGame.ME3) return MEGame.LE3;
            return game;
        }

        /// <summary>
        /// Gets the other generation version of this game. Does not work on anything but OT/LE games.
        /// </summary>
        /// <param name="game">Input game</param>
        /// <returns>Game that will not be this generation</returns>
        public static MEGame ToOppositeGeneration(this MEGame game)
        {
            if (game.IsOTGame()) return ToLEVersion(game);
            if (game.IsLEGame()) return ToOTVersion(game);
            return game;
        }

        /// <summary>
        /// Returns the name of the CookedPC directory for a specific game
        /// </summary>
        /// <remarks>This method is different to <see cref="LegendaryExplorerCore.GameFilesystem.MEDirectories.CookedName"/>,
        /// though they should produce the same result.</remarks>
        /// <param name="game">Input game</param>
        /// <returns>String of CookedDir name</returns>
        /// <exception cref="Exception">Game has no CookedPC directory</exception>
        public static string CookedDirName(this MEGame game) => game switch
        {
            MEGame.ME1 => "CookedPC",
            MEGame.ME2 => "CookedPC",
            MEGame.UDK => throw new Exception($"{game} does not support CookedDirName()"),
            MEGame.LELauncher => throw new Exception($"{game} does not support CookedDirName()"),
            _ => "CookedPCConsole"
        };

        /// <summary>
        /// Gets the standard file extension for a package for the specified PC game
        /// </summary>
        /// <param name="game">The game to check</param>
        /// <param name="isMapFile">ME1 ONLY: If this is a map file like BIOA_STA00</param>
        /// <param name="isScriptFile">ME1 ONLY: If this is a script file like BIOC_Base</param>
        /// <returns>File extension including the dot</returns>
        public static string PCPackageFileExtension(this MEGame game, bool isMapFile = false, bool isScriptFile = false) => game switch
        {
            MEGame.ME1 when isMapFile => ".sfm",
            MEGame.ME1 when isScriptFile => ".u",
            MEGame.ME1 => ".upk",
            MEGame.LELauncher => throw new Exception($"{game} does not support PCPackageFileExtension()"),
            _ => ".pcc"
        };
    }
}