using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace VPipeRobotSolidWorks
{
    internal static class NormalizeHarnessPartNames
    {
        private sealed class Rename
        {
            public string OldName;
            public string NewName;
            public Rename(string oldName, string newName) { OldName = oldName; NewName = newName; }
        }

        [STAThread]
        private static int Main()
        {
            try
            {
                SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                ModelDoc2 model = (ModelDoc2)sw.ActiveDoc;
                if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    throw new InvalidOperationException("Open the V-pipe robot assembly first.");
                AssemblyDoc assembly = (AssemblyDoc)model;
                string assemblyPath = model.GetPathName();
                string parts = Path.Combine(Path.GetDirectoryName(assemblyPath), "parts");

                Rename[] mappings = new Rename[] {
                    new Rename("POWER_HARNESS_D0p4_L11p6_0", "POWER_HARNESS_D4p4_L115p6_0"),
                    new Rename("POWER_HARNESS_D0p4_L6p1_1", "POWER_HARNESS_D4p4_L61p0_1"),
                    new Rename("POWER_HARNESS_D0p4_L6p0_2", "POWER_HARNESS_D4p4_L60p4_2"),
                    new Rename("POWER_HARNESS_D0p4_L9p3_3", "POWER_HARNESS_D4p4_L93p3_3"),
                    new Rename("SIGNAL_HARNESS_D0p3_L11p4_0", "SIGNAL_HARNESS_D3p2_L114p4_0"),
                    new Rename("SIGNAL_HARNESS_D0p3_L6p4_1", "SIGNAL_HARNESS_D3p2_L64p4_1"),
                    new Rename("SIGNAL_HARNESS_D0p3_L6p4_2", "SIGNAL_HARNESS_D3p2_L64p1_2"),
                    new Rename("SIGNAL_HARNESS_D0p3_L9p3_3", "SIGNAL_HARNESS_D3p2_L93p3_3")
                };

                List<string> oldPaths = new List<string>();
                foreach (Rename mapping in mappings)
                {
                    string oldPath = Path.Combine(parts, mapping.OldName + ".SLDPRT");
                    string newPath = Path.Combine(parts, mapping.NewName + ".SLDPRT");
                    if (!File.Exists(oldPath))
                    {
                        if (File.Exists(newPath)) continue;
                        throw new FileNotFoundException("Harness source part not found", oldPath);
                    }
                    File.Copy(oldPath, newPath, true);

                    object[] raw = assembly.GetComponents(false) as object[];
                    Component2 match = null;
                    foreach (object item in raw)
                    {
                        Component2 component = (Component2)item;
                        string componentPath = component.GetPathName();
                        if (string.Equals(componentPath, oldPath, StringComparison.OrdinalIgnoreCase))
                        {
                            match = component;
                            break;
                        }
                    }
                    if (match == null) throw new InvalidOperationException("Assembly reference not found: " + oldPath);
                    model.ClearSelection2(true);
                    match.Select4(false, null, false);
                    bool replaced = assembly.ReplaceComponents2(newPath, "", false,
                        (int)swReplaceComponentsConfiguration_e.swReplaceComponentsConfiguration_MatchName, true);
                    if (!replaced) throw new InvalidOperationException("ReplaceComponents2 failed for " + oldPath);
                    oldPaths.Add(oldPath);
                }

                model.ClearSelection2(true);
                model.ForceRebuild3(false);
                int saveCode = model.SaveAs3(assemblyPath, 0,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent);
                if (saveCode != 0) throw new InvalidOperationException("Assembly save failed: " + saveCode);

                // The assembly has been saved against the corrected copies. Close
                // obsolete source documents before removing the generated leftovers.
                foreach (string oldPath in oldPaths)
                {
                    sw.CloseDoc(Path.GetFileName(oldPath));
                    if (File.Exists(oldPath)) File.Delete(oldPath);
                }

                File.WriteAllText(Path.Combine(Path.GetDirectoryName(assemblyPath), "HARNESS_PART_MAPPING.txt"),
                    "Corrected generated harness part names; geometry and transforms are unchanged.\r\n" +
                    "POWER: diameter 4.4 mm; segment lengths 115.6, 61.0, 60.4, 93.3 mm.\r\n" +
                    "SIGNAL: diameter 3.2 mm; segment lengths 114.4, 64.4, 64.1, 93.3 mm.\r\n");
                Console.WriteLine("RENAMED=" + mappings.Length);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message);
                return 1;
            }
        }
    }
}
