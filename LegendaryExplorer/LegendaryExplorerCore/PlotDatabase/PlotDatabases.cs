using System;
using System.Collections.Generic;
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
                MEGame.ME1 => isbioware ? Le1PlotDatabase : Le1ModDatabase,
                MEGame.ME2 => isbioware ? Le2PlotDatabase : Le2ModDatabase,
                MEGame.ME3 => isbioware ? Le3PlotDatabase : Le3ModDatabase,
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

            EnsureDatabaseLoaded(game, false);
            var mdb = GetDatabaseForGame(game, false);
            if (mdb.Bools.ContainsKey(id))
            {
                return mdb.Bools[id];
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

            EnsureDatabaseLoaded(game, false);
            var mdb = GetDatabaseForGame(game, false);
            if (mdb.Ints.ContainsKey(id))
            {
                return mdb.Ints[id];
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

            EnsureDatabaseLoaded(game, false);
            var mdb = GetDatabaseForGame(game, false);
            if (mdb.Floats.ContainsKey(id))
            {
                return mdb.Floats[id];
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

            EnsureDatabaseLoaded(game, false);
            var mdb = GetDatabaseForGame(game, false);
            if (mdb.Floats.ContainsKey(id))
            {
                return mdb.Conditionals[id];
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

            EnsureDatabaseLoaded(game, false);
            var mdb = GetDatabaseForGame(game, false);
            if (mdb.Transitions.ContainsKey(id))
            {
                return mdb.Transitions[id];
            }
            return null;
        }

        private static void EnsureDatabaseLoaded(MEGame game, bool isbioware)
        {
            if (GetDatabaseForGame(game, isbioware) == null)
            {
                LoadDatabase(game, isbioware);
            }
        }

        public static void LoadDatabase(MEGame game, bool isbioware)
        {
            var db = new PlotDatabase(game, isbioware);
            if(!isbioware)
            {
                if (!game.IsLEGame()) 
                    return;
                switch (game) //Temp create
                {
                    case MEGame.LE3:
                        Le3ModDatabase = new PlotDatabase();
                        Le3ModDatabase.refGame = MEGame.LE3;
                        Le3ModDatabase.IsBioware = false;
                        Le3ModDatabase.Organizational = new Dictionary<int, PlotElement>();
                        Le3ModDatabase.Organizational.Add(100000, new PlotElement(0, 100000, "LE3/ME3 Mods", PlotElementType.Region, -1, new List<PlotElement>()));
                        break;
                    case MEGame.LE2:
                        Le2ModDatabase = new PlotDatabase();
                        Le2ModDatabase.refGame = MEGame.LE2;
                        Le2ModDatabase.IsBioware = false;
                        Le2ModDatabase.Organizational = new Dictionary<int, PlotElement>();
                        Le2ModDatabase.Organizational.Add(100000, new PlotElement(0, 100000, "LE2/ME2 Mods", PlotElementType.Region, -1, new List<PlotElement>()));
                        break;
                    case MEGame.LE1:
                        Le1ModDatabase = new PlotDatabase();
                        Le1ModDatabase.refGame = MEGame.LE1;
                        Le1ModDatabase.IsBioware = false;
                        Le1ModDatabase.Organizational = new Dictionary<int, PlotElement>();
                        Le1ModDatabase.Organizational.Add(100000, new PlotElement(0, 100000, "LE1/ME1 Mods", PlotElementType.Region, -1, new List<PlotElement>()));
                        break;
                }
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
    }
}
