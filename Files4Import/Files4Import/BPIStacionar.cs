using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;


namespace Dvina.Files4Import
{
    /// <summary>
    /// Файлs xls -> csv с данными измерений на "Верхнеуссурийский стационар БПИ 2014г" (Лупаков, 2019).
    /// 
    /// Верхнеуссурийский стационар БПИ 2014г																																																
    /// н.опр. - не определялось                    Полевой анализатор YSI ProPlus                  Полевой рН метр "Эксперт"			Макросостав
    /// н.обн. - ниже предела обнаружения Электропроводность (SPC) Сумма ионов (TDS) Хроматограф LC 10Avp YSI pro plus
    /// дата    №пробы Расход измеренный, л/с t °C SPС, mS/cm SPС, mS(расчет) M, мг/л М, мг/л(расчет) рН  рН Взвесь, мг/л С мг/л НСО3-, мг/л Cl, мг/л SO4, мг/л NO3, мг/л NO3, мг/л Ca++ мг/л Mg++мг/л K+мг/л Na+мг/л Si мг/л
    /// 24.05.2014	594	руч.Еловый, гх пост	13,2	5,2	39,2	40,39	25,35	22,21	5,72	5,65	0,29	3,3	2,44	0,77	9,37	3,46		3,12	0,52	1,34	1,19	5,27																										
    /// 24.05.2014	595	руч.Резервный, устье   14,8	5,0	48,5	49,16	31,85	30,16	6,62	6,54	3,24	3,3	8,91	0,83	7,48	4,48		5,02	0,49	1,20	1,76	6,52																										
    /// 24.05.2014	596	руч.Еловый, устье  50,3	6,0	41,4	42,11	26,65	25,23	6,53	6,41	1,43	3,1	6,71	0,77	7,43	2,99		3,83	0,42	1,11	1,96	6,43																										
    /// 
    /// </summary>

    public class BPIStacionar
    {
        // ASSIGHN DEVICES 2 VARIABLES

        readonly static List<string> _deviceNames = new List<string>
        {
            "НЕИЗВЕСТНО",
            "Полевой анализатор YSI ProPlus",
            "Полевой рН метр \"Эксперт\"",
            "Хроматограф LC 10Avp"
        };
        readonly static List<int> _varXdev = new List<int>
                {
                    0,0,
                    1,1,1,1,1,
                    2,
                    3,3,3,3,3,3,
                    1,1,1,1,1,1
                };
                /// <summary>
        /// Коды переменных и типа значения (value_type.id) Амур для переменных в файле 
        /// </summary>
        readonly static List<int[/*variable_id, value_type_id*/]> _amurVars = new List<int[]>
                {
                    new int[]{ 1500, 2}, //"Расход измеренный, л/с"
                    new int[]{ 1412,2 }, //"t °C" ВОДЫ
                    new int[]{ 1501,2 }, //"SPС, mS/cm"
                    new int[]{ 1501,1 },//"SPС, mS(расчет)"
                    new int[]{   -1,1 },//"M, мг/л" - пока не импортируем, не понятно что это...
                    new int[]{   -1,1 },//"М, мг/л(расчет)" - пока не импортируем, не понятно что это...
                    new int[]{ 1501,2 },//"рН"
                    new int[]{ 1501,1 },//"рН"
                    new int[]{ 1503,2 },//"Взвесь, мг/л"
                    new int[]{ 1504,2 }, //"С мг/л"
                    new int[]{ 1505,2 },//"НСО3-,мг/л "
                    new int[]{ 1506,2 },//"Cl, мг/л"
                    new int[]{ 1507,2 },//"SO4, мг/л"
                    new int[]{ 1508,2 },//"NO3, мг/л" - другой прибор
                    new int[]{ 1508,2 },//"NO3, мг/л" - другой прибор
                    new int[]{ 1509,2 },//"Ca++ мг/л"
                    new int[]{ 1510,2 },//"Mg++мг/л"
                    new int[]{ 1511,2 },//"K+мг/л"
                    new int[]{ 1512,2 },//"Na+мг/л"
                    new int[]{ 1513,2 },//"Si мг/л"
                };
        readonly static List<string> _fileVarColumnNames = new List<string>
                {
                    "дата","№пробы","",
                    "Расход измеренный, л/с","t °C", // Прибор неизвестен {2,3}
                    "SPС, mS/cm","SPС, mS(расчет)","M, мг/л","М, мг/л(расчет)","рН", // Полевой анализатор YSI ProPlus {4-8}
                    "рН",// Полевой рН метр "Эксперт" {9}
                    "Взвесь, мг/л","С мг/л", "НСО3-,мг/л ", "Cl, мг/л","SO4, мг/л","NO3, мг/л", // Хроматограф LC 10Avp {10-15}
                    "NO3, мг/л", "Ca++ мг/л","Mg++мг/л","K+мг/л","Na+мг/л","Si мг/л" // YSI pro plus {16-21}
                };

        /// <summary>
        /// Чтение данных из файла и запись в БД Амур через REST сервис.
        /// </summary>
        /// <param name="filePath">Путь к файлу.</param>
        /// <param name="client">REST</param>
        /// <returns></returns>
        public static async Task<int> Import(HttpClient client, string filePath, FileType dataFileType)
        {
            // PARSE

            List<Data> datas = Parse(filePath, dataFileType);

            // GET UTC OFFSET

            SiteAttributeValue sav = await SiteAttributesAPI.GetByFilterAsync(client, new SiteAttribute
            { DateS = DateTime.Today, SiteAttributeTypeId = (int)EnumSiteAttrType.UTCOffset, SiteId = SITE_ID_BPISTAC });
            if (sav == null)
                throw new Exception($"Отсутствует UTCOffset для сайта id={SITE_ID_BPISTAC}.");
            int utcOffset = int.Parse(sav.Value);

            // GET Sample attributes
            Site parentSite = await SitesAPI.GetByIdAsync(client, SITE_ID_BPISTAC);
            List<Site> sampleSites = await GetSampleSites(client, parentSite, utcOffset, datas.Select(x => x.PointName).Distinct());
            List<DeviceItem> deviceItems = await GetDeviceItems(client, _deviceNames);
            List<Sample> samples = await GetSamples(client, sampleSites, datas);
            List<SampleDeviceItem> sampleDeviceItems = await GetSampleDeviceItems(client, deviceItems, samples);

            // PREPARE CATALOG BUFFER

            List<Catalog> catalogs = await CatalogsAPI.GetByFilterAsync(client, new CatalogFilter { Sites = sampleSites.Select(x => x.Id).ToList() });

            // INSERT DATA
            int itemsCount = 0;
            int bufItemsLength = 50000;

            for (int i = 0; i < datas.Count; i++)
            {
                Data data = datas[i];

                if (_amurVars.Count != datas[i].Values.Count)
                    throw new Exception("(_amurVars.Count != datas[i].Values.Count)");

                // GET SAMPLE_DEVICE_ITEM

                int siteId;
                {
                    IEnumerable<Site> sites1 = sampleSites.Where(x => x.Name.ToUpper() == data.PointName.Trim().ToUpper());
                    if (sites1.Count() != 1) throw new Exception("(sites.Count() != 1)");
                    siteId = sites1.ElementAt(0).Id;
                }
                int sampleId;
                {
                    IEnumerable<Sample> samples1 = samples.Where(x => x.SiteId == siteId && x.SampleNum == data.SampleNum);
                    if (samples1.Count() != 1) throw new Exception("(samples1.Count() != 1)");
                    sampleId = samples1.ElementAt(0).Id;
                }

                for (int iVar = 0; iVar < _amurVars.Count;)
                {
                    // GET SAMPLE DEVICE ITEM
                    int sampleDeviceItemId;
                    {
                        IEnumerable<SampleDeviceItem> sampleDeviceItems1 = sampleDeviceItems.Where(x =>
                            x.SampleId == sampleId
                            && x.DeviceItemId == deviceItems[_varXdev[iVar]].Id
                        );
                        if (sampleDeviceItems1.Count() != 1) throw new Exception("(sampleDeviceItems1.Count() != 1)");
                        sampleDeviceItemId = sampleDeviceItems1.ElementAt(0).Id;
                    }

                    // GET or CREATE CATALOGS FOR VARIABLE

                    Catalog catalog = new Catalog
                    {
                        MethodId = (int)EnumMethod.ObservationInSitu,
                        ParentId = null,
                        SiteId = siteId,
                        SourceId = SOURCE_ID_PGI,
                        VariableId = _amurVars[iVar][0],
                        ValueTypeId = _amurVars[iVar][1],
                        OffsetTypeId = (int)EnumOffsetType.Sample,
                        OffsetValue = sampleDeviceItemId
                    };

                    List<Catalog> catalogs1 = catalogs.FindAll(x =>
                        x.MethodId == catalog.MethodId
                        && !x.ParentId.HasValue
                        && x.SiteId == catalog.SiteId
                        && x.SourceId == catalog.SourceId
                        && x.VariableId == catalog.VariableId
                        && x.ValueTypeId == catalog.ValueTypeId
                        && x.OffsetTypeId == catalog.OffsetTypeId
                        && x.OffsetValue == catalog.OffsetValue
                    );

                    if (catalogs1.Count == 0)
                    {
                        catalog.Id = await CatalogsAPI.CreateAsync(client, catalog);
                        catalogs.Add(catalog);
                    }
                    else if (catalogs1.Count == 1)
                        catalog = catalogs1[0];
                    else
                        throw new Exception($"(catalogs1.Count = {catalogs1.Count })");


                    // CONVERT DATA 2 AMUR DATAVALUES
                    List<DataValue> bufDatas = new List<DataValue>(bufItemsLength);
                    for (int k = 0; k < bufItemsLength; k++)
                    {
                        if (iVar < datas[i].Values.Count)
                        {
                            bufDatas.Add(new DataValue()
                            {
                                CatalogId = catalog.Id,
                                Date = data.Date,
                                DateTypeId = (int)EnumDateType.LOC,
                                UTCOffset = utcOffset,
                                Value = data.Values[iVar],
                                FlagAQC = (byte)EnumFlagAQC.NoAQC
                            });
                            iVar++;
                            itemsCount++;
                        }
                    }

                    // INSERT DATAVALUES BUF
                    Console.WriteLine($"Insert data buf ({bufDatas.Count} elements)...");
                    //////await DataValuesAPI.Insert(client, bufDatas);
                }
            }
            return itemsCount;
        }

        private static async Task<List<SampleDeviceItem>> GetSampleDeviceItems(HttpClient client, List<DeviceItem> deviceItems, List<Sample> samples)
        {
            Console.Write("GetSampleDeviceItems started...");

            List<SampleDeviceItem> ret = new List<SampleDeviceItem>();
            foreach (var sample in samples)
            {
                foreach (var deviceItem in deviceItems)
                {
                    SampleDeviceItem sampleDeviceItem = new SampleDeviceItem { SampleId = sample.Id, DeviceItemId = deviceItem.Id };

                    List<SampleDeviceItem> sampleDeviceItems = await SampleDeviceItemsAPI.GetByFilterAsync(client, sampleDeviceItem);
                    if (sampleDeviceItems == null || sampleDeviceItems.Count == 0)
                    {
                        sampleDeviceItem.Id = await SampleDeviceItemsAPI.CreateAsync(client, sampleDeviceItem);
                    }
                    else if (sampleDeviceItems.Count == 1)
                        sampleDeviceItem = sampleDeviceItems[0];
                    else
                        throw new Exception($"More than one sampleDeviceItem for device item id= [{deviceItem.Id}]");

                    ret.Add(sampleDeviceItem);
                }
            }
            Console.WriteLine($" and endeded. {ret.Count} items generated.");
            return ret;
        }

        private static async Task<List<Sample>> GetSamples(HttpClient client, List<Site> sites, List<Data> datas)
        {
            Console.Write("GetSamples started...");

            List<Sample> ret = new List<Sample>();
            foreach (var data in datas)
            {
                // SITE
                List<Site> sites1 = sites.Where(x => x.Name.ToUpper() == data.PointName.Trim().ToUpper()).ToList();
                if (sites1.Count != 1)
                    throw new Exception($"(sites1.Count = {sites1.Count }) for [{sites1[0].Name}] & [{sites1[1].Name}]");
                int siteId = sites1[0].Id;

                Sample sample = new Sample { SiteId = siteId, SampleNum = data.SampleNum };

                List<Sample> samples = await SamplesAPI.GetByFilterAsync(client, sample);
                if (samples == null || samples.Count == 0)
                {
                    sample.Id = await SamplesAPI.CreateAsync(client, sample);
                }
                else if (samples.Count == 1)
                    sample = samples[0];
                else
                    throw new Exception($"More than one device with name [{data}]");

                ret.Add(sample);
            }
            Console.WriteLine($" and endeded. {ret.Count} items generated.");
            return ret;
        }

        private static async Task<List<DeviceItem>> GetDeviceItems(HttpClient client, List<string> deviceNames)
        {
            Console.Write("GetDeviceItems started...");

            List<DeviceItem> ret = new List<DeviceItem>();
            foreach (var deviceName in deviceNames)
            {
                // DEVICE

                Device device = new Device { Name = deviceName, MadeById = (int)EnumLegalEntity.UNKNOWN };

                List<Device> devices = await DevicesAPI.GetByFilterAsync(client, device);
                if (devices == null || devices.Count == 0)
                {
                    device.Id = await DevicesAPI.CreateAsync(client, device);
                }
                else if (devices.Count == 1)
                    device = devices[0];
                else
                    throw new Exception($"More than one device with name [{deviceName}]");

                // DEVICE ITEM

                DeviceItem deviceItem = new DeviceItem { CodeNum = "0", DeviceId = device.Id };

                List<DeviceItem> deviceItems = await DeviceItemsAPI.GetByFilterAsync(client, deviceItem);
                if (deviceItems == null || deviceItems.Count == 0)
                {
                    deviceItem.Id = await DeviceItemsAPI.CreateAsync(client, deviceItem);
                }
                else if (deviceItems.Count == 1)
                    deviceItem = deviceItems[0];
                else
                    throw new Exception($"More than one device item for device with name [{deviceName}]");

                ret.Add(deviceItem);
            }
            Console.WriteLine($" and endeded. {ret.Count} items generated.");
            return ret;
        }

        private static async Task<List<Site>> GetSampleSites(HttpClient client, Site parentSite, double utcOffset, IEnumerable<string> pointNames)
        {
            Console.Write("GetSampleSites started...");
            List<Site> ret = new List<Site>();

            foreach (var pointName1 in pointNames)
            {
                string pointName = pointName1.Trim();
                if (ret.Exists(x => x.Name.ToUpper() == pointName.ToUpper() && x.ParentId == parentSite.Id))
                    continue;

                List<Site> sites = await SitesAPI.GetByFilterAsync(client, new SiteFilter { NameLike = pointName, ParentId = parentSite.Id });
                sites = sites.Where(x => x.Name.ToUpper() == pointName.ToUpper()).ToList();

                Site site;

                if (sites == null || sites.Count == 0)
                {
                    site = new Site
                    {
                        Name = pointName,
                        SiteTypeId = (int)EnumSiteType.SamplePoint,
                        Code = pointName.Substring(0, pointName.Length < 20 ? pointName.Length : 20),

                        AddrRegionId = parentSite.AddrRegionId,
                        Lat = parentSite.Lat,
                        Lon = parentSite.Lon,
                        OwnerId = parentSite.OwnerId,
                        ParentId = parentSite.Id
                    };
                    site.Id = await SitesAPI.CreateAsync(client, site);

                    SiteAttributesAPI.CreateAsync(client, new SiteAttributeValue
                    {
                        DateS = DATE_S_SITE_ATTR,
                        SiteAttributeTypeId = (int)EnumSiteAttrType.UTCOffset,
                        SiteId = site.Id,
                        Value = utcOffset.ToString()
                    });
                }
                else if (sites.Count == 1)
                    site = sites[0];
                else
                    throw new Exception($"More than one site with name [{pointName}]");

                ret.Add(site);
            }
            Console.WriteLine($" and endeded. {ret.Count} items generated.");
            return ret;
        }

        static List<Data> Parse(string filePath, FileType dataFileType)
        {
            switch (dataFileType)
            {
                case FileType.Type1: return ParseType1(filePath);
                default:
                    throw new Exception("switch(dataFileType) - " + dataFileType);
            }
        }
        static List<Data> ParseType1(string filePath)
        {
            char splitter = ';';
            string line = "EMPTY";
            List<Data> ret = new List<Data>();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            System.IO.StreamReader sr = new System.IO.StreamReader(filePath, Encoding.GetEncoding("windows-1251"));//, Encoding.UTF8);//Encoding.GetEncoding("windows-1251")) ;

            try
            {
                // PARCE HEADER

                // Верхнеуссурийский стационар БПИ 2014г
                line = sr.ReadLine();
                string[] cells = line.Split(splitter);
                if (cells[0].IndexOf("Верхнеуссурийский стационар БПИ") < 0)
                    throw new Exception("IndexOf(\"Верхнеуссурийский стационар БПИ\") < 0.");

                // Приборы (две строки в файле)
                List<string> sdevices = new List<string>
                {
                    "н.опр. - не определялось","Полевой анализатор YSI ProPlus","\"Полевой рН метр \"\"Эксперт\"\"\"","Макросостав",
                    "н.обн. - ниже предела обнаружения","Электропроводность (SPC)","Сумма ионов (TDS)","Хроматограф LC 10Avp","YSI pro plus"
                };

                for (int i = 0; i < 2; i++)
                {
                    line = sr.ReadLine();
                    cells = line.Split(splitter);
                    foreach (string cell in cells)
                    {
                        string item = cell.Trim();
                        if (!string.IsNullOrEmpty(item))
                        {
                            if (!sdevices.Exists(x => x.IndexOf(item) >= 0))
                                throw new Exception($"Device [{item}] is not exists.");
                        }
                    }
                }

                // Переменные

                line = sr.ReadLine();
                cells = line.Split(splitter);
                for (int i = 0; i < _fileVarColumnNames.Count; i++)
                {
                    if (_fileVarColumnNames[i].Trim() != cells[i].Trim())
                        throw new Exception($"Variable [{_fileVarColumnNames[i].Trim()}] != [{cells[i].Trim()}].");
                }


                // READ DATA BODY

                while (!sr.EndOfStream)
                {
                    line = sr.ReadLine().Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    cells = line.Split(splitter);

                    Data data = new Data { Date = DateTime.Parse(cells[0]), SampleNum = int.Parse(cells[1]), PointName = cells[2] };
                    for (int i = 3; i < _fileVarColumnNames.Count; i++)
                    {
                        string cell = cells[i].Trim();
                        double value = double.NaN;
                        double value1 = double.NaN;
                        string valueAddon = null;

                        if (!string.IsNullOrEmpty(cell))
                        {
                            value = StrVia.ParseDouble(cell);
                            if (double.IsNaN(value))
                            {
                                if (cell != "н.опр." && cell == "#ЗНАЧ!" && cell == "проба не отбиралась" && cell == "too few water")
                                {
                                    if (cell == "н.обн.")
                                    {
                                        value = 0;
                                        valueAddon = "н.обн.";
                                    }
                                    else if (cell.ElementAt(0) == '<')
                                    {
                                        value = StrVia.ParseDouble(cell.Remove(0, 1));
                                        if (double.IsNaN(value))
                                            throw new Exception($"1) Не удалось разобрать строку для ячейки i=[{i}] значение [{cell}]. Строка \n[{line}]");
                                        valueAddon = "<";
                                    }
                                    else if (cell.IndexOf('/') >= 0)
                                    {
                                        string[] cell1 = cell.Split('/');
                                        value = StrVia.ParseDouble(cell1[0]);
                                        value1 = StrVia.ParseDouble(cell1[1]);
                                        if (double.IsNaN(value) || double.IsNaN(value1))
                                            throw new Exception($"3) Не удалось разобрать строку для ячейки i=[{i}] значение [{cell}]. Строка \n[{line}]");
                                        valueAddon = "<";
                                    }
                                    else
                                        throw new Exception($"2) Не удалось разобрать строку для ячейки i=[{i}] значение [{cell}]. Строка \n[{line}]");
                                }
                            }
                        }
                        data.Values.Add(value);
                        data.Values1.Add(value1);
                        data.ValueAddons.Add(valueAddon);
                    }
                    ret.Add(data);

                    //if (ret.Count == 5) break;
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
            public DateTime Date;
            public int SampleNum;
            public string PointName;
            public List<double> Values = new List<double>();
            public List<double> Values1 = new List<double>();
            public List<string> ValueAddons = new List<string>();
        }
    }
}
