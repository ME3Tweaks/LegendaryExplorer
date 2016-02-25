using System;
using System.Collections.Generic;
using MassEffect3.SaveFormats;

namespace MassEffect3.SaveEdit
{
	internal static class NewGameStuff
	{
		public enum SFXPlotType
		{
			Invalid = 0,
			Float,
			Integer,
			Boolean,
		}

		private static readonly List<CopyPlot> Me1ToMe3PlotCopy = new List<CopyPlot>();
		private static readonly List<CopyPlot> Me2ToMe3PlotCopy = new List<CopyPlot>();
		private static readonly List<NewGameCanonPlot> Me1CanonPlotVariables = new List<NewGameCanonPlot>();
		private static readonly List<NewGameCanonPlot> Me2CanonPlotVariables = new List<NewGameCanonPlot>();

		public static void SetNewGamePlotStates(
			this SFXSaveGameFile target,
			SFXSaveGameFile legacyImport,
			SFXSaveGameFile plusImport)
		{
			if (legacyImport != null)
			{
				target.CopyMe2PlotData(legacyImport);
				target.SetPlayerPlotData();
				target.DoMe2PlotImport(legacyImport);
			}
			else if (plusImport != null)
			{
				target.DoMe3NewGamePlusImport(plusImport);
				target.SetPlayerPlotData();
			}
			else
			{
				target.SetPlayerPlotData();
				target.ApplyCanonPlots(Me1CanonPlotVariables);
				target.ApplyCanonPlots(Me2CanonPlotVariables);
			}
		}

		public static void ClearAllVariables(
			this SFXSaveGameFile target,
			bool persistMe1Me2Plots)
		{
			throw new NotImplementedException();

			// TODO: figure this out...

			if (persistMe1Me2Plots == false)
			{
				target.Plot.BoolVariables.Length = 0;
				target.Plot.IntVariables.Clear();
				target.Plot.FloatVariables.Clear();
			}
			else
			{
				target.Plot.BoolVariables.Length = Constants.ME1PlotTable_Bool_CutoffIndex + 1;
				for (var i = 0;
					i < target.Plot.BoolVariables.Length &&
					i < Constants.ME1PlotTable_IndexOffset;
					i++)
				{
					target.Plot.BoolVariables[i] = false;
				}

				target.Plot.IntVariables.RemoveAll(iv => iv.Index < Constants.ME1PlotTable_IndexOffset ||
														iv.Index > Constants.ME1PlotTable_Int_CutoffIndex);
				target.Plot.FloatVariables.RemoveAll(iv => iv.Index < Constants.ME1PlotTable_IndexOffset ||
															iv.Index > Constants.ME1PlotTable_Float_CutoffIndex);
			}
		}

		public static void DoMe3NewGamePlusImport(
			this SFXSaveGameFile target,
			SFXSaveGameFile plusImport)
		{
			throw new NotImplementedException();
		}

		public static void CopyMe2PlotData(
			this SFXSaveGameFile target,
			SFXSaveGameFile legacyImport)
		{
			throw new NotImplementedException();
		}

		public static void SetPlayerPlotData(
			this SFXSaveGameFile target)
		{
			target.Plot.SetBoolVariable(Constants.ME3_Plots_Utility_Player_Info_Female_Player,
				target.Player.IsFemale);
			target.Plot.SetIntVariable(Constants.ME3_Plots_Utility_Player_Info_Childhood, (int) target.Player.Origin);
			target.Plot.SetIntVariable(Constants.ME3_Plots_Utility_Player_Info_Reputation,
				(int) target.Player.Notoriety);
		}

		public static void DoMe2PlotImport(
			this SFXSaveGameFile target,
			SFXSaveGameFile legacyImport)
		{
			if (legacyImport.ME1Plot.BoolVariables.Length > 0 ||
				legacyImport.ME1Plot.IntVariables.Count > 0 ||
				legacyImport.ME1Plot.FloatVariables.Count > 0)
			{
				target.MergeMe1PlotRecord(legacyImport.ME1Plot);
				target.FixMe1PlotsDuringMe2PlotImport();
				target.CopyPlots(Me1ToMe3PlotCopy);
				target.Plot.SetBoolVariable(Constants.ME3_Plots_Bool_Is_ME1_Import, true);
			}
			else
			{
				target.ApplyCanonPlots(Me1CanonPlotVariables);
				target.DoDarkHorseMe1PlotCopyAndPlotLogicFix();
			}

			target.CopyPlots(Me2ToMe3PlotCopy);
			target.Plot.SetBoolVariable(Constants.ME3_Plots_Bool_Is_ME2_Import, true);
		}

		public static void MergeMe1PlotRecord(
			this SFXSaveGameFile target,
			ME1PlotTable me1Plots)
		{
			for (var i = 0;
				i < me1Plots.BoolVariables.Length
				/*&&
                 PlotConstants.ME1PlotTable_IndexOffset + i < PlotConstants.ME1PlotTable_Bool_CutoffIndex*/;
				i++)
			{
				target.Plot.SetBoolVariable(
					Constants.ME1PlotTable_IndexOffset + i,
					me1Plots.BoolVariables[i]);
			}

			for (var i = 0;
				i < me1Plots.IntVariables.Count
				/*&&
                 PlotConstants.ME1PlotTable_IndexOffset + i < PlotConstants.ME1PlotTable_Int_CutoffIndex*/;
				i++)
			{
				target.Plot.SetIntVariable(
					Constants.ME1PlotTable_IndexOffset + i,
					me1Plots.IntVariables[i]);
			}

			for (var i = 0;
				i < me1Plots.FloatVariables.Count
				/*&&
                 PlotConstants.ME1PlotTable_IndexOffset + i < PlotConstants.ME1PlotTable_Float_CutoffIndex*/;
				i++)
			{
				target.Plot.SetFloatVariable(
					Constants.ME1PlotTable_IndexOffset + i,
					me1Plots.FloatVariables[i]);
			}
		}

		public static void FixMe1PlotsDuringMe2PlotImport(this SFXSaveGameFile target)
		{
			var councilIsAlive =
				target.Plot.GetBoolVariable(Constants.ME2__ME1_Plots_for_ME2__CH4_Star_Citadel__Council_Alive);
			target.Plot.SetBoolVariable(
				Constants.ME3__ME1_Plots_for_ME3__CH4_Star_Citadel__Final_Choice__Choice_Is_Made__Save_the_Council,
				councilIsAlive);
			target.Plot.SetBoolVariable(
				Constants.
					ME3__ME1_Plots_for_ME3__CH4_Star_Citadel__Final_Choice__Choice_Is_Made__Destroy_the_Council,
				councilIsAlive == false);

			var andersonIsABro =
				target.Plot.GetBoolVariable(Constants.ME2__ME1_Plots_for_ME2__CH4_Star_Citadel__Anderson_chosen);
			target.Plot.SetBoolVariable(
				Constants.ME3__ME1_Plots_for_ME3__CH4_Star_Citadel__Final_Choice__Chose_ambassador,
				andersonIsABro == false);
			target.Plot.SetBoolVariable(
				Constants.ME3__ME1_Plots_for_ME3__CH4_Star_Citadel__Final_Choice__Chose_Anderson,
				andersonIsABro);
		}

		public static void ApplyCanonPlots(this SFXSaveGameFile target, List<NewGameCanonPlot> aCanonPlotVariables)
		{
			throw new NotImplementedException();
		}

		public static void CopyPlots(this SFXSaveGameFile target, List<CopyPlot> toCopy)
		{
			foreach (var plot in toCopy)
			{
				switch (plot.Type)
				{
					case SFXPlotType.Boolean:
					{
						target.Plot.SetBoolVariable(plot.TId, target.Plot.GetBoolVariable(plot.SId));
						break;
					}

					case SFXPlotType.Integer:
					{
						target.Plot.SetIntVariable(plot.TId, target.Plot.GetIntVariable(plot.SId));
						break;
					}

					case SFXPlotType.Float:
					{
						target.Plot.SetFloatVariable(plot.TId, target.Plot.GetFloatVariable(plot.SId));
						break;
					}
				}
			}
		}

		public static void DoDarkHorseMe1PlotCopyAndPlotLogicFix(
			this SFXSaveGameFile target)
		{
			if (target.Plot.GetBoolVariable(Constants.PS3DarkHorseME1PlayedPlotCheck_Bool))
			{
				if (
					target.Plot.GetBoolVariable(
						Constants.ME2__ME1_Plots_for_ME2__Background_and_Relationships__Kaidan_romance_True))
				{
					target.Plot.SetIntVariable(
						Constants.ME3__ME1_Plots_for_ME3__Global_Plots__Henchman_Kaidan__Romance_Buddy_dialog_count,
						4);
					target.Plot.SetBoolVariable(
						Constants.ME3__ME1_Plots_for_ME3__Global_Plots__Henchman_Kaidan__Romance_active, true);
				}

				if (
					target.Plot.GetBoolVariable(
						Constants.ME2__ME1_Plots_for_ME2__Background_and_Relationships__Ashley_romance_True))
				{
					target.Plot.SetIntVariable(
						Constants.ME3__ME1_Plots_for_ME3__Global_Plots__Henchman_Ash__Romance_Buddy_dialog_count, 4);
					target.Plot.SetBoolVariable(
						Constants.ME3__ME1_Plots_for_ME3__Global_Plots__Henchman_Ash__romance_active, true);
				}

				if (
					target.Plot.GetBoolVariable(
						Constants.ME2__ME1_Plots_for_ME2__Background_and_Relationships__Liara_romance_True))
				{
					target.Plot.SetIntVariable(
						Constants.ME3__ME1_Plots_for_ME3__Global_Plots__Henchman_Liara__Romance_Buddy_dialog_count,
						4);
					target.Plot.SetBoolVariable(
						Constants.ME3__ME1_Plots_for_ME3__Global_Plots__Henchman_Liara__Romance_active, true);
				}

				if (target.Plot.GetBoolVariable(Constants.ME2__ME1_Plots_for_ME2__CH2_Virmire__Ash_died) &&
					target.Plot.GetBoolVariable(Constants.ME2__ME1_Plots_for_ME2__CH2_Virmire__Kaidan_died))
				{
					var isFemale = target.Plot.GetBoolVariable(Constants.ME3_Plots_Utility_Player_Info_Female_Player);
					target.Plot.SetBoolVariable(
						Constants.ME3__ME1_Plots_for_ME3__CH2_Virmire__The_Choice__Rescued_Kaidan, isFemale == false);
					target.Plot.SetBoolVariable(
						Constants.ME3__ME1_Plots_for_ME3__CH2_Virmire__The_Choice__Rescued_Ash, isFemale);
				}
				else
				{
					var ashleyDied =
						target.Plot.GetBoolVariable(Constants.ME2__ME1_Plots_for_ME2__CH2_Virmire__Ash_died);
					target.Plot.SetBoolVariable(
						Constants.ME3__ME1_Plots_for_ME3__CH2_Virmire__The_Choice__Rescued_Kaidan,
						ashleyDied);
					target.Plot.SetBoolVariable(
						Constants.ME3__ME1_Plots_for_ME3__CH2_Virmire__The_Choice__Rescued_Ash, ashleyDied == false);
				}

				var rachniAreAlive =
					target.Plot.GetBoolVariable(Constants.ME2__ME1_Plots_for_ME2__CH2_Noveria__Rachni_Alive);
				target.Plot.SetBoolVariable(
					Constants.ME3__ME1_Plots_for_ME3__CH2_Noveria__Rachni_Queen__Queen_Dealt_With__Queen_Released,
					rachniAreAlive);
				target.Plot.SetBoolVariable(
					Constants.ME3__ME1_Plots_for_ME3__CH2_Noveria__Rachni_Queen__Queen_Dealt_With__Queen_eliminated,
					rachniAreAlive == false);

				var wrexDied =
					target.Plot.GetBoolVariable(Constants.ME2__ME1_Plots_for_ME2__CH2_Virmire_Wrex_Died);
				target.Plot.SetBoolVariable(
					Constants.
						ME3__ME1_Plots_for_ME3__CH2_Virmire__Krogan_conundrum__Failure__Failure_KilledBy_Player,
					wrexDied);
				target.Plot.SetBoolVariable(Constants.ME3__ME1_Plots_for_ME3__Utility__Henchman__InParty__Krogan,
					wrexDied == false);

				target.FixMe1PlotsDuringMe2PlotImport();
			}
		}

		// ReSharper disable InconsistentNaming
		private static class Constants
		{
			public const int ME1PlotTable_IndexOffset = 10000;
			public const int ME1PlotTable_Bool_CutoffIndex = 17655;
			public const int ME1PlotTable_Int_CutoffIndex = 10148;
			public const int ME1PlotTable_Float_CutoffIndex = 10039;

			public const int ME2__ME1_Plots_for_ME2__CH4_Star_Citadel__Council_Alive = 1554;
			public const int ME2__ME1_Plots_for_ME2__CH4_Star_Citadel__Anderson_chosen = 1556;
			public const int ME2__ME1_Plots_for_ME2__Background_and_Relationships__Kaidan_romance_True = 1529;
			public const int ME2__ME1_Plots_for_ME2__Background_and_Relationships__Ashley_romance_True = 1528;
			public const int ME2__ME1_Plots_for_ME2__Background_and_Relationships__Liara_romance_True = 1530;
			public const int ME2__ME1_Plots_for_ME2__CH2_Virmire__Ash_died = 1540;
			public const int ME2__ME1_Plots_for_ME2__CH2_Virmire__Kaidan_died = 1541;
			public const int ME2__ME1_Plots_for_ME2__CH2_Noveria__Rachni_Alive = 3151;
			public const int ME2__ME1_Plots_for_ME2__CH2_Virmire_Wrex_Died = 3752;

			public const int PS3DarkHorseME1PlayedPlotCheck_Bool = 7447;

			public const int ME3_Plots_Bool_Is_ME1_Import = 22226;
			public const int ME3_Plots_Bool_Is_ME2_Import = 21554;
			public const int ME3_Plots_Utility_Player_Info_Female_Player = 17662;
			public const int ME3_Plots_Utility_Player_Info_Childhood = 10296;
			public const int ME3_Plots_Utility_Player_Info_Reputation = 10297;

			public const int ME3__ME1_Plots_for_ME3__CH4_Star_Citadel__Final_Choice__Choice_Is_Made__Save_the_Council
				= 13001;

			public const int ME3__ME1_Plots_for_ME3__CH4_Star_Citadel__Final_Choice__Choice_Is_Made__Destroy_the_Council
				= 13002;

			public const int ME3__ME1_Plots_for_ME3__CH4_Star_Citadel__Final_Choice__Chose_ambassador = 15434;
			public const int ME3__ME1_Plots_for_ME3__CH4_Star_Citadel__Final_Choice__Chose_Anderson = 15435;
			public const int ME3__ME1_Plots_for_ME3__Global_Plots__Henchman_Kaidan__Romance_Buddy_dialog_count = 10015;
			public const int ME3__ME1_Plots_for_ME3__Global_Plots__Henchman_Kaidan__Romance_active = 13960;
			public const int ME3__ME1_Plots_for_ME3__Global_Plots__Henchman_Ash__Romance_Buddy_dialog_count = 10017;
			public const int ME3__ME1_Plots_for_ME3__Global_Plots__Henchman_Ash__romance_active = 14281;
			public const int ME3__ME1_Plots_for_ME3__Global_Plots__Henchman_Liara__Romance_Buddy_dialog_count = 10016;
			public const int ME3__ME1_Plots_for_ME3__Global_Plots__Henchman_Liara__Romance_active = 14169;
			public const int ME3__ME1_Plots_for_ME3__CH2_Virmire__The_Choice__Rescued_Kaidan = 13828;
			public const int ME3__ME1_Plots_for_ME3__CH2_Virmire__The_Choice__Rescued_Ash = 13827;
			public const int ME3__ME1_Plots_for_ME3__CH2_Noveria__Rachni_Queen__Queen_Dealt_With__Queen_Released = 12587;

			public const int ME3__ME1_Plots_for_ME3__CH2_Noveria__Rachni_Queen__Queen_Dealt_With__Queen_eliminated =
				12588;

			public const int ME3__ME1_Plots_for_ME3__CH2_Virmire__Krogan_conundrum__Failure__Failure_KilledBy_Player =
				13029;

			public const int ME3__ME1_Plots_for_ME3__Utility__Henchman__InParty__Krogan = 13942;
		}

		public struct CopyPlot
		{
			// ReSharper disable InconsistentNaming
			public int SId { get; set; }
			public int TId { get; set; }
			// ReSharper restore InconsistentNaming
			public SFXPlotType Type { get; set; }
		}

		public struct NewGameCanonPlot
		{
			public int Conditional { get; set; }
			public int ConditionalParameter { get; set; }
			public PlotIdenfitier Id { get; set; }
			public int Value { get; set; }
		}

		public struct PlotIdenfitier
		{
			public int Index { get; set; }
			public SFXPlotType PlotType { get; set; }
		}

		// TODO: fill these in...
		// ReSharper restore InconsistentNaming
	}
}