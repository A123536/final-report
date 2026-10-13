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
        private const double PipeId = 150.0;
        private const double PipeOd = 154.0;       // 2 mm visualization wall
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
        private readonly Dictionary<string, string> _partFiles = new Dictionary<string, string>();

        private static readonly V3 Ex = new V3(1, 0, 0);
        private static readonly V3 Ey = new V3(0, 1, 0);
        private static readonly V3 Ez = new V3(0, 0, 1);

        public Generator(SldWorks sw, string root)
        {
            _sw = sw;
            _modeler = (Modeler)_sw.GetModeler();
            _math = (MathUtility)_sw.GetMathUtility();
            _root = root;
            _parts = Path.Combine(root, "parts");
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

            CreateTube("Pipe_ID150_OD154_L430", PipeOd, PipeId, PipeLength, pipe);
            CreateCylinder("DrivenWheel_D60_W28", WheelDiameter, WheelWidth, blue);
            CreateCylinder("PassiveWheel_D60_W28", WheelDiameter, WheelWidth, grey);
            CreateCylinder("WheelHub_D18_L36", 18, 36, steel);
            CreateCylinder("OmniRoller_D6_L8", 6, 8, orange);
            CreateBox("LinkRail_122x7x16", 122, 7, 16, dark);
            CreateCylinder("WheelShaft_D8_L74", 8, 74, steel);
            CreateCylinder("CenterAxle_D10_L92", 10, 92, steel);
            CreateTube("BearingEnvelope_22x8x7", 22, 8, 7, dark);
            CreateTube("Gear40T_m1_Envelope_D42_ID8_W7", 42, 8, 7, orange);
            CreateTube("Gear25T_m1_Envelope_D27_ID6_W7", 27, 6, 7, orange);
            CreateCylinder("GearboxEnvelope_D37_L24", 37, 24, steel);
            CreateCylinder("MotorEnvelope_D32p7_L21p7", 32.7, 21.7, steel);
            CreateCylinder("EncoderEnvelope_D33p8_L13p8", 33.8, 13.8, dark);
            CreateTube("TorsionSpringEnvelope_OD38_ID12_W6p4", 38, 12, 6.4, purple);
            CreateBox("CenterStopTab_12x9x18", 12, 9, 18, dark);
            CreateCylinder("InsertionLockPin_D4_L84", 4, 84, dark);
            CreateBox("EquipmentBay_92x64x30", 92, 64, 30, lightBlue);
            CreateBox("Buck24to12_65x42x17", 65, 42, 17, blue);
            CreateBox("FuseTerminal_24x16x12", 24, 16, 12, cyan);
            CreateBox("MC33926_50x30x12", 50, 30, 12, red);
            CreateBox("STM32F411_53x22x10", 53, 22, 10, green);
            CreateBox("RS485_25x18x9", 25, 18, 9, cyan);
            CreateBox("FutureCameraBay_50x48x32", 50, 48, 32, camera);
            CreateCylinder("CameraLensEnvelope_D18_L7", 18, 7, dark);
            CreateCylinder("M3Boss_D3p2_L21", 3.2, 21, dark);
            CreateCylinder("M3CameraMount_D3p2_L7", 3.2, 7, dark);
            CreateCylinder("M12GlandEnvelope_D14_L15", 14, 15, dark);
            CreateCylinder("ExternalTether_D7_L30", 7, 30, red);
            CreateBox("CableTieBridge_10x76x5", 10, 76, 5, dark);
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
            string wheelKey = driven ? "DrivenWheel_D60_W28" : "PassiveWheel_D60_W28";
            AddAxis(assembly, wheelKey, prefix + "_WHEEL_D60_W28", center, Ey);
            AddAxis(assembly, "WheelHub_D18_L36", prefix + "_HUB_D18", center, Ey);

            // Stylized roller envelopes identify the purchased component as an omni wheel.
            for (int i = 0; i < 12; ++i)
            {
                double angle = 2.0 * Math.PI * i / 12.0;
                V3 roller = center + new V3(25.0 * Math.Cos(angle),
                    (i % 2 == 0 ? 1.0 : -1.0) * (WheelWidth / 2.0 - 3.2),
                    25.0 * Math.Sin(angle));
                AddAxis(assembly, "OmniRoller_D6_L8", prefix + "_ROLLER_" + (i + 1).ToString("00"), roller, Ey);
            }
        }

        private void AddDriveModule(AssemblyDoc assembly, V3 active, V3 towardCenter,
                                    int side, string prefix)
        {
            V3 u = towardCenter.Unit();
            V3 motorAxis = active + u * 32.5;
            double gearY = -side * 20.0;

            AddAxis(assembly, "WheelShaft_D8_L74", prefix + "_WHEEL_SHAFT_D8", active, Ey);
            AddAxis(assembly, "BearingEnvelope_22x8x7", prefix + "_BEARING_LEFT", active + new V3(0, -23, 0), Ey);
            AddAxis(assembly, "BearingEnvelope_22x8x7", prefix + "_BEARING_RIGHT", active + new V3(0, 23, 0), Ey);
            AddAxis(assembly, "Gear40T_m1_Envelope_D42_ID8_W7", prefix + "_GEAR_40T_m1", active + new V3(0, gearY, 0), Ey);
            AddAxis(assembly, "Gear25T_m1_Envelope_D27_ID6_W7", prefix + "_GEAR_25T_m1", motorAxis + new V3(0, gearY, 0), Ey);

            double gearboxY = gearY + side * 15.0;
            double motorY = gearY + side * (24.0 + 21.7 / 2.0);
            double encoderY = gearY + side * (24.0 + 21.7 + 13.8 / 2.0);
            AddAxis(assembly, "GearboxEnvelope_D37_L24", prefix + "_JGB37_GEARBOX", motorAxis + new V3(0, gearboxY, 0), Ey);
            AddAxis(assembly, "MotorEnvelope_D32p7_L21p7", prefix + "_JGB37_MOTOR", motorAxis + new V3(0, motorY, 0), Ey);
            AddAxis(assembly, "EncoderEnvelope_D33p8_L13p8", prefix + "_ENCODER", motorAxis + new V3(0, encoderY, 0), Ey);
        }

        private void AddBoard(AssemblyDoc assembly, string key, string name, V3 podCenter,
                              V3 u, V3 normal, double alongOffset, double yOffset,
                              double length, double width, double height)
        {
            V3 boardCenter = podCenter + u * alongOffset + new V3(0, yOffset, 0);
            Add(assembly, key, name, boardCenter, u, Ey, normal);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    V3 bossCenter = boardCenter + u * (sx * (length / 2.0 - 5.0))
                        + new V3(0, sy * (width / 2.0 - 4.0), 0);
                    AddAxis(assembly, "M3Boss_D3p2_L21", name + "_M3_BOSS_" + sx + "_" + sy,
                        bossCenter, normal);
                }
            }
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

            double pipeRadius = PipeId / 2.0;
            double radialOffset = Math.Sqrt(pipeRadius * pipeRadius - Math.Pow(WheelWidth / 2.0, 2.0))
                - WheelDiameter / 2.0;
            double radialSpan = 2.0 * radialOffset;
            double halfWheelbase = Math.Sqrt(LinkLength * LinkLength - radialSpan * radialSpan);
            double vAngle = 2.0 * Math.Acos(radialSpan / LinkLength) * 180.0 / Math.PI;

            V3 rear = new V3(-halfWheelbase, 0, -radialOffset);
            V3 center = new V3(0, 0, radialOffset);
            V3 front = new V3(halfWheelbase, 0, -radialOffset);

            // Transparent pipe shell, local cylinder Z axis mapped to global X.
            Add(assembly, "Pipe_ID150_OD154_L430", "PIPE_ID150_OD154_L430_CONCEPT",
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
            Add(assembly, "LinkRail_122x7x16", "REAR_LINK_RAIL_L", rearRailCenter + new V3(0, -35, 0), rearU, Ey, rearN);
            Add(assembly, "LinkRail_122x7x16", "REAR_LINK_RAIL_R", rearRailCenter + new V3(0, 35, 0), rearU, Ey, rearN);
            Add(assembly, "LinkRail_122x7x16", "FRONT_LINK_RAIL_L", frontRailCenter + new V3(0, -35, 0), frontU, Ey, frontN);
            Add(assembly, "LinkRail_122x7x16", "FRONT_LINK_RAIL_R", frontRailCenter + new V3(0, 35, 0), frontU, Ey, frontN);

            AddAxis(assembly, "CenterAxle_D10_L92", "CENTER_HINGE_AXLE_D10", center, Ey);
            AddAxis(assembly, "BearingEnvelope_22x8x7", "CENTER_BEARING_LEFT", center + new V3(0, -28, 0), Ey);
            AddAxis(assembly, "BearingEnvelope_22x8x7", "CENTER_BEARING_RIGHT", center + new V3(0, 28, 0), Ey);
            AddAxis(assembly, "TorsionSpringEnvelope_OD38_ID12_W6p4", "TORSION_SPRING_LEFT_ENVELOPE_NOT_FINAL", center + new V3(0, -28, 0), Ey);
            AddAxis(assembly, "TorsionSpringEnvelope_OD38_ID12_W6p4", "TORSION_SPRING_RIGHT_ENVELOPE_NOT_FINAL", center + new V3(0, 28, 0), Ey);
            Add(assembly, "CenterStopTab_12x9x18", "CENTER_STOP_TAB_REAR", center + new V3(-18, 0, -18), Ex, Ey, Ez);
            Add(assembly, "CenterStopTab_12x9x18", "CENTER_STOP_TAB_FRONT", center + new V3(18, 0, -18), Ex, Ey, Ez);
            AddAxis(assembly, "InsertionLockPin_D4_L84", "INSERTION_LOCK_PIN_D4", center + new V3(-15, 0, -13), Ey);

            AddDriveModule(assembly, rear, center - rear, -1, "REAR_DRIVE");
            AddDriveModule(assembly, front, center - front, 1, "FRONT_DRIVE");

            V3 rearInward = (center - rear).Unit();
            V3 rearPodNormal = V3.Cross(rearInward, Ey).Unit();
            V3 rearPod = rear + (center - rear) * 0.53 + rearPodNormal * 1.0;
            Add(assembly, "EquipmentBay_92x64x30", "REAR_POWER_BAY_92x64x30", rearPod, rearInward, Ey, rearPodNormal);
            AddBoard(assembly, "Buck24to12_65x42x17", "BUCK_24V_TO_12V_ENVELOPE", rearPod,
                rearInward, rearPodNormal, -7, 0, 65, 42, 17);
            AddBoard(assembly, "FuseTerminal_24x16x12", "FUSE_TERMINAL_ENVELOPE", rearPod,
                rearInward, rearPodNormal, 32, 0, 24, 16, 12);

            V3 frontInward = (center - front).Unit();
            V3 frontPodNormal = V3.Cross(frontInward, Ey).Unit();
            V3 frontPod = front + (center - front) * 0.53 + frontPodNormal * 1.0;
            Add(assembly, "EquipmentBay_92x64x30", "FRONT_CONTROL_BAY_92x64x30", frontPod, frontInward, Ey, frontPodNormal);
            AddBoard(assembly, "MC33926_50x30x12", "DUAL_MC33926_ENVELOPE", frontPod,
                frontInward, frontPodNormal, -14, -16, 50, 30, 12);
            AddBoard(assembly, "STM32F411_53x22x10", "STM32F411_ENVELOPE", frontPod,
                frontInward, frontPodNormal, 13, 18, 53, 22, 10);
            AddBoard(assembly, "RS485_25x18x9", "RS485_MAX3485_ENVELOPE", frontPod,
                frontInward, frontPodNormal, 33, -18, 25, 18, 9);

            V3 cameraCenter = new V3(175, 0, 0);
            Add(assembly, "FutureCameraBay_50x48x32", "FUTURE_CAMERA_BAY_50x48x32", cameraCenter, Ex, Ey, Ez);
            AddAxis(assembly, "CameraLensEnvelope_D18_L7", "CAMERA_LENS_PORT_D18", cameraCenter + new V3(28.5, 0, 0), Ex);
            for (int sy = -1; sy <= 1; sy += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    AddAxis(assembly, "M3CameraMount_D3p2_L7", "CAMERA_M3_MOUNT_" + sy + "_" + sz,
                        cameraCenter + new V3(-18.5, sy * 18, sz * 10), Ex);
                }
            }

            V3 tetherEntry = new V3(-171, 0, -12);
            AddAxis(assembly, "M12GlandEnvelope_D14_L15", "M12_CABLE_GLAND_ENVELOPE", tetherEntry + new V3(-7.5, 0, 0), Ex);
            AddAxis(assembly, "ExternalTether_D7_L30", "EXTERNAL_TETHER_ENVELOPE", tetherEntry + new V3(-15, 0, 0), Ex);

            V3[] bridges = new V3[] {
                rearPod - rearInward * 38, rearPod + rearInward * 38,
                frontPod - frontInward * 38, frontPod + frontInward * 38
            };
            for (int i = 0; i < bridges.Length; ++i)
            {
                V3 u = i < 2 ? rearInward : frontInward;
                V3 n = V3.Cross(u, Ey).Unit();
                Add(assembly, "CableTieBridge_10x76x5", "CABLE_TIE_BRIDGE_" + (i + 1), bridges[i], u, Ey, n);
            }

            double[] powerColor = Appearance(0.86, 0.18, 0.18, 0.0);
            double[] signalColor = Appearance(0.16, 0.69, 0.37, 0.0);
            V3[] powerPoints = new V3[] {
                tetherEntry,
                rearPod + new V3(0, -18, 0),
                center + new V3(0, -18, -21),
                frontPod + new V3(0, -18, 0),
                cameraCenter + new V3(-25, -18, 0)
            };
            V3[] signalPoints = new V3[] {
                tetherEntry + new V3(0, 10, 0),
                rearPod + new V3(0, 18, 0),
                center + new V3(0, 18, -12),
                frontPod + new V3(0, 18, 0),
                cameraCenter + new V3(-25, 18, 0)
            };
            for (int i = 0; i < powerPoints.Length - 1; ++i)
                AddCableSegment(assembly, powerPoints[i], powerPoints[i + 1], 4.4, "POWER_HARNESS", i, powerColor);
            for (int i = 0; i < signalPoints.Length - 1; ++i)
                AddCableSegment(assembly, signalPoints[i], signalPoints[i + 1], 3.2, "SIGNAL_HARNESS", i, signalColor);

            model.ClearSelection2(true);
            model.ForceRebuild3(false);
            model.ShowNamedView2("*Isometric", (int)swStandardViews_e.swIsometricView);
            model.ViewZoomtofit2();

            string assemblyPath = Path.Combine(_root, "V_Pipe_Robot_Concept_ID150.SLDASM");
            int result = model.SaveAs3(assemblyPath, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent);
            if (result != 0) throw new InvalidOperationException("Assembly save failed, code " + result);

            string report = Path.Combine(_root, "MODEL_VALIDATION.txt");
            File.WriteAllText(report,
                "V-pipe robot SOLIDWORKS concept baseline\r\n" +
                "Pipe ID: 150.000 mm (target range 140-160 mm)\r\n" +
                "Wheel: D60.000 x W28.000 mm placeholder\r\n" +
                "Link axis spacing: 150.000 mm\r\n" +
                "Nominal V angle: " + vAngle.ToString("F6", CultureInfo.InvariantCulture) + " deg\r\n" +
                "Rear/Front half wheelbase: " + halfWheelbase.ToString("F6", CultureInfo.InvariantCulture) + " mm\r\n" +
                "Radial wheel-axis offset: " + radialOffset.ToString("F6", CultureInfo.InvariantCulture) + " mm\r\n" +
                "Gear pair: module 1, 25T/40T, center distance 32.500 mm\r\n" +
                "Equipment bays: 92 x 64 x 30 mm\r\n" +
                "Camera reserve: 50 x 48 x 32 mm\r\n" +
                "IMPORTANT: wheel details, spring coil geometry, PCB holes and gear teeth are envelopes pending purchased-part measurements.\r\n");
            return assemblyPath;
        }

        public string Run()
        {
            BuildPartLibrary();
            return BuildAssembly();
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
                Generator generator = new Generator(sw, output);
                string assembly = generator.Run();
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
