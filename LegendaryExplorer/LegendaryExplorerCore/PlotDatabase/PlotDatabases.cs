using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase.Databases;
using LegendaryExplorerCore.PlotDatabase.PlotElements;

namespace LegendaryExplorerCore.PlotDatabase
{
    public static class PlotDatabases
    {
        public static BasegamePlotDatabase Le1PlotDatabase => LazyLe1PlotDatabase.Value;
        private static readonly Lazy<BasegamePlotDatabase> LazyLe1PlotDatabase = new(() => BasegamePlotDatabase.CreateBasegamePlotDatabase(MEGame.LE1));

        public static BasegamePlotDatabase Le2PlotDatabase => LazyLe2PlotDatabase.Value;
        private static readonly Lazy<BasegamePlotDatabase> LazyLe2PlotDatabase = new(() => BasegamePlotDatabase.CreateBasegamePlotDatabase(MEGame.LE2));

        public static BasegamePlotDatabase Le3PlotDatabase => LazyLe3PlotDatabase.Value;
        private static readonly Lazy<BasegamePlotDatabase> LazyLe3PlotDatabase = new(() => BasegamePlotDatabase.CreateBasegamePlotDatabase(MEGame.LE3));

        public static ModPlotContainer Le1ModContainer { get; } = new ModPlotContainer(MEGame.LE1);
        public static ModPlotContainer Le2ModContainer { get; } = new ModPlotContainer(MEGame.LE2);
        public static ModPlotContainer Le3ModContainer { get; } = new ModPlotContainer(MEGame.LE3);

        public static BasegamePlotDatabase GetBasegamePlotDatabaseForGame(MEGame game)
        {
            if (game.IsGame1()) return Le1PlotDatabase;
            else if (game.IsGame2()) return Le2PlotDatabase;
            else if (game.IsGame3()) return Le3PlotDatabase;
            throw new ArgumentOutOfRangeException($"Game {game} has no plot database");
        }

        public static ModPlotContainer GetModPlotContainerForGame(MEGame game)
        {
            return game switch
            {
                MEGame.LE1 => Le1ModContainer,
                MEGame.LE2 => Le2ModContainer,
                MEGame.LE3 => Le3ModContainer,
                _ => throw new ArgumentOutOfRangeException($"Game {game} has no mod plot database")
            };
        }

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

        public static string FindPlotPathFromID(int id, PlotElementType type, MEGame game)
        {
            return FindPlotElementFromID(id, type, game)?.Path ?? "";
        }

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
