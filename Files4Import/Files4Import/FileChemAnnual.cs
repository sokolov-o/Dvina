using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Moo.Common;
using System.IO;
using System.Globalization;

namespace Import.Files
{
    /// <summary>
    /// Файл *.csv с данными ежегодников по химии (от БИГ, 2020).
    /// 
    /// Время отбора пробы ;доли ширины аз. Град;жесткость млмоль;глубина. м;скорость течения;время хранения дни;расход реки м?/л;запах баллы ;гидрокарбон мг/л;прозрачность  см;кальций мг/л;температура град. С;взвешенные вещества мг/л;pH;Кислород мг/л ;насыщ. Кислор процент ;углекислый газ мг/л;магний мг/л;хлориды мг/л ;сульфат мг/л;минирализация мг/л;Цветность град ;окисл бихром мг/л;БПК 5 мг/л;фенолы мг/л ;нефтепродукты мг/л ;СПАВ мг/л;Трифлуралин мкг/л;ДДЭ мг/л;ДДТ мкг/л;Альфа-ГХЦГ мкг/л;Гамма-ГХЦГ мкг/л;Бета-ГХЦГ мкг/л;ГХБ мкг/л ;Ам. Соль мкг/л;Азот аммон. мг/л;азот нитрит. мг/л;азот нитрат. Мг/л ;Фосфаты мг/л;кремний мг/л;фосфор общий мг/л;железо общее мг/л ;медь мг/л;цинк мкг/л;никель мкг/л;хром общий мкг/л;ртуть мкг/л;марганец мкг/л ;;
    /// 05.01.2004 12:20;0.2;;0.5;0.13;1;61.7;;;;;0;24;7.69;9.77;67;;;;;;;22.3;1.92;0.002;0;;;;;;;;;;0.43;0.027;3.92;;;0.032;;2.8;3.4;0;4;0;82;;
    /// 11.02.2004 11:25;0.2;9.4;0.5;0.12;1;50.8;0;297;18;140;0;12;7.78;8.72;60;11.3;29.2;291;144;1090;15;30;1.84;0.001;0;;;;;;;;;;0.3;0.013;5.53;0.052;4.4;0.06;0.02;5.6;2;0;2;0;74;;
    /// 09.03.2004 11:45;0.2;;0.5;0.08;1;31.1;;;;;0;13;7.96;10.6;72;;;;;;;20;1.07;0.002;0;;;;;;;;;;0.37;0.035;3.5;;;0.054;;4.8;2.8;18;0;0;89;;

    /// </summary>

    public class FileChemAnnual
    {
        public static List<string[/*varname from file & Dvina.VariableId */]> _FILE_COLUMN_NAMES = new List<string[]>()
        {
            new string[]{ "Время отбора пробы",null },
            new string[]{ "доли ширины аз. Град",null },
            new string[]{ "жесткость млмоль","16039" },
            new string[]{ "глубина. м","16040" },
            new string[]{ "скорость течения","16041" },
            new string[]{ "время хранения дни",null },
            new string[]{ "расход реки м?/л","1292" },
            new string[]{ "запах баллы","16042" },
            new string[]{ "гидрокарбон мг/л",null },
            new string[]{ "прозрачность см",null },
            new string[]{ "кальций мг/л",null },
            new string[]{ "температура град.С","1412" },
            new string[]{ "взвешенные вещества мг/л",null },
            new string[]{ "pH",null },
            new string[]{ "Кислород мг/л",null },
            new string[]{ "насыщ.Кислор процент",null },
            new string[]{ "углекислый газ мг/л",null },
            new string[]{ "магний мг/л",null },
            new string[]{ "хлориды мг/л",null },
            new string[]{ "сульфат мг/л",null },
            new string[]{ "минирализация мг/л",null },
            new string[]{ "Цветность град",null },
            new string[]{ "окисл бихром мг/л",null },
            new string[]{ "БПК 5 мг/л",null },
            new string[]{ "фенолы мг/л",null },
            new string[]{ "нефтепродукты мг/л",null },
            new string[]{ "СПАВ мг/л",null },
            new string[]{ "Трифлуралин мкг/л",null },
            new string[]{ "ДДЭ мг/л",null },
            new string[]{ "ДДТ мкг/л",null },
            new string[]{ "Альфа-ГХЦГ мкг/л",null },
            new string[]{ "Гамма-ГХЦГ мкг/л",null },
            new string[]{ "Бета-ГХЦГ мкг/л",null },
            new string[]{ "ГХБ мкг/л ",null },
            new string[]{ "Ам.Соль мкг/л",null },
            new string[]{ "Азот аммон.мг/л",null },
            new string[]{ "азот нитрит.мг/л",null },
            new string[]{ "азот нитрат.Мг/л",null },
            new string[]{ "Фосфаты мг/л",null },
            new string[]{ "кремний мг/л",null },
            new string[]{ "фосфор общий мг/л",null },
            new string[]{ "железо общее мг/л",null },
            new string[]{ "медь мг/л",null },
            new string[]{ "цинк мкг/л",null },
            new string[]{ "никель мкг/л",null },
            new string[]{ "хром общий мкг/л",null },
            new string[]{ "ртуть мкг/л",null },
            new string[]{ "марганец мкг/л",null }
        };

        /// <summary>
        /// Данные одной строки из файла.
        /// </summary>
        public class FileRowData
        {
            public string SiteCode = null;
            public DateTime Date = DateTime.MinValue;
            public double[] Values;
            public FileRowData()
            {
                Values = new double[ValuesLength];
                for (int i = 0; i < Values.Length; i++)
                {
                    Values[i] = double.NaN;
                }
            }
            static public int ValuesLength
            {
                get
                {
                    return _FILE_COLUMN_NAMES.Count - 1;
                }
            }
            static public int? GetVariableId(int valuesIndex)
            {
                string s = _FILE_COLUMN_NAMES[valuesIndex + 1][1];

                return (string.IsNullOrEmpty(s)) ? null : (int?)int.Parse(s);
            }
        }
        static Encoding GetEncoding(string filePath)
        {
            char splitter = ';';
            string line = "NULL";
            Encoding[] encodings = new Encoding[] { Encoding.GetEncoding("windows-1251"), Encoding.GetEncoding(866), Encoding.UTF8 };

            for (int i = 0; i < encodings.Length; i++)
            {
                StreamReader sr = new System.IO.StreamReader(filePath, encodings[i]);

                line = sr.ReadLine();
                string[] cells = line.Split(splitter);

                if (_FILE_COLUMN_NAMES.Count != cells.Length - 2)
                    continue;

                bool ok = true;
                for (int j = 0; j < _FILE_COLUMN_NAMES.Count; j++)
                {
                    if (_FILE_COLUMN_NAMES[j][0].Replace(" ", "") != cells[j].Replace(" ", ""))
                    {
                        ok = false;
                        break;
                    }
                }
                if (!ok)
                    continue;

                return encodings[i];
            }
            return null;
        }
        static public List<FileRowData> Parse(string filePath)
        {
            Console.WriteLine($"Parse file {filePath}");

            char splitter = ';';
            string line = "NULL";
            System.IO.StreamReader sr = null;

            try
            {
                sr = new System.IO.StreamReader(filePath, GetEncoding(filePath));

                // GET SITE CODE FROM FILE NAME
                FileInfo fa = new FileInfo(filePath);
                string siteCode = fa.Name.Split('.')[1];

                // READ & CHECK FILE HEADER

                line = sr.ReadLine();
                string[] cells = line.Split(splitter);

                if (_FILE_COLUMN_NAMES.Count != cells.Length - 2)
                    throw new Exception($"Количество столбцов файла отличается от заданного: [{cells.Length}] != [{_FILE_COLUMN_NAMES.Count}]");

                for (int i = 0; i < _FILE_COLUMN_NAMES.Count; i++)
                {
                    if (_FILE_COLUMN_NAMES[i][0].Replace(" ", "") != cells[i].Replace(" ", ""))
                        throw new Exception($"Заголовок столбца файла i = {i} отличается от формата:[{_FILE_COLUMN_NAMES[i]}] != [{cells[i]}]");
                }

                // READ DATA BODY

                List<FileRowData> ret = new List<FileRowData>();
                int iLineCount = 0;

                while (!sr.EndOfStream)
                {
                    iLineCount++;
                    line = sr.ReadLine().Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    cells = line.Split(splitter);
                    if (string.IsNullOrEmpty(cells[0])) continue;

                    FileRowData rowData = new FileRowData() { SiteCode = siteCode, Date = DateTime.Parse(cells[0], CultureInfo.CreateSpecificCulture("ru-RU")) };

                    for (int i = 1; i < _FILE_COLUMN_NAMES.Count; i++)
                    {
                        double value = double.NaN;

                        if (!string.IsNullOrEmpty(cells[i]))
                        {
                            if (!double.TryParse(cells[i].Replace(".", ","), out value))
                                throw new Exception($"Не удается преобразовать значение [{cells[i]}] в double в столбце [{_FILE_COLUMN_NAMES[i]}]");
                        }
                        rowData.Values[i - 1] = value;
                    }
                    ret.Add(rowData);
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
    }
}
