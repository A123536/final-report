using System;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace VPipeRobotSolidWorks
{
    internal static class AuditSolidWorksInterferences
    {
        [STAThread]
        private static int Main(string[] args)
        {
            InterferenceDetectionMgr manager = null;
            SldWorks sw = null;
            ModelDoc2 model = null;
            try
            {
                if (args.Length != 1) throw new ArgumentException("Expected one assembly path.");
                sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                int error = 0, warning = 0;
                model = sw.OpenDoc6(Path.GetFullPath(args[0]),
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent |
                    (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly,
                    "", ref error, ref warning);
                if (model == null) throw new InvalidOperationException("Open failed: " + error);

                AssemblyDoc assembly = (AssemblyDoc)model;
                manager = assembly.InterferenceDetectionManager;
                manager.TreatCoincidenceAsInterference = false;
                manager.ShowIgnoredInterferences = true;
                manager.TreatSubAssembliesAsComponents = true;
                manager.IncludeMultibodyPartInterferences = true;
                manager.MakeInterferingPartsTransparent = false;
                manager.CreateFastenersFolder = false;
                // The pipe is hidden in the default editing display state, but
                // must still participate in a geometric clearance audit.
                manager.IgnoreHiddenBodies = false;
                manager.UseTransform = true;

                object[] raw = manager.GetInterferences() as object[];
                Console.WriteLine("INTERFERENCES=" + (raw == null ? 0 : raw.Length));
                if (raw != null)
                {
                    for (int i = 0; i < raw.Length; ++i)
                    {
                        Interference interference = (Interference)raw[i];
                        object[] components = interference.Components as object[];
                        string names = "";
                        if (components != null)
                        {
                            for (int j = 0; j < components.Length; ++j)
                            {
                                if (j > 0) names += " | ";
                                names += ((Component2)components[j]).Name2;
                            }
                        }
                        Console.WriteLine((i + 1) + "\t" +
                            (interference.Volume * 1.0e9).ToString("F6") + " mm3\t" + names);
                    }
                }
                manager.Done();
                manager = null;
                // InterferenceDetectionManager can set an in-memory save flag.
                // The document was opened read-only and is explicitly closed, so
                // the audit cannot rewrite the assembly or trigger a user prompt.
                sw.CloseDoc(model.GetTitle());
                model = null;
                return 0;
            }
            catch (Exception ex)
            {
                if (manager != null) manager.Done();
                if (sw != null && model != null) sw.CloseDoc(model.GetTitle());
                Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message);
                return 1;
            }
        }
    }
}
