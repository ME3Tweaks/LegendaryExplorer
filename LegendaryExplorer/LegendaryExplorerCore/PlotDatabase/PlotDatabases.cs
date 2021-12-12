using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase.Databases;
using LegendaryExplorerCore.PlotDatabase.PlotElements;

namespace LegendaryExplorerCore.PlotDatabase
{
    /// <summary>
    /// Manages plot element databases for all games. Contains static methods used to lookup plot element info by ID.
    /// </summary>
    public static class PlotDatabases
    {
        /// <summary>Gets the Basegame Plot Database for LE1</summary>
        public static BasegamePlotDatabase Le1PlotDatabase => LazyLe1PlotDatabase.Value;
        private static readonly Lazy<BasegamePlotDatabase> LazyLe1PlotDatabase = new(() => new BasegamePlotDatabase(MEGame.LE1));

        /// <summary>Gets the Basegame Plot Database for LE2</summary>
        public static BasegamePlotDatabase Le2PlotDatabase => LazyLe2PlotDatabase.Value;
        private static readonly Lazy<BasegamePlotDatabase> LazyLe2PlotDatabase = new(() => new BasegamePlotDatabase(MEGame.LE2));

        /// <summary>Gets the Basegame Plot Database for LE3</summary>
        public static BasegamePlotDatabase Le3PlotDatabase => LazyLe3PlotDatabase.Value;
        private static readonly Lazy<BasegamePlotDatabase> LazyLe3PlotDatabase = new(() => new BasegamePlotDatabase(MEGame.LE3));

        /// <summary>Gets the Mod Plot Database container for LE1</summary>
        public static ModPlotContainer Le1ModContainer { get; } = new ModPlotContainer(MEGame.LE1);

        /// <summary>Gets the Mod Plot Database container for LE2</summary>
        public static ModPlotContainer Le2ModContainer { get; } = new ModPlotContainer(MEGame.LE2);

        /// <summary>Gets the Mod Plot Database container for LE3</summary>
        public static ModPlotContainer Le3ModContainer { get; } = new ModPlotContainer(MEGame.LE3);

        /// <summary>
        /// Gets the <see cref="BasegamePlotDatabase"/> associated with the input game
        /// </summary>
        /// <param name="game">Game</param>
        /// <returns>Basegame plot database for game</returns>
        /// <exception cref="ArgumentException">Game has no plot database</exception>
        public static BasegamePlotDatabase GetBasegamePlotDatabaseForGame(MEGame game)
        {
            if (game.IsGame1()) return Le1PlotDatabase;
            else if (game.IsGame2()) return Le2PlotDatabase;
            else if (game.IsGame3()) return Le3PlotDatabase;
            throw new ArgumentException($"Game {game} has basegame plot database", nameof(game));
        }

        /// <summary>
        /// Gets the <see cref="ModPlotContainer"/> associated with the input game
        /// </summary>
        /// <param name="game">Game</param>
        /// <returns>Mod plot container for game</returns>
        /// <exception cref="ArgumentException">Game has no mod plot container</exception>
        public static ModPlotContainer GetModPlotContainerForGame(MEGame game)
        {
            return game switch
            {
                MEGame.LE1 => Le1ModContainer,
                MEGame.LE2 => Le2ModContainer,
                MEGame.LE3 => Le3ModContainer,
                _ => throw new ArgumentException($"Game {game} has no mod plot database", nameof(game))
            };
        }

        /// <summary>
        /// Finds a boolean plot element in any loaded database that matches the given ID
        /// </summary>
        /// <param name="id">Plot bool id</param>
        /// <param name="game">Game to search in</param>
        /// <returns>PlotBool that matches ID, <c>null</c> if not found</returns>
        public static PlotBool FindPlotBoolByID(int id, MEGame game)
        {
            var db = GetBasegamePlotDatabaseForGame(game);
            if (db.Bools.ContainsKey(id))
            {
                return db.Bools[id];
            }

            if (game.IsLEGame())
            {
                foreach (var mdb in GetModPlotContainerForGame(game).Mods)
                {
                    if (mdb.Bools.ContainsKey(id))
                    {
                        return mdb.Bools[id];
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds an integer plot element in any loaded database that matches the given ID
        /// </summary>
        /// <param name="id">Plot int id</param>
        /// <param name="game">Game to search in</param>
        /// <returns>PlotElement that matches ID, <c>null</c> if not found</returns>
        public static PlotElement FindPlotIntByID(int id, MEGame game)
        {
            var db = GetBasegamePlotDatabaseForGame(game);
            if (db.Ints.ContainsKey(id))
            {
                return db.Ints[id];
            }

            if (game.IsLEGame())
            {
                foreach (var mdb in GetModPlotContainerForGame(game).Mods)
                {
                    if (mdb.Ints.ContainsKey(id))
                    {
                        return mdb.Ints[id];
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds a float plot element in any loaded database that matches the given ID
        /// </summary>
        /// <param name="id">Plot float id</param>
        /// <param name="game">Game to search in</param>
        /// <returns>PlotElement that matches ID, <c>null</c> if not found</returns>
        public static PlotElement FindPlotFloatByID(int id, MEGame game)
        {
            var db = GetBasegamePlotDatabaseForGame(game);
            if (db.Floats.ContainsKey(id))
            {
                return db.Floats[id];
            }

            if (game.IsLEGame())
            {
                foreach (var mdb in GetModPlotContainerForGame(game).Mods)
                {
                    if (mdb.Floats.ContainsKey(id))
                    {
                        return mdb.Floats[id];
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds a conditional plot element in any loaded database that matches the given ID
        /// </summary>
        /// <param name="id">Plot conditional id</param>
        /// <param name="game">Game to search in</param>
        /// <returns>PlotConditional that matches ID, <c>null</c> if not found</returns>
        public static PlotConditional FindPlotConditionalByID(int id, MEGame game)
        {
            var db = GetBasegamePlotDatabaseForGame(game);
            if (db.Conditionals.ContainsKey(id))
            {
                return db.Conditionals[id];
            }

            if (game.IsLEGame())
            {
                foreach (var mdb in GetModPlotContainerForGame(game).Mods)
                {
                    if (mdb.Conditionals.ContainsKey(id))
                    {
                        return mdb.Conditionals[id];
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds a transition plot element in any loaded database that matches the given ID
        /// </summary>
        /// <param name="id">Plot transition id</param>
        /// <param name="game">Game to search in</param>
        /// <returns>PlotTransition that matches ID, <c>null</c> if not found</returns>
        public static PlotTransition FindPlotTransitionByID(int id, MEGame game)
        {
            var db = GetBasegamePlotDatabaseForGame(game);
            if (db.Transitions.ContainsKey(id))
            {
                return db.Transitions[id];
            }

            if (game.IsLEGame())
            {
                foreach (var mdb in GetModPlotContainerForGame(game).Mods)
                {
                    if (mdb.Transitions.ContainsKey(id))
                    {
                        return mdb.Transitions[id];
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds the first plot element of the given type with the given plot id in any loaded database
        /// </summary>
        /// <remarks>This does not search for Organizational plot elements (non-game states)</remarks>
        /// <param name="id">PlotID of desired element</param>
        /// <param name="type">Type of element</param>
        /// <param name="game">Game to search in</param>
        /// <returns>PlotElement that matches ID and type, <c>null</c> if not found</returns>
        public static PlotElement FindPlotElementFromID(int id, PlotElementType type, MEGame game)
        {
            switch (type)
            {
                case PlotElementType.Flag:
                case PlotElementType.State:
                case PlotElementType.SubState:
                    return FindPlotBoolByID(id, game);
                case PlotElementType.Integer:
                    return FindPlotIntByID(id, game);
                case PlotElementType.Float:
                    return FindPlotFloatByID(id, game);
                case PlotElementType.Conditional:
                    return FindPlotConditionalByID(id, game);
                case PlotElementType.Transition:
                case PlotElementType.Consequence:
                    return FindPlotTransitionByID(id, game);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Finds the path of the first plot element of the given type with the given plot id in any loaded database
        /// </summary>
        /// <remarks>This does not search for Organizational plot elements (non-game states)</remarks>
        /// <param name="id">PlotID of desired element</param>
        /// <param name="type">Type of element</param>
        /// <param name="game">Game to search in</param>
        /// <returns>Path of plot element matching ID and type, empty string if not found</returns>
        public static string FindPlotPathFromID(int id, PlotElementType type, MEGame game)
        {
            return FindPlotElementFromID(id, type, game)?.Path ?? "";
        }

        /// <summary>
        /// Returns the <see cref="PlotDatabasebase"/> that contains a given PlotElement
        /// </summary>
        /// <param name="el">Element to locate database for</param>
        /// <param name="game">Game to search in</param>
        /// <returns>Database containing the given plot element. Defaults to basegame database</returns>
        public static PlotDatabaseBase GetDatabaseContainingElement(PlotElement el, MEGame game)
        {
            PlotElement e = el;
            while (e != null && e.Parent != null)
            {
                if (e is PlotModElement pme)
                {
                    return GetModPlotContainerForGame(game).Mods.FirstOrDefault(m => m.ModRoot == pme);
                }
                e = e.Parent;
            }
            return GetBasegamePlotDatabaseForGame(game);
        }

        /// <summary>
        /// Gets or creates a plot element that is the parent of all PlotElements for a single game, basegame and mod-added
        /// </summary>
        /// <remarks>This element is used as the root for the plot TreeView in the PlotDatabase tool</remarks>
        /// <param name="game">Game to get root element for</param>
        /// <param name="appdataFolder">Application AppData folder to load mod databases from</param>
        /// <returns>Root plot element</returns>
        public static PlotElement BridgeBasegameAndModDatabases(MEGame game, string appdataFolder)
        {
            var mpc = GetModPlotContainerForGame(game);
            if(mpc.Mods.IsEmpty()) mpc.LoadModsFromDisk(appdataFolder);

            var roots = new List<PlotElement> { GetBasegamePlotDatabaseForGame(game).Root, mpc.GameHeader };

            // Do we already have a bridge?
            if (roots.Count > 0 && roots.TrueForAll(r => r.Parent != null && r.Parent == roots[0].Parent))
            {
                return roots[0].Parent;
            }
            else
            {
                var gameRootPlot = GetNewRootPlotElement(game, roots, true);
                return gameRootPlot;
            }
        }

        /// <summary>
        /// Creates a new "root" plot element to contain all basegame and mod elements for a single game
        /// </summary>
        /// <param name="game">Game to create root element for</param>
        /// <param name="children">List of children for the new root element</param>
        /// <param name="assignParents">If <c>true</c>, all children will have their parents assigned to new root</param>
        /// <returns>New root element</returns>
        /// <exception cref="ArgumentException">Game is not LegendaryEdition</exception>
        public static PlotElement GetNewRootPlotElement(MEGame game, List<PlotElement> children, bool assignParents = false)
        {
            var gameNumber = game switch
            {
                MEGame.LE3 => 3,
                MEGame.LE2 => 2,
                MEGame.LE1 => 1,
                _ => throw new ArgumentException("Cannot create root plot element for non-LE game", nameof(game))
            };
            var plotParent = new PlotElement(0, 0, $"Legendary Edition - Mass Effect {gameNumber} Plots",
                PlotElementType.None, null, children);

            if (assignParents)
            {
                foreach (var el in children) el.AssignParent(plotParent);
            }

            return plotParent;
        }
    }
}
