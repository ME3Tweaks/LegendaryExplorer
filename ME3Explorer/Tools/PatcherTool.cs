using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace ME3Explorer.Tools
{
    public static class PatcherTool
    {
        //How to use:
        //Call function PatcherTool.CreatePatch(X, Y, Z, B)
        // or           PatcherTool.CreatePatch(Y, Z, B)
        // All variables are string type, format path:
        // X = Working directory where EXE will be written and executed from.
        // If not used, a Temporary folder will be used.
        // Preferably where two needed files are located
        // Y = OldFile [needed] - path to file from which you created modification
        // Z = NewFile [needed] - the file you modified
        // B = Patch file [created] - the file that will be created, "patch", you can name 
        // it whatever you want and it does not require extension

        //Call function PatcherTool.ApplyPatch(X, Y, Z)
        // or           PatcherTool.ApplyPatch(Y, Z)
        //All variables are string type, format path:
        // X = Working directory where EXE will be written and executed from.
        // If not used, a Temporary folder will be used.
        // Preferably where two needed files are located
        // Y = OldFile [needed, overwritten] - file to which patch will be applied
        // B = Patch file [needed]
        

        // Basic formula for patching, for reference
        //  Create Patch: NewFile - OldFile = Patch
        //  Apply Patch:  OldFile + Patch = NewFile
        // After applying patch, resulting NewFile 
        // should be same to starting NewFile from which
        // the Patch was created

        //without workingDirectory, current user's temporary folder will be used
        public static void CreatePatch(string OriginalFile, string ModifiedFile, string Patch)
        {
            string workingDirectory = Path.GetDirectoryName(Application.ExecutablePath) + "\\exec\\";

            System.Diagnostics.ProcessStartInfo patchinfo = new System.Diagnostics.ProcessStartInfo(workingDirectory + "bsdiff.exe");
            patchinfo.WorkingDirectory = workingDirectory;
            patchinfo.UseShellExecute = false;
            patchinfo.CreateNoWindow = true;
            patchinfo.Arguments = " \"" + OriginalFile + "\" \"" + ModifiedFile + "\" \"" + Patch + "\"";

            System.Diagnostics.Process patch = new System.Diagnostics.Process();
            patch.StartInfo = patchinfo;
            patch.Start();
            patch.WaitForExit();
        }



        public static void ApplyPatch(string workingDirectory, string OrgFile, string ModFile, string Patch)
        {
            System.Diagnostics.ProcessStartInfo patchinfo = new System.Diagnostics.ProcessStartInfo(workingDirectory + "bspatch.exe");
            patchinfo.WorkingDirectory = workingDirectory;
            patchinfo.UseShellExecute = false;
            patchinfo.CreateNoWindow = true;
            patchinfo.Arguments = " \"" + OrgFile + "\" \"" + ModFile + "\" \"" + Patch + "\"";

            System.Diagnostics.Process patch = new System.Diagnostics.Process();
            patch.StartInfo = patchinfo;
            patch.Start();
            patch.WaitForExit();
        }

    }
}
