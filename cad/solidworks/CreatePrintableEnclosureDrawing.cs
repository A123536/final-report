using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace VPipeRobotSolidWorks
{
    internal static class CreatePrintableEnclosureDrawing
    {
        private sealed class Item
        {
            public string PartNo;
            public string FileName;
            public string Description;
            public int Quantity;
            public double X;
            public double Y;
            public double Z;

            public Item(string partNo, string fileName, string description, int quantity,
                        double x, double y, double z)
            {
                PartNo = partNo;
                FileName = fileName;
                Description = description;
                Quantity = quantity;
                X = x;
                Y = y;
                Z = z;
            }
        }

        private static double M(double mm) { return mm / 1000.0; }

        private static MathTransform Translation(MathUtility math, double x, double y, double z)
        {
            double[] data = new double[] {
                1, 0, 0,
                0, 1, 0,
                0, 0, 1,
                M(x), M(y), M(z),
                1, 0, 0, 0
            };
            return (MathTransform)math.CreateTransform(data);
        }

        private static Component2 Add(AssemblyDoc assembly, MathUtility math,
                                      string partPath, string name,
                                      double x, double y, double z)
        {
            Component2 component = assembly.AddComponent5(partPath,
                (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                "", false, "", 0, 0, 0);
            if (component == null) throw new InvalidOperationException("Could not add " + name);
            component.Name2 = name;
            component.Transform2 = Translation(math, x, y, z);
            component.Select4(false, null, false);
            assembly.FixComponent();
            return component;
        }

        private static string FindStandardView(ModelDoc2 model, string english,
                                               string chinese, string fallback)
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

        private static View AddView(DrawingDoc drawing, string modelPath, string orientation,
                                    double x, double y, double scale)
        {
            View view = drawing.CreateDrawViewFromModelView3(modelPath, orientation, x, y, 0);
            if (view == null) throw new InvalidOperationException("Could not create " + orientation + " view.");
            view.UseSheetScale = 0;
            view.ScaleDecimal = scale;
            if (modelPath.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase))
                view.ReferencedConfiguration = "PRINTABLE_PARTS_EXPLODED";
            view.SetDisplayMode3(false, (int)swDisplayMode_e.swFACETED_HIDDEN_GREYED, false, true);
            return view;
        }

        private static void Text(DrawingDoc drawing, string value,
                                 double x, double y, double z, double height, double angle)
        {
            Note note = (Note)drawing.CreateText2(value, x, y, z, height, angle);
            if (note == null) throw new InvalidOperationException("Could not create note.");
            Annotation annotation = (Annotation)note.GetAnnotation();
            TextFormat format = (TextFormat)annotation.GetTextFormat(0);
            format.TypeFaceName = "Microsoft YaHei";
            format.CharHeight = height;
            format.WidthFactor = 1.0;
            format.CharSpacingFactor = 1.0;
            annotation.SetTextFormat(0, false, format);
        }

        private static void DetailSheet(SldWorks sw, DrawingDoc drawing, ModelDoc2 drawingModel,
                                        string partsRoot, Item item, int index)
        {
            string sheetName = item.PartNo;
            if (!drawing.NewSheet3(sheetName, (int)swDwgPaperSizes_e.swDwgPaperA3size,
                (int)swDwgTemplates_e.swDwgTemplateNone, 1, 1, true, "", 0.420, 0.297, ""))
                throw new InvalidOperationException("Cannot create detail sheet " + sheetName);
            ((Sheet)drawing.GetCurrentSheet()).SheetFormatVisible = false;
            string path = Path.Combine(partsRoot, item.FileName);
            ModelDoc2 part = (ModelDoc2)sw.GetOpenDocumentByName(path);
            if (part == null)
            {
                int e=0,w=0;
                part = sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref e, ref w);
            }
            string face = index < 4 ? FindStandardView(part,"Front","前视","*Front")
                : FindStandardView(part,"Right","右视","*Right");
            AddView(drawing, path, face, 0.100, 0.182, 1.25);
            AddView(drawing, path, FindStandardView(part,"Top","上视","*Top"), 0.260, 0.185, 1.25);
            AddView(drawing, path, FindStandardView(part,"Isometric","等轴测","*Isometric"), 0.335, 0.080, 1.0);
            Text(drawing, item.PartNo + " | " + item.Description, 0.018, 0.279, 0, 0.0045, 0);
            Text(drawing, "PETG / FDM | QTY 1 | mm | Orthographic scale 1.25:1 | ISO 1:1",
                0.018, 0.266, 0, 0.0032, 0);
            string[] notes;
            if (index < 4)
            {
                bool cover = index % 2 == 0;
                notes = new string[] {
                    cover ? "Body: 72 x 62 x 22; cavity 68 x 58 x 20; wall/roof 2.0."
                          : "Plate: 72 x 62 x 2.4; overall height with tabs 4.4.",
                    "Overall width with fastening bosses/ears: 69.0.",
                    cover ? "4 x D4.2 through pilots in D8 x 8 bosses; M3 heat-set inserts."
                          : "4 x D3.4 clearance holes. Do not tap the printed plate.",
                    "Hole coordinates relative to body centre (X,Y):",
                    index < 2 ? "(-25,-30.5), (25,-30.5), (-25,30.5), (5,30.5)."
                              : "(-25,-30.5), (25,-30.5), (-25,30.5), (25,30.5).",
                    cover ? "2 x cable slots at end-wall rim: 12 wide x 5.1 clear height."
                          : "6 locating tabs: 2.0 engagement; 0.30 clearance to cavity.",
                    "4 x M3x10 LOW BUTTON heads: D<=5.7, H<=1.7; M3 inserts L5-6.",
                    cover ? "Print roof on bed, opening upward; support boss starts locally."
                          : "Print flat underside on bed; locating tabs upward."
                };
            }
            else
            {
                notes = new string[] {
                    index == 4 ? "Body: 50 x 48 x 32; wall 2.0; front open."
                               : "Front plate: 48 x 32 x 2.4; D19.5 lens aperture.",
                    "4 holes along X; (Y,Z) relative to optical axis:",
                    "(-19.5,-11.5), (-19.5,11.5), (19.5,-11.5), (19.5,11.5).",
                    index == 4 ? "4 x D4.0 pilots in D6 x 9 bosses; M2.5 heat-set inserts."
                               : "4 x D3.0 screw clearance; tabs engage 2.0 with 0.50 clearance.",
                    index == 4 ? "2 rear-edge cable slots: 9 wide x 12 high; add split grommets."
                               : "Locate by four tabs, then close with 4 x M2.5x8 screws.",
                    index == 4 ? "Print rear wall down; support the two cable-entry bridges."
                               : "Print front face down; tabs grow upward.",
                    "Camera/lens measured mounting details remain to be fitted.",
                    "Pilot sizes must match the purchased insert; print a test first."
                };
            }
            for (int line=0; line<notes.Length; ++line)
                Text(drawing, notes[line], 0.018, 0.111-line*0.010, 0, 0.0030, 0);
            Text(drawing, "V-Pipe Robot | Printable enclosure Rev C | Sheet " + (index+2) + "/7 | Prototype",
                0.018, 0.014, 0, 0.0028, 0);
            drawingModel.ForceRebuild3(false);
        }

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 1)
                    throw new ArgumentException("Expected the model output directory.");

                string modelRoot = Path.GetFullPath(args[0]);
                string partsRoot = Path.Combine(modelRoot, "parts");
                string output = Path.Combine(modelRoot, "printable_enclosure");
                Directory.CreateDirectory(output);

                Item[] items = new Item[] {
                    new Item("E-01", "PowerBay_TopCover_72x62x22_t2.SLDPRT",
                        "Power bay top cover, 2.0 mm wall", 1, -92, 0, 22),
                    new Item("E-02", "PowerBay_BasePlate_72x62x2p4.SLDPRT",
                        "Power bay base plate, 2.4 mm", 1, -92, 0, -22),
                    new Item("E-03", "ControlBay_TopCover_72x62x22_t2.SLDPRT",
                        "Control bay top cover, 2.0 mm wall", 1, 0, 0, 22),
                    new Item("E-04", "ControlBay_BasePlate_72x62x2p4.SLDPRT",
                        "Control bay base plate, 2.4 mm", 1, 0, 0, -22),
                    new Item("E-05", "CameraHousing_Body_50x48x32_t2.SLDPRT",
                        "Camera housing body, rear-wall print orientation", 1, 88, 0, 0),
                    new Item("E-06", "CameraHousing_FrontBezel_48x32x2p4_D19p5.SLDPRT",
                        "Camera front bezel, D19.5 aperture", 1, 126, 0, 0)
                };

                foreach (Item item in items)
                {
                    string path = Path.Combine(partsRoot, item.FileName);
                    if (!File.Exists(path)) throw new FileNotFoundException("Missing enclosure part", path);
                }

                SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                sw.Visible = true;
                sw.CloseAllDocuments(true);
                string assemblyTemplate = @"C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2026\templates\gb_assembly.asmdot";
                string drawingTemplate = @"C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2026\templates\gb_a3.drwdot";
                if (!File.Exists(assemblyTemplate) || !File.Exists(drawingTemplate))
                    throw new FileNotFoundException("SOLIDWORKS GB templates were not found.");

                foreach (Item item in items)
                {
                    int error = 0, warning = 0;
                    sw.OpenDoc6(Path.Combine(partsRoot, item.FileName),
                        (int)swDocumentTypes_e.swDocPART,
                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                        "", ref error, ref warning);
                }

                ModelDoc2 assemblyModel = (ModelDoc2)sw.NewDocument(assemblyTemplate, 0, 0, 0);
                if (assemblyModel == null) throw new InvalidOperationException("Could not create enclosure assembly.");
                AssemblyDoc assembly = (AssemblyDoc)assemblyModel;
                MathUtility math = (MathUtility)sw.GetMathUtility();
                List<Component2> components = new List<Component2>();
                foreach (Item item in items)
                    components.Add(Add(assembly, math, Path.Combine(partsRoot, item.FileName), item.PartNo,
                        item.X, item.Y, item.Z));

                Configuration configuration = (Configuration)assemblyModel.GetActiveConfiguration();
                configuration.Name = "PRINTABLE_PARTS_EXPLODED";
                CustomPropertyManager properties = assemblyModel.Extension.get_CustomPropertyManager("");
                properties.Add3("Description", (int)swCustomInfoType_e.swCustomInfoText,
                    "V-pipe robot six-piece printable enclosure exploded assembly",
                    (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                properties.Add3("Fasteners", (int)swCustomInfoType_e.swCustomInfoText,
                    "8x M3x10 low button head (D<=5.7 H<=1.7) plus M3 inserts; 4x M2.5x8 plus inserts",
                    (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                properties.Add3("PrintClearance", (int)swCustomInfoType_e.swCustomInfoText,
                    "0.30 mm bay tabs; 0.50 mm camera tabs; verify printer calibration",
                    (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);

                // Save a second, actually closed arrangement for fit verification.
                for (int i=0; i<components.Count; ++i)
                {
                    double x = i<2 ? -92 : i<4 ? 0 : i==4 ? 88 : 114.2;
                    double z = i<4 && i%2==1 ? -12.2 : 0;
                    components[i].Transform2 = Translation(math, x, 0, z);
                }
                assemblyModel.ForceRebuild3(false);
                string assembledPath = Path.Combine(output, "Printable_Enclosure_Assembled_ID150.SLDASM");
                if (assemblyModel.SaveAs3(assembledPath, 0,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent) != 0)
                    throw new InvalidOperationException("Assembled enclosure save failed.");
                for (int i=0; i<components.Count; ++i)
                    components[i].Transform2 = Translation(math, items[i].X, items[i].Y, items[i].Z);
                assemblyModel.ForceRebuild3(false);
                assemblyModel.ShowNamedView2("*Isometric", (int)swStandardViews_e.swIsometricView);
                assemblyModel.ViewZoomtofit2();
                string assemblyPath = Path.Combine(output, "Printable_Enclosure_Exploded_ID150.SLDASM");
                int saveCode = assemblyModel.SaveAs3(assemblyPath, 0,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent);
                if (saveCode != 0) throw new InvalidOperationException("Assembly save failed: " + saveCode);

                int exportError = 0, exportWarning = 0;
                bool ok = assemblyModel.Extension.SaveAs(
                    Path.Combine(output, "printable_enclosure_exploded.png"),
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    null, ref exportError, ref exportWarning);
                if (!ok) throw new InvalidOperationException("Exploded preview export failed: " + exportError);
                ok = assemblyModel.Extension.SaveAs(
                    Path.Combine(output, "Printable_Enclosure_Exploded_ID150.STEP"),
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    null, ref exportError, ref exportWarning);
                if (!ok) throw new InvalidOperationException("STEP export failed: " + exportError);

                string topName = FindStandardView(assemblyModel, "Top", "上视", "*Top");
                string frontName = FindStandardView(assemblyModel, "Front", "前视", "*Front");
                string isoName = FindStandardView(assemblyModel, "Isometric", "等轴测", "*Isometric");

                ModelDoc2 drawingModel = (ModelDoc2)sw.NewDocument(drawingTemplate,
                    (int)swDwgPaperSizes_e.swDwgPaperA3size, 0, 0);
                if (drawingModel == null) throw new InvalidOperationException("Could not create A3 drawing.");
                DrawingDoc drawing = (DrawingDoc)drawingModel;
                ((Sheet)drawing.GetCurrentSheet()).SheetFormatVisible = false;

                AddView(drawing, assemblyPath, isoName, 0.125, 0.172, 0.55);
                AddView(drawing, assemblyPath, topName, 0.305, 0.188, 0.43);
                AddView(drawing, assemblyPath, frontName, 0.305, 0.092, 0.43);

                Text(drawing, "V-PIPE ROBOT - 3D PRINTABLE ENCLOSURE SPLIT DRAWING",
                    0.035, 0.283, 0, 0.0055, 0);
                Text(drawing, "Six printed parts | A3 exploded arrangement | Units: mm | Scale: as shown",
                    0.035, 0.271, 0, 0.0035, 0);
                Text(drawing, "E-01  POWER TOP COVER 72x62x22 t=2.0     QTY 1",
                    0.035, 0.124, 0, 0.0032, 0);
                Text(drawing, "E-02  POWER BASE 72x62 t=2.4              QTY 1",
                    0.035, 0.114, 0, 0.0032, 0);
                Text(drawing, "E-03  CONTROL TOP COVER 72x62x22 t=2.0   QTY 1",
                    0.035, 0.104, 0, 0.0032, 0);
                Text(drawing, "E-04  CONTROL BASE 72x62 t=2.4            QTY 1",
                    0.035, 0.094, 0, 0.0032, 0);
                Text(drawing, "E-05  CAMERA BODY 50x48x32 t=2.0          QTY 1",
                    0.035, 0.084, 0, 0.0032, 0);
                Text(drawing, "E-06  CAMERA BEZEL 48x32x2.4 / D19.5      QTY 1",
                    0.035, 0.074, 0, 0.0032, 0);
                Text(drawing, "FASTENERS: 8x M3x10 LOW BUTTON + inserts; 4x M2.5x8 + inserts",
                    0.035, 0.058, 0, 0.0030, 0);
                Text(drawing, "PRINT: PETG / FDM, 0.20 layer, 5 walls, 30-40% infill; covers roof-down",
                    0.035, 0.048, 0, 0.0030, 0);
                Text(drawing, "LOCATING TABS: bays 0.30 clearance; camera 0.50. See E-01 to E-06 sheets.",
                    0.035, 0.038, 0, 0.0030, 0);
                Text(drawing, "PROTOTYPE - VERIFY INSERT PILOTS ON A COUPON. DETAILS ON SHEETS 2-7.",
                    0.035, 0.026, 0, 0.0030, 0);

                drawingModel.ForceRebuild3(false);
                drawingModel.ViewZoomtofit2();
                string drawingPath = Path.Combine(output, "Printable_Enclosure_Split_A3.SLDDRW");
                saveCode = drawingModel.SaveAs3(drawingPath, 0,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent);
                if (saveCode != 0) throw new InvalidOperationException("Drawing save failed: " + saveCode);
                exportError = 0;
                exportWarning = 0;
                ok = drawingModel.Extension.SaveAs(
                    Path.Combine(output, "printable_enclosure_split_drawing.png"),
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    null, ref exportError, ref exportWarning);
                if (!ok) throw new InvalidOperationException("Drawing preview export failed: " + exportError);

                string firstSheet = ((Sheet)drawing.GetCurrentSheet()).GetName();
                for (int i=0; i<items.Length; ++i)
                {
                    DetailSheet(sw, drawing, drawingModel, partsRoot, items[i], i);
                    ExportPreview(drawingModel, Path.Combine(output, items[i].PartNo+"_detail.png"));
                }
                drawing.ActivateSheet(firstSheet);
                // Drawing view generation can dirty the referenced assembly.
                // Save that reference first so the drawing caches its final
                // timestamp and does not request another save when reopened.
                int modelSaveError=0, modelSaveWarning=0;
                if (!assemblyModel.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    ref modelSaveError, ref modelSaveWarning))
                    throw new InvalidOperationException("Referenced assembly save failed: "+modelSaveError);
                drawingModel.ForceRebuild3(false);
                if (drawingModel.SaveAs3(drawingPath, 0,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent) != 0)
                    throw new InvalidOperationException("Multi-sheet drawing save failed.");
                ExportPreview(drawingModel, Path.Combine(output, "Printable_Enclosure_Split_A3.pdf"));

                List<string> bom = new List<string>();
                bom.Add("V-pipe robot printable enclosure bill of materials");
                bom.Add("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                bom.Add("");
                foreach (Item item in items)
                    bom.Add(item.PartNo + "\tQTY " + item.Quantity + "\t" + item.FileName + "\t" + item.Description);
                bom.Add("H-01\tQTY 8\tM3x10 low button head screw, head D<=5.7 H<=1.7\tEquipment bay cover/base fastening");
                bom.Add("H-02\tQTY 8\tM3 heat-set insert, nominal OD 4.2-4.6 x L 5-6\tCalibrate pilot before print");
                bom.Add("H-03\tQTY 4\tM2.5x8 socket-head screw\tCamera bezel fastening");
                bom.Add("H-04\tQTY 4\tM2.5 heat-set insert\tCalibrate pilot before print");
                File.WriteAllLines(Path.Combine(output, "PRINTABLE_ENCLOSURE_BOM.txt"), bom.ToArray());

                sw.CloseDoc(drawingModel.GetTitle());
                sw.CloseDoc(assemblyModel.GetTitle());
                Console.WriteLine("ASSEMBLY=" + assemblyPath);
                Console.WriteLine("DRAWING=" + drawingPath);
                Console.WriteLine("PARTS=" + items.Length);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        private static void ExportPreview(ModelDoc2 model, string path)
        {
            int e=0,w=0;
            if (!model.Extension.SaveAs(path, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null, ref e, ref w)) throw new InvalidOperationException("Export failed: "+path+" / "+e);
        }
    }
}
