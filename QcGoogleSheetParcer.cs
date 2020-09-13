﻿using PlayerAndEditorGUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;


namespace QuizCannersUtilities
{

#pragma warning disable IDE0034 // Simplify 'default' expression
#pragma warning disable IDE0044 // Add readonly modifier

    [Serializable]
    public class QcGoogleSheetParcer : IPEGI
    {
        [SerializeField] private string editUrl = "https://docs.google.com/spreadsheets/d/XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX/edit#gid=0";
        [SerializeField] private string url = "https://docs.google.com/spreadsheets/d/e/XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX/pub?";
        [SerializeField] public List<SheetPage> pages = new List<SheetPage>();


        [Serializable] 
        public class  SheetPage : IGotDisplayName, IPEGI_ListInspect
        {
            public string pageName;
            public int pageIndex;

            public bool InspectInList(IList list, int ind, ref int edited)
            {
                var changed = false;
                "Name".edit(40, ref pageName).changes(ref changed);
                "#gid=".edit(50, ref pageIndex).changes(ref changed);
                return changed;
            }

            public string NameForDisplayPEGI() => pageName;
        }


        [NonSerialized] private UnityWebRequest request;

        [NonSerialized] private CsvReader reader;

        [NonSerialized] public string jsonText;
        
        [NonSerialized] private JToken token;

        [NonSerialized] private EncodedJsonInspector jsonInspector;

        [NonSerialized] private JTokenInspector jTokenInspector = new JTokenInspector();

        public JToken GetJToken()
        {
            if (jsonText.IsNullOrEmpty())
                Read();

            if (token == null && !jsonText.IsNullOrEmpty())
                token = JToken.Parse(jsonText);

            return token;
        }

        public void To<K,V>(Dictionary<K, V> dic, Func<V,K> keyFactory)
        {
            var tmpList = JsonUtility.FromJson<List<V>>(jsonText);  //JsonConvert.DeserializeObject<List<V>>(jsonText);
            dic.Clear();
            
            foreach (var l in tmpList){
                var key = keyFactory(l);
                dic[key] = l;
            }
        }

        public void To<T>(List<T> list)
        {
            var tmpList = JsonUtility.FromJson<List<T>>(jsonText); //JsonConvert.DeserializeObject<List<T>>(jsonText);
            list.Clear();
            list.AddRange(tmpList);
        }

        public void StartDownload()
        {
            if (pages.IsNullOrEmptyCollection()) 
                Debug.LogError("No page assigned");
            else
                StartDownload(pages[0]);
        }

        public void StartDownload(SheetPage page)
        {
            jsonText = null;
            token = null;
            request = UnityWebRequest.Get("{0}gid={1}&single=true&output=csv".F(url, page.pageIndex.ToString()));
            request.SendWebRequest();
        }

        public bool IsDownloading() => request != null && !request.isDone && !request.isNetworkError;

        public IEnumerator DownloadingCoro()
        {
            while (IsDownloading())
            {
                yield return null;
            }
        }

        public void Read()
        {
            if (request != null && request.isDone)
            {
                reader = new CsvReader(request);

                jsonText = ToJson(reader);

                token = null;

                jTokenInspector = new JTokenInspector();

                jsonInspector = new EncodedJsonInspector(jsonText);

                request = null;
            }
            else
                Debug.LogError("Couldn't Read");

        }


        #region Inspector
        private int _inspectedStuff = -1;

        [SerializeField] private int _selectedPage = 0;
        
        public bool Inspect()
        {
            var changed = false;

            pegi.nl();

            if (_inspectedStuff == -1)
            {
                if (request == null)
                {
                    "Page:".select_Index(40, ref _selectedPage, pages);

                    if (pages.Count > _selectedPage && "Download".Click().nl())
                        StartDownload(pages[_selectedPage]);
                }
                else
                {
                    if ("Clear Request".Click())
                    {
                        request.Dispose();
                        request = null;
                    } else 

                    if (request.isDone)
                    {
                        "Download finished".nl();

                        if ("Read".Click().nl())
                            Read();
                    }
                    else
                    {
                        "Thread state: ".F(Mathf.FloorToInt(request.downloadProgress * 100).ToString()).nl();

                        if ("Cancel Trhread".Click().nl())
                            request.Dispose();

                    }
                }
            }

            pegi.nl();

            if ("Source".enter(ref _inspectedStuff, 0).nl())
            {
                "Sheet URL (to Edit))".edit(ref editUrl).changes(ref changed);

                if (_inspectedStuff < 1 && "Open".Click())
                    Application.OpenURL(editUrl);

                pegi.nl();

                "Published CSV Urls (to download)".edit(ref url).changes(ref changed);

                pegi.FullWindowService.DocumentationClickOpen(() =>
                    "GoogleSheet->File->Publish To Web-> Publish... Copy link for .csv document");

                pegi.nl();

                if (url != null)
                {
                    var ind = url.LastIndexOf("pub?");
                    if ((ind > 10 && ind < url.Length - 4) && "Clear Url Ending".Click().nl(ref changed))
                    {
                        url = url.Substring(startIndex: 0, length: ind + 4);
                    }
                }

                "Pages".edit_List(ref pages).nl(ref changed);
            }
            
            if ("Json".conditional_enter(jsonInspector != null, ref _inspectedStuff, 1).nl())
                jsonInspector.Nested_Inspect().nl(ref changed);

            if ("JToken".enter(ref _inspectedStuff, 2).nl())
            {
                var tok = GetJToken();

                if (tok == null)
                    "No Token data found".write();
                else
                    jTokenInspector.Inspect(tok).changes(ref changed);
            }

            return changed;
        }

        #endregion

        private string ToJson(CsvReader csv)
        {
            var result = new StringBuilder()
                .Append("[");

            for (var rowIndex = 0; rowIndex < csv.rows.Count; rowIndex++)
            {
                var row = csv.rows[rowIndex];
                if (rowIndex != 0 && rowIndex != csv.rows.Count)
                    result.Append(",");

                result.Append("{");

                for (var index = 0; index < row.Keys.Count; index++)
                {
                    var rowKey = row.Keys[index];

                    result.Append("\"")
                    .Append(rowKey)
                    .Append("\"")
                    .Append(":")
                    .Append("\"")
                    .Append(row[rowKey])
                    .Append("\"");

                    if (index < row.Keys.Count - 1)
                        result.Append(",");
                }

                result.Append("}");
            }

            result.Append("]");

            return result.ToString();
        }
        
        internal class Row
        {
            public OrderedDictionary data;

            public IList<string> Keys
            {
                get { return data.Keys.Cast<string>().ToList(); }
            }

            public string this[string i]
            {
                get { return data[i] as string; }
            }

            public string this[int i]
            {
                get { return data[i] as string; }
            }

            public Row(OrderedDictionary fill)
            {
                data = fill;
            }

            public int Count
            {
                get { return data.Count; }
            }
            
            public Row next = null;
            public Row prev = null;
        }

        internal class CsvCell
        {
            private readonly int x = 0;
            private readonly int y = 0;
            List<Row> table;

            public CsvCell(int _x, int _y, List<Row> _table)
            {
                x = _x;
                y = _y;
                table = _table;
            }

            public bool IsAttribute() => table[x].Keys[y].StartsWith("Attr", StringComparison.InvariantCultureIgnoreCase);
            
            public CsvCell GetOffset(int _x, int _y) => new CsvCell(x + _x, y + _y, table);
 
            public bool HasValue() => table != null && x < table.Count && y < table[x].Count && table[x][y] != "";
            
            public string GetValue()
            {
                try
                {
                    if (x < table.Count && y < table[x].Count)
                    {
                        return table[x][y];
                    }
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }

                return "";
            }
        }

        internal class CsvReader 
        {
            public List<string> columns;
            public List<Row> rows = new List<Row>();

            public CsvReader(UnityWebRequest content)
            {
                var lines = new StringReader(content.downloadHandler.text);
                using (lines)
                {
                    string line;
                    line = lines.ReadLine();
                    rows.Clear();
                    if (line != null)
                    {
                        columns = line.Split(',').Select(v => v.Trim()).ToList();
                        int rowIndex = 0;
                        while ((line = lines.ReadLine()) != null)
                        {
                            var rawCells = new List<string>();
                            try
                            {
                                bool good = true;
                                string accumulatedCell = "";
                                for (int i = 0; i < line.Length; i++)
                                {
                                    if (line[i] == ',' && good)
                                    {
                                        rawCells.Add(accumulatedCell.Trim());
                                        accumulatedCell = "";
                                    }
                                    else if (line.Length > (i + 1) && line[i] == '"' && line[i + 1] == '"')
                                    {
                                        accumulatedCell += '"';
                                        i++;
                                    }
                                    else if (line[i] == '"')
                                    {
                                        good = !good;
                                    }
                                    else
                                        accumulatedCell += line[i];
                                }

                                rawCells.Add(accumulatedCell);
                            }
                            catch (Exception e)
                            {
                                Debug.LogError(e.Message + e.StackTrace + ", at row " + rowIndex);
                            }

                            OrderedDictionary cells = new OrderedDictionary();
                            try
                            {
                                int indexCounter = 0;
                                foreach (string cell in rawCells)
                                {
                                    if (indexCounter < columns.Count)
                                        cells[columns[indexCounter]] = cell;
                                    indexCounter++;
                                }

                                rows.Add(new Row(cells));
                            }
                            catch (Exception e)
                            {
                                Debug.LogError(e.Message + e.StackTrace + ", at row " + rowIndex);
                            }

                            rowIndex++;
                        }

                        for (int i = 0; i < rows.Count; i++)
                        {
                            if (i > 0)
                            {
                                rows[i].prev = rows[i - 1];
                                rows[i - 1].next = rows[i];
                            }
                        }
                    }
                }
            }
        }

    }

}