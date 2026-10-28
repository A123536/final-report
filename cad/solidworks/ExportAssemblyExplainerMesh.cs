using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace VPipeRobotSolidWorks
{
    // Read-only display tessellation. This creates no feature or display-state
    // change in the engineering assembly and never saves a SOLIDWORKS document.
    internal static class ExportAssemblyExplainerMesh
    {
        [STAThread]
        private static int Main(string[] args)
        {
            SldWorks sw=null; ModelDoc2 model=null; bool ownsApp=false, ownsDocument=false;
            try
            {
                if(args.Length!=2)throw new ArgumentException("Expected assembly and output JSON paths.");
                try {sw=(SldWorks)Marshal.GetActiveObject("SldWorks.Application");}
                catch {sw=(SldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application"));ownsApp=true;}
                string path=Path.GetFullPath(args[0]);
                model=(ModelDoc2)sw.GetOpenDocumentByName(path);
                if(model==null)
                {
                    int e=0,w=0;
                    model=sw.OpenDoc6(path,(int)swDocumentTypes_e.swDocASSEMBLY,
                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent|(int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly,
                        "",ref e,ref w);
                    ownsDocument=true;
                    if(model==null)throw new InvalidOperationException("Assembly open failed "+e);
                }
                bool flagBefore=model.GetSaveFlag();
                object[] raw=((AssemblyDoc)model).GetComponents(false) as object[];
                Dictionary<string,object> geometries=new Dictionary<string,object>();
                List<object> components=new List<object>();
                int triangleCount=0;
                foreach(object item in raw)
                {
                    Component2 component=(Component2)item;
                    string key=Path.GetFileNameWithoutExtension(component.GetPathName());
                    if(!geometries.ContainsKey(key))
                    {
                        ModelDoc2 part=(ModelDoc2)component.GetModelDoc2();
                        if(part==null)throw new InvalidOperationException("Unresolved part "+key);
                        Array tess=((PartDoc)part).GetTessTriangles(true) as Array;
                        if(tess==null||tess.Length==0)throw new InvalidOperationException("No tessellation "+key);
                        double[] positions=new double[tess.Length];
                        for(int i=0;i<tess.Length;i++)positions[i]=Math.Round(Convert.ToDouble(tess.GetValue(i))*1000,3);
                        geometries.Add(key,positions);triangleCount+=tess.Length/9;
                    }
                    double[] matrix=component.Transform2.ArrayData as double[];
                    double[] converted=(double[])matrix.Clone();
                    for(int i=0;i<9;i++)converted[i]=Math.Round(converted[i],9);
                    for(int i=9;i<12;i++)converted[i]=Math.Round(converted[i]*1000,3);
                    components.Add(new Dictionary<string,object>{{"name",component.Name2},{"part",key},{"transform",converted}});
                }
                JavaScriptSerializer serializer=new JavaScriptSerializer();serializer.MaxJsonLength=int.MaxValue;
                string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output,serializer.Serialize(new Dictionary<string,object>{
                    {"units","mm"},{"source","V_Pipe_Robot_Concept_ID150.SLDASM / Rev C"},
                    {"engineeringWarning","Display meshes include conceptual envelopes; new procurement hardware still needs fit verification."},
                    {"geometries",geometries},{"components",components}}));
                Console.WriteLine("GEOMETRIES="+geometries.Count+" COMPONENTS="+components.Count+" UNIQUE_TRIANGLES="+triangleCount);
                Console.WriteLine("SAVE_FLAG_BEFORE="+flagBefore+" SAVE_FLAG_AFTER="+model.GetSaveFlag());
                Console.WriteLine("OUTPUT_BYTES="+new FileInfo(output).Length);
                return 0;
            }
            catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
            finally
            {
                if(sw!=null&&model!=null&&ownsDocument)sw.CloseDoc(model.GetTitle());
                if(sw!=null&&ownsApp)sw.ExitApp();
            }
        }
    }
}
