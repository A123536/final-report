using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace VPipeRobotSolidWorks
{
    // Copy only the parts referenced by a saved assembly, then replace every
    // reference while the document is closed. No staging paths may remain.
    internal static class PublishPrintableEnclosure
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 3)
                    throw new ArgumentException("Expected staged assembly, revised parts directory, target model directory.");
                string source = Path.GetFullPath(args[0]);
                string revised = Path.GetFullPath(args[1]);
                string target = Path.GetFullPath(args[2]);
                string targetParts = Path.Combine(target, "parts");
                string targetAssembly = Path.Combine(target, Path.GetFileName(source));
                if (source.Equals(targetAssembly, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Source and target assembly must differ.");
                SldWorks sw = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
                int error = 0, warning = 0;
                ModelDoc2 doc = sw.OpenDoc6(source, (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent |
                    (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly, "", ref error, ref warning);
                if (doc == null) throw new InvalidOperationException("Source open failed: " + error);
                object[] raw = ((AssemblyDoc)doc).GetComponents(false) as object[];
                SortedSet<string> paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (object item in raw) paths.Add(((Component2)item).GetPathName());
                sw.CloseDoc(doc.GetTitle());
                sw.CloseAllDocuments(true);
                Directory.CreateDirectory(targetParts);
                File.Copy(source, targetAssembly, true);
                foreach (string oldPath in paths)
                {
                    string file = Path.GetFileName(oldPath);
                    string replacement = Path.Combine(revised, file);
                    File.Copy(File.Exists(replacement) ? replacement : oldPath,
                        Path.Combine(targetParts, file), true);
                    if (!sw.ReplaceReferencedDocument(targetAssembly, oldPath,
                        Path.Combine(targetParts, file)))
                        throw new InvalidOperationException("Failed to relink " + oldPath);
                }
                doc = sw.OpenDoc6(targetAssembly, (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref error, ref warning);
                if (doc == null) throw new InvalidOperationException("Published open failed: " + error);
                raw = ((AssemblyDoc)doc).GetComponents(false) as object[];
                SortedSet<string> references = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (object item in raw)
                {
                    string path = Path.GetFullPath(((Component2)item).GetPathName());
                    if (!path.StartsWith(targetParts + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Nonportable reference: " + path);
                    references.Add(path);
                }
                doc.ForceRebuild3(false);
                if (doc.SaveAs3(targetAssembly, 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent) != 0)
                    throw new InvalidOperationException("Published save failed.");
                File.WriteAllLines(Path.Combine(target, "REFERENCED_PARTS_ID150.txt"),
                    new List<string>(references).ToArray());
                sw.CloseAllDocuments(true);
                Console.WriteLine("COMPONENTS=" + raw.Length);
                Console.WriteLine("REFERENCED_PARTS=" + references.Count);
                Console.WriteLine("OUTSIDE_TARGET=0");
                Console.WriteLine("PUBLISHED=" + targetAssembly);
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
