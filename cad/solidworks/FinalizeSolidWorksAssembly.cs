using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace VPipeRobotSolidWorks
{
    internal static class FinalizeSolidWorksAssembly
    {
        private static double[] Appearance(double r, double g, double b, double transparency)
        {
            return new double[] { r, g, b, 0.35, 0.70, 0.25, 0.25, transparency, 0.0 };
        }

        private static string[] DisplayStateNames(Configuration configuration)
        {
            object raw = configuration.GetDisplayStates();
            string[] strings = raw as string[];
            if (strings != null) return strings;
            object[] objects = raw as object[];
            if (objects == null) return new string[0];
            string[] result = new string[objects.Length];
            for (int i = 0; i < objects.Length; ++i) result[i] = Convert.ToString(objects[i]);
            return result;
        }

        private static bool Contains(string[] values, string expected)
        {
            foreach (string value in values)
                if (string.Equals(value, expected, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void SetText(CustomPropertyManager manager, string name, string value)
        {
            manager.Add3(name, (int)swCustomInfoType_e.swCustomInfoText, value,
                (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
        }

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                ModelDoc2 model;
                if (args.Length > 0)
                {
                    int openError = 0, openWarning = 0;
                    model = sw.OpenDoc6(Path.GetFullPath(args[0]),
                        (int)swDocumentTypes_e.swDocASSEMBLY,
                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                        "", ref openError, ref openWarning);
                    if (model == null) throw new InvalidOperationException("Assembly open failed: " + openError);
                }
                else
                {
                    model = (ModelDoc2)sw.ActiveDoc;
                }
                if (model == null || model.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    throw new InvalidOperationException("Open the V-pipe robot assembly before running this utility.");

                string assemblyPath = model.GetPathName();
                string output = Path.GetDirectoryName(assemblyPath);
                AssemblyDoc assembly = (AssemblyDoc)model;
                object[] raw = assembly.GetComponents(false) as object[];
                if (raw == null) throw new InvalidOperationException("Could not enumerate assembly components.");

                Component2 pipe = null;
                List<string> names = new List<string>();
                int hidden = 0;
                foreach (object item in raw)
                {
                    Component2 component = (Component2)item;
                    string name = component.Name2;
                    names.Add(name);
                    if (component.Visible == (int)swComponentVisibilityState_e.swComponentHidden) hidden++;
                    if (name.IndexOf("Pipe_ID150_OD154_L430", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pipe = component;
                        component.MaterialPropertyValues = Appearance(0.47, 0.69, 0.82, 0.88);
                    }
                    else if (name.IndexOf("EquipmentBayShell_70x60x20", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        component.MaterialPropertyValues = Appearance(0.52, 0.66, 0.80, 0.60);
                    }
                    else if (name.IndexOf("FutureCameraBay_50x48x32", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        component.MaterialPropertyValues = Appearance(0.57, 0.31, 0.75, 0.50);
                    }
                }
                if (pipe == null) throw new InvalidOperationException("Pipe component was not found.");

                Configuration configuration = (Configuration)model.GetActiveConfiguration();
                if (!string.Equals(configuration.Name, "ID150_NOMINAL", StringComparison.OrdinalIgnoreCase))
                    configuration.Name = "ID150_NOMINAL";

                string[] states = DisplayStateNames(configuration);
                if (!Contains(states, "ROBOT_ONLY"))
                {
                    string source = states.Length > 0 ? states[0] : null;
                    if (source != null && !string.Equals(source, "WITH_PIPE", StringComparison.OrdinalIgnoreCase))
                        configuration.RenameDisplayState(source, "ROBOT_ONLY");
                    else
                        configuration.CreateDisplayState("ROBOT_ONLY");
                }
                states = DisplayStateNames(configuration);
                if (!Contains(states, "WITH_PIPE")) configuration.CreateDisplayState("WITH_PIPE");

                pipe.SetVisibilityInAsmDisplayStates((int)swComponentVisibilityState_e.swComponentVisible,
                    (int)swInConfigurationOpts_e.swSpecifyConfiguration, new string[] { "WITH_PIPE" });
                pipe.SetVisibilityInAsmDisplayStates((int)swComponentVisibilityState_e.swComponentHidden,
                    (int)swInConfigurationOpts_e.swSpecifyConfiguration, new string[] { "ROBOT_ONLY" });

                CustomPropertyManager properties = model.Extension.get_CustomPropertyManager("");
                SetText(properties, "Project", "V-shaped in-pipe inspection robot");
                SetText(properties, "Model status", "Revised interference-checked concept - not manufacturing release");
                SetText(properties, "Pipe ID range", "140-160 mm");
                SetText(properties, "Nominal pipe ID", "150 mm");
                SetText(properties, "Wheel envelope", "D60 x W28 mm; D26 core and roller envelopes");
                SetText(properties, "Link axis spacing", "150 mm");
                SetText(properties, "Nominal V angle", "108.757667 deg");
                SetText(properties, "Mechanical stops", "90-125 deg");
                SetText(properties, "Gear train", "module 1: 25T motor / 25T idler / 40T wheel; ratio 1.6");
                SetText(properties, "Equipment bays", "70 x 60 x 20 mm open-bottom shells, 2 mm wall");
                SetText(properties, "Interference verification", "140/150/160 mm: zero unintended solid interference");
                SetText(properties, "Target mass", "1.6 kg; maximum 1.8 kg");
                SetText(properties, "Target speed", "0.15 m/s");
                SetText(properties, "Target travel", "10 m with 15 m tether");

                model.ClearSelection2(true);
                configuration.ApplyDisplayState("WITH_PIPE");
                model.ForceRebuild3(false);
                model.ShowNamedView2("*Isometric", (int)swStandardViews_e.swIsometricView);
                model.ViewZoomtofit2();
                int error = 0, warning = 0;
                bool ok = model.Extension.SaveAs(Path.Combine(output, "overview_with_pipe.png"),
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref error, ref warning);
                if (!ok) throw new InvalidOperationException("Pipe overview export failed: " + error);

                configuration.ApplyDisplayState("ROBOT_ONLY");
                model.ForceRebuild3(false);
                model.ShowNamedView2("*Isometric", (int)swStandardViews_e.swIsometricView);
                model.ViewZoomtofit2();
                error = 0; warning = 0;
                ok = model.Extension.SaveAs(Path.Combine(output, "overview_robot_only.png"),
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref error, ref warning);
                if (!ok) throw new InvalidOperationException("Robot overview export failed: " + error);

                int saveCode = model.SaveAs3(assemblyPath, 0,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent);
                if (saveCode != 0) throw new InvalidOperationException("Assembly save failed: " + saveCode);

                names.Sort(StringComparer.OrdinalIgnoreCase);
                string[] finalStates = DisplayStateNames(configuration);
                File.WriteAllText(Path.Combine(output, "SOLIDWORKS_ASSEMBLY_AUDIT.txt"),
                    "Assembly: " + assemblyPath + "\r\n" +
                    "Configuration: " + configuration.Name + "\r\n" +
                    "Display states: " + string.Join(", ", finalStates) + "\r\n" +
                    "Top-level component count: " + raw.Length + "\r\n" +
                    "Hidden before finalization: " + hidden + "\r\n" +
                    "Default saved state: pipe hidden; robot visible\r\n" +
                    "Pipe display override: transparency 0.88\r\n\r\n" +
                    string.Join("\r\n", names.ToArray()) + "\r\n");

                Console.WriteLine("COMPONENTS=" + raw.Length);
                Console.WriteLine("SAVED=" + assemblyPath);
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
