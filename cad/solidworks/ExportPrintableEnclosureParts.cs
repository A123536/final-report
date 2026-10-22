using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace VPipeRobotSolidWorks
{
    internal static class ExportPrintableEnclosureParts
    {
        private static readonly string[] Names = new string[] {
            "PowerBay_TopCover_72x62x22_t2", "PowerBay_BasePlate_72x62x2p4",
            "ControlBay_TopCover_72x62x22_t2", "ControlBay_BasePlate_72x62x2p4",
            "CameraHousing_Body_50x48x32_t2", "CameraHousing_FrontBezel_48x32x2p4_D19p5"
        };

        private static void Export(ModelDoc2 doc, string path)
        {
            int e = 0, w = 0;
            if (!doc.Extension.SaveAs(path, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null, ref e, ref w)) throw new InvalidOperationException("Export failed " + path + ": " + e);
        }

        private static void CheckBore(SldWorks sw, Body2 body, double x, double y,
                                      double z, double diameter, double length)
        {
            Modeler modeler = (Modeler)sw.GetModeler();
            Body2 probe = (Body2)modeler.CreateBodyFromCyl(new double[] {
                x/1000, y/1000, (z-length/2)/1000, 0, 0, 1, diameter/2000, length/1000 });
            Body2 copy = (Body2)body.Copy();
            int e = 0;
            object[] results = copy.Operations2((int)swBodyOperationType_e.SWBODYINTERSECT, probe, out e) as object[];
            if (results != null && results.Length > 0)
                throw new InvalidOperationException("Bore contains solid material at " + x + "," + y);
        }

        [STAThread]
        private static int Main(string[] args)
        {
            SldWorks sw = null;
            int oldUnits = 0, oldQuality = 0;
            bool oldBinary = false, prefs = false;
            try
            {
                if (args.Length != 1) throw new ArgumentException("Expected target model directory.");
                string root = Path.GetFullPath(args[0]);
                string output = Path.Combine(root, "printable_enclosure");
                Directory.CreateDirectory(Path.Combine(output, "STL_mm"));
                Directory.CreateDirectory(Path.Combine(output, "STEP_parts"));
                sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                sw.CloseAllDocuments(true);
                oldUnits = sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits);
                oldQuality = sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality);
                oldBinary = sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat);
                prefs = true;
                sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits, (int)swLengthUnit_e.swMM);
                sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, 1);
                sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat, true);
                MathUtility math = (MathUtility)sw.GetMathUtility();
                List<string> report = new List<string>();
                report.Add("Six-part enclosure geometry and export audit (mm)\r\n");
                for (int i = 0; i < Names.Length; ++i)
                {
                    int e = 0, w = 0;
                    string native = Path.Combine(root, "parts", Names[i] + ".SLDPRT");
                    ModelDoc2 model = sw.OpenDoc6(native, (int)swDocumentTypes_e.swDocPART,
                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref e, ref w);
                    if (model == null) throw new InvalidOperationException("Open failed: " + e);
                    object[] bodies = ((PartDoc)model).GetBodies2((int)swBodyType_e.swSolidBody, false) as object[];
                    if (bodies == null || bodies.Length != 1) throw new InvalidOperationException("Expected one solid: " + Names[i]);
                    Body2 body = (Body2)bodies[0];
                    FaultEntity faults = body.Check3;
                    int faultCount = faults == null ? 0 : faults.Count;
                    if (faultCount != 0) throw new InvalidOperationException("Invalid solid: " + Names[i] + ": " + faultCount);
                    if (i < 4)
                    {
                        foreach (double side in new double[] {-1,1})
                        foreach (double x in (i < 2 && side > 0 ? new double[] {-25,5} : new double[] {-25,25}))
                            CheckBore(sw, body, x, side*30.5, i%2==0 ? -7 : 0,
                                i%2==0 ? 4.0 : 3.2, i%2==0 ? 7.5 : 4.0);
                    }
                    CustomPropertyManager props = model.Extension.get_CustomPropertyManager("");
                    props.Add3("Part number", (int)swCustomInfoType_e.swCustomInfoText,
                        "E-" + (i+1).ToString("00"), (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                    props.Add3("Material specification", (int)swCustomInfoType_e.swCustomInfoText,
                        "PETG - FDM prototype; nominal density 1.27 g/cm3",
                        (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                    model.SaveAs3(native, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent);
                    Export(model, Path.Combine(output, "STEP_parts", "E-"+(i+1).ToString("00")+"_"+Names[i]+".STEP"));
                    Body2 printBody = (Body2)body.Copy();
                    double[] transform = i%2==0 && i<4
                        ? new double[] {1,0,0, 0,-1,0, 0,0,-1, 0,0,0.011, 1,0,0,0}
                        : i<4
                        ? new double[] {1,0,0, 0,1,0, 0,0,1, 0,0,0.0012, 1,0,0,0}
                        : i==4
                        ? new double[] {0,0,1, 0,1,0, -1,0,0, 0,0,0.025, 1,0,0,0}
                        : new double[] {0,0,-1, 0,1,0, 1,0,0, 0,0,0.0012, 1,0,0,0};
                    if (!printBody.ApplyTransform((MathTransform)math.CreateTransform(transform)))
                        throw new InvalidOperationException("Print orientation failed.");
                    ModelDoc2 printModel = (ModelDoc2)sw.NewDocument(
                        @"C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2026\templates\gb_part.prtdot", 0, 0, 0);
                    if (((PartDoc)printModel).CreateFeatureFromBody3(printBody, false,
                        (int)swCreateFeatureBodyOpts_e.swCreateFeatureBodyCheck)==null)
                        throw new InvalidOperationException("Print body failed.");
                    printModel.ForceRebuild3(false);
                    Export(printModel, Path.Combine(output, "STL_mm", "E-"+(i+1).ToString("00")+"_"+Names[i]+".stl"));
                    sw.CloseDoc(printModel.GetTitle());
                    double[] mass = body.GetMassProperties(1270.0) as double[];
                    report.Add("E-"+(i+1).ToString("00")+"\t"+Names[i]+"\tSOLIDS=1\tBODY_CHECK=0\tBORES_CLEAR="+(i<4?"4":"N/A")+
                        "\tVOLUME_MM3="+(mass[3]*1e9).ToString("F3",CultureInfo.InvariantCulture));
                    sw.CloseDoc(model.GetTitle());
                }
                File.WriteAllLines(Path.Combine(output,"PRINT_PART_GEOMETRY_AUDIT.txt"), report.ToArray());
                Console.WriteLine(string.Join("\r\n", report.ToArray()));
                Console.WriteLine("EXPORTED_STL_MM=6\r\nEXPORTED_STEP=6");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
            finally
            {
                if (prefs && sw != null)
                {
                    sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits, oldUnits);
                    sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, oldQuality);
                    sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat, oldBinary);
                }
            }
        }
    }
}
