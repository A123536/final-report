using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace VPipeRobotSolidWorks
{
    internal struct V3
    {
        public double X;
        public double Y;
        public double Z;

        public V3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static V3 operator +(V3 a, V3 b) { return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static V3 operator -(V3 a, V3 b) { return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static V3 operator *(V3 a, double s) { return new V3(a.X * s, a.Y * s, a.Z * s); }
        public double Length() { return Math.Sqrt(X * X + Y * Y + Z * Z); }
        public V3 Unit() { double n = Length(); return new V3(X / n, Y / n, Z / n); }
        public static V3 Cross(V3 a, V3 b)
        {
            return new V3(a.Y * b.Z - a.Z * b.Y,
                          a.Z * b.X - a.X * b.Z,
                          a.X * b.Y - a.Y * b.X);
        }
    }

    internal sealed class Generator
    {
        // All design constants are expressed in millimetres here and converted
        // to metres only when calling the SOLIDWORKS API.
        private const double PipeLength = 430.0;
        private const double PipeCenterX = 25.0;
        private const double WheelDiameter = 60.0;
        private const double WheelWidth = 28.0;    // placeholder pending purchased wheel
        private const double LinkLength = 150.0;

        private readonly SldWorks _sw;
        private readonly Modeler _modeler;
        private readonly MathUtility _math;
        private readonly string _partTemplate;
        private readonly string _asmTemplate;
        private readonly string _root;
        private readonly string _parts;
        private readonly double _pipeId;
        private readonly string _pipeTag;
        private readonly Dictionary<string, string> _partFiles = new Dictionary<string, string>();

        private static readonly V3 Ex = new V3(1, 0, 0);
        private static readonly V3 Ey = new V3(0, 1, 0);
        private static readonly V3 Ez = new V3(0, 0, 1);

        public Generator(SldWorks sw, string root, double pipeId)
        {
            _sw = sw;
            _modeler = (Modeler)_sw.GetModeler();
            _math = (MathUtility)_sw.GetMathUtility();
            _root = root;
            _parts = Path.Combine(root, "parts");
            _pipeId = pipeId;
            _pipeTag = pipeId.ToString("0", CultureInfo.InvariantCulture);
            Directory.CreateDirectory(_root);
            Directory.CreateDirectory(_parts);
            _partTemplate = @"C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2026\templates\gb_part.prtdot";
            _asmTemplate = @"C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2026\templates\gb_assembly.asmdot";
            if (!File.Exists(_partTemplate) || !File.Exists(_asmTemplate))
                throw new InvalidOperationException("SOLIDWORKS 2026 GB templates were not found.");
        }

        private static double M(double mm) { return mm / 1000.0; }

        private static double[] Appearance(double r, double g, double b, double transparency)
        {
            return new double[] { r, g, b, 0.35, 0.70, 0.25, 0.25, transparency, 0.0 };
        }

        private string PartPath(string key) { return Path.Combine(_parts, key + ".SLDPRT"); }

        private ModelDoc2 NewPart()
        {
            ModelDoc2 model = (ModelDoc2)_sw.NewDocument(_partTemplate, 0, 0.0, 0.0);
            if (model == null) throw new InvalidOperationException("Could not create part document.");
            return model;
        }

        private void SaveAndClosePart(ModelDoc2 model, string path)
        {
            model.ForceRebuild3(false);
            int saveError = model.SaveAs3(path, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent);
            if (saveError != 0) throw new InvalidOperationException("Save failed for " + path + ", code " + saveError);
            string title = model.GetTitle();
            _sw.CloseDoc(title);
        }

        private string CreateBox(string key, double x, double y, double z, double[] color)
        {
            string path = PartPath(key);
            if (File.Exists(path) && new FileInfo(path).Length > 1024)
            {
                _partFiles[key] = path;
                return path;
            }
            ModelDoc2 model = NewPart();
            PartDoc part = (PartDoc)model;
            double[] dim = new double[] { 0, 0, -M(z) / 2.0, 0, 0, 1, M(x), M(y), M(z) };
            Body2 body = _modeler.CreateBodyFromBox3(dim);
            if (body == null || part.CreateFeatureFromBody3(body, false, (int)swCreateFeatureBodyOpts_e.swCreateFeatureBodyCheck) == null)
                throw new InvalidOperationException("Box creation failed for " + key);
            model.MaterialPropertyValues = color;
            SaveAndClosePart(model, path);
            _partFiles[key] = path;
            return path;
        }

        private string CreateCylinder(string key, double diameter, double length, double[] color)
        {
            string path = PartPath(key);
            if (File.Exists(path) && new FileInfo(path).Length > 1024)
            {
                _partFiles[key] = path;
                return path;
            }
            ModelDoc2 model = NewPart();
            PartDoc part = (PartDoc)model;
            double[] dim = new double[] { 0, 0, -M(length) / 2.0, 0, 0, 1, M(diameter) / 2.0, M(length) };
            Body2 body = (Body2)_modeler.CreateBodyFromCyl(dim);
            if (body == null || part.CreateFeatureFromBody3(body, false, (int)swCreateFeatureBodyOpts_e.swCreateFeatureBodyCheck) == null)
                throw new InvalidOperationException("Cylinder creation failed for " + key);
            model.MaterialPropertyValues = color;
            SaveAndClosePart(model, path);
            _partFiles[key] = path;
            return path;
        }

        private string CreateTube(string key, double outerDiameter, double innerDiameter, double length, double[] color)
        {
            string path = PartPath(key);
            if (File.Exists(path) && new FileInfo(path).Length > 1024)
            {
                _partFiles[key] = path;
                return path;
            }
            ModelDoc2 model = NewPart();
            PartDoc part = (PartDoc)model;
            double[] outerDim = new double[] { 0, 0, -M(length) / 2.0, 0, 0, 1, M(outerDiameter) / 2.0, M(length) };
            double[] innerDim = new double[] { 0, 0, -M(length + 2.0) / 2.0, 0, 0, 1, M(innerDiameter) / 2.0, M(length + 2.0) };
            Body2 outer = (Body2)_modeler.CreateBodyFromCyl(outerDim);
            Body2 inner = (Body2)_modeler.CreateBodyFromCyl(innerDim);
            int opError = 0;
            object resultObject = outer.Operations2((int)swBodyOperationType_e.SWBODYCUT, inner, out opError);
            object[] results = resultObject as object[];
            if (results == null || results.Length == 0)
                throw new InvalidOperationException("Tube Boolean failed for " + key + ", code " + opError);
            Body2 body = (Body2)results[0];
            if (part.CreateFeatureFromBody3(body, false, (int)swCreateFeatureBodyOpts_e.swCreateFeatureBodyCheck) == null)
                throw new InvalidOperationException("Tube feature failed for " + key);
            model.MaterialPropertyValues = color;
            SaveAndClosePart(model, path);
            _partFiles[key] = path;
            return path;
        }

        private Body2 BoxBody(double cx, double cy, double cz,
                              double sx, double sy, double sz)
        {
            double[] dim = new double[] {
                M(cx), M(cy), M(cz - sz / 2.0), 0, 0, 1,
                M(sx), M(sy), M(sz)
            };
            Body2 body = _modeler.CreateBodyFromBox3(dim);
            if (body == null) throw new InvalidOperationException("Could not create box body.");
            return body;
        }

        private Body2 CylinderBody(V3 center, V3 axis, double diameter, double length)
        {
            V3 unit = axis.Unit();
            V3 start = center - unit * (length / 2.0);
            double[] dim = new double[] {
                M(start.X), M(start.Y), M(start.Z),
                unit.X, unit.Y, unit.Z,
                M(diameter) / 2.0, M(length)
            };
            Body2 body = (Body2)_modeler.CreateBodyFromCyl(dim);
            if (body == null) throw new InvalidOperationException("Could not create cylinder body.");
            return body;
        }

        private static Body2 BooleanBody(Body2 first, Body2 second,
                                         swBodyOperationType_e operation, string label)
        {
            int error = 0;
            object[] result = first.Operations2((int)operation, second, out error) as object[];
            if (result == null || result.Length != 1)
                throw new InvalidOperationException(label + " Boolean failed, code " + error);
            return (Body2)result[0];
        }

        private string SaveBodyPart(string key, Body2 body, double[] color)
        {
            string path = PartPath(key);
            if (File.Exists(path) && new FileInfo(path).Length > 1024)
            {
                _partFiles[key] = path;
                return path;
            }
            ModelDoc2 model = NewPart();
            PartDoc part = (PartDoc)model;
            if (part.CreateFeatureFromBody3(body, false,
                (int)swCreateFeatureBodyOpts_e.swCreateFeatureBodyCheck) == null)
                throw new InvalidOperationException("Body feature failed for " + key);
            model.MaterialPropertyValues = color;
            SaveAndClosePart(model, path);
            _partFiles[key] = path;
            return path;
        }

        private string CreatePrintableBayCover(string key, double length, double width,
                                               double height, double wall, double[] color)
        {
            string path = PartPath(key);
            if (File.Exists(path) && new FileInfo(path).Length > 1024)
            {
                _partFiles[key] = path;
                return path;
            }

            // Print roof-down; walls grow upward. Use local supports under the
            // projecting boss shoulders; inserts stay outside the PCB bay.
            double wallHeight = height - wall;
            double wallCenterZ = -wall / 2.0;
            Body2 body = BoxBody(0, 0, height / 2.0 - wall / 2.0,
                length, width, wall);
            body = BooleanBody(body,
                BoxBody(0, -(width - wall) / 2.0, wallCenterZ,
                    length, wall, wallHeight),
                swBodyOperationType_e.SWBODYADD, key + " left wall");
            body = BooleanBody(body,
                BoxBody(0, (width - wall) / 2.0, wallCenterZ,
                    length, wall, wallHeight),
                swBodyOperationType_e.SWBODYADD, key + " right wall");
            body = BooleanBody(body,
                BoxBody(-(length - wall) / 2.0, 0, wallCenterZ,
                    wall, width - 2.0 * wall, wallHeight),
                swBodyOperationType_e.SWBODYADD, key + " rear wall");
            body = BooleanBody(body,
                BoxBody((length - wall) / 2.0, 0, wallCenterZ,
                    wall, width - 2.0 * wall, wallHeight),
                swBodyOperationType_e.SWBODYADD, key + " front wall");

            // Keep the bosses just inside the side-wall envelope.  This leaves
            // the existing cable lanes at Y=+/-37 mm clear while the board
            // envelopes still retain more than 5 mm of lateral clearance.
            double bossY = width / 2.0 - 0.5;
            double bossZ = -height / 2.0 + 4.0;
            foreach (double side in new double[] { -1.0, 1.0 })
            {
                // The positive-Y front boss of the power bay is moved inward
                // along X so the separately serviceable fuse terminal can sit
                // beside the cover without sharing material with the boss.
                double[] bossXs = key.StartsWith("PowerBay", StringComparison.OrdinalIgnoreCase) && side > 0
                    ? new double[] { -25.0, 5.0 }
                    : new double[] { -25.0, 25.0 };
                foreach (double bossX in bossXs)
                {
                    Body2 boss = CylinderBody(new V3(bossX, side * bossY, bossZ), Ez, 8.0, 8.0);
                    Body2 pilot = CylinderBody(new V3(bossX, side * bossY, bossZ), Ez, 4.2, 10.0);
                    body = BooleanBody(body, boss, swBodyOperationType_e.SWBODYADD,
                        key + " external insert boss");
                    // Drill AFTER joining: otherwise the wall fills the bore.
                    body = BooleanBody(body, pilot, swBodyOperationType_e.SWBODYCUT,
                        key + " heat-set insert pilot");
                }
            }
            // Open-at-rim cable slots avoid trapped cables and print without
            // a closed horizontal roof when the cover is printed roof-down.
            foreach (double end in new double[] { -1.0, 1.0 })
                body = BooleanBody(body,
                    BoxBody(end * (length / 2.0 - wall / 2.0), 0,
                        -height / 2.0 + 2.5, wall + 2.0, 12.0, 5.2),
                    swBodyOperationType_e.SWBODYCUT, key + " cable rim slot");
            return SaveBodyPart(key, body, color);
        }

        private string CreatePrintableBayBase(string key, double length, double width,
                                              double thickness, double[] color)
        {
            string path = PartPath(key);
            if (File.Exists(path) && new FileInfo(path).Length > 1024)
            {
                _partFiles[key] = path;
                return path;
            }

            Body2 body = BoxBody(0, 0, 0, length, width, thickness);
            double earY = width / 2.0 - 0.5;
            foreach (double side in new double[] { -1.0, 1.0 })
            {
                double[] earXs = key.StartsWith("PowerBay", StringComparison.OrdinalIgnoreCase) && side > 0
                    ? new double[] { -25.0, 5.0 }
                    : new double[] { -25.0, 25.0 };
                foreach (double earX in earXs)
                {
                    Body2 ear = CylinderBody(new V3(earX, side * earY, 0), Ez, 8.0, thickness);
                    Body2 clearance = CylinderBody(new V3(earX, side * earY, 0), Ez, 3.4,
                        thickness + 2.0);
                    body = BooleanBody(body, ear, swBodyOperationType_e.SWBODYADD,
                        key + " fastening ear");
                    body = BooleanBody(body, clearance, swBodyOperationType_e.SWBODYCUT,
                        key + " M3 clearance");
                }
            }
            // Discontinuous registration tabs: 0.30 mm nominal clearance to
            // the 68 x 58 mm cavity, with 2.0 mm engagement above the base.
            foreach (double end in new double[] { -1.0, 1.0 })
            foreach (double side in new double[] { -1.0, 1.0 })
                body = BooleanBody(body, BoxBody(end * 33.1, side * 24.5,
                    thickness / 2.0 + 0.9, 1.2, 4.0, 2.2),
                    swBodyOperationType_e.SWBODYADD, key + " axial registration tab");
            foreach (double side in new double[] { -1.0, 1.0 })
                body = BooleanBody(body, BoxBody(-10.0, side * 28.1,
                    thickness / 2.0 + 0.9, 4.0, 1.2, 2.2),
                    swBodyOperationType_e.SWBODYADD, key + " lateral registration tab");
            return SaveBodyPart(key, body, color);
        }

        private string CreateCameraHousingBody(string key, double length, double width,
                                               double height, double wall, double[] color)
        {
            string path = PartPath(key);
            if (File.Exists(path) && new FileInfo(path).Length > 1024)
            {
                _partFiles[key] = path;
                return path;
            }

            // Rear wall plus four longitudinal walls; +X remains open for the
            // removable lens bezel.  Print with the rear wall on the build plate.
            Body2 body = BoxBody(-length / 2.0 + wall / 2.0, 0, 0,
                wall, width, height);
            double runLength = length - wall + 0.5;
            double runCenterX = wall / 2.0 - 0.25;
            body = BooleanBody(body,
                BoxBody(runCenterX, -(width - wall) / 2.0, 0,
                    runLength, wall, height),
                swBodyOperationType_e.SWBODYADD, key + " lower side wall");
            body = BooleanBody(body,
                BoxBody(runCenterX, (width - wall) / 2.0, 0,
                    runLength, wall, height),
                swBodyOperationType_e.SWBODYADD, key + " upper side wall");
            body = BooleanBody(body,
                BoxBody(runCenterX, 0, -(height - wall) / 2.0,
                    runLength, width - 2.0 * wall, wall),
                swBodyOperationType_e.SWBODYADD, key + " bottom wall");
            body = BooleanBody(body,
                BoxBody(runCenterX, 0, (height - wall) / 2.0,
                    runLength, width - 2.0 * wall, wall),
                swBodyOperationType_e.SWBODYADD, key + " top wall");

            foreach (double y in new double[] { -19.5, 19.5 })
            foreach (double z in new double[] { -11.5, 11.5 })
            {
                Body2 boss = CylinderBody(new V3(length / 2.0 - 4.5, y, z), Ex, 6.0, 9.0);
                Body2 pilot = CylinderBody(new V3(length / 2.0 - 4.5, y, z), Ex, 4.0, 10.0);
                boss = BooleanBody(boss, pilot, swBodyOperationType_e.SWBODYCUT,
                    key + " M2.5 insert pilot");
                body = BooleanBody(body, boss, swBodyOperationType_e.SWBODYADD,
                    key + " bezel boss");
            }
            // Two rear-edge entries clear the existing power/signal harnesses.
            // Their edges remain accessible for a split grommet after printing.
            foreach (double side in new double[] { -1.0, 1.0 })
                body = BooleanBody(body, BoxBody(-length / 2.0 + 0.5,
                    side * 20.5, 0, 7.0, 9.0, 12.0),
                    swBodyOperationType_e.SWBODYCUT, key + " cable entry");
            return SaveBodyPart(key, body, color);
        }

        private string CreateCameraBezel(string key, double width, double height,
                                         double thickness, double[] color)
        {
            string path = PartPath(key);
            if (File.Exists(path) && new FileInfo(path).Length > 1024)
            {
                _partFiles[key] = path;
                return path;
            }

            Body2 body = BoxBody(0, 0, 0, thickness, width, height);
            body = BooleanBody(body, CylinderBody(new V3(0, 0, 0), Ex, 19.5,
                thickness + 2.0), swBodyOperationType_e.SWBODYCUT, key + " lens aperture");
            foreach (double y in new double[] { -19.5, 19.5 })
            foreach (double z in new double[] { -11.5, 11.5 })
                body = BooleanBody(body, CylinderBody(new V3(0, y, z), Ex, 3.0,
                    thickness + 2.0), swBodyOperationType_e.SWBODYCUT,
                    key + " M2.5 clearance");
            // Four short bezel tabs enter the camera cavity with 0.50 mm
            // clearance and do not enter the optical aperture or screw bosses.
            foreach (double side in new double[] { -1.0, 1.0 })
            {
                body = BooleanBody(body, BoxBody(-thickness / 2.0 - 0.9,
                    side * 20.9, 0, 2.2, 1.2, 8.0),
                    swBodyOperationType_e.SWBODYADD, key + " side locating tab");
                body = BooleanBody(body, BoxBody(-thickness / 2.0 - 0.9,
                    0, side * 12.9, 2.2, 8.0, 1.2),
                    swBodyOperationType_e.SWBODYADD, key + " vertical locating tab");
            }
            return SaveBodyPart(key, body, color);
        }

        private string CreateEquipmentBayShell(string key, double length, double width,
                                               double height, double wall, double[] color)
        {
            string path = PartPath(key);
            if (File.Exists(path) && new FileInfo(path).Length > 1024)
            {
                _partFiles[key] = path;
                return path;
            }
            ModelDoc2 model = NewPart();
            PartDoc part = (PartDoc)model;
            double sideHeight = height - wall;
            double sideZ = -wall / 2.0;
            double topZ = height / 2.0 - wall / 2.0;

            Func<double, double, double, double, double, double, Body2> box =
                delegate(double cx, double cy, double cz, double sx, double sy, double sz)
                {
                    double[] dim = new double[] {
                        M(cx), M(cy), M(cz - sz / 2.0), 0, 0, 1,
                        M(sx), M(sy), M(sz)
                    };
                    return _modeler.CreateBodyFromBox3(dim);
                };

            Body2 shell = box(0, 0, topZ, length, width, wall);
            Body2 left = box(0, -(width - wall) / 2.0, sideZ, length, wall, sideHeight);
            Body2 right = box(0, (width - wall) / 2.0, sideZ, length, wall, sideHeight);
            foreach (Body2 addition in new Body2[] { left, right })
            {
                int opError = 0;
                object[] joined = shell.Operations2((int)swBodyOperationType_e.SWBODYADD,
                    addition, out opError) as object[];
                if (joined == null || joined.Length == 0)
                    throw new InvalidOperationException("Equipment shell Boolean failed for " + key + ", code " + opError);
                shell = (Body2)joined[0];
            }
            if (part.CreateFeatureFromBody3(shell, false,
                (int)swCreateFeatureBodyOpts_e.swCreateFeatureBodyCheck) == null)
                throw new InvalidOperationException("Equipment shell feature failed for " + key);
            model.MaterialPropertyValues = color;
            SaveAndClosePart(model, path);
            _partFiles[key] = path;
            return path;
        }

        private void BuildPartLibrary()
        {
            double[] blue = Appearance(0.15, 0.46, 0.72, 0.0);
            double[] grey = Appearance(0.43, 0.43, 0.43, 0.0);
            double[] orange = Appearance(0.92, 0.49, 0.18, 0.0);
            double[] dark = Appearance(0.16, 0.18, 0.21, 0.0);
            double[] steel = Appearance(0.45, 0.48, 0.52, 0.0);
            double[] purple = Appearance(0.63, 0.29, 0.71, 0.0);
            double[] red = Appearance(0.80, 0.20, 0.22, 0.0);
            double[] green = Appearance(0.18, 0.63, 0.35, 0.0);
            double[] cyan = Appearance(0.20, 0.75, 0.75, 0.0);
            double[] lightBlue = Appearance(0.33, 0.60, 0.82, 0.55);
            double[] camera = Appearance(0.57, 0.31, 0.75, 0.45);
            double[] pipe = Appearance(0.47, 0.69, 0.82, 0.82);

            CreateTube("Pipe_ID" + _pipeTag + "_OD" + (_pipeId + 4.0).ToString("0", CultureInfo.InvariantCulture) + "_L430",
                _pipeId + 4.0, _pipeId, PipeLength, pipe);
            // The purchased wheel remains a D60 x W28 envelope.  The solid core
            // is reduced to D26 and the rollers define the D60 contact envelope;
            // this avoids the impossible full-disk overlap in the first model.
            CreateTube("DrivenWheelCore_D26_ID18_W20", 26, 18, 20, blue);
            CreateTube("PassiveWheelCore_D26_ID18_W20", 26, 18, 20, grey);
            CreateBox("DrivenWheelSpoke_11x4x4", 11, 4, 4, blue);
            CreateBox("PassiveWheelSpoke_11x4x4", 11, 4, 4, grey);
            CreateTube("WheelHub_OD18_ID8_L36", 18, 8, 36, steel);
            CreateCylinder("OmniRoller_D6_L8", 6, 8, orange);
            CreateBox("LinkRail_122x7x16", 122, 7, 16, dark);
            CreateCylinder("WheelShaft_D8_L74", 8, 74, steel);
            CreateCylinder("CenterAxle_D8_L92", 8, 92, steel);
            CreateTube("Bearing619_8_19x8x6", 19, 8, 6, dark);
            CreateTube("Gear40T_m1_Envelope_D42_ID8_W7", 42, 8, 7, orange);
            CreateTube("Gear25T_m1_Envelope_D27_ID6_W7", 27, 6, 7, orange);
            CreateCylinder("IdlerShaft_D6_L14", 6, 14, steel);
            CreateTube("IdlerBearingEnvelope_OD13_ID6_W5", 13, 6, 5, dark);
            CreateCylinder("GearboxEnvelope_D37_L24", 37, 24, steel);
            CreateCylinder("MotorEnvelope_D32p7_L21p7", 32.7, 21.7, steel);
            CreateCylinder("EncoderEnvelope_D33p8_L13p8", 33.8, 13.8, dark);
            CreateTube("TorsionSpringEnvelope_OD38_ID12_W6p4", 38, 12, 6.4, purple);
            CreateBox("CenterStopTab_12x6x12", 12, 6, 12, dark);
            CreateCylinder("InsertionLockPin_D4_L40", 4, 40, dark);
            CreatePrintableBayCover("PowerBay_TopCover_72x62x22_t2", 72, 62, 22, 2, lightBlue);
            CreatePrintableBayBase("PowerBay_BasePlate_72x62x2p4", 72, 62, 2.4, lightBlue);
            CreatePrintableBayCover("ControlBay_TopCover_72x62x22_t2", 72, 62, 22, 2, lightBlue);
            CreatePrintableBayBase("ControlBay_BasePlate_72x62x2p4", 72, 62, 2.4, lightBlue);
            CreateBox("Buck24to12_65x42x17", 65, 42, 17, blue);
            CreateBox("FuseTerminal_24x16x12", 24, 16, 12, cyan);
            CreateBox("MC33926_50x30x12", 50, 30, 12, red);
            CreateBox("STM32F411_53x22x10", 53, 22, 10, green);
            CreateBox("RS485_25x18x9", 25, 18, 9, cyan);
            CreateCameraHousingBody("CameraHousing_Body_50x48x32_t2", 50, 48, 32, 2, camera);
            CreateCameraBezel("CameraHousing_FrontBezel_48x32x2p4_D19p5", 48, 32, 2.4, camera);
            CreateCylinder("CameraLensEnvelope_D18_L7", 18, 7, dark);
            CreateCylinder("M12GlandEnvelope_D14_L15", 14, 15, dark);
            CreateCylinder("ExternalTether_D7_L30", 7, 30, red);
            CreateBox("CableTieBridge_10x60x5", 10, 60, 5, dark);
        }

        private MathTransform Transform(V3 localX, V3 localY, V3 localZ, V3 translationMm)
        {
            double[] a = new double[] {
                localX.X, localX.Y, localX.Z,
                localY.X, localY.Y, localY.Z,
                localZ.X, localZ.Y, localZ.Z,
                M(translationMm.X), M(translationMm.Y), M(translationMm.Z),
                1.0, 0.0, 0.0, 0.0
            };
            return (MathTransform)_math.CreateTransform(a);
        }

        private static void BasisFromZ(V3 zDirection, out V3 xAxis, out V3 yAxis, out V3 zAxis)
        {
            zAxis = zDirection.Unit();
            V3 helper = Math.Abs(zAxis.Z) < 0.9 ? Ez : Ey;
            xAxis = V3.Cross(helper, zAxis).Unit();
            yAxis = V3.Cross(zAxis, xAxis).Unit();
        }

        private Component2 Add(AssemblyDoc assembly, string key, string instanceName,
                               V3 centerMm, V3 localX, V3 localY, V3 localZ)
        {
            string path = _partFiles[key];
            Component2 component = assembly.AddComponent5(path,
                (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                "", false, "", 0.0, 0.0, 0.0);
            if (component == null) throw new InvalidOperationException("Could not add " + instanceName);
            component.Name2 = instanceName;
            component.Transform2 = Transform(localX, localY, localZ, centerMm);
            component.Select4(false, null, false);
            assembly.FixComponent();
            return component;
        }

        private Component2 AddAxis(AssemblyDoc assembly, string key, string instanceName,
                                   V3 centerMm, V3 axis)
        {
            V3 lx, ly, lz;
            BasisFromZ(axis, out lx, out ly, out lz);
            return Add(assembly, key, instanceName, centerMm, lx, ly, lz);
        }

        private void AddWheel(AssemblyDoc assembly, V3 center, string prefix, bool driven)
        {
            string wheelKey = driven ? "DrivenWheelCore_D26_ID18_W20" : "PassiveWheelCore_D26_ID18_W20";
            AddAxis(assembly, wheelKey, prefix + "_WHEEL_CORE_D26", center, Ey);
            AddAxis(assembly, "WheelHub_OD18_ID8_L36", prefix + "_HUB_OD18_ID8", center, Ey);

            string spokeKey = driven ? "DrivenWheelSpoke_11x4x4" : "PassiveWheelSpoke_11x4x4";
            for (int i = 0; i < 6; ++i)
            {
                double angle = 2.0 * Math.PI * i / 6.0;
                V3 radial = new V3(Math.Cos(angle), 0, Math.Sin(angle));
                V3 tangent = V3.Cross(radial, Ey).Unit();
                Add(assembly, spokeKey, prefix + "_SPOKE_" + (i + 1).ToString("00"),
                    center + radial * 18.5, radial, Ey, tangent);
            }

            // Stylized roller envelopes identify the purchased component as an omni wheel.
            for (int i = 0; i < 12; ++i)
            {
                double angle = 2.0 * Math.PI * i / 12.0;
                V3 roller = center + new V3(27.0 * Math.Cos(angle),
                    (i % 2 == 0 ? 1.0 : -1.0) * (WheelWidth / 2.0 - 4.0),
                    27.0 * Math.Sin(angle));
                AddAxis(assembly, "OmniRoller_D6_L8", prefix + "_ROLLER_" + (i + 1).ToString("00"), roller, Ey);
            }
        }

        private void AddDriveModule(AssemblyDoc assembly, V3 active, V3 towardCenter,
                                    int side, string prefix)
        {
            V3 u = towardCenter.Unit();
            V3 idlerAxis = active + u * 32.5;
            V3 motorAxis = active + u * 57.5;
            double gearY = -side * 31.0;

            AddAxis(assembly, "WheelShaft_D8_L74", prefix + "_WHEEL_SHAFT_D8", active, Ey);
            AddAxis(assembly, "Bearing619_8_19x8x6", prefix + "_BEARING_LEFT", active + new V3(0, -22, 0), Ey);
            AddAxis(assembly, "Bearing619_8_19x8x6", prefix + "_BEARING_RIGHT", active + new V3(0, 22, 0), Ey);
            AddAxis(assembly, "Gear40T_m1_Envelope_D42_ID8_W7", prefix + "_GEAR_40T_m1", active + new V3(0, gearY, 0), Ey);
            AddAxis(assembly, "Gear25T_m1_Envelope_D27_ID6_W7", prefix + "_IDLER_GEAR_25T_m1", idlerAxis + new V3(0, gearY, 0), Ey);
            AddAxis(assembly, "IdlerShaft_D6_L14", prefix + "_IDLER_SHAFT_D6", idlerAxis + new V3(0, gearY, 0), Ey);
            AddAxis(assembly, "IdlerBearingEnvelope_OD13_ID6_W5", prefix + "_IDLER_BEARING",
                idlerAxis + new V3(0, gearY + side * 7.5, 0), Ey);
            AddAxis(assembly, "Gear25T_m1_Envelope_D27_ID6_W7", prefix + "_MOTOR_GEAR_25T_m1", motorAxis + new V3(0, gearY, 0), Ey);

            double gearboxY = gearY + side * (3.5 + 1.0 + 24.0 / 2.0);
            double motorY = gearY + side * (3.5 + 1.0 + 24.0 + 21.7 / 2.0);
            double encoderY = gearY + side * (3.5 + 1.0 + 24.0 + 21.7 + 13.8 / 2.0);
            AddAxis(assembly, "GearboxEnvelope_D37_L24", prefix + "_JGB37_GEARBOX", motorAxis + new V3(0, gearboxY, 0), Ey);
            AddAxis(assembly, "MotorEnvelope_D32p7_L21p7", prefix + "_JGB37_MOTOR", motorAxis + new V3(0, motorY, 0), Ey);
            AddAxis(assembly, "EncoderEnvelope_D33p8_L13p8", prefix + "_ENCODER", motorAxis + new V3(0, encoderY, 0), Ey);
        }

        private void AddBoard(AssemblyDoc assembly, string key, string name, V3 podCenter,
                              V3 u, V3 normal, double alongOffset, double yOffset,
                              double normalOffset, double length, double width, double height)
        {
            V3 boardCenter = podCenter + u * alongOffset + new V3(0, yOffset, 0) + normal * normalOffset;
            Add(assembly, key, name, boardCenter, u, Ey, normal);
        }

        private void AddCableSegment(AssemblyDoc assembly, V3 p0, V3 p1,
                                     double diameter, string prefix, int index, double[] color)
        {
            V3 vector = p1 - p0;
            double length = vector.Length();
            string diameterToken = diameter.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', 'p');
            string lengthToken = length.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', 'p');
            string key = prefix + "_D" + diameterToken + "_L" + lengthToken + "_" + index;
            CreateCylinder(key, diameter, length, color);
            // Newly created part is closed; load it before insertion.
            int e = 0, w = 0;
            _sw.OpenDoc6(_partFiles[key], (int)swDocumentTypes_e.swDocPART,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref e, ref w);
            AddAxis(assembly, key, prefix + "_SEGMENT_" + (index + 1), (p0 + p1) * 0.5, vector);
            _sw.CloseDoc(Path.GetFileName(_partFiles[key]));
        }

        private string BuildAssembly()
        {
            // Load the reusable parts so AddComponent5 can resolve them reliably.
            foreach (string path in _partFiles.Values)
            {
                int e = 0, w = 0;
                _sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref e, ref w);
            }

            ModelDoc2 model = (ModelDoc2)_sw.NewDocument(_asmTemplate, 0, 0.0, 0.0);
            if (model == null) throw new InvalidOperationException("Could not create assembly document.");
            AssemblyDoc assembly = (AssemblyDoc)model;

            double pipeRadius = _pipeId / 2.0;
            double radialOffset = Math.Sqrt(pipeRadius * pipeRadius - Math.Pow(WheelWidth / 2.0, 2.0))
                - WheelDiameter / 2.0;
            double radialSpan = 2.0 * radialOffset;
            double halfWheelbase = Math.Sqrt(LinkLength * LinkLength - radialSpan * radialSpan);
            double vAngle = 2.0 * Math.Acos(radialSpan / LinkLength) * 180.0 / Math.PI;

            V3 rear = new V3(-halfWheelbase, 0, -radialOffset);
            V3 center = new V3(0, 0, radialOffset);
            V3 front = new V3(halfWheelbase, 0, -radialOffset);

            // Transparent pipe shell, local cylinder Z axis mapped to global X.
            string pipeKey = "Pipe_ID" + _pipeTag + "_OD" + (_pipeId + 4.0).ToString("0", CultureInfo.InvariantCulture) + "_L430";
            Add(assembly, pipeKey, "PIPE_ID" + _pipeTag + "_CONCEPT",
                new V3(PipeCenterX, 0, 0), Ey, Ez, Ex);

            AddWheel(assembly, rear, "REAR_DRIVEN", true);
            AddWheel(assembly, center, "CENTER_PASSIVE", false);
            AddWheel(assembly, front, "FRONT_DRIVEN", true);

            V3 rearU = (center - rear).Unit();
            V3 frontU = (front - center).Unit();
            V3 rearN = V3.Cross(rearU, Ey).Unit();
            V3 frontN = V3.Cross(frontU, Ey).Unit();

            V3 rearRailCenter = (rear + center) * 0.5;
            V3 frontRailCenter = (center + front) * 0.5;
            Add(assembly, "LinkRail_122x7x16", "REAR_LINK_RAIL_L", rearRailCenter + new V3(0, -43, 0), rearU, Ey, rearN);
            Add(assembly, "LinkRail_122x7x16", "REAR_LINK_RAIL_R", rearRailCenter + new V3(0, 43, 0), rearU, Ey, rearN);
            Add(assembly, "LinkRail_122x7x16", "FRONT_LINK_RAIL_L", frontRailCenter + new V3(0, -43, 0), frontU, Ey, frontN);
            Add(assembly, "LinkRail_122x7x16", "FRONT_LINK_RAIL_R", frontRailCenter + new V3(0, 43, 0), frontU, Ey, frontN);

            AddAxis(assembly, "CenterAxle_D8_L92", "CENTER_HINGE_AXLE_D8", center, Ey);
            AddAxis(assembly, "Bearing619_8_19x8x6", "CENTER_BEARING_LEFT", center + new V3(0, -21, 0), Ey);
            AddAxis(assembly, "Bearing619_8_19x8x6", "CENTER_BEARING_RIGHT", center + new V3(0, 21, 0), Ey);
            AddAxis(assembly, "TorsionSpringEnvelope_OD38_ID12_W6p4", "TORSION_SPRING_LEFT_ENVELOPE_NOT_FINAL", center + new V3(0, -32, 0), Ey);
            AddAxis(assembly, "TorsionSpringEnvelope_OD38_ID12_W6p4", "TORSION_SPRING_RIGHT_ENVELOPE_NOT_FINAL", center + new V3(0, 32, 0), Ey);
            Add(assembly, "CenterStopTab_12x6x12", "CENTER_STOP_TAB_REAR", center + new V3(-18, -43, -18), Ex, Ey, Ez);
            Add(assembly, "CenterStopTab_12x6x12", "CENTER_STOP_TAB_FRONT", center + new V3(18, 43, -18), Ex, Ey, Ez);
            AddAxis(assembly, "InsertionLockPin_D4_L40", "INSERTION_LOCK_PIN_D4", center + new V3(-18, -43, -18), Ey);

            AddDriveModule(assembly, rear, center - rear, -1, "REAR_DRIVE");
            AddDriveModule(assembly, front, center - front, 1, "FRONT_DRIVE");

            V3 rearInward = (center - rear).Unit();
            V3 rearPodNormal = V3.Cross(rearInward, Ey).Unit();
            V3 rearPod = rear + rearInward * 85.0 + rearPodNormal * 33.0;
            Add(assembly, "PowerBay_TopCover_72x62x22_t2", "REAR_POWER_BAY_TOP_COVER", rearPod, rearInward, Ey, rearPodNormal);
            Add(assembly, "PowerBay_BasePlate_72x62x2p4", "REAR_POWER_BAY_BASE_PLATE",
                rearPod - rearPodNormal * 12.2, rearInward, Ey, rearPodNormal);
            AddBoard(assembly, "Buck24to12_65x42x17", "BUCK_24V_TO_12V_ENVELOPE", rearPod,
                rearInward, rearPodNormal, 0, 0, -1.0, 65, 42, 17);
            Add(assembly, "FuseTerminal_24x16x12", "FUSE_TERMINAL_EXTERNAL",
                rearPod + rearInward * 30 + new V3(0, 39.0, 0) - rearPodNormal * 12.0,
                rearInward, Ey, rearPodNormal);

            V3 frontInward = (center - front).Unit();
            V3 frontPodNormal = V3.Cross(frontInward, Ey).Unit() * -1.0;
            V3 frontPod = front + frontInward * 85.0 + frontPodNormal * 33.0;
            Add(assembly, "ControlBay_TopCover_72x62x22_t2", "FRONT_CONTROL_BAY_TOP_COVER", frontPod, frontInward, Ey, frontPodNormal);
            Add(assembly, "ControlBay_BasePlate_72x62x2p4", "FRONT_CONTROL_BAY_BASE_PLATE",
                frontPod - frontPodNormal * 12.2, frontInward, Ey, frontPodNormal);
            AddBoard(assembly, "MC33926_50x30x12", "DUAL_MC33926_ENVELOPE", frontPod,
                frontInward, frontPodNormal, -8, -11.5, 0, 50, 30, 12);
            AddBoard(assembly, "STM32F411_53x22x10", "STM32F411_ENVELOPE", frontPod,
                frontInward, frontPodNormal, -8, 15.5, 0, 53, 22, 10);
            Add(assembly, "RS485_25x18x9", "RS485_MAX3485_ROTATED", frontPod + frontInward * 25,
                Ey, frontInward * -1.0, frontPodNormal);

            V3 cameraCenter = new V3(175, 0, 0);
            Add(assembly, "CameraHousing_Body_50x48x32_t2", "CAMERA_HOUSING_BODY", cameraCenter, Ex, Ey, Ez);
            Add(assembly, "CameraHousing_FrontBezel_48x32x2p4_D19p5", "CAMERA_HOUSING_FRONT_BEZEL",
                cameraCenter + new V3(26.2, 0, 0), Ex, Ey, Ez);
            AddAxis(assembly, "CameraLensEnvelope_D18_L7", "CAMERA_LENS_PORT_D18", cameraCenter + new V3(28.5, 0, 0), Ex);

            V3 tetherEntry = new V3(-171, 0, -12);
            AddAxis(assembly, "M12GlandEnvelope_D14_L15", "M12_CABLE_GLAND_ENVELOPE", tetherEntry + new V3(-7.5, 0, 0), Ex);
            AddAxis(assembly, "ExternalTether_D7_L30", "EXTERNAL_TETHER_ENVELOPE", tetherEntry + new V3(-15, 0, 0), Ex);

            V3[] bridges = new V3[] {
                rearPod - rearInward * 24 + rearPodNormal * 13.5,
                rearPod + rearInward * 24 + rearPodNormal * 13.5,
                frontPod - frontInward * 24 + frontPodNormal * 13.5,
                frontPod + frontInward * 24 + frontPodNormal * 13.5
            };
            for (int i = 0; i < bridges.Length; ++i)
            {
                V3 u = i < 2 ? rearInward : frontInward;
                V3 n = V3.Cross(u, Ey).Unit();
                Add(assembly, "CableTieBridge_10x60x5", "CABLE_TIE_BRIDGE_" + (i + 1), bridges[i], u, Ey, n);
            }

            double[] powerColor = Appearance(0.86, 0.18, 0.18, 0.0);
            double[] signalColor = Appearance(0.16, 0.69, 0.37, 0.0);
            V3 frontMotorBypass = front + frontInward * 57.5 + frontPodNormal * 25.0;
            V3 frontWheelBypass = front + frontPodNormal * 35.0;
            V3[] powerPoints = new V3[] {
                tetherEntry,
                new V3(-160, -37, -12),
                rearPod + new V3(0, -37, 0),
                center + new V3(0, -37, -32),
                frontPod + new V3(0, -37, 0),
                frontMotorBypass + new V3(0, -37, 0),
                frontWheelBypass + new V3(0, -37, 0),
                cameraCenter + new V3(-25, -22, 0)
            };
            V3[] signalPoints = new V3[] {
                tetherEntry + new V3(0, 10, 0),
                new V3(-160, 36.5, -12),
                rearPod + new V3(0, 36.5, 0),
                center + new V3(0, 36.5, -32),
                frontPod + new V3(0, 36.5, 0),
                frontMotorBypass + new V3(0, 36.5, 0),
                frontWheelBypass + new V3(0, 36.5, 0),
                cameraCenter + new V3(-25, 22, 0)
            };
            for (int i = 0; i < powerPoints.Length - 1; ++i)
                AddCableSegment(assembly, powerPoints[i], powerPoints[i + 1], 4.4, "POWER_HARNESS", i, powerColor);
            for (int i = 0; i < signalPoints.Length - 1; ++i)
                AddCableSegment(assembly, signalPoints[i], signalPoints[i + 1], 3.2, "SIGNAL_HARNESS", i, signalColor);

            model.ClearSelection2(true);
            model.ForceRebuild3(false);
            model.ShowNamedView2("*Isometric", (int)swStandardViews_e.swIsometricView);
            model.ViewZoomtofit2();

            string assemblyPath = Path.Combine(_root, "V_Pipe_Robot_Concept_ID" + _pipeTag + ".SLDASM");
            int result = model.SaveAs3(assemblyPath, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent);
            if (result != 0) throw new InvalidOperationException("Assembly save failed, code " + result);

            string report = Path.Combine(_root, "MODEL_VALIDATION_ID" + _pipeTag + ".txt");
            File.WriteAllText(report,
                "V-pipe robot SOLIDWORKS revised interference-free concept\r\n" +
                "Pipe ID: " + _pipeId.ToString("F3", CultureInfo.InvariantCulture) + " mm (target range 140-160 mm)\r\n" +
                "Wheel: D60.000 x W28.000 mm placeholder\r\n" +
                "Link axis spacing: 150.000 mm\r\n" +
                "Nominal V angle: " + vAngle.ToString("F6", CultureInfo.InvariantCulture) + " deg\r\n" +
                "Rear/Front half wheelbase: " + halfWheelbase.ToString("F6", CultureInfo.InvariantCulture) + " mm\r\n" +
                "Radial wheel-axis offset: " + radialOffset.ToString("F6", CultureInfo.InvariantCulture) + " mm\r\n" +
                "Gear train: module 1, 25T motor / 25T idler / 40T wheel; overall ratio 1.600\r\n" +
                "Printable equipment enclosures: 72 x 62 x 24.4 mm assembled; separate cover/base; 2.0/2.4 mm walls\r\n" +
                "Fastening: four external M3 insert bosses per equipment enclosure\r\n" +
                "Camera enclosure: 50 x 48 x 32 mm body plus removable 2.4 mm front bezel\r\n" +
                "IMPORTANT: wheel details, spring coil geometry, PCB holes and gear teeth are envelopes pending purchased-part measurements.\r\n");
            return assemblyPath;
        }

        public string Run()
        {
            BuildPartLibrary();
            return BuildAssembly();
        }

        public string RunPrintableEnclosuresOnly()
        {
            double[] blue = Appearance(0.33, 0.60, 0.82, 0.0);
            double[] purple = Appearance(0.57, 0.31, 0.75, 0.0);
            CreatePrintableBayCover("PowerBay_TopCover_72x62x22_t2", 72, 62, 22, 2, blue);
            CreatePrintableBayBase("PowerBay_BasePlate_72x62x2p4", 72, 62, 2.4, blue);
            CreatePrintableBayCover("ControlBay_TopCover_72x62x22_t2", 72, 62, 22, 2, blue);
            CreatePrintableBayBase("ControlBay_BasePlate_72x62x2p4", 72, 62, 2.4, blue);
            CreateCameraHousingBody("CameraHousing_Body_50x48x32_t2", 50, 48, 32, 2, purple);
            CreateCameraBezel("CameraHousing_FrontBezel_48x32x2p4_D19p5", 48, 32, 2.4, purple);
            return _parts;
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                string output = args.Length > 0 ? Path.GetFullPath(args[0]) :
                    Path.GetFullPath(Path.Combine(System.Environment.CurrentDirectory, "cad", "solidworks", "model"));
                SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                sw.Visible = true;
                double pipeId = args.Length > 1 ? double.Parse(args[1], CultureInfo.InvariantCulture) : 150.0;
                if (pipeId < 140.0 || pipeId > 160.0)
                    throw new ArgumentOutOfRangeException("pipeId", "Pipe ID must remain inside the 140-160 mm design range.");
                Generator generator = new Generator(sw, output, pipeId);
                string assembly = args.Length > 2 && args[2] == "--enclosures-only"
                    ? generator.RunPrintableEnclosuresOnly() : generator.Run();
                Console.WriteLine("CREATED=" + assembly);
                return 0;
            }
            catch (Exception ex)
            {
                try { Console.Error.WriteLine("TYPE=" + ex.GetType().FullName); } catch { }
                try { Console.Error.WriteLine("MESSAGE=" + ex.Message); } catch { }
                try { Console.Error.WriteLine("STACK=" + ex.StackTrace); } catch { }
                if (ex.InnerException != null)
                {
                    try { Console.Error.WriteLine("INNER=" + ex.InnerException.GetType().FullName + ": " + ex.InnerException.Message); } catch { }
                }
                return 1;
            }
        }
    }
}
