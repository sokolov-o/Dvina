using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Import.Files;
using static Import.Files.FileChemAnnual;
using Moo.Common;

namespace _ImportFiles
{
    class Program
    {
        static readonly string DIR_FOR_IMPORT = @"C:\Users\SOV\Documents\Data\ИВП\Ежегодники\";
        static readonly bool IS_MOVE_TO_IMPORTED_DIR = true;
        static void Main()
        {
            Moo.DataManager.MonsoonConnectionString = Properties.Settings.Default.DvinaConnectionString;
            Moo.DataObs.DataManager.MonsoonDataObservationConnectionString = Properties.Settings.Default.DvinaDataObservationConnectionString;

            foreach (var file in Directory.GetFiles(DIR_FOR_IMPORT, "*.csv"))
            {
                //List<FileRowData> data = FileChemAnnual.Parse(file);
                var datafile = FileChemAnnual.Parse(file);

                if (datafile != null)
                {
                    Console.WriteLine($"Site {datafile[0].SiteCode}, {datafile.Count} file rows readed.");

                    List<DataValue> datavals = DataValue.Convert(datafile);
                    Moo.DataObs.DataObsDataSetTableAdapters.qta qta = new Moo.DataObs.DataObsDataSetTableAdapters.qta();
                    foreach (DataValue dataval in datavals)
                    {
                        qta.SaveSingleDataValue(dataval.CatalogId, dataval.Date, dataval.OffsetValue, dataval.Value, dataval.FlagAQC, dataval.UTCOffset, dataval.DataSourceId);
                    }

                    if (IS_MOVE_TO_IMPORTED_DIR)
                    {
                        CommonFileProcess.MoveFile2DirImported(file);
                    }
                }
                else
                    Console.WriteLine($"ERROR: 0 file rows readed.");
            }

            Console.WriteLine("Press ENTER...");
            Console.ReadLine();
        }
    }
}
