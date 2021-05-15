using System;
using System.Windows.Markup;

#if !SILVERLIGHT

namespace LegendaryExplorer.Tools.PlotEditor
{
	/// <summary>
	///     Implements a markup extension that allows instances of <see cref="TypeConverter" /> to be easily created.
	/// </summary>
	/// <remarks>
	///     This markup extension allows instance of <see cref="TypeConverter" /> to be easily created inline in a XAML binding. See
	///     the example below.
	/// </remarks>
	/// <example>
	///     The following shows how to use the <c>TypeConverterExtension</c> inside a binding to convert integer values to strings
	///     and back:
	///     <code lang="xml">
	/// <![CDATA[
	/// <TextBox Text="{Binding Age, Converter={TypeConverter sys:Int32, sys:String}}"/>
	/// ]]>
	/// </code>
	/// </example>
	public sealed class TypeConverterExtension : MarkupExtension
	{
		private readonly TypeExtension sourceTypeExtension;
		private readonly TypeExtension targetTypeExtension;

		/// <summary>
		///     Initializes a new instance of the TypeConverterExtension class.
		/// </summary>
		public TypeConverterExtension()
		{
			sourceTypeExtension = new TypeExtension();
			targetTypeExtension = new TypeExtension();
		}

		/// <summary>
		///     Initializes a new instance of the TypeConverterExtension class with the specified source and target types.
		/// </summary>
		/// <param name="sourceType">
		///     The source type for the <see cref="TypeConverter" />.
		/// </param>
		/// <param name="targetType">
		///     The target type for the <see cref="TypeConverter" />.
		/// </param>
		public TypeConverterExtension(Type sourceType, Type targetType)
		{
			sourceTypeExtension = new TypeExtension(sourceType);
			targetTypeExtension = new TypeExtension(targetType);
		}

		/// <summary>
		///     Initializes a new instance of the TypeConverterExtension class with the specified source and target types.
		/// </summary>
		/// <param name="sourceTypeName">
		///     The source type name for the <see cref="TypeConverter" />.
		/// </param>
		/// <param name="targetTypeName">
		///     The target type name for the <see cref="TypeConverter" />.
		/// </param>
		public TypeConverterExtension(string sourceTypeName, string targetTypeName)
		{
			sourceTypeExtension = new TypeExtension(sourceTypeName);
			targetTypeExtension = new TypeExtension(targetTypeName);
		}

		/// <summary>
		///     Gets or sets the source type for the <see cref="TypeConverter" />.
		/// </summary>
		[ConstructorArgument("sourceType")]
		public Type SourceType
		{
			get { return sourceTypeExtension.Type; }
			set { sourceTypeExtension.Type = value; }
		}

		/// <summary>
		///     Gets or sets the target type for the <see cref="TypeConverter" />.
		/// </summary>
		[ConstructorArgument("targetType")]
		public Type TargetType
		{
			get { return targetTypeExtension.Type; }
			set { targetTypeExtension.Type = value; }
		}

		/// <summary>
		///     Gets or sets the name of the source type for the <see cref="TypeConverter" />.
		/// </summary>
		[ConstructorArgument("sourceTypeName")]
		public string SourceTypeName
		{
			get { return sourceTypeExtension.TypeName; }
			set { sourceTypeExtension.TypeName = value; }
		}

		/// <summary>
		///     Gets or sets the name of the target type for the <see cref="TypeConverter" />.
		/// </summary>
		[ConstructorArgument("targetTypeName")]
		public string TargetTypeName
		{
			get { return targetTypeExtension.TypeName; }
			set { targetTypeExtension.TypeName = value; }
		}

		/// <summary>
		///     Provides an instance of <see cref="TypeConverter" /> based on this <c>TypeConverterExtension</c>.
		/// </summary>
		/// <param name="serviceProvider">
		///     An object that can provide services.
		/// </param>
		/// <returns>
		///     The instance of <see cref="TypeConverter" />.
		/// </returns>
		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			Type sourceType = null;
			Type targetType = null;

			if ((sourceTypeExtension.Type != null) || (sourceTypeExtension.TypeName != null))
			{
				sourceType = sourceTypeExtension.ProvideValue(serviceProvider) as Type;
			}

			if ((targetTypeExtension.Type != null) || (targetTypeExtension.TypeName != null))
			{
				targetType = targetTypeExtension.ProvideValue(serviceProvider) as Type;
			}

			// just let the TypeExtensions do the type resolving via the service provider
			return new TypeConverter(sourceType, targetType);
		}
	}
}

#endif
