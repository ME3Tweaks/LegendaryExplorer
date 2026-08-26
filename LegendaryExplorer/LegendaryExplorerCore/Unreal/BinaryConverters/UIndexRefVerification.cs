using System;

//This file is also compiled into the LegendaryExplorerCore.SourceGenerators project,
//so nothing in this file can reference anything else in LegendaryExplorerCore, or use anything not in netstandard2.0

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    [Flags]
    public enum UIndexRefFlags
    {
        //which games this is valid for
        AllGames = 0,
        ME1 = 1 << 1,
        ME2 = 1 << 2,
        ME3 = 1 << 3,
        LE1 = 1 << 4,
        LE2 = 1 << 5,
        LE3 = 1 << 6,
        UDK = 1 << 7,

        //if this is a map, which half of the pair this applies to. Ignored for all other fields.
        Key = 1 << 8,
        Value = 1 << 9
    }

    /// <summary>
    /// Marks a UIndex field as a reference to an entry of one of the specified classes.
    /// The source generator emits a <see cref="ObjectBinary.VerifyUIndexRefs"/> override for the containing type,
    /// which must be declared <c>partial</c>.
    /// </summary>
    /// <remarks>
    /// Use the parameterless form for references that can point to any class. Those are still visited,
    /// but no type checking is performed on them.
    /// <para/>
    /// Fields that refer to different classes depending on the game should be annotated multiple times, one for each class, with the appropriate game <see cref="UIndexRefFlags"/> set.
    /// If one attribute has no game flags set, it is assumed to apply to all games not covered by other attributes.
    /// <para/>
    /// A map whose keys and values are both UIndexes can be annotated twice, once with
    /// <see cref="UIndexRefFlags.Key"/> and once with <see cref="UIndexRefFlags.Value"/>.
    /// <para/>
    /// A combination of the above is also possible, for example a map whose keys are UIndexes of class "A" in ME1 and class "B" otherwise, and whose values are UIndexes of class "C" in all games, would be annotated with three attributes, like so:
    /// <code>
    /// [<see cref="UIndexRefAttribute">UIndexRef</see>("A", <see cref="UIndexRefFlags.ME1"/> | <see cref="UIndexRefFlags.Key"/>)]
    /// [<see cref="UIndexRefAttribute">UIndexRef</see>("B", <see cref="UIndexRefFlags.Key"/>)]
    /// [<see cref="UIndexRefAttribute">UIndexRef</see>("C", <see cref="UIndexRefFlags.Value"/>)]
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class UIndexRefAttribute : Attribute
    {
        public string AcceptedClass { get; }

        public UIndexRefFlags Flags { get; set; } = UIndexRefFlags.AllGames;

        public UIndexRefAttribute(string acceptedClass = null, UIndexRefFlags flags = UIndexRefFlags.AllGames)
        {
            AcceptedClass = acceptedClass;
            Flags = flags;
        }
    }

    /// <summary>
    /// Marks a field whose type (or element type, for arrays and Lists) is itself a type containing
    /// <see cref="UIndexRefAttribute"/> fields, for example <see cref="MaterialResource"/>.
    /// Generated code recurses into it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UIndexRefContainerAttribute : Attribute
    {
    }

    /// <summary>
    /// Opts a type out of UIndex ref generation entirely: nothing is emitted for it, and its UIndex fields
    /// don't produce "not annotated" warnings. Use this for types whose references can't be expressed with
    /// <see cref="UIndexRefAttribute"/> and which therefore need a hand written VerifyUIndexRefs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class ManualUIndexRefVerificationAttribute : Attribute
    {
    }

    /// <summary>
    /// Receives every <see cref="UIndexRefAttribute"/>-annotated UIndex in an <see cref="ObjectBinary"/>.
    /// See <see cref="ObjectBinary.VerifyUIndexRefs"/>.
    /// </summary>
    public interface IUIndexRefVerifier
    {
        /// <param name="uIndex">The value of the field. 0 means no reference.</param>
        /// <param name="acceptedClass">The class the reference is allowed to be an instance of.
        /// Null when the field can refer to any class.</param>
        /// <param name="fieldPath">Path of the field within the ObjectBinary, e.g. "SM3MaterialResource.UniformExpressionTextures[7]".</param>
        void Verify(int uIndex, string acceptedClass, string fieldPath);
    }
}
