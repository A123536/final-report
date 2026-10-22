using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace VPipeRobotSolidWorks
{
    // Save rebuilt drawing references in dependency order. Never clears a save
    // flag or disables warnings; a fresh reopen is the acceptance criterion.
    internal static class StabilizeSavedCadDocuments
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if(args.Length!=2)throw new ArgumentException("Expected saved drawing and authorized model root.");
                string path=Path.GetFullPath(args[0]);
                string root=Path.GetFullPath(args[1]).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                SldWorks sw=(SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                for(int attempt=0;attempt<4;attempt++)
                {
                    sw.CloseAllDocuments(true);
                    int e=0,w=0;
                    ModelDoc2 drawing=sw.OpenDoc6(path,(int)swDocumentTypes_e.swDocDRAWING,
                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent,"",ref e,ref w);
                    if(drawing==null)throw new InvalidOperationException("Open failed: "+e);
                    object[] raw=sw.GetDocuments() as object[];
                    List<ModelDoc2> documents=new List<ModelDoc2>();
                    foreach(object item in raw)
                    {
                        ModelDoc2 document=(ModelDoc2)item;
                        if(!document.GetPathName().StartsWith(root,StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Unauthorized document: "+document.GetPathName());
                        if(document.GetSaveFlag())documents.Add(document);
                    }
                    Console.WriteLine("ATTEMPT="+attempt+" DIRTY="+documents.Count);
                    if(documents.Count==0){sw.CloseAllDocuments(true);return 0;}
                    documents.Sort(delegate(ModelDoc2 a,ModelDoc2 b){return a.GetType().CompareTo(b.GetType());});
                    foreach(ModelDoc2 document in documents)
                    {
                        e=0;w=0;
                        if(!document.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent,ref e,ref w))
                            throw new InvalidOperationException("Save failed: "+document.GetTitle()+" "+e);
                        Console.WriteLine("SAVED="+document.GetTitle());
                    }
                }
                sw.CloseAllDocuments(true);
                throw new InvalidOperationException("Repeated reopen still reports modified documents.");
            }
            catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        }
    }
}
