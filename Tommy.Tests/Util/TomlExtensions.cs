﻿using System.Text;
using SimpleJSON;

namespace Tommy.Tests.Util
{
    public static class TomlExtensions
    {
        /// <summary>
        /// Formats TOML Node to JSON Encoding format used by toml-lang/compliance
        /// see
        /// https://github.com/toml-lang/compliance/blob/master/docs/json-encoding.md
        /// </summary>
        /// <param name="node">Node to parse</param>
        /// <returns>JSON representation of the TOML node</returns>
        public static string ToComplianceTestJson(this TomlNode node)
        {
            var obj = new JSONObject();
            Traverse(obj, node);
            return obj.ToString();
        }

        private static string EscapeJson(this string txt)
        {
            var stringBuilder = new StringBuilder(txt.Length + 2);
            for (var i = 0; i < txt.Length; i++)
            {
                var c = txt[i];

                static string CodePoint(string txt, ref int i, char c) => char.IsSurrogatePair(txt, i)
                    ? $"\\U{char.ConvertToUtf32(txt, i++):X8}"
                    : $"\\u{(ushort) c:X4}";

                stringBuilder.Append(c switch
                {
                    '\b'  => @"\b",
                    '\n'  => @"\n",
                    '\f'  => @"\f",
                    '\r'  => @"\r",
                    '\\'  => @"\",
                    '\t'  => @"\t",
                    var _ => c
                });
            }

            return stringBuilder.ToString();
        }

        private static void Traverse(JSONNode obj, TomlNode node, string nodeKey = null, bool isChild = false)
        {
            static void Add(JSONNode obj, string k, JSONNode add)
            {
                if (k != null)
                    obj.Add(k, add);
                else
                    obj.Add(add);
            }

            // Normal table, add it to the root
            if (node is TomlTable tbl && isChild)
            {
                var jsonObj = new JSONObject();
                Add(obj, nodeKey, jsonObj);
                Traverse(jsonObj, tbl);
                return;
            }

            if (node.HasValue)
            {
                switch (node)
                {
                    case TomlString str:
                        Add(obj, nodeKey, new JSONObject {["type"] = "string", ["value"] = str.Value.EscapeJson()});
                        break;
                    case TomlInteger i:
                        Add(obj, nodeKey, new JSONObject {["type"] = "integer", ["value"] = i.Value.ToString()});
                        break;
                    case TomlFloat f:
                        Add(obj, nodeKey, new JSONObject {["type"] = "float", ["value"] = f.ToInlineToml()});
                        break;
                    case TomlDateTimeLocal dtl:
                        Add(obj,
                            nodeKey,
                            new JSONObject
                            {
                                ["type"] = "local " +
                                           dtl switch
                                           {
                                               var _ when dtl.OnlyDate => "date",
                                               var _ when dtl.OnlyTime => "time",
                                               var _                   => "datetime"
                                           },
                                ["value"] = dtl.ToInlineToml()
                            });
                        break;
                    case TomlDateTimeOffset dto:
                        Add(obj,
                            nodeKey,
                            new JSONObject {["type"] = "offset datetime", ["value"] = dto.ToInlineToml()});
                        break;
                    case TomlBoolean b:
                        Add(obj, nodeKey, new JSONObject {["type"] = "boolean", ["value"] = b.ToInlineToml()});
                        break;
                    case TomlArray arr:
                        var tomlObj = new JSONObject {["type"] = "array", ["value"] = new JSONArray()};
                        var jsonArray = tomlObj["value"].AsArray;
                        Add(obj, nodeKey, tomlObj);
                        foreach (var arrValue in arr.Children)
                            Traverse(jsonArray, arrValue, isChild: true);
                        break;
                }

                return;
            }

            foreach (var key in node.Keys)
                Traverse(obj, node[key], key, true);
        }
    }
}