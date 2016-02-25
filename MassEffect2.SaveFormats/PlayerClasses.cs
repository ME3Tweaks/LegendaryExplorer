using System.Collections.Generic;

namespace MassEffect2.SaveFormats
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

		public static readonly List<PlayerClass> Classes;

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
				new PlayerClass(VanguardClassName, VanguardDisplayName)
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