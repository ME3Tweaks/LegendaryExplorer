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
        public static PlotDatabase Le1PlotDatabase;
        public static PlotDatabase Le2PlotDatabase;
        public static PlotDatabase Le3PlotDatabase;
        public static PlotDatabase Le1ModDatabase;
        public static PlotDatabase Le2ModDatabase;
        public static PlotDatabase Le3ModDatabase;

        public static PlotDatabase GetDatabaseForGame(MEGame game, bool isbioware)
        {
            var db = game switch
            {
                MEGame.ME1 => Le1PlotDatabase,
                MEGame.ME2 => Le2PlotDatabase,
                MEGame.ME3 => Le3PlotDatabase,
                MEGame.LE1 => isbioware ? Le1PlotDatabase : Le1ModDatabase,
                MEGame.LE2 => isbioware ? Le2PlotDatabase : Le2ModDatabase,
                MEGame.LE3 => isbioware ? Le3PlotDatabase : Le3ModDatabase,
                _ => throw new ArgumentOutOfRangeException($"Game {game} has no plot database")
            };
            return db;
        }

        public static SortedDictionary<int, PlotElement> GetMasterDictionaryForGame(MEGame game, bool isbioware = true)
        {
            EnsureDatabaseLoaded(game, isbioware);
            var db = GetDatabaseForGame(game, isbioware);
            return db.GetMasterDictionary();
        }

        public static PlotBool FindPlotBoolByID(int id, MEGame game)
        {
            EnsureDatabaseLoaded(game, true);
            var db = GetDatabaseForGame(game, true);
            if (db.Bools.ContainsKey(id))
            {
                return db.Bools[id];
            }

            if (game.IsLEGame())
            {
                EnsureDatabaseLoaded(game, false);
                var mdb = GetDatabaseForGame(game, false);
                if (mdb.Bools.ContainsKey(id))
                {
                    return mdb.Bools[id];
                }
            }
            return null;
        }

        public static PlotElement FindPlotIntByID(int id, MEGame game)
        {
            EnsureDatabaseLoaded(game, true);
            var db = GetDatabaseForGame(game, true);
            if (db.Ints.ContainsKey(id))
            {
                return db.Ints[id];
            }

            if (game.IsLEGame())
            {
                EnsureDatabaseLoaded(game, false);
                var mdb = GetDatabaseForGame(game, false);
                if (mdb.Ints.ContainsKey(id))
                {
                    return mdb.Ints[id];
                }
            }
            return null;
        }

        public static PlotElement FindPlotFloatByID(int id, MEGame game)
        {
            EnsureDatabaseLoaded(game, true);
            var db = GetDatabaseForGame(game, true);
            if (db.Floats.ContainsKey(id))
            {
                return db.Floats[id];
            }

            if (game.IsLEGame())
            {
                EnsureDatabaseLoaded(game, false);
                var mdb = GetDatabaseForGame(game, false);
                if (mdb.Floats.ContainsKey(id))
                {
                    return mdb.Floats[id];
                }
            }
            return null;
        }

        public static PlotConditional FindPlotConditionalByID(int id, MEGame game)
        {
            EnsureDatabaseLoaded(game, true);
            var db = GetDatabaseForGame(game, true);
            if (db.Conditionals.ContainsKey(id))
            {
                return db.Conditionals[id];
            }

            if (game.IsLEGame())
            {
                EnsureDatabaseLoaded(game, false);
                var mdb = GetDatabaseForGame(game, false);
                if (mdb.Conditionals.ContainsKey(id))
                {
                    return mdb.Conditionals[id];
                }
            }
            return null;
        }

        public static PlotTransition FindPlotTransitionByID(int id, MEGame game)
        {
            EnsureDatabaseLoaded(game, true);
            var db = GetDatabaseForGame(game, true);
            if (db.Transitions.ContainsKey(id))
            {
                return db.Transitions[id];
            }

            if (game.IsLEGame())
            {
                EnsureDatabaseLoaded(game, false);
                var mdb = GetDatabaseForGame(game, false);
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
            var db = new PlotDatabase(game, isbioware);
            if(!isbioware)
            {
                if (!game.IsLEGame())
                    return false;
                string mdbPath = null;
                if(appDataPath != null)
                    mdbPath = Path.Combine(appDataPath, $"PlotDBMods{game}.json");
                if(mdbPath != null && File.Exists(mdbPath))
                {
                    db.LoadPlotsFromJSON(game, isbioware, mdbPath);
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
            else
            {
                db.LoadPlotsFromJSON(game, isbioware);
                switch (game)
                {
                    case MEGame.ME1:
                    case MEGame.LE1:
                        Le1PlotDatabase = db;
                        break;
                    case MEGame.ME2:
                    case MEGame.LE2:
                        Le2PlotDatabase = db;
                        break;
                    case MEGame.ME3:
                    case MEGame.LE3:
                        Le3PlotDatabase = db;
                        break;
                }
            }
            return true;
        }

        public static void LoadAllPlotDatabases()
        {
            Le1PlotDatabase = new PlotDatabase(MEGame.LE1, true);
            Le1PlotDatabase.LoadPlotsFromJSON(MEGame.LE1);

            Le2PlotDatabase = new PlotDatabase(MEGame.LE2, true);
            Le2PlotDatabase.LoadPlotsFromJSON(MEGame.LE2);

            Le3PlotDatabase = new PlotDatabase(MEGame.LE3, true);
            Le3PlotDatabase.LoadPlotsFromJSON(MEGame.LE3);
        }

        public static void CreateNewModDatabase(MEGame game)
        {
            switch (game)
            {
                case MEGame.LE3:
                    Le3ModDatabase = new PlotDatabase(MEGame.LE3, false);
                    Le3ModDatabase.Organizational.Add(100000, new PlotElement(0, 100000, "LE3/ME3 Mods", PlotElementType.Region, 0, new List<PlotElement>()));
                    break;
                case MEGame.LE2:
                    Le2ModDatabase = new PlotDatabase(MEGame.LE2, false);
                    Le2ModDatabase.Organizational.Add(100000, new PlotElement(0, 100000, "LE2/ME2 Mods", PlotElementType.Region, 0, new List<PlotElement>()));
                    break;
                case MEGame.LE1:
                    Le1ModDatabase = new PlotDatabase(MEGame.LE1, false);
                    Le1ModDatabase.Organizational.Add(100000, new PlotElement(0, 100000, "LE1/ME1 Mods", PlotElementType.Region, 0, new List<PlotElement>()));
                    break;
            }
        }

    }
}
