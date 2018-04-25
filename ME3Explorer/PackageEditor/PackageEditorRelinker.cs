using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer
{
    public partial class PackageEditor
    {
        /// <summary>
        /// Attempts to relink unreal property data using propertycollection when cross porting an export
        /// </summary>
        private List<string> relinkObjects2(IMEPackage importpcc)
        {
            List<string> relinkResults = new List<string>();
            //relink each modified export

            //We must convert this to a list, as this list will be updated as imports are cross mapped during relinking.
            //This process speeds up same-relinks later.
            //This is a list because otherwise we would get a concurrent modification exception.
            //Since we only enumerate exports and append imports to this list we will not need to worry about recursive links
            //I am sure this won't come back to be a pain for me.
            List<KeyValuePair<int, int>> crossPCCObjectMappingList = crossPCCObjectMap.ToList();
            for (int i = 0; i < crossPCCObjectMappingList.Count; i++)
            {
                KeyValuePair<int, int> entry = crossPCCObjectMappingList[i];
                if (entry.Key > 0)
                {
                    PropertyCollection transplantProps = importpcc.Exports[entry.Key].GetProperties();
                    relinkResults.AddRange(relinkPropertiesRecursive(importpcc, pcc, transplantProps, crossPCCObjectMappingList));
                    pcc.getExport(entry.Value).WriteProperties(transplantProps);
                }
            }

            return relinkResults;
        }

        private List<string> relinkPropertiesRecursive(IMEPackage importingPCC, IMEPackage destinationPCC, PropertyCollection transplantProps, List<KeyValuePair<int, int>> crossPCCObjectMappingList)
        {
            List<string> relinkResults = new List<string>();
            foreach (UProperty prop in transplantProps)
            {
                if (prop is StructProperty)
                {
                    relinkResults.AddRange(relinkPropertiesRecursive(importingPCC, destinationPCC, (prop as StructProperty).Properties, crossPCCObjectMappingList));
                }
                else if (prop is ArrayProperty<StructProperty>)
                {
                    foreach (StructProperty arrayStructProperty in prop as ArrayProperty<StructProperty>)
                    {
                        relinkResults.AddRange(relinkPropertiesRecursive(importingPCC, destinationPCC, arrayStructProperty.Properties, crossPCCObjectMappingList));
                    }
                }
                else if (prop is ArrayProperty<ObjectProperty>)
                {
                    foreach (ObjectProperty objProperty in prop as ArrayProperty<ObjectProperty>)
                    {
                        string result = relinkObjectProperty(importingPCC, destinationPCC, objProperty, crossPCCObjectMappingList);
                        if (result != null)
                        {
                            relinkResults.Add(result);
                        }
                    }
                }
                if (prop is ObjectProperty)
                {
                    //relink
                    string result = relinkObjectProperty(importingPCC, destinationPCC, prop as ObjectProperty, crossPCCObjectMappingList);
                    if (result != null)
                    {
                        relinkResults.Add(result);
                    }
                }
            }
            return relinkResults;
        }

        private string relinkObjectProperty(IMEPackage importingPCC, IMEPackage destinationPCC, ObjectProperty objProperty, List<KeyValuePair<int, int>> crossPCCObjectMappingList)
        {
            if (objProperty.Value == 0)
            {
                return null; //do not relink 0
            }
            int sourceObjReference = objProperty.Value;

            if (sourceObjReference > 0)
            {
                sourceObjReference--; //make 0 based for mapping.
            }
            if (sourceObjReference < 0)
            {
                sourceObjReference++; //make 0 based for mapping.
            }
            if (objProperty.Name != null)
            {
                Debug.WriteLine(objProperty.Name);
            }
            KeyValuePair<int, int> mapping = crossPCCObjectMappingList.Where(pair => pair.Key == sourceObjReference).FirstOrDefault();
            var defaultKVP = default(KeyValuePair<int, int>); //struct comparison

            if (!mapping.Equals(defaultKVP))
            {
                //relink
                objProperty.Value = (mapping.Value - 1);
                IEntry entry = destinationPCC.getEntry(mapping.Value - 1);
                string s = "";
                if (entry != null)
                {
                    s = entry.GetFullPath;
                }
                Debug.WriteLine("Relink hit: " + sourceObjReference + objProperty.Name + ": " + s);
            }
            else if (objProperty.Value < 0) //It's an unmapped import
            {
                //objProperty is currently pointing to importingPCC as that is where we read the properties from
                int n = objProperty.Value;
                int origvalue = n;
                int importZeroIndex = Math.Abs(n) - 1;
                //Debug.WriteLine("Relink miss, attempting JIT relink on " + n + " " + rootNode.Text);
                if (n < 0 && importZeroIndex < importingPCC.ImportCount)
                {
                    //Get the original import
                    ImportEntry origImport = importingPCC.getImport(importZeroIndex);
                    string origImportFullName = origImport.GetFullPath;
                    //Debug.WriteLine("We should import " + origImport.GetFullPath);

                    ImportEntry crossImport = getOrAddCrossImport(origImportFullName, importingPCC, destinationPCC);

                    if (crossImport != null)
                    {
                        crossPCCObjectMappingList.Add(new KeyValuePair<int, int>(sourceObjReference, crossImport.UIndex + 1)); //add to mapping to speed up future relinks
                        objProperty.Value = crossImport.UIndex;
                        Debug.WriteLine("Relink hit: Dynamic CrossImport for " + origvalue + " " + importingPCC.getEntry(origvalue).GetFullPath + " -> " + objProperty.Value);

                    }
                    else
                    {
                        Debug.WriteLine("Relink failed: CrossImport porting failed for " + objProperty.Name + " " + objProperty.Value + ": " + importingPCC.getEntry(origvalue).GetFullPath);
                        return "Relink failed: CrossImport porting failed for " + objProperty.Name + " " + objProperty.Value + " " + destinationPCC.getEntry(objProperty.Value).GetFullPath;
                    }
                }
            }
            else
            {
                Debug.WriteLine("Relink failed: " + objProperty.Name + " " + objProperty.Value + " " + importingPCC.getEntry(objProperty.Value).GetFullPath);
                return "Relink failed: " + objProperty.Name + " " + objProperty.Value + " " + importingPCC.getEntry(objProperty.Value).GetFullPath;
            }
            return null;
        }

        /// <summary>
        /// Adds an import from the importingPCC to the destinationPCC with the specified importFullName, or returns the existing one if it can be found. 
        /// This method will look at importingPCC's import upstream chain and check for the most downstream one's existence in destinationPCC, 
        /// including if none can be founc (in which case the entire upstream is copied). It will then create new imports to match the remaining 
        /// downstream ones and return the originally named import, however now located in destinationPCC.
        /// </summary>
        /// <param name="importFullName">GetFullPath() of an import from ImportingPCC</param>
        /// <param name="importingPCC">PCC to import imports from</param>
        /// <param name="destinationPCC">PCC to add imports to</param>
        /// <returns></returns>
        private ImportEntry getOrAddCrossImport(string importFullName, IMEPackage importingPCC, IMEPackage destinationPCC)
        {
            //This code is kind of ugly, sorry.

            //see if this import exists locally
            foreach (ImportEntry imp in destinationPCC.Imports)
            {
                if (imp.GetFullPath == importFullName)
                {
                    return imp;
                }
            }

            //Import doesn't exist, so we're gonna need to add it
            //But first we need to figure out what needs to be added upstream as links
            //Search upstream until we find something, or we can't get any more upstreams
            string[] importParts = importFullName.Split('.');
            List<int> upstreamLinks = new List<int>(); //0 = top level, 1 = next level... n = what we wanted to import
            int upstreamCount = 1;

            ImportEntry upstreamImport = null;
            //get number of required upstream imports that do not yet exist
            while (upstreamCount < importParts.Count())
            {
                string upstream = String.Join(".", importParts, 0, importParts.Count() - upstreamCount);
                foreach (ImportEntry imp in destinationPCC.Imports)
                {
                    if (imp.GetFullPath == upstream)
                    {
                        upstreamImport = imp;
                        break;
                    }
                }

                if (upstreamImport != null)
                {
                    //We found an upsteam import that already exists
                    break;
                }
                upstreamCount++;
            }

            IExportEntry donorUpstreamExport = null;
            ImportEntry mostdownstreamimport = null;
            if (upstreamImport == null)
            {
                //We have to import the entire upstream chain
                string fullobjectname = importParts[0];
                ImportEntry donorTopLevelImport = null;
                foreach (ImportEntry imp in importingPCC.Imports) //importing side info we will move to our dest pcc
                {
                    if (imp.GetFullPath == fullobjectname)
                    {
                        donorTopLevelImport = imp;
                        break;
                    }
                }

                if (donorTopLevelImport == null)
                {
                    //This is issue KinkoJiro had. It is aborting relinking at this step. Will need to find a way to
                    //work with exports as parents for imports which will block it.
                    //Update: This has been partially implemented.
                    Debug.WriteLine("No upstream import was found in the source file. It's probably an export: " + importFullName);
                    foreach (IExportEntry exp in destinationPCC.Exports) //importing side info we will move to our dest pcc
                    {
                        //Console.WriteLine(exp.GetFullPath);
                        if (exp.GetFullPath == fullobjectname)
                        {
                            // = imp;
                            //We will need to find a way to cross map this as this will block cross import mapping unless these exports already exist.
                            Debug.WriteLine("FOUND UPSTREAM, AS EXPORT!");
                            donorUpstreamExport = exp;
                            upstreamCount--; //level 1 now from the top down
                                             //Create new import with this as higher IDK
                            break;
                        }
                    }
                    if (donorUpstreamExport == null)
                    {
                        Debug.WriteLine("An error has occured. Could not find an upstream import or export for relinking: " + fullobjectname + " from " + pcc.FileName);
                        return null;
                    }
                }

                if (donorUpstreamExport == null)
                {
                    //Create new toplevel import and set that as the most downstream one. (top = bottom at this point)
                    int downstreamPackageName = destinationPCC.FindNameOrAdd(donorTopLevelImport.PackageFile);
                    int downstreamClassName = destinationPCC.FindNameOrAdd(donorTopLevelImport.ClassName);
                    int downstreamName = destinationPCC.FindNameOrAdd(fullobjectname);

                    mostdownstreamimport = new ImportEntry(destinationPCC);
                    // mostdownstreamimport.idxLink = downstreamLinkIdx; ??
                    mostdownstreamimport.idxClassName = downstreamClassName;
                    mostdownstreamimport.idxObjectName = downstreamName;
                    mostdownstreamimport.idxPackageName = downstreamPackageName;
                    destinationPCC.addImport(mostdownstreamimport); //Add new top level downstream import
                    upstreamImport = mostdownstreamimport;
                    upstreamCount--; //level 1 now from the top down
                                     //return null;
                }
            }

            //Have an upstream import, now we need to add downstream imports.
            while (upstreamCount > 0)
            {
                upstreamCount--;
                string fullobjectname = String.Join(".", importParts, 0, importParts.Count() - upstreamCount);
                ImportEntry donorImport = null;

                //Get or create names for creating import and get upstream linkIdx
                int downstreamName = destinationPCC.FindNameOrAdd(importParts[importParts.Count() - upstreamCount - 1]);
                foreach (ImportEntry imp in importingPCC.Imports) //importing side info we will move to our dest pcc
                {
                    if (imp.GetFullPath == fullobjectname)
                    {
                        donorImport = imp;
                        break;
                    }
                }
                int downstreamPackageName = destinationPCC.FindNameOrAdd(Path.GetFileNameWithoutExtension(donorImport.PackageFile));
                int downstreamClassName = destinationPCC.FindNameOrAdd(donorImport.ClassName);

                mostdownstreamimport = new ImportEntry(destinationPCC);
                mostdownstreamimport.idxLink = donorUpstreamExport == null ? upstreamImport.UIndex : donorUpstreamExport.UIndex;
                mostdownstreamimport.idxClassName = downstreamClassName;
                mostdownstreamimport.idxObjectName = downstreamName;
                mostdownstreamimport.idxPackageName = downstreamPackageName;
                destinationPCC.addImport(mostdownstreamimport);
                upstreamImport = mostdownstreamimport;
            }
            return mostdownstreamimport;
        }
    }
}
