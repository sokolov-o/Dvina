using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Import.Files;

namespace _ImportFiles
{
    class Program
    {
        static string DIR_FOR_IMPORT = @"C:\Users\SOV\Documents\Data\ИВП\Ежегодники\";
        static void Main(string[] args)
        {
            FileChemAnnual.Parse(DIR_FOR_IMPORT + "ChemAnn.100140542005620.csv");

            Console.WriteLine("Press ENTER...");
            Console.ReadLine();
        }
    }
}
