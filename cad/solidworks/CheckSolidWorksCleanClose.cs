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
                if (args.Length != 1) throw new ArgumentException("Expected one assembly path.");
                SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                sw.CloseAllDocuments(true);
                int error = 0, warning = 0;
                ModelDoc2 assembly = sw.OpenDoc6(Path.GetFullPath(args[0]),
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    "", ref error, ref warning);
                if (assembly == null) throw new InvalidOperationException("Open failed: " + error);

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
                bool closed = sw.CloseAllDocuments(true);
                Console.WriteLine("CLOSED_WITHOUT_SAVE=" + closed);
                return dirty == 0 && closed ? 0 : 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message);
                return 1;
            }
        }
    }
}
