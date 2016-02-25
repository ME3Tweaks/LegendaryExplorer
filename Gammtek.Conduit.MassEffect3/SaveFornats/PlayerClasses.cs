using System.Collections.Generic;

namespace MassEffect3.SaveFormats
{
	public sealed class PlayerClasses
	{
		public const int AdeptDisplayName = 93954;
		public const int EngineerDisplayName = 93953;
		public const int InfiltratorDisplayName = 93955;
		public const int SentinelDisplayName = 93957;
		public const int SoldierDisplayName = 93952;
		public const int VanguardDisplayName = 93956;

		public const string AdeptClassName = "SFXGame.SFXPawn_PlayerAdept";
		public const string EngineerClassName = "SFXGame.SFXPawn_PlayerEngineer";
		public const string InfiltratorClassName = "SFXGame.SFXPawn_PlayerInfiltrator";
		public const string SentinelClassName = "SFXGame.SFXPawn_PlayerSentinel";
		public const string SoldierClassName = "SFXGame.SFXPawn_PlayerSoldier";
		public const string VanguardClassName = "SFXGame.SFXPawn_PlayerVanguard";

		public const string AdeptNonCombatClassName = "SFXGame.SFXPawn_PlayerAdeptNonCombat";
		public const string EngineerNonCombatClassName = "SFXGame.SFXPawn_PlayerEngineerNonCombat";
		public const string InfiltratorNonCombatClassName = "SFXGame.SFXPawn_PlayerInfiltratorNonCombat";
		public const string SentinelNonCombatClassName = "SFXGame.SFXPawn_PlayerSentinelNonCombat";
		public const string SoldierNonCombatClassName = "SFXGame.SFXPawn_PlayerSoldierNonCombat";
		public const string VanguardNonCombatClassName = "SFXGame.SFXPawn_PlayerVanguardNonCombat";

		public static readonly List<PlayerClass> Classes;
		//public static readonly Dictionary<> 

		static PlayerClasses()
		{
			Classes = new List<PlayerClass>
			{
				// Combat
				new PlayerClass(AdeptClassName, AdeptDisplayName),
				new PlayerClass(EngineerClassName, EngineerDisplayName),
				new PlayerClass(InfiltratorClassName, InfiltratorDisplayName),
				new PlayerClass(SentinelClassName, SentinelDisplayName),
				new PlayerClass(SoldierClassName, SoldierDisplayName),
				new PlayerClass(VanguardClassName, VanguardDisplayName),

				// Non-Combat
				new PlayerClass(AdeptNonCombatClassName, AdeptDisplayName),
				new PlayerClass(EngineerNonCombatClassName, EngineerDisplayName),
				new PlayerClass(InfiltratorNonCombatClassName, InfiltratorDisplayName),
				new PlayerClass(SentinelNonCombatClassName, SentinelDisplayName),
				new PlayerClass(SoldierNonCombatClassName, SoldierDisplayName),
				new PlayerClass(VanguardNonCombatClassName, VanguardDisplayName)
			};
		}

		public class PlayerClass
		{
			public PlayerClass(string className, int displayName)
			{
				ClassName = className;
				DisplayName = displayName;
			}

			public string ClassName { get; set; }
			public int DisplayName { get; set; }
		}
	}
}