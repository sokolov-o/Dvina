using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Import.Files
{
    public class CommonFileProcess
    {
        public static void MoveFile2DirImported(string filePath)
        {
            FileInfo fi = new FileInfo(filePath);
            string dirImportedName = fi.DirectoryName + "\\Imported";
            if (!Directory.Exists(dirImportedName))
                Directory.CreateDirectory(dirImportedName);
            File.Move(filePath, dirImportedName + "\\" + fi.Name);
        }
    }
}
