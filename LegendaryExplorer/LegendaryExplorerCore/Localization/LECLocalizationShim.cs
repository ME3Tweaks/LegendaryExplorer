using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Localization
{
    /// <summary>
    /// Compatibility shim for M3 &lt;-&gt; LEC localization. This provides non-localized string values based on their keys.
    /// </summary>
    public class LECLocalizationShim
    {
        /// <summary>
        /// Non-localized text converter. Use if you don't want to localize the output.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="parms"></param>
        /// <returns></returns>
        public static string NonLocalizedStringConverter(string key, params object[] parms)
        {
            return LECLocalizationShim.GetString(key, parms);
        }

        public delegate string GetLocalizedStringDelegate(string str, params object[] parms);

        public static string GetString(string key, params object[] parms)
        {
            // Basic non-localized converter
            switch (key)
            {
                case string_interp_warningTemplateOwnerClassOutsideTables:
                    return string.Format("{0} TemplateOwnerClass (Data offset 0x{1}) ({2}) is outside of import/export table", parms);
                case string_checkingNameAndObjectReferences:
                    return string.Format("Checking name and object references", parms);
                case string_interp_fatalExportCircularReference:
                    return string.Format("{0}, export {1} has a circular self reference for its link. The game and the toolset will be unable to handle this condition", parms);
                case string_interp_warningArchetypeOutsideTables:
                    return string.Format("{0} Archetype {1} is outside of import/export table", parms);
                case string_interp_warningGenericExportPrefix:
                    return string.Format("{0}, export {1} {2} ({3})", parms);
                case string_interp_warningSuperclassOutsideTables:
                    return string.Format("{0} Header SuperClass {1} is outside of import/export table", parms);
                case string_interp_warningClassOutsideTables:
                    return string.Format("{0} Header Class {1} is outside of import/export table", parms);
                case string_interp_warningLinkOutsideTables:
                    return string.Format("{0} Header Link {1} is outside of import/export table", parms);
                case string_interp_warningComponentMapItemOutsideTables:
                    return string.Format("{0} Header Component Map item ({1}) is outside of import/export table", parms);
                case string_interp_warningExportStackElementOutsideTables:
                    return string.Format("{0} Export Stack[{1}] ({2}) is outside of import/export table", parms);
                case string_interp_warningExceptionParsingProperties:
                    return string.Format("{0} Exception occurred while parsing properties: {1}", parms);
                case string_interp_warningBinaryReferenceOutsideTables:
                    return string.Format("{0} Binary reference ({1}) is outside of import/export table", parms);
                case string_interp_warningBinaryReferenceTrashed:
                    return string.Format("{0} Binary reference ({1}) is a Trashed object", parms);
                case string_interp_warningBinaryNameReferenceOutsideNameTable:
                    return string.Format("{0} Found invalid binary reference for a name", parms);
                case string_interp_warningUnableToParseBinary:
                    return string.Format("{0} Unable to parse binary. It may be malformed. Error message: {1}. Note the error message is likely code-context specific and is not useful without running application in debug mode to determine it's context", parms);
                case string_interp_warningImportLinkOutideOfTables:
                    return string.Format("{0}, import {1} has an invalid link value that is outside of the import/export table: {2}", parms);
                case string_interp_fatalImportCircularReference:
                    return string.Format("{0}, import {1} has a circular self reference for its link. The game and the toolset will be unable to handle this condition", parms);
                case string_interp_refCheckInvalidNameValue:
                    return string.Format("{0}, invalid name reference found for {1} on {2}", parms);
                case string_interp_warningPropertyTypingWrongPrefix:
                    return string.Format("{0}, entry {1} {2} ({3}), @ 0x{4}:", parms);
                case string_interp_warningFoundBrokenPropertyData:
                    return string.Format("{0} Found broken property data! This should be investigated and fixed as this is almost guaranteed to cause game crashes", parms);
                case string_interp_warningReferenceNotInExportTable:
                    return string.Format("{0} {1} Export {2} is outside of export table", parms);
                case string_interp_nested_warningReferenceNoInExportTable:
                    return string.Format("{0} [Nested property] Export {1} is outside of export table", parms);

                case string_interp_warningReferenceNotInImportTable:
                    return string.Format("{0} {1} Import {2} is outside of import table", parms);
                case string_interp_nested_warningReferenceNoInImportTable:
                    return string.Format("{0} [Nested property] Import {1} is outside of import table", parms);
                case string_interp_nested_warningTrashedExportReference:
                    return string.Format("{0} [Nested property] Export {1} is a Trashed object", parms);
                case string_interp_warningWrongPropertyTypingWrongMessage:
                    return string.Format("{0} {1} references entry {2} {3}, but it appears to be wrong type. Property type expects a class (or subclass) of {4}, but the referenced one is of type {5}", parms);
                case string_interp_nested_warningWrongClassPropertyTypingWrongMessage:
                    return string.Format("{0} [Nested Property] references entry {1} {2}, but it appears to be wrong type. Property type expects a class (or subclass) {3}, but the referenced one is of type {4}", parms);
                case string_interp_warningWrongObjectPropertyTypingWrongMessage:
                    return string.Format("{0} {1} references entry {2} {3}, but it appears to be wrong type. Property type expects an instance of an object of class (or subclass) {4}, but the referenced one is of type {5}", parms);
                case string_interp_nested_warningWrongObjectPropertyTypingWrongMessage:
                    return string.Format("{0} [Nested Property] references entry {1} {2}, but it appears to be wrong type. Property type expects an instance of an object of class (or subclass) {3}, but the referenced one is of type {4}", parms);
                case string_interp_warningDelegatePropertyIsOutsideOfExportTable:
                    return string.Format("{0} DelegateProperty {1} is outside of export table", parms);
                case string_interp_XDoesNotSupportGameY:
                    return string.Format("{0} does not support game {1}", parms);
                case string_interp_invalidNameIndexonNameProperty:
                    return string.Format("{0} Invalid name table index for NameProperty value on property {1}", parms);

            }

            return $"ERROR! STRING KEY NOT IN LOCALIZATION TABLE: {key}";
        }

        // DO NOT CHANGE THESE KEYS
        // THEY ARE IDENTICAL TO THE ONES IN M3
        // CHANGING THEM WILL CAUSE M3 LOCALIZATION FOR STRING TO FAIL
        internal const string string_interp_warningTemplateOwnerClassOutsideTables = "string_interp_warningTemplateOwnerClassOutsideTables";
        internal const string string_checkingNameAndObjectReferences = "string_checkingNameAndObjectReferences";
        internal const string string_interp_fatalExportCircularReference = "string_interp_fatalExportCircularReference";
        internal const string string_interp_warningArchetypeOutsideTables = "string_interp_warningArchetypeOutsideTables";
        internal const string string_interp_warningGenericExportPrefix = "string_interp_warningGenericExportPrefix";
        internal const string string_interp_warningSuperclassOutsideTables = "string_interp_warningSuperclassOutsideTables";
        internal const string string_interp_warningClassOutsideTables = "string_interp_warningClassOutsideTables";
        internal const string string_interp_warningLinkOutsideTables = "string_interp_warningLinkOutsideTables";
        internal const string string_interp_warningComponentMapItemOutsideTables = "string_interp_warningComponentMapItemOutsideTables";
        internal const string string_interp_warningExportStackElementOutsideTables = "string_interp_warningExportStackElementOutsideTables";
        internal const string string_interp_warningExceptionParsingProperties = "string_interp_warningExceptionParsingProperties";
        internal const string string_interp_warningBinaryReferenceOutsideTables = "string_interp_warningBinaryReferenceOutsideTables";
        internal const string string_interp_warningBinaryReferenceTrashed = "string_interp_warningBinaryReferenceTrashed";
        internal const string string_interp_warningBinaryNameReferenceOutsideNameTable = "string_interp_warningBinaryNameReferenceOutsideNameTable";
        internal const string string_interp_warningUnableToParseBinary = "string_interp_warningUnableToParseBinary";
        internal const string string_interp_warningImportLinkOutideOfTables = "string_interp_warningImportLinkOutideOfTables";
        internal const string string_interp_fatalImportCircularReference = "string_interp_fatalImportCircularReference";
        internal const string string_interp_refCheckInvalidNameValue = "string_interp_refCheckInvalidNameValue";
        internal const string string_interp_warningPropertyTypingWrongPrefix = "string_interp_warningPropertyTypingWrongPrefix";
        internal const string string_interp_warningFoundBrokenPropertyData = "string_interp_warningFoundBrokenPropertyData";
        internal const string string_interp_warningReferenceNotInExportTable = "string_interp_warningReferenceNotInExportTable";
        internal const string string_interp_nested_warningReferenceNoInExportTable = "string_interp_nested_warningReferenceNoInExportTable";

        internal const string string_interp_warningReferenceNotInImportTable = "string_interp_warningReferenceNotInImportTable";
        internal const string string_interp_nested_warningReferenceNoInImportTable = "string_interp_nested_warningReferenceNoInImportTable";
        internal const string string_interp_nested_warningTrashedExportReference = "string_interp_nested_warningTrashedExportReference";
        internal const string string_interp_warningWrongPropertyTypingWrongMessage = "string_interp_warningWrongPropertyTypingWrongMessage";
        internal const string string_interp_nested_warningWrongClassPropertyTypingWrongMessage = "string_interp_nested_warningWrongClassPropertyTypingWrongMessage";
        internal const string string_interp_warningWrongObjectPropertyTypingWrongMessage = "string_interp_warningWrongObjectPropertyTypingWrongMessage";
        internal const string string_interp_nested_warningWrongObjectPropertyTypingWrongMessage = "string_interp_nested_warningWrongObjectPropertyTypingWrongMessage";
        internal const string string_interp_warningDelegatePropertyIsOutsideOfExportTable = "string_interp_warningDelegatePropertyIsOutsideOfExportTable";

        internal const string string_interp_XDoesNotSupportGameY = "string_interp_XDoesNotSupportGameY";
        internal const string string_interp_invalidNameIndexonNameProperty = "string_interp_invalidNameIndexonNameProperty";
    }
}
