using System.Collections.Generic;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Tools.WwiseEditor
{
    /// <summary>
    /// Static Wwise bus ID -> design-time name mappings, per game. Wwise bus IDs (an FNV-1 hash of the bus's
    /// lowercased name) never change once a game ships, so these are embedded rather than loaded from a file.
    /// </summary>
    public static class WwiseBusNames
    {
        /// <summary>
        /// Returns the bus ID -> name map for the given game, or an empty map if none is known.
        /// </summary>
        public static IReadOnlyDictionary<uint, string> ForGame(MEGame game) => game switch
        {
            MEGame.LMP => LE3,
            MEGame.LE3 => LE3,
            _ => Empty
        };

        private static readonly Dictionary<uint, string> Empty = new();

        private static readonly Dictionary<uint, string> LE3 = new()
        {
            { 88325593, "Env-VO-Ambient-Duck" },
            { 113282184, "Slowdown and Bleedout UnAffected" },
            { 222669541, "Env-Snd-1-Powers" },
            { 301674290, "Env-VO-Conversation" },
            { 370644045, "Env-Snd-1-Weapons-Foley-Expand" },
            { 381320459, "Mus-1-Moderate Ducking" },
            { 383753028, "Env-Snd-5-Critical" },
            { 420012082, "Env-VO-Exertions" },
            { 440861076, "Env-Music" },
            { 455854849, "Env-Duck-VO-Amb-NonDuck-DuckedByConvo" },
            { 589371832, "NonEnv-Snd-0-CineDes" },
            { 750429206, "PlayerWeapons-Fire" },
            { 896595700, "Env-Snd-1-Weapons-Foley-Collapse" },
            { 1333022690, "Env-Snd-1-Weapons" },
            { 1384098367, "Ducking Control Buses" },
            { 1428943757, "Env-Snd-0-CineDesign" },
            { 1503374211, "NonSlowdown-Music" },
            { 1578058874, "Env-Snd-1-Bullets" },
            { 1589046909, "NonEnv-VO-Radio-Convo" },
            { 1636967957, "Ducker-Weapons-Assault" },
            { 1656579942, "Env-Snd-1-Physics" },
            { 1712487096, "Env-Snd-4-Explosions" },
            { 1746439591, "Env-Snd-1-Placeables" },
            { 1778000592, "Env-VO-Ambient-NonDuck" },
            { 1787345229, "Music - Death" },
            { 2052984724, "Env-Snd-2-PlayerWeapons" },
            { 2072553425, "NonSlowdown-PlayerCombat" },
            { 2091861047, "NonEnv-VO-Radio-Critical" },
            { 2101962433, "Non-Environmental" },
            { 2122765108, "Ducker-CreatureLarge-Proximity" },
            { 2218411713, "Mus-0-No Ducking - Hubs" },
            { 2238812953, "NonSlowdown-FinaleBink" },
            { 2253961981, "NonSlowdown-GUI Sounds" },
            { 2262755739, "Env-Snd-1-Creatures" },
            { 2339288502, "Env-Snd-1-Amb-Stream" },
            { 2341776094, "Env-VO" },
            { 2413696188, "NonSlowdown-SniperScopes" },
            { 2590885273, "Env-VO-SoundSet-NonDuck" },
            { 2645236474, "Env-Snd-1-Norm-SlowRel" },
            { 2679493409, "Env-Snd-0-LevelEvents" },
            { 2684598355, "Env-Snd-1-Weapons-NonPlayer_Env" },
            { 2724304647, "Env-Snd-1-Amb-NonStream" },
            { 2732954577, "Slowdown and Bleedout Affected" },
            { 2780884782, "NonSlowdown-Dialog" },
            { 2942448014, "Env-Snd-0-NonDucking" },
            { 3033195755, "PlayerWeapons-Env" },
            { 3131594682, "Ducker-MuteAmbiences" },
            { 3203258222, "Mus-0-No Ducking" },
            { 3217963062, "SET YOUR BUS" },
            { 3234900597, "NonSlowdown-Sound Effects" },
            { 3332344874, "Mus-0-No Ducking - Intermittent" },
            { 3366596028, "NonEnv-Music-Score" },
            { 3402299164, "Mus-2-Heavy Ducking" },
            { 3553752469, "Env-Snd-1-VFX" },
            { 3698917345, "NonEnv-Snd-0-NonDucking" },
            { 3707038729, "Ducker-Weapons-Sniper" },
            { 3803692087, "Master Audio Bus" },
            { 3805802625, "NonSlowdown-ShieldDown" },
            { 3877006278, "NonSlowdown-BulletWizzes" },
            { 3922907557, "Enviromental" },
            { 3923201285, "Env-VO-Ambient-Critical" },
            { 3964092493, "Env-Snd-3-CreatureCritical" },
            { 3965866029, "Ducker-Placeables-FullDuck" },
            { 4023386529, "Env-Snd-1-Norm-FastRel" },
            { 4052811563, "PlayerBulletImpacts" },
            { 4054232556, "LevelTransBinks-Duck" },
            { 4130147015, "PlayerWeapons-Bass" },
            { 4244958222, "Env-Snd-1-Weapons-NonPlayer_Fire" },
            { 4260460850, "Env-VO-SoundSet-Duck" },
            { 4265682411, "Env-Snd-2-PlayerPowers" },
            { 4267684745, "NonEnv-VO" },
        };
    }
}
