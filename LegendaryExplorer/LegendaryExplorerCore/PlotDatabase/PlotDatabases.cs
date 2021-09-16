using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

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

        public static ModPlotDatabase Le1ModDatabase;
        public static ModPlotDatabase Le2ModDatabase;
        public static ModPlotDatabase Le3ModDatabase;

        public static PlotDatabase GetDatabaseForGame(MEGame game, bool isBioware)
        {
            return (isBioware ? GetBasegamePlotDatabaseForGame(game) : GetModPlotDatabaseForGame(game));
        }

        public static BasegamePlotDatabase GetBasegamePlotDatabaseForGame(MEGame game)
        {
            if (game.IsGame1()) return Le1PlotDatabase;
            else if (game.IsGame2()) return Le2PlotDatabase;
            else if (game.IsGame3()) return Le3PlotDatabase;
            throw new ArgumentOutOfRangeException($"Game {game} has no plot database");
        }

        public static ModPlotDatabase GetModPlotDatabaseForGame(MEGame game)
        {
            return game switch
            {
                MEGame.LE1 => Le1ModDatabase,
                MEGame.LE2 => Le2ModDatabase,
                MEGame.LE3 => Le3ModDatabase,
                _ => throw new ArgumentOutOfRangeException($"Game {game} has no mod plot database")
            };
        }

        public static SortedDictionary<int, PlotElement> GetMasterDictionaryForGame(MEGame game, bool isBioware = true)
        {
            EnsureDatabaseLoaded(game, isBioware);
            var db = GetDatabaseForGame(game, isBioware);
            return db.GetMasterDictionary();
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
                EnsureDatabaseLoaded(game, false);
                var mdb = GetModPlotDatabaseForGame(game);
                if (mdb.Bools.ContainsKey(id))
                {
                    return mdb.Bools[id];
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
                EnsureDatabaseLoaded(game, false);
                var mdb = GetModPlotDatabaseForGame(game);
                if (mdb.Ints.ContainsKey(id))
                {
                    return mdb.Ints[id];
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
                EnsureDatabaseLoaded(game, false);
                var mdb = GetModPlotDatabaseForGame(game);
                if (mdb.Floats.ContainsKey(id))
                {
                    return mdb.Floats[id];
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
                EnsureDatabaseLoaded(game, false);
                var mdb = GetModPlotDatabaseForGame(game);
                if (mdb.Conditionals.ContainsKey(id))
                {
                    return mdb.Conditionals[id];
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
                EnsureDatabaseLoaded(game, false);
                var mdb = GetModPlotDatabaseForGame(game);
                if (mdb.Transitions.ContainsKey(id))
                {
                    return mdb.Transitions[id];
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

        private static void EnsureDatabaseLoaded(MEGame game, bool isbioware)
        {
            if (GetDatabaseForGame(game, isbioware) == null)
            {
                LoadDatabase(game, isbioware);
            }
        }

        public static bool LoadDatabase(MEGame game, bool isbioware, string appDataPath = null)
        {
            if(!isbioware)
            {
                if (!game.IsLEGame())
                    return false;
                string mdbPath = null;
                var db = new ModPlotDatabase();
                if(appDataPath != null)
                    mdbPath = Path.Combine(appDataPath, $"PlotDBMods{game}.json");
                if(mdbPath != null && File.Exists(mdbPath))
                {
                    db.LoadPlotsFromFile(game, mdbPath);
                    switch (game)
                    {
                        case MEGame.LE1:
                            Le1ModDatabase = db;
                            break;
                        case MEGame.LE2:
                            Le2ModDatabase = db;
                            break;
                        case MEGame.LE3:
                            Le3ModDatabase = db;
                            break;
                    }
                    return true;
                }
                CreateNewModDatabase(game);
            }
            return true;
        }

        public static void CreateNewModDatabase(MEGame game)
        {
            var modDb = ModPlotDatabase.CreateModPlotDatabase(game);
            switch (game)
            {
                case MEGame.LE3:
                    Le3ModDatabase = modDb;
                    break;
                case MEGame.LE2:
                    Le2ModDatabase = modDb;
                    break;
                case MEGame.LE1:
                    Le1ModDatabase = modDb;
                    break;
            }
        }

        public static PlotElement BridgeBasegameAndModDatabases(MEGame game, string modJsonFileDir)
        {
            var roots = new List<PlotElement> { GetBasegamePlotDatabaseForGame(game).Root };
            if (LoadDatabase(game, false, modJsonFileDir))
            {
                roots.Add(GetModPlotDatabaseForGame(game).Root);
            }

            // Do we already have a bridge? (no - we just loaded a new mod database from file)
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
                MEGame.LE1 => 1
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
