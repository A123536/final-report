using System;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace VPipeRobotSolidWorks
{
    internal static class CheckSolidWorksCleanClose
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length < 1 || args.Length > 2)
                    throw new ArgumentException("Expected a saved assembly/drawing path and optional --show.");
                SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                sw.CloseAllDocuments(true);
                int error = 0, warning = 0;
                string documentPath = Path.GetFullPath(args[0]);
                int type = Path.GetExtension(documentPath).Equals(".SLDDRW", StringComparison.OrdinalIgnoreCase)
                    ? (int)swDocumentTypes_e.swDocDRAWING : (int)swDocumentTypes_e.swDocASSEMBLY;
                ModelDoc2 assembly = sw.OpenDoc6(documentPath,
                    type,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    "", ref error, ref warning);
                if (assembly == null) throw new InvalidOperationException("Open failed: " + error);

                string[] dependencies = sw.GetDocumentDependencies2(documentPath, true, true, false) as string[];
                int temporaryReferences = 0, missingReferences = 0, dependencyFiles = 0;
                if (dependencies != null)
                    foreach (string dependency in dependencies)
                    {
                        if (dependency.IndexOf(".codex_tmp", StringComparison.OrdinalIgnoreCase) >= 0)
                            temporaryReferences++;
                        if (Path.IsPathRooted(dependency))
                        {
                            dependencyFiles++;
                            if (!File.Exists(dependency)) missingReferences++;
                        }
                    }
                Console.WriteLine("TEMPORARY_REFERENCES=" + temporaryReferences);
                Console.WriteLine("DEPENDENCY_FILES=" + dependencyFiles);
                Console.WriteLine("MISSING_REFERENCES=" + missingReferences);

                object[] documents = sw.GetDocuments() as object[];
                int dirty = 0;
                Console.WriteLine("DOCUMENTS=" + (documents == null ? 0 : documents.Length));
                if (documents != null)
                {
                    foreach (object item in documents)
                    {
                        ModelDoc2 document = (ModelDoc2)item;
                        bool flag = document.GetSaveFlag();
                        if (flag) dirty++;
                        Console.WriteLine((flag ? "DIRTY\t" : "CLEAN\t") + document.GetTitle());
                    }
                }
                Console.WriteLine("DIRTY_COUNT=" + dirty);
                bool show = args.Length == 2 && args[1] == "--show";
                bool closed = show ? true : sw.CloseAllDocuments(true);
                Console.WriteLine("CLOSED_WITHOUT_SAVE=" + closed);
                if (show) { sw.Visible = true; Console.WriteLine("LEFT_OPEN_FOR_REVIEW=True"); }
                return dirty == 0 && closed && temporaryReferences == 0 && missingReferences == 0 ? 0 : 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message);
                return 1;
            }
        }
    }
}
