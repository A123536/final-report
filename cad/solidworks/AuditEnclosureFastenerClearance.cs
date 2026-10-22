using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace VPipeRobotSolidWorks
{
    internal static class AuditEnclosureFastenerClearance
    {
        private sealed class Solid { public string Name; public Body2 Body; }

        private static Body2 Cylinder(Modeler modeler, double[] transform, double x,
                                      double y, double z, int axis, double diameter, double length)
        {
            double cx=transform[9]+(x*transform[0]+y*transform[3]+z*transform[6])/1000;
            double cy=transform[10]+(x*transform[1]+y*transform[4]+z*transform[7])/1000;
            double cz=transform[11]+(x*transform[2]+y*transform[5]+z*transform[8])/1000;
            double dx=transform[axis],dy=transform[axis+1],dz=transform[axis+2];
            return (Body2)modeler.CreateBodyFromCyl(new double[] {
                cx-dx*length/2000,cy-dy*length/2000,cz-dz*length/2000,
                dx,dy,dz,diameter/2000,length/1000 });
        }

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if(args.Length!=1)throw new ArgumentException("Expected assembly path.");
                SldWorks sw=(SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                sw.CloseAllDocuments(true);
                int e=0,w=0;
                ModelDoc2 model=sw.OpenDoc6(Path.GetFullPath(args[0]),(int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent|(int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly,
                    "",ref e,ref w);
                if(model==null)throw new InvalidOperationException("Open failed "+e);
                Modeler modeler=(Modeler)sw.GetModeler();
                object[] components=((AssemblyDoc)model).GetComponents(false) as object[];
                List<Solid> solids=new List<Solid>();
                List<Solid> fasteners=new List<Solid>();
                int screws=0;
                foreach(object value in components)
                {
                    Component2 component=(Component2)value;
                    object info=null;
                    object[] bodies=component.GetBodies3((int)swBodyType_e.swSolidBody,out info) as object[];
                    if(bodies!=null)foreach(object raw in bodies)
                    {
                        Body2 copy=(Body2)((Body2)raw).Copy();
                        copy.ApplyTransform(component.Transform2);
                        solids.Add(new Solid {Name=component.Name2,Body=copy});
                    }
                    string name=Path.GetFileNameWithoutExtension(component.GetPathName());
                    double[] t=component.Transform2.ArrayData as double[];
                    if(name.Contains("Bay_BasePlate"))
                    {
                        foreach(double side in new double[]{-1,1})
                        foreach(double x in (name.StartsWith("Power")&&side>0?new double[]{-25,5}:new double[]{-25,25}))
                        {
                            screws++;
                            string label=component.Name2+"/M3_"+screws;
                            fasteners.Add(new Solid{Name=label+"_shaft",Body=Cylinder(modeler,t,x,side*30.5,3.8,6,3,10)});
                            // Low button head required by encoder clearance.
                            fasteners.Add(new Solid{Name=label+"_head",Body=Cylinder(modeler,t,x,side*30.5,-2.05,6,5.7,1.7)});
                        }
                    }
                    if(name.Contains("CameraHousing_FrontBezel"))
                    {
                        foreach(double y in new double[]{-19.5,19.5})
                        foreach(double z in new double[]{-11.5,11.5})
                        {
                            screws++;
                            string label=component.Name2+"/M2p5_"+screws;
                            fasteners.Add(new Solid{Name=label+"_shaft",Body=Cylinder(modeler,t,-2.8,y,z,0,2.5,8)});
                            fasteners.Add(new Solid{Name=label+"_head",Body=Cylinder(modeler,t,2.45,y,z,0,4.5,2.5)});
                        }
                    }
                }
                int clashes=0;
                foreach(Solid screw in fasteners)
                foreach(Solid solid in solids)
                {
                    Body2 a=(Body2)screw.Body.Copy(),b=(Body2)solid.Body.Copy();
                    int err=0;
                    object[] intersections=a.Operations2((int)swBodyOperationType_e.SWBODYINTERSECT,b,out err) as object[];
                    if(intersections==null)continue;
                    foreach(object intersection in intersections)
                    {
                        double[] mass=((Body2)intersection).GetMassProperties(1) as double[];
                        if(mass!=null&&mass[3]>1e-15)
                        {
                            clashes++;
                            Console.WriteLine("CLASH\t"+screw.Name+"\t"+solid.Name+"\t"+(mass[3]*1e9).ToString("F6")+" mm3");
                        }
                    }
                }
                Console.WriteLine("FASTENER_ENVELOPES="+screws);
                Console.WriteLine("SHAFT_AND_HEAD_CLASHES="+clashes);
                sw.CloseDoc(model.GetTitle());
                return clashes==0?0:2;
            }
            catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        }
    }
}
