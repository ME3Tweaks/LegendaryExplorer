using System.Collections.Generic;

namespace MassEffect2.SaveEdit.Squad
{
	public static class Powers
	{
		// ReSharper disable InconsistentNaming
		public static PowerClass AdeptFitness { get; private set; }
		public static PowerClass AdpetPassive { get; private set; }
		public static PowerClass AdrenalineRush { get; private set; }
		public static PowerClass AndersonPassive { get; private set; }
		public static PowerClass AriaPassive { get; private set; }
		public static PowerClass ArmorPiercingAmmo { get; private set; }
		public static PowerClass AshleyPassive { get; private set; }
		public static PowerClass Barrier { get; private set; }
		public static PowerClass BioticCharge { get; private set; }
		public static PowerClass BioticProtector { get; private set; }
		public static PowerClass Carnage { get; private set; }
		public static PowerClass ClusterGrenade { get; private set; }
		public static PowerClass CombatDrone { get; private set; }
		public static PowerClass ConcussiveShot { get; private set; }
		public static PowerClass CryoAmmo { get; private set; }
		public static PowerClass CryoBlast { get; private set; }
		public static PowerClass DarkChannel { get; private set; }
		public static PowerClass Decoy { get; private set; }
		public static PowerClass DefenseDrone { get; private set; }
		public static PowerClass DefenseMatrix { get; private set; }
		public static PowerClass DisruptorAmmo { get; private set; }
		public static PowerClass Dominate { get; private set; }
		public static PowerClass EDIPassive { get; private set; }
		public static PowerClass EnergyDrain { get; private set; }
		public static PowerClass EngineerFitness { get; private set; }
		public static PowerClass EngineerPassive { get; private set; }
		public static PowerClass Flare { get; private set; }
		public static PowerClass Fortification { get; private set; }
		public static PowerClass FragGrenade { get; private set; }
		public static PowerClass GarrusPassive { get; private set; }
		public static PowerClass GruntPassive { get; private set; }
		public static PowerClass IncendiaryAmmo { get; private set; }
		public static PowerClass Incinerate { get; private set; }
		public static PowerClass InfernoGrenade { get; private set; }
		public static PowerClass InfiltratorFitness { get; private set; }
		public static PowerClass InfiltratorPassive { get; private set; }
		public static PowerClass JacobPassive { get; private set; }
		public static PowerClass JackPassive { get; private set; }
		public static PowerClass JamesPassive { get; private set; }
		public static PowerClass JavikPassive { get; private set; }
		public static PowerClass KaidanPassive { get; private set; }
		public static PowerClass KasumiPassive { get; private set; }
		public static PowerClass Lash { get; private set; }
		public static PowerClass LiaraPassive { get; private set; }
		public static PowerClass LiftGrenade { get; private set; }
		public static PowerClass Marksman { get; private set; }
		public static PowerClass MirandaPassive { get; private set; }
		public static PowerClass Nova { get; private set; }
		public static PowerClass NyreenPassive { get; private set; }
		public static PowerClass Overload { get; private set; }
		public static PowerClass Pull { get; private set; }
		public static PowerClass ProximityMine { get; private set; }
		public static PowerClass Reave { get; private set; }
		public static PowerClass Sabotage { get; private set; }
		public static PowerClass SamaraPassive { get; private set; }
		public static PowerClass SentinelFitness { get; private set; }
		public static PowerClass SentinelPassive { get; private set; }
		public static PowerClass SentryTurret { get; private set; }
		public static PowerClass Shockwave { get; private set; }
		public static PowerClass Singularity { get; private set; }
		public static PowerClass Slam { get; private set; }
		public static PowerClass SoldierFitness { get; private set; }
		public static PowerClass SoldierPassive { get; private set; }
		public static PowerClass Stasis { get; private set; }
		public static PowerClass StickyGrenade { get; private set; }
		public static PowerClass StimulantPack { get; private set; }
		public static PowerClass TacticalCloak { get; private set; }
		public static PowerClass TaliPassive { get; private set; }
		public static PowerClass TechArmor { get; private set; }
		public static PowerClass Throw { get; private set; }
		public static PowerClass Unity { get; private set; }
		public static PowerClass VanguardFitness { get; private set; }
		public static PowerClass VanguardPassive { get; private set; }
		public static PowerClass Warp { get; private set; }
		public static PowerClass WarpAmmo { get; private set; }
		public static PowerClass WrexPassive { get; private set; }
		public static PowerClass ZaeedPassive { get; private set; }

		public static Dictionary<PowerId, PowerClass> Classes { get; private set; }
		// ReSharper restore InconsistentNaming

		static Powers()
		{
			AdeptFitness = new PowerClass("SFXGameContent.SFXPowerCustomAction_AdeptMeleePassive", "AdeptMeleePassive");
			AdpetPassive = new PowerClass("SFXGameContent.SFXPowerCustomAction_AdeptPassive", "AdeptPassive");
			AdrenalineRush = new PowerClass("SFXGameContent.SFXPowerCustomAction_AdrenalineRush", "AdrenalineRush");
			AndersonPassive = new PowerClass("SFXGameContent.SFXPowerCustomAction_AndersonPassive", "AndersonPassive");
			AriaPassive = new PowerClass("SFXGameContentDLC_EXP_Pack002.SFXPowerCustomAction_AriaPassive", "AriaPassive");
			ArmorPiercingAmmo = new PowerClass("SFXGameContent.SFXPowerCustomAction_ArmorPiercingAmmo", "ArmorPiercingAmmo");
			AshleyPassive = new PowerClass("SFXGameContent.SFXPowerCustomAction_AshleyPassive", "AshleyPassive");
			Barrier = new PowerClass("SFXGameContent.SFXPowerCustomAction_Barrier", "Barrier");
			BioticCharge = new PowerClass("SFXGameContent.SFXPowerCustomAction_BioticCharge", "BioticCharge");
			BioticProtector = new PowerClass("SFXGameContentDLC_EXP_Pack002.SFXPowerCustomAction_NyreenBubbleShield", "BubbleShield");
			Carnage = new PowerClass("SFXGameContent.SFXPowerCustomAction_Carnage", "Carnage");
			ClusterGrenade = new PowerClass("SFXGameContent.SFXPowerCustomAction_BioticGrenade", "BioticGrenade");
			CombatDrone = new PowerClass("SFXGameContent.SFXPowerCustomAction_CombatDrone", "CombatDrone");
			ConcussiveShot = new PowerClass("SFXGameContent.SFXPowerCustomAction_ConcussiveShot", "ConcussiveShot");
			CryoAmmo = new PowerClass("SFXGameContent.SFXPowerCustomAction_CryoAmmo", "CryoAmmo");
			CryoBlast = new PowerClass("SFXGameContent.SFXPowerCustomAction_CryoBlast", "CryoBlast");
			DarkChannel = new PowerClass("SFXGameContent.SFXPowerCustomAction_DarkChannel", "DarkChannel");
			Decoy = new PowerClass("SFXGameContent.SFXPowerCustomAction_Decoy", "Decoy");
			DefenseDrone = new PowerClass("SFXGameContent.SFXPowerCustomAction_ProtectorDrone", "ProtectorDrone");
			DefenseMatrix = new PowerClass("SFXGameContent.SFXPowerCustomAction_GethShieldBoost", "GethShieldBoost");
			DisruptorAmmo = new PowerClass("SFXGameContent.SFXPowerCustomAction_DisruptorAmmo", "DisruptorAmmo");
			Dominate = new PowerClass("SFXGameContentDLC_EXP_Pack001.SFXPowerCustomAction_Dominate", "Dominate");
			EDIPassive = new PowerClass("SFXGameContent.SFXPowerCustomAction_EDIPassive", "EDIPassive");
			EnergyDrain = new PowerClass("SFXGameContent.SFXPowerCustomAction_EnergyDrain", "EnergyDrain");
			EngineerFitness = new PowerClass("SFXGameContent.SFXPowerCustomAction_EngineerMeleePassive", "EngineerMeleePassive");
			EngineerPassive = new PowerClass("SFXGameContent.SFXPowerCustomAction_EngineerPassive", "EngineerPassive");
			Flare = new PowerClass("SFXGameContentDLC_EXP_Pack002.SFXPowerCustomAction_BioticFlare", "Flare");
			Fortification = new PowerClass("SFXGameContent.SFXPowerCustomAction_Fortification", "Fortification");
			FragGrenade = new PowerClass("SFXGameContent.SFXPowerCustomAction_FragGrenade", "FragGrenade");
			GarrusPassive = new PowerClass("SFXGameContent.SFXPowerCustomAction_GarrusPassive", "GarrusPassive");
			GruntPassive = new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_GruntPassive", "GruntPassive");
			IncendiaryAmmo = new PowerClass("SFXGameContent.SFXPowerCustomAction_IncendiaryAmmo", "IncendiaryAmmo");
			Incinerate = new PowerClass("SFXGameContent.SFXPowerCustomAction_Incinerate", "Incinerate");
			InfernoGrenade = new PowerClass("SFXGameContent.SFXPowerCustomAction_InfernoGrenade", "InfernoGrenade");
			InfiltratorFitness = new PowerClass("SFXGameContent.SFXPowerCustomAction_InfiltratorMeleePassive", "InfiltratorMeleePassive");
			InfiltratorPassive = new PowerClass("SFXGameContent.SFXPowerCustomAction_InfiltratorPassive", "InfiltratorPassive");
			JacobPassive = new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_JacobPassive", "JacobPassive");
			JackPassive = new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_JackPassive", "JackPassive");
			JamesPassive = new PowerClass("SFXGameContent.SFXPowerCustomAction_JimmyPassive", "JimmyPassive");
			JavikPassive = new PowerClass("SFXGameContent.SFXPowerCustomAction_ProtheanPassive", "ProtheanPassive");
			KaidanPassive = new PowerClass("SFXGameContent.SFXPowerCustomAction_KaidenPassive", "KaidenPassive");
			KasumiPassive = new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_KasumiPassive", "KasumiPassive");
			Lash = new PowerClass("SFXGameContentDLC_EXP_Pack002.SFXPowerCustomAction_AriaLash", "AriaLash");
			LiaraPassive = new PowerClass("SFXGameContent.SFXPowerCustomAction_LiaraPassive", "LiaraPassive");
			LiftGrenade = new PowerClass("SFXGameContent.SFXPowerCustomAction_LiftGrenade", "LiftGrenade");
			Marksman = new PowerClass("SFXGameContent.SFXPowerCustomAction_Marksman", "Marksman");
			MirandaPassive = new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_MirandaPassive", "MirandaPassive");
			Nova = new PowerClass("SFXGameContent.SFXPowerCustomAction_Discharge", "Discharge");
			NyreenPassive = new PowerClass("SFXGameContentDLC_EXP_Pack002.SFXPowerCustomAction_NyreenPassive", "NyreenPassive");
			Overload = new PowerClass("SFXGameContent.SFXPowerCustomAction_Overload", "Overload");
			Pull = new PowerClass("SFXGameContent.SFXPowerCustomAction_Pull", "Pull");
			ProximityMine = new PowerClass("SFXGameContent.SFXPowerCustomAction_ProximityMine", "ProximityMine");
			Reave = new PowerClass("SFXGameContent.SFXPowerCustomAction_Reave", "Reave");
			Sabotage = new PowerClass("SFXGameContent.SFXPowerCustomAction_AIHacking", "Hacking");
			SamaraPassive = new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_SamaraPassive", "SamaraPassive");
			SentinelFitness = new PowerClass("SFXGameContent.SFXPowerCustomAction_SentinelMeleePassive", "SentinelMeleePassive");
			SentinelPassive = new PowerClass("SFXGameContent.SFXPowerCustomAction_SentinelPassive", "SentinelPassive");
			SentryTurret = new PowerClass("SFXGameContent.SFXPowerCustomAction_SentryTurrent", "SentryTurrent");
			Shockwave = new PowerClass("SFXGameContent.SFXPowerCustomAction_Shockwave", "Shockwave");
			Singularity = new PowerClass("SFXGameContent.SFXPowerCustomAction_Singularity", "Singularity");
			Slam = new PowerClass("SFXGameContent.SFXPowerCustomAction_Slam", "Slam");
			SoldierFitness = new PowerClass("SFXGameContent.SFXPowerCustomAction_SoldierMeleePassive", "SoldierMeleePassive");
			SoldierPassive = new PowerClass("SFXGameContent.SFXPowerCustomAction_SoldierPassive", "SoldierPassive");
			Stasis = new PowerClass("SFXGameContent.SFXPowerCustomAction_Stasis", "Stasis");
			StickyGrenade = new PowerClass("SFXGameContent.SFXPowerCustomAction_StickyGrenade", "StickyGrenade");
			StimulantPack = new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_StimPack", "StimPack");
			TacticalCloak = new PowerClass("SFXGameContent.SFXPowerCustomAction_Cloak", "Cloak");
			TaliPassive = new PowerClass("SFXGameContent.SFXPowerCustomAction_TaliPassive", "TaliPassive");
			TechArmor = new PowerClass("SFXGameContent.SFXPowerCustomAction_TechArmor", "TechArmor");
			Throw = new PowerClass("SFXGameContent.SFXPowerCustomAction_Throw", "Throw");
			Unity = new PowerClass("SFXGameContent.SFXPowerCustomAction_Unity", "Unity");
			VanguardFitness = new PowerClass("SFXGameContent.SFXPowerCustomAction_VanguardMeleePassive", "VanguardMeleePassive");
			VanguardPassive = new PowerClass("SFXGameContent.SFXPowerCustomAction_VanguardPassive", "VanguardPassive");
			Warp = new PowerClass("SFXGameContent.SFXPowerCustomAction_Warp", "Warp");
			WarpAmmo = new PowerClass("SFXGameContent.SFXPowerCustomAction_WarpAmmo", "WarpAmmo");
			WrexPassive = new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_WrexPassive", "WrexPassive");
			ZaeedPassive = new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_ZaeedPassive", "ZaeedPassive");

			//
			Classes = new Dictionary<PowerId, PowerClass>
			{
				{PowerId.AdeptFitness, AdeptFitness},
				{PowerId.AdpetPassive, AdpetPassive},
				{PowerId.AdrenalineRush, AdrenalineRush},
				{PowerId.AndersonPassive, AndersonPassive},
				{PowerId.AriaPassive, AriaPassive},
				{PowerId.ArmorPiercingAmmo, ArmorPiercingAmmo},
				{PowerId.AshleyPassive, AshleyPassive},
				{PowerId.Barrier, Barrier},
				{PowerId.BioticCharge, BioticCharge},
				{PowerId.BioticProtector, BioticProtector},
				{PowerId.Carnage, Carnage},
				{PowerId.ClusterGrenade, ClusterGrenade},
				{PowerId.CombatDrone, CombatDrone},
				{PowerId.ConcussiveShot, ConcussiveShot},
				{PowerId.CryoAmmo, CryoAmmo},
				{PowerId.CryoBlast, CryoBlast},
				{PowerId.DarkChannel, DarkChannel},
				{PowerId.Decoy, Decoy},
				{PowerId.DefenseDrone, DefenseDrone},
				{PowerId.DefenseMatrix, DefenseMatrix},
				{PowerId.DisruptorAmmo, DisruptorAmmo},
				{PowerId.Dominate, Dominate},
				{PowerId.EDIPassive, EDIPassive},
				{PowerId.EnergyDrain, EnergyDrain},
				{PowerId.EngineerFitness, EngineerFitness},
				{PowerId.EngineerPassive, EngineerPassive},
				{PowerId.Flare, Flare},
				{PowerId.Fortification, Fortification},
				{PowerId.FragGrenade, FragGrenade},
				{PowerId.GarrusPassive, GarrusPassive},
				{PowerId.GruntPassive, GruntPassive},
				{PowerId.IncendiaryAmmo, IncendiaryAmmo},
				{PowerId.Incinerate, Incinerate},
				{PowerId.InfernoGrenade, InfernoGrenade},
				{PowerId.InfiltratorFitness, InfiltratorFitness},
				{PowerId.InfiltratorPassive, InfiltratorPassive},
				{PowerId.JacobPassive, JacobPassive},
				{PowerId.JackPassive, JackPassive},
				{PowerId.JamesPassive, JamesPassive},
				{PowerId.JavikPassive, JavikPassive},
				{PowerId.KaidanPassive, KaidanPassive},
				{PowerId.KasumiPassive, KasumiPassive},
				{PowerId.Lash, Lash},
				{PowerId.LiaraPassive, LiaraPassive},
				{PowerId.LiftGrenade, LiftGrenade},
				{PowerId.Marksman, Marksman},
				{PowerId.MirandaPassive, MirandaPassive},
				{PowerId.Nova, Nova},
				{PowerId.NyreenPassive, NyreenPassive},
				{PowerId.Overload, Overload},
				{PowerId.Pull, Pull},
				{PowerId.ProximityMine, ProximityMine},
				{PowerId.Reave, Reave},
				{PowerId.Sabotage, Sabotage},
				{PowerId.SamaraPassive, SamaraPassive},
				{PowerId.SentinelFitness, SentinelFitness},
				{PowerId.SentinelPassive, SentinelPassive},
				{PowerId.SentryTurret, SentryTurret},
				{PowerId.Shockwave, Shockwave},
				{PowerId.Singularity, Singularity},
				{PowerId.Slam, Slam},
				{PowerId.SoldierFitness, SoldierFitness},
				{PowerId.SoldierPassive, SoldierPassive},
				{PowerId.Stasis, Stasis},
				{PowerId.StickyGrenade, StickyGrenade},
				{PowerId.StimulantPack, StimulantPack},
				{PowerId.TacticalCloak, TacticalCloak},
				{PowerId.TaliPassive, TaliPassive},
				{PowerId.TechArmor, TechArmor},
				{PowerId.Throw, Throw},
				{PowerId.Unity, Unity},
				{PowerId.VanguardFitness, VanguardFitness},
				{PowerId.VanguardPassive, VanguardPassive},
				{PowerId.Warp, Warp},
				{PowerId.WarpAmmo, WarpAmmo},
				{PowerId.WrexPassive, WrexPassive},
				{PowerId.ZaeedPassive, ZaeedPassive}
			};
		}
	}
}