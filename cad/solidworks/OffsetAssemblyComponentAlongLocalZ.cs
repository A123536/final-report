using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace VPipeRobotSolidWorks
{
    internal static class OffsetAssemblyComponentAlongLocalZ
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 3 && args.Length != 4)
                    throw new ArgumentException("Expected assembly path, component-name fragment, optional local axis X/Y/Z, and offset in mm.");
                string assemblyPath = Path.GetFullPath(args[0]);
                string fragment = args[1];
                string axisName = args.Length == 4 ? args[2].ToUpperInvariant() : "Z";
                double offsetM = double.Parse(args[args.Length - 1], CultureInfo.InvariantCulture) / 1000.0;
                int axisIndex = axisName == "X" ? 0 : axisName == "Y" ? 3 : axisName == "Z" ? 6 : -1;
                if (axisIndex < 0) throw new ArgumentException("Local axis must be X, Y, or Z.");

                SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                sw.CloseAllDocuments(true);
                int error = 0, warning = 0;
                ModelDoc2 model = sw.OpenDoc6(assemblyPath,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                    "", ref error, ref warning);
                if (model == null) throw new InvalidOperationException("Assembly open failed: " + error);
                AssemblyDoc assembly = (AssemblyDoc)model;
                object[] raw = assembly.GetComponents(false) as object[];
                if (raw == null) throw new InvalidOperationException("Could not enumerate components.");

                Component2 target = null;
                int matches = 0;
                foreach (object item in raw)
                {
                    Component2 component = (Component2)item;
                    if (component.Name2.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        target = component;
                        matches++;
                    }
                }
                if (matches != 1) throw new InvalidOperationException("Expected one matching component, found " + matches);

                MathTransform oldTransform = target.Transform2;
                double[] data = oldTransform.ArrayData as double[];
                if (data == null || data.Length < 12)
                    throw new InvalidOperationException("Unexpected transform array.");
                data[9] += data[axisIndex] * offsetM;
                data[10] += data[axisIndex + 1] * offsetM;
                data[11] += data[axisIndex + 2] * offsetM;
                MathUtility math = (MathUtility)sw.GetMathUtility();
                target.Transform2 = (MathTransform)math.CreateTransform(data);

                model.ForceRebuild3(false);
                int saveCode = model.SaveAs3(assemblyPath, 0,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent);
                if (saveCode != 0) throw new InvalidOperationException("Assembly save failed: " + saveCode);
                Console.WriteLine("OFFSET_COMPONENT=" + target.Name2);
                Console.WriteLine("LOCAL_" + axisName + "_OFFSET_MM=" +
                    (offsetM * 1000.0).ToString("F3", CultureInfo.InvariantCulture));
                sw.CloseDoc(model.GetTitle());
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
