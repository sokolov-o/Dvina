using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using SOV.Common;
using Amur.Service.Client;
using Amur.Data.Models;
using Amur.Data.Filters;
using System.IO;

namespace Import.Files
{
    /// <summary>
    /// Файл *.csv с данными АГК (от Пелагеи, 2019).
    /// 
    /// Есть несколько типов таких файлов.
    /// </summary>

    public class FileData
    {
        public enum FileType
        {
            /// <summary>
            /// Имя файла: YYYY_mm_DD_SITENAME
            /// SITENAME не используется - название пункта берем из строк данных.
            /// 
            /// Данные Type1:
            /// 
            /// station,variable,time,value
            /// АГК-4, RiverLevel,2017-07-01 00:00:00,162.86232
            /// АГК-4, RiverLevel,2017-07-01 00:10:00,162.875015
            /// АГК-4, RiverLevel,2017-07-01 00:20:00,162.873062
            /// ...
            /// </summary>
            Type1,
            /// <summary>
            /// Данные Type2:
            /// 
            /// Время;Rivlevel from sensor, m;Rivlevel BHS, m
            /// 06.04.2014 9:30;-4.296;644.224
            /// 06.04.2014 9:40;-4.271;644.249
            /// 06.04.2014 9:50;-4.292;644.228
            /// ...
            /// Данные Type2:
            /// 
            /// Datetime;Prec, mm;TypePrec
            ///23.10.2018 16:50;0; нет осадков
            ///23.10.2018 17:10;0; нет осадков
            ///23.10.2018 17:20;0; нет осадков
            /// ...
            /// Данные Type2:
            /// 
            /// Datetime;AtmPress, mmHg
            ///23.10.2018 16:47;706.108
            ///23.10.2018 16:50;706.108
            ///23.10.2018 17:07;706.033
            /// </summary>
            Type2,
            Unknown
        }
        /// <summary>
        /// Чтение данных из файла и запись в БД Амур через REST сервис.
        /// </summary>
        /// <param name="dirPath">Путь к файлу.</param>
        /// <param name="client">REST</param>
        /// <returns></returns>
        public static int Import(HttpClient client, string dirPath)
        {
            // SCAN & PARCE DIR FILES

            List<Data> datas;
            int itemsInserted = 0;
            foreach (var filePath in Directory.GetFiles(dirPath, "*.csv"))
            {
                //Console.Write($"{filePath}...");

                datas = null;

                switch (GetFileType(filePath))
                {
                    case FileType.Type2:
                        datas = ParseType2(client, filePath);
                        break;
                    default:
                        Console.WriteLine("unknown, skipped.");
                        continue;
                }

                int itemsCount = 0;
                if (datas != null)
                {
                    itemsCount = InsertData(client, datas);
                    MoveFile2DirImported(filePath);
                }

                itemsInserted += itemsCount;

                Console.WriteLine($"{itemsCount } data items Amur-inserted.");
            }
            return itemsInserted;

        }

        private static void MoveFile2DirImported(string filePath)
        {
            FileInfo fi = new FileInfo(filePath);
            string dirImportedName = fi.DirectoryName + "\\Imported";
            if (!Directory.Exists(dirImportedName))
                Directory.CreateDirectory(dirImportedName);
            File.Move(filePath, dirImportedName + "\\" + fi.Name);
        }

        static int InsertData(HttpClient client, List<Data> datas)
        {
            int bufItemsLength = 1000;
            int itemsCount = 0;

            for (int i = 0; i < datas.Count; i++)
            {
                if (datas[i].Varoff == null) continue;

                Site site = datas[i].Site;

                // GET UTC OFFSET

                SiteAttributeValue sav = SiteAttributesAPI.GetByFilterAsync(client, new SiteAttribute
                { DateS = DateTime.Today, SiteAttributeTypeId = (int)EnumSiteAttrType.UTCOffset, SiteId = site.Id })
                    .Result;
                if (sav == null)
                    throw new Exception($"Отсутствует UTCOffset для сайта [{site.Name}] ({site.Id}).");
                int siteUTCOffset = int.Parse(sav.Value);

                // GET or CREATE CATALOG

                List<Catalog> catalogs = CatalogsAPI.GetByFilterAsync(client, new CatalogFilter
                {
                    Sites = new List<int> { site.Id },
                    Variables = new List<int> { datas[i].Varoff.VariableId }
                }).Result;
                if (catalogs.Count > 1) throw new Exception("(catalogs.Count > 1)");

                int catalogId;
                if (catalogs.Count == 0)
                {
                    Catalog catalog = new Catalog
                    {
                        SiteId = site.Id,
                        VariableId = datas[i].Varoff.VariableId * (datas[i].Varoff.VariableId < 0 ? -1 : 1),
                        ValueTypeId = (int)EnumValueType.FieldObservation,
                        MethodId = (int)EnumMethod.ObservationInSitu,
                        SourceId = FileSite.EMERCIT_ORG_ID,

                        OffsetTypeId = datas[i].Varoff.OffsetTypeId,
                        OffsetValue = datas[i].Varoff.OffsetValue,

                        ParentId = 0
                    };
                    catalogId = CatalogsAPI.CreateAsync(client, catalog).Result;
                }
                else
                    catalogId = catalogs[0].Id;

                for (int j = 0; j < datas[i].DateValues.Count;)
                {
                    // CONVERT DATA 2 AMUR DATAVALUES
                    List<DataValue> bufDatas = new List<DataValue>(bufItemsLength);
                    for (int k = 0; k < bufItemsLength; k++)
                    {
                        if (j < datas[i].DateValues.Count)
                        {
                            bufDatas.Add(new DataValue()
                            {
                                CatalogId = catalogId,
                                Date = datas[i].DateValues[j].Date,
                                DateTypeId = (int)EnumDateType.LOC,
                                UTCOffset = siteUTCOffset,
                                Value = datas[i].DateValues[j].Value,
                                FlagAQC = (byte)EnumFlagAQC.NoAQC
                            });
                            j++;
                            itemsCount++;
                        }
                    }

                    // INSERT DATAVALUES BUF

                    Console.WriteLine($"Insert {bufDatas.Count} elements from data buf. Total inserted {itemsCount} of {datas[i].DateValues.Count}.");

                    DataValuesAPI.InsertAsync(client, bufDatas);
                }
            }

            return itemsCount;
        }
        static FileType GetFileType(string filePath)
        {
            FileInfo fi = new FileInfo(filePath);

            char splitter = '-';
            string[] cells = fi.Name.Split(splitter);

            switch (cells[0].Trim().ToUpper())
            {
                case "АГК":
                    return FileType.Type2;
                default:
                    return FileType.Unknown;
            }
        }
        static Site GetFileSite(HttpClient client, string filePath)
        {
            FileInfo fi = new FileInfo(filePath);

            char splitter = '-';
            string[] cells = fi.Name.Split(splitter);

            string siteCode = (cells[0] + splitter + int.Parse(cells[1]) + splitter + int.Parse(cells[2])).ToUpper();
            List<Site> sites = SitesAPI.GetByFilterAsync(client, new SiteFilter { CodeLike = siteCode, OwnerId = FileSite.EMERCIT_ORG_ID }).Result;
            sites = sites.Where(x => x.Code == siteCode).ToList();

            if (sites.Count == 1)
                return sites[0];

            Console.WriteLine($"(sites.Count ={sites.Count})");
            return null;
        }
        static List<Data> ParseType2(HttpClient client, string filePath)
        {
            Site site = GetFileSite(client, filePath);
            if (site == null)
                return null;

            char splitter = ';';
            string line = "EMPTY";

            Dictionary<string/*filecolumn name*/, Varoff> columnXVaroff = new Dictionary<string, Varoff>
            {
                { "Rivlevel from sensor, m", new Varoff{ VariableId=2, OffsetTypeId=0, OffsetValue=0 } },
                { "Rivlevel BHS, m", null},
                { "AtmPress, mmHg" , new Varoff{ VariableId=1249, OffsetTypeId=102, OffsetValue=2 } },
                { "DewPoint, °C" , new Varoff{ VariableId=1350, OffsetTypeId=102, OffsetValue=2 } },
                { "Rhum, %" , new Varoff{ VariableId=1352, OffsetTypeId=102, OffsetValue=2 } },
                { "Prec, mm" , new Varoff{ VariableId=1360, OffsetTypeId=102, OffsetValue=2 } },
                { "TypePrec" , new Varoff{ VariableId=-1023, OffsetTypeId=102, OffsetValue=2 } }, // Знак минус означает необх. перекодировки (текст->код)
                { "Temp, °C" , new Varoff{ VariableId=1300, OffsetTypeId=102, OffsetValue=2 } },

            };
            Dictionary<string, double> typePrecipitationXCode = new Dictionary<string, double>
            {
                {"нет осадков",4 },
                {"жидкие", 1},
                {"ледяной дождь",6 },
                {"мокрый снег", 5},
                {"твёрдые", 2}
            };

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            System.IO.StreamReader sr = new System.IO.StreamReader(filePath, Encoding.GetEncoding("windows-1251"));//.UTF8);//.GetEncoding("windows-1251")) ;

            try
            {
                // READ HEADER

                line = sr.ReadLine();
                string[] cells = line.Split(splitter);

                if (cells[0] != "Datetime")
                    throw new Exception("First column is not Datetime.");

                List<Data> ret = new List<Data>();

                for (int i = 1; i < cells.Length; i++)
                {
                    if (!columnXVaroff.TryGetValue(cells[i], out Varoff varoff))
                        throw new Exception($"Unknown column {cells[i]}.");

                    ret.Add(new Data { Site = site, Varoff = varoff, DateValues = varoff == null ? null : new List<DateValue>(100000) });
                }

                // READ DATA BODY
                int iLineCount = 0;
                while (!sr.EndOfStream)
                {
                    iLineCount++;
                    line = sr.ReadLine().Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    cells = line.Split(splitter);

                    DateTime date = DateTime.Parse(cells[0]);

                    for (int i = 1; i < cells.Length; i++)
                    {
                        if (ret[i - 1].Varoff != null)
                        {
                            double value = double.NaN;
                            if (ret[i - 1].Varoff.VariableId < 0)
                            {
                                if (ret[i - 1].Varoff.VariableId == -1023)
                                {
                                    if (!typePrecipitationXCode.TryGetValue(cells[i].Trim(), out value))
                                        throw new Exception($"Unknown string categorical value {cells[i].Trim()} in column #{i} for variableId ={ret[i - 1].Varoff.VariableId}.");
                                }
                                else
                                    throw new Exception($"Unknown categorical value column #{i} for variableId ={ret[i - 1].Varoff.VariableId}.");
                            }
                            else
                                value = StrVia.ParseDouble(cells[i]);

                            if (double.IsNaN(value))
                                throw new Exception($"Value for date {date} in column #{i} for variableId ={ret[i - 1].Varoff.VariableId} is NaN.");

                            ret[i - 1].DateValues.Add(new DateValue { Date = date, Value = value });
                        }
                    }
                    //if (iLineCount == 10) break;
                }
                return ret;
            }
            catch (Exception ex)
            {
                Console.WriteLine(line + "\n\n" + ex.ToString());
                return null;
            }
            finally
            {
                if (sr != null) sr.Close();
            }
        }
        public class Data
        {
            public Site Site;
            public Varoff Varoff;

            public List<DateValue> DateValues { get; set; }
        }
        public class DateValue
        {
            public DateTime Date { get; set; }
            public double Value { get; set; }
        }

        ////static List<Data> ParseType1(string filePath)
        ////{
        ////    char splitter = ',';
        ////    string line = "EMPTY";
        ////    string[] file_columns = new string[]
        ////    {
        ////        "station", "variable", "time", "value"
        ////    };

        ////    List<Data> ret = new List<Data>();

        ////    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ////    System.IO.StreamReader sr = new System.IO.StreamReader(filePath, Encoding.UTF8);//.GetEncoding("windows-1251")) ;

        ////    try
        ////    {
        ////        // READ HEADER

        ////        line = sr.ReadLine();
        ////        string[] columns = line.Split(splitter);
        ////        for (int i = 0; i < file_columns.Length; i++)
        ////        {
        ////            if (file_columns[i] != columns[i])
        ////                throw new Exception("File columns not compatible.");
        ////        }
        ////        string[] cells;

        ////        // READ DATA BODY

        ////        while (!sr.EndOfStream)
        ////        {
        ////            line = sr.ReadLine().Trim();
        ////            if (string.IsNullOrEmpty(line)) continue;
        ////            cells = line.Split(splitter);

        ////            Data data = ret.FirstOrDefault(x => x.SiteName == cells[0].Trim() && x.VariableName == cells[1].Trim());
        ////            if (data == null)
        ////            {
        ////                data = new Data() { SiteName = cells[0], VariableName = cells[1], DateValues = new List<DateValue>(500000) };
        ////                ret.Add(data);
        ////            }

        ////            data.DateValues.Add(new DateValue { Date = DateTime.Parse(cells[2]), Value = StrVia.ParseDouble(cells[3]) });

        ////            //if (ret[0].Values.Count == 5) break;
        ////        }
        ////        return ret;
        ////    }
        ////    catch (Exception ex)
        ////    {
        ////        Console.WriteLine(line + "\n\n" + ex.ToString());
        ////        return null;
        ////    }
        ////    finally
        ////    {
        ////        if (sr != null) sr.Close();
        ////    }
        ////}
    }
}
