using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Amur.Service.Client;
using Amur.Data.Models;
using Amur.Data.Filters;
using System.Linq;

namespace Import.Files
{
    /// <summary>
    /// Файл *.csv с мета-данными станций (от Пелагеи, 2019).
    /// В первой строке файла указаны названия столбцов.
    /// </summary>
    public class FileSite
    {
        public static int EMERCIT_SITE_TYPE_ID = 6; // АГК
        public static int EMERCIT_ADDR_REGION_ID = 138; // Краснодарский
        public static int EMERCIT_ORG_ID = 1297; // EMERCIT

        /// <summary>
        /// Чтение данных из файла и запись в БД Амур через REST сервис.
        /// </summary>
        /// <param name="filePath">Путь к файлу.</param>
        /// <param name="client">REST</param>
        /// <returns></returns>
        public static async Task<int> Import(HttpClient client, string filePath)
        {
            // Дата актуальности, которая фигурирует при записи атрибутов пункта.
            DateTime importSiteAttrDateActual = new DateTime(2000, 1, 1);

            // PARSE FILE

            List<FileSite.SiteEMERCIT> sites = FileSite.Parse(filePath);
            Console.WriteLine($"{sites.Count} readed from file [{filePath}]");

            // INSERT DATA
            List<Site> allSites = await SitesAPI.GetByFilterAsync(client, new Amur.Data.Filters.SiteFilter { OwnerId = EMERCIT_ORG_ID, SiteTypeId = EMERCIT_SITE_TYPE_ID });

            int i = 1;
            foreach (var siteE in sites)
            {
                Console.WriteLine($"Insert site #{i++}");

                // GET EXISTING or CREATE SITE

                Site existSite = allSites.FirstOrDefault(x => x.Code == siteE.Site.Code && x.Name == siteE.Site.Name);
                int siteId = (existSite == null) ? await SitesAPI.CreateAsync(client, siteE.Site) : existSite.Id;

                // GET EXISTING or CREATE GEOOBJECTS

                int? goFallIntoId = await GetGeoObjectId(client, siteE.FallIntoName);
                int? goId = await GetGeoObjectId(client, siteE.RiverName, (int)EnumGeoObject.River, goFallIntoId);
                if (goId.HasValue)
                {
                    List<SiteXGeoObject> sgos = await SiteXGeoObjectAPI.GetBySiteIdAsync(client, siteId);
                    if (sgos == null || sgos.Count == 0)
                        SiteXGeoObjectAPI.CreateAsync(client, new SiteXGeoObject { SiteId = siteId, GeoObjectId = (int)goId, OrderBy = -1 });
                }
                // CREATE SITE ATTRIBUTES

                await SiteAttributesAPI.CreateAsync(client, siteId, (int)EnumSiteAttrType.CatchmentArea, siteE.CatchmentArea, importSiteAttrDateActual);
                await SiteAttributesAPI.CreateAsync(client, siteId, (int)EnumSiteAttrType.NYa, siteE.NYa, importSiteAttrDateActual);
                await SiteAttributesAPI.CreateAsync(client, siteId, (int)EnumSiteAttrType.OYa, siteE.OYa, importSiteAttrDateActual);
                await SiteAttributesAPI.CreateAsync(client, siteId, (int)EnumSiteAttrType.DistFromMouth, siteE.DistFromMouth, importSiteAttrDateActual);
                await SiteAttributesAPI.CreateAsync(client, siteId, (int)EnumSiteAttrType.MarkSiteZeroBS77, siteE.DeviceLevel, importSiteAttrDateActual);
                await SiteAttributesAPI.CreateAsync(client, siteId, (int)EnumSiteAttrType.UTCOffset, siteE.UTCOffset, importSiteAttrDateActual);
            }
            Console.WriteLine($"Site import ended...");
            return sites.Count;
        }
        static async Task<int?> GetGeoObjectId(HttpClient client, string geoobName, int defaultGeoobTypeId = 41 /*река*/, int? goFallInto = null)
        {
            int? id = null;
            if (!string.IsNullOrEmpty(geoobName))
            {
                List<GeoObject> gos = await GeoObjectAPI.GetByFilterAsync(client, new GeoObjectFilter { NameLike = $"{geoobName}" });
                gos = gos.Where(x => x.Name == geoobName).ToList();

                if (gos == null || gos.Count == 0)
                    id = await GeoObjectAPI.CreateAsync(client, geoobName);
                else if (gos.Count == 1)
                    id = gos[0].Id;
                else
                    throw new Exception($"{geoobName} more than 1 geoobject...");
            }
            return id;
        }
        static List<SiteEMERCIT> Parse(string filePath)
        {
            char splitter = ';';
            string line = "EMPTY";
            string[] file_columns = new string[]
            {
                "№", "Широта", "Долгота", "АГК", "Река", "Пункт","Куда впадает","Уровень датчика, м", // 7
                "Отметка НЯ, м","Отметка ОЯ, м","Комментарий","Расстояние от устья, км","Площадь бассейна, км2"
            };

            List<SiteEMERCIT> ret = new List<SiteEMERCIT>();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            System.IO.StreamReader sr = new System.IO.StreamReader(filePath, Encoding.GetEncoding("windows-1251"));
            try
            {
                // READ FILE CAPTION

                line = sr.ReadLine();
                string[] columns = line.Split(splitter);
                for (int i = 0; i < file_columns.Length; i++)
                {
                    if (file_columns[i] != columns[i])
                        throw new Exception("File columns error.");
                }
                string[] cells;

                // READ FILE ROWS

                while (!sr.EndOfStream)
                {
                    line = sr.ReadLine().Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    cells = line.Split(splitter);

                    SiteEMERCIT site = new SiteEMERCIT()
                    {
                        Site = new Site()
                        {
                            Code = cells[3].Trim(),

                            SiteTypeId = EMERCIT_SITE_TYPE_ID, // АГК
                            AddrRegionId = EMERCIT_ADDR_REGION_ID, // Краснодарский
                            OwnerId = EMERCIT_ORG_ID, // EMERCIT

                            Name = cells[5].Trim(),
                            ParentId = null,
                            Lat = SOV.Common.Support.ParseDouble(cells[1]),
                            Lon = SOV.Common.Support.ParseDouble(cells[2]),
                            Description = string.IsNullOrEmpty(cells[10].Trim()) ? null : cells[10].Trim(),
                        },

                        RiverName = cells[4].Trim(),
                        FallIntoName = cells[6].Trim(),
                        CatchmentArea = SOV.Common.Support.ParseDouble(cells[12]),
                        NYa = SOV.Common.Support.ParseDouble(cells[8]),
                        OYa = SOV.Common.Support.ParseDouble(cells[9]),
                        DistFromMouth = SOV.Common.Support.ParseDouble(cells[11]),
                        DeviceLevel = SOV.Common.Support.ParseDouble(cells[7]),
                        UTCOffset = 3
                    };
                    site.Site.Name = string.IsNullOrEmpty(site.Site.Name) ? site.Site.Code : site.Site.Name;
                    ret.Add(site);
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
        public class SiteEMERCIT
        {
            public Site Site;
            public string RiverName { get; set; }
            public string FallIntoName { get; set; }
            public double CatchmentArea { get; set; }
            public double OYa { get; set; }
            public double NYa { get; set; }
            public double DistFromMouth { get; set; }
            public double DeviceLevel { get; set; }
            public int UTCOffset { get; set; }

            override public string ToString()
            {
                return JsonConvert.SerializeObject(this);
            }
        }
    }
}
