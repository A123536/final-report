using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace VPipeRobotSolidWorks
{
    internal static class RelinkSolidWorksReferences
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 5)
                    throw new ArgumentException("Expected official assembly, official drawing, staged assembly, staged reference list and official parts directory.");
                string assemblyPath = Path.GetFullPath(args[0]);
                string drawingPath = Path.GetFullPath(args[1]);
                string stagedAssembly = Path.GetFullPath(args[2]);
                string referenceList = Path.GetFullPath(args[3]);
                string partsDirectory = Path.GetFullPath(args[4]);
                SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                sw.CloseAllDocuments(true);

                int replaced = 0;
                foreach (string line in File.ReadAllLines(referenceList))
                {
                    string oldPath = Path.GetFullPath(line.Trim());
                    string newPath = Path.Combine(partsDirectory, Path.GetFileName(oldPath));
                    if (!File.Exists(newPath)) throw new FileNotFoundException("Official part missing", newPath);
                    if (sw.ReplaceReferencedDocument(assemblyPath, oldPath, newPath)) replaced++;
                }
                bool drawingRelinked = sw.ReplaceReferencedDocument(drawingPath, stagedAssembly, assemblyPath);

                int error = 0, warning = 0;
                ModelDoc2 model = sw.OpenDoc6(assemblyPath, (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    "", ref error, ref warning);
                if (model == null) throw new InvalidOperationException("Official assembly open failed: " + error);
                object[] components = ((AssemblyDoc)model).GetComponents(false) as object[];
                SortedSet<string> badPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (object item in components)
                {
                    string path = Path.GetFullPath(((Component2)item).GetPathName());
                    if (!path.StartsWith(partsDirectory + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase)) badPaths.Add(path);
                }
                Console.WriteLine("PART_REFERENCES_RELINKED=" + replaced);
                Console.WriteLine("DRAWING_RELINKED=" + drawingRelinked);
                Console.WriteLine("COMPONENT_INSTANCES=" + components.Length);
                Console.WriteLine("OUTSIDE_OFFICIAL_PARTS=" + badPaths.Count);
                foreach (string path in badPaths) Console.WriteLine("BAD\t" + path);
                sw.CloseAllDocuments(true);
                return badPaths.Count == 0 ? 0 : 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message);
                return 1;
            }
        }
    }
}
