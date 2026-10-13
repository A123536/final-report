using System;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace VPipeRobotSolidWorks
{
    internal static class CreateGeneralArrangementDrawing
    {
        private static View AddView(DrawingDoc drawing, string modelPath, string orientation,
                                    double x, double y, double scale)
        {
            View view = drawing.CreateDrawViewFromModelView3(modelPath, orientation, x, y, 0.0);
            if (view == null) throw new InvalidOperationException("Could not create drawing view " + orientation);
            view.UseSheetScale = 0;
            view.ScaleDecimal = scale;
            view.ReferencedConfiguration = "ID150_NOMINAL";
            view.DisplayState = "ROBOT_ONLY";
            view.SetDisplayMode3(false, (int)swDisplayMode_e.swFACETED_HIDDEN_GREYED, false, true);
            return view;
        }

        private static string FindStandardView(ModelDoc2 model, string english, string chinese, string fallback)
        {
            object raw = model.GetModelViewNames();
            object[] objects = raw as object[];
            if (objects != null)
            {
                foreach (object item in objects)
                {
                    string name = Convert.ToString(item);
                    if (name.IndexOf(english, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf(chinese, StringComparison.OrdinalIgnoreCase) >= 0)
                        return name;
                }
            }
            string[] strings = raw as string[];
            if (strings != null)
            {
                foreach (string name in strings)
                    if (name.IndexOf(english, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf(chinese, StringComparison.OrdinalIgnoreCase) >= 0)
                        return name;
            }
            return fallback;
        }

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                ModelDoc2 active = (ModelDoc2)sw.ActiveDoc;
                if (active != null && active.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
                    sw.CloseDoc(active.GetTitle());
                sw.CloseDoc("V_Pipe_Robot_Concept_ID150_GA.SLDDRW");

                string assemblyPath = args.Length > 0 ? Path.GetFullPath(args[0]) :
                    Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "cad", "solidworks", "model",
                        "V_Pipe_Robot_Concept_ID150.SLDASM"));
                int openError = 0, openWarning = 0;
                ModelDoc2 assemblyModel = sw.OpenDoc6(assemblyPath, (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref openError, ref openWarning);
                if (assemblyModel == null)
                    throw new InvalidOperationException("Could not open assembly: " + openError);
                Configuration assemblyConfiguration = (Configuration)assemblyModel.GetActiveConfiguration();
                assemblyConfiguration.ApplyDisplayState("ROBOT_ONLY");

                string topName = FindStandardView(assemblyModel, "Top", "上视", "*Top");
                string frontName = FindStandardView(assemblyModel, "Front", "前视", "*Front");
                string isoName = FindStandardView(assemblyModel, "Isometric", "等轴测", "*Isometric");
                string output = Path.GetDirectoryName(assemblyPath);
                string drawingTemplate = @"C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2026\templates\gb_a3.drwdot";
                if (!File.Exists(drawingTemplate)) throw new FileNotFoundException("A3 GB drawing template not found", drawingTemplate);

                ModelDoc2 drawingModel = (ModelDoc2)sw.NewDocument(drawingTemplate,
                    (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.0, 0.0);
                if (drawingModel == null) throw new InvalidOperationException("Could not create A3 drawing.");
                DrawingDoc drawing = (DrawingDoc)drawingModel;

                // SOLIDWORKS Top view looks along global Y and therefore exposes
                // the designed X-Z V geometry. Front exposes the transverse width.
                AddView(drawing, assemblyPath, topName, 0.105, 0.185, 0.36);
                AddView(drawing, assemblyPath, frontName, 0.305, 0.185, 0.30);
                AddView(drawing, assemblyPath, isoName, 0.220, 0.075, 0.28);

                drawing.CreateText2("V-SHAPED PIPE ROBOT - CONCEPT GENERAL ARRANGEMENT",
                    0.020, 0.278, 0.0, 0.006, 0.0);
                drawing.CreateText2("Nominal pipe ID 150 mm | Target range 140-160 mm",
                    0.020, 0.266, 0.0, 0.004, 0.0);
                drawing.CreateText2("Wheel envelope D60 x W28 mm | Link axis spacing 150 mm | Nominal V angle 108.757667 deg",
                    0.020, 0.256, 0.0, 0.0035, 0.0);
                drawing.CreateText2("CONCEPT MODEL - NOT FOR MANUFACTURING. Purchased-part, spring and PCB details remain provisional.",
                    0.020, 0.246, 0.0, 0.0035, 0.0);

                drawingModel.ForceRebuild3(false);
                drawingModel.ViewZoomtofit2();
                string drawingPath = Path.Combine(output, "V_Pipe_Robot_Concept_ID150_GA.SLDDRW");
                int saveCode = drawingModel.SaveAs3(drawingPath, 0,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent);
                if (saveCode != 0) throw new InvalidOperationException("Drawing save failed: " + saveCode);

                int error = 0, warning = 0;
                bool ok = drawingModel.Extension.SaveAs(Path.Combine(output, "general_arrangement.png"),
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref error, ref warning);
                if (!ok) throw new InvalidOperationException("Drawing preview export failed: " + error);

                Console.WriteLine("DRAWING=" + drawingPath);
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
