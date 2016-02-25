using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;

namespace MassEffect3.DifficultyEditor
{
	/// <summary>
	///     Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		private const string CategoryPattern = @"
			(?<CategoryFull>
				[\r\n\t ]*
				\(
				(?<Category>
					(?:
						[\r\n\t ]*
						Category
						[\r\n\t ]*
						=
						[\r\n\t ]*
						""
						(?<CategoryName>[0-9a-zA-Z_]+)
						""
					) # Category=CategoryName
					[\r\n\t ]*
					,
					[\r\n\t ]*
					(?:
						CategoryData
						[\r\n\t ]*
						=
						[\r\n\t ]*
						\(
						(?<CategoryData>
							[\r\n\t ]*
							(?<StatFull>
								[\r\n\t ]*
								\(
								(?<Stat>
									[\r\n\t ]*
									StatName
									[\r\n\t ]*
									=
									[\r\n\t ]*
									""
									(?<StatName>[0-9a-zA-Z_]+)
									""
									[\r\n\t ]*
									,
									[\r\n\t ]*
									StatRange
									[\r\n\t ]*
									=
									[\r\n\t ]*
									\(
									(?<StatRange>
										[\r\n\t ]*
										X
										[\r\n\t ]*
										=
										[\r\n\t ]*
										(?<StatRangeX>[-+]?[0-9]*\.?[0-9]*)
										f?
										[\r\n\t ]*
										,
										[\r\n\t ]*
										Y
										[\r\n\t ]*
										=
										[\r\n\t ]*
										(?<StatRangeY>[-+]?[0-9]*\.?[0-9]*)
										f?
										[\r\n\t ]*
									) # StatRange=(X,Y)
									\)
									[\r\n\t ]*
								)
								[\r\n\t ]*
								\)
								[\r\n\t ]*
								,?
							)*
						)
						[\r\n\t ]*
						\)
					)
				)
				[\r\n\t ]*
				\)
			)
			";

		private const string CategoryNamePattern = "";
		private const string CategoryDataPattern = "";

		private const string FullCategoryPattern = "";
		private const string FullStatPattern = "";

		private const string StatPattern = @"
			(?:
				(?<Stat>
					[\r\n\t ]*
					StatName
					[\r\n\t ]*
					=
					[\r\n\t ]*
					""
					(?<StatName>[0-9a-zA-Z_]+)
					""
					[\r\n\t ]*
					,
					[\r\n\t ]*
					StatRange
					[\r\n\t ]*
					=
					[\r\n\t ]*
					\(
					(?<StatRange>
						[\r\n\t ]*
						X
						[\r\n\t ]*
						=
						[\r\n\t ]*
						(?<StatRangeX>[-+]?[0-9]*\.?[0-9]*)
						f?
						[\r\n\t ]*
						,
						[\r\n\t ]*
						Y
						[\r\n\t ]*
						=
						[\r\n\t ]*
						(?<StatRangeY>[-+]?[0-9]*\.?[0-9]*)
						f?
						[\r\n\t ]*
					) # StatRange=(X,Y)
					\)
					[\r\n\t ]*
				)
			)
			";

		private const string StatNamePattern = "";
		private const string StatRangePattern = "";
		private const string StatRangeXPattern = "";
		private const string StatRangeYPattern = "";

		private static readonly Regex CategoryRegex = new Regex(CategoryPattern,
																RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

		private static readonly Regex CategoryNameRegex = new Regex("");
		private static readonly Regex CategoryDataRegex = new Regex("");

		private static readonly Regex StatRegex = new Regex(StatPattern,
															RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

		public MainWindow()
		{
			InitializeComponent();
		}

		public IDictionary<string, IList<DifficultyData>> Categories { get; set; }

		private void ConvertButton_Click(object sender, RoutedEventArgs e)
		{
			var textRange = new TextRange(
				InputTextBox.Document.ContentStart,
				InputTextBox.Document.ContentEnd
				);

			var text = textRange.Text;

			RegexTest(text);
		}

		private void RegexTest(string text)
		{
			Categories = new SortedDictionary<string, IList<DifficultyData>>();

			// Create a Flow Document  
			var flowDoc = new FlowDocument();

			try
			{
				var categoryMatch = CategoryRegex.Match(text);

				while (categoryMatch.Success)
				{
					var statStrings = from Capture capture in categoryMatch.Groups["Stat"].Captures select capture.Value.Trim();
					var categoryName = categoryMatch.Groups["CategoryName"].Value;
					var match = categoryMatch;
					var difficultyDatas = (from str in statStrings
						select StatRegex.Match(str)
						into statMatch
						where match.Success
						let statName = statMatch.Groups["StatName"].Value
						let statRangeX = Convert.ToSingle(statMatch.Groups["StatRangeX"].Value)
						let statRangeY = Convert.ToSingle(statMatch.Groups["StatRangeY"].Value)
						select new DifficultyData(statName, new FloatVector2(statRangeX, statRangeY))).ToList();

					categoryMatch = categoryMatch.NextMatch();
					var orderedDifficultyDatas = difficultyDatas.OrderBy(difficultyData => difficultyData.StatName);

					Categories.Add(categoryName, orderedDifficultyDatas.ToList());
				}

				foreach (var category in Categories)
				{
					var paragraph = new Paragraph();
					paragraph.Inlines.Add(category.Key);
					paragraph.Inlines.Add(new LineBreak());

					foreach (var data in category.Value)
					{
						// StatName
						paragraph.Inlines.Add(string.Format("Name: {0}", data.StatName));
						paragraph.Inlines.Add(new LineBreak());

						// StatRange.X
						paragraph.Inlines.Add(string.Format("X: {0:0.00####}", data.StatRange.X));
						paragraph.Inlines.Add(new LineBreak());

						// StatRange.Y
						paragraph.Inlines.Add(string.Format("Y: {0:0.00####}", data.StatRange.Y));
						paragraph.Inlines.Add(new LineBreak());

						//
						paragraph.Inlines.Add(new LineBreak());
					}

					categoryMatch = categoryMatch.NextMatch();
					flowDoc.Blocks.Add(paragraph);
				}
			}
			catch (ArgumentException ex)
			{
				// Syntax error in the regular expression
				throw ex;
			}

			OutputTextBox.Document = flowDoc;
		}
	}
}
