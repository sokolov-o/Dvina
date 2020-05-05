using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Import.Files;

namespace _ImportFiles
{
    public class DataValue
    {
        public DateTime Date;
        public int CatalogId;
        public double OffsetValue;
        public double Value;
        public short FlagAQC;
        public byte UTCOffset;
        public int DataSourceId;

        static int GetSiteId(string siteCode)
        {
            Moo.MooDataSet mooDS = new Moo.MooDataSet();
            Moo.MooDataSetTableAdapters.SitesTableAdapter sitesTA = new Moo.MooDataSetTableAdapters.SitesTableAdapter();

            sitesTA.Fill(mooDS.Sites, null, siteCode, null, null, null);
            if (mooDS.Sites.Count == 0)
                return 0;
            else if (mooDS.Sites.Count > 1)
                return -1;
            else
                return mooDS.Sites[0].Id;
        }
        static Dictionary<int/*VariableId*/, int/*CatalogId*/> GetCatalogIds(int siteId, List<int> variableIds)
        {
            Moo.DataObs.DataObsDataSet ds = new Moo.DataObs.DataObsDataSet();
            Moo.DataObs.DataObsDataSetTableAdapters.CatalogsTableAdapter ta = new Moo.DataObs.DataObsDataSetTableAdapters.CatalogsTableAdapter();
            Moo.DataObs.DataObsDataSetTableAdapters.qta qta = new Moo.DataObs.DataObsDataSetTableAdapters.qta();

            int methodId = 0; // Наблюдения
            int valueTypeId = 2; // Наблюдения
            int offsetTypeId = 0; // Нет смещения 
            int dateTypeId = 2; // Local time
            int sourceId = 1402; // ИВП ДВО РАН

            ta.Fill(ds.Catalogs,
                Moo.DataObs.DataManager.GetDataTableArrayOfInt(new List<int>() { siteId }),
                Moo.DataObs.DataManager.GetDataTableArrayOfInt(variableIds),
                Moo.DataObs.DataManager.GetDataTableArrayOfInt(new List<int>() { methodId }),
                Moo.DataObs.DataManager.GetDataTableArrayOfInt(new List<int>() { sourceId }),
                 Moo.DataObs.DataManager.GetDataTableArrayOfInt(new List<int>() { offsetTypeId }),
                 Moo.DataObs.DataManager.GetDataTableArrayOfInt(new List<int>() { valueTypeId }),
                 Moo.DataObs.DataManager.GetDataTableArrayOfInt(new List<int>() { dateTypeId })
                );

            Dictionary<int/*VariableId*/, int/*CatalogId*/> ret = new Dictionary<int, int>();

            foreach (var variableId in variableIds)
            {
                IEnumerable<Moo.DataObs.DataObsDataSet.CatalogsRow> ctlRows = ds.Catalogs.Where(x => x.VariableId == variableId);
                int catalogId = -1;

                // NO CATALOG RECORD => CREATE
                if (ctlRows.Count() == 0)
                {
                    catalogId = (int)qta.InsertCatalogs(siteId, variableId, offsetTypeId, methodId, sourceId, valueTypeId, dateTypeId);
                }
                // CATALOG RECORD EXISTS => USE
                else if (ctlRows.Count() == 1)
                    catalogId = ctlRows.ElementAt(0).Id;
                // MORE THAN ONE CATALOG RECORD => THROW
                else
                    throw new Exception($"(ds.Catalogs.Where(x => x.VariableId == {variableId}).Count() > 1)");

                ret.Add(variableId, catalogId);
            }

            return ret;
        }
        static public List<DataValue> Convert(List<FileChemAnnual.FileRowData> data)
        {
            // GET SITE ID
            int siteId = GetSiteId(data[0].SiteCode);
            if (siteId <= 0)
                throw new Exception($"Отсутствует пункт с кодом {data[0].SiteCode}.");

            // GET CATALOGS
            List<int> variableIds = new List<int>();
            for (int i = 0; i < FileChemAnnual.FileRowData.ValuesLength; i++)
            {
                int? variableId = FileChemAnnual.FileRowData.GetVariableId(i);
                if (variableId.HasValue)
                    variableIds.Add((int)variableId);
            }
            Dictionary<int/*VariableId*/, int/*CatalogId*/> catalogIds = GetCatalogIds(siteId, variableIds);

            // CONVERT FILE DATA 2 DESTINATION DATAVALUE
            List<DataValue> ret = new List<DataValue>();
            foreach (var item in data)
            {
                for (int i = 0; i < item.Values.Length; i++)
                {
                    int? variableId = FileChemAnnual.FileRowData.GetVariableId(i);

                    if (!double.IsNaN(item.Values[i]) && variableId.HasValue)
                    {
                        // GET CATALOG
                        int catalogId = catalogIds[(int)variableId];

                        // ADD DATA
                        ret.Add(new DataValue
                        {
                            Date = item.Date,
                            CatalogId = catalogId,
                            Value = item.Values[i],
                            UTCOffset = 255,

                            DataSourceId = 1,
                            FlagAQC = 0,
                            OffsetValue = 0
                        }
                        );
                    }
                }
            }
            return ret;
        }
    }
}
