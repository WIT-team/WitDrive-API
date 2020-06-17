using System;
using System.Collections.Generic;
using System.Linq;
using MDBFS.Misc;
using Newtonsoft.Json.Linq;

namespace MDBFS.Filesystem
{
    public enum ESearchCondition
    {
        Eq,
        Ne,
        Lt,
        Lte,
        Gt,
        Gte,

        //Reqex,
        Contains
    }

    public class ElementSearchQuery
    {
        // ReSharper disable once InconsistentNaming
        public string ID { get; set; }

        // ReSharper disable once InconsistentNaming
        public string ParentID { get; set; }
        public List<(ESearchCondition condition, string value)> Name { get; set; }
        public List<(ESearchCondition condition, DateTime value)> Opened { get; set; }
        public List<(ESearchCondition condition, DateTime value)> Created { get; set; }
        public List<(ESearchCondition condition, DateTime value)> Modified { get; set; }
        public List<(ESearchCondition condition, bool value)> Removed { get; set; }
        public List<(string fieldName, ESearchCondition condition, object value)> Metadata { get; set; }
        public List<(string fieldName, ESearchCondition condition, object value)> CustomMetadata { get; set; }

        public ElementSearchQuery()
        {
            ID = null;
            ParentID = null;
            Name = new List<(ESearchCondition condition, string value)>();
            Opened = new List<(ESearchCondition condition, DateTime value)>();
            Created = new List<(ESearchCondition condition, DateTime value)>();
            Modified = new List<(ESearchCondition condition, DateTime value)>();
            Removed = new List<(ESearchCondition condition, bool value)>();
            Metadata = new List<(string fieldName, ESearchCondition condition, object value)>();
            CustomMetadata = new List<(string fieldName, ESearchCondition condition, object value)>();
        }

        public string Serialize()
        {
            var res = new JObject();
            if (ID != null) res[nameof(ID)] = ID;
            if (ParentID != null) res[nameof(ParentID)] = ParentID;

            var jo = new JObject();
            if (Name.Any())
                foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                {
                    var jaInner = new JArray();
                    foreach (var (condition, value) in Name.Where(x => x.condition == sc).ToList()) jaInner.Add(value);
                    if (jaInner.Any()) jo[sc.ToString()] = jaInner;
                }

            res[nameof(Name)] = jo;
            jo = new JObject();

            if (Opened.Any())
            {
                foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                {
                    var jaInner = new JArray();
                    foreach (var (condition, value) in Opened.Where(x => x.condition == sc).ToList())
                        jaInner.Add(value);
                    if (jaInner.Any()) jo[sc.ToString()] = jaInner;
                }

                res[nameof(Opened)] = jo;
            }


            jo = new JObject();
            if (Created.Any())
            {
                foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                {
                    var jaInner = new JArray();
                    foreach (var (condition, value) in Created.Where(x => x.condition == sc).ToList())
                        jaInner.Add(value);
                    if (jaInner.Any()) jo[sc.ToString()] = jaInner;
                }

                res[nameof(Created)] = jo;
            }


            jo = new JObject();
            if (Modified.Any())
            {
                foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                {
                    var jaInner = new JArray();
                    foreach (var (condition, value) in Modified.Where(x => x.condition == sc).ToList())
                        jaInner.Add(value);
                    if (jaInner.Any()) jo[sc.ToString()] = jaInner;
                }

                res[nameof(Modified)] = jo;
            }


            jo = new JObject();
            if (Removed.Any())
            {
                foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                {
                    var jaInner = new JArray();
                    foreach (var (condition, value) in Removed.Where(x => x.condition == sc).ToList())
                        jaInner.Add(value);
                    if (jaInner.Any()) jo[sc.ToString()] = jaInner;
                }

                res[nameof(Removed)] = jo;
            }


            var a =
                new Dictionary<string, Dictionary<ESearchCondition, List<object>>>();


            if (Metadata.Any())
            {
                foreach (var (fieldName, condition, value) in Metadata)
                {
                    if (!a.ContainsKey(fieldName)) a[fieldName] = new Dictionary<ESearchCondition, List<object>>();
                    if (!a[fieldName].ContainsKey(condition)) a[fieldName][condition] = new List<object>();
                    a[fieldName][condition].Add(value);
                }

                var joFields = new JObject();
                foreach (var dict in a) //fields
                {
                    var joConditions = new JObject();
                    foreach (var dict2 in dict.Value) //conditions
                    {
                        var jaValues = new JArray();
                        foreach (var elem in dict2.Value) //values
                            jaValues.Add(new JValue(elem));

                        joConditions[dict2.Key.ToString()] = jaValues;
                    }

                    joFields[dict.Key] = joConditions;
                }

                res[nameof(Metadata)] = joFields;
            }


            if (CustomMetadata.Any())
            {
                a =
                    new Dictionary<string, Dictionary<ESearchCondition, List<object>>>();

                foreach (var (fieldName, condition, value) in CustomMetadata)
                {
                    if (!a.ContainsKey(fieldName)) a[fieldName] = new Dictionary<ESearchCondition, List<object>>();
                    if (!a[fieldName].ContainsKey(condition)) a[fieldName][condition] = new List<object>();
                    a[fieldName][condition].Add(value);
                }

                var joFields = new JObject();
                foreach (var dict in a) //fields
                {
                    var joConditions = new JObject();
                    foreach (var dict2 in dict.Value) //conditions
                    {
                        var jaValues = new JArray();
                        foreach (var elem in dict2.Value) //values
                            jaValues.Add(new JValue(elem));

                        joConditions[dict2.Key.ToString()] = jaValues;
                    }

                    joFields[dict.Key] = joConditions;
                }

                res[nameof(CustomMetadata)] = joFields;
            }

            return res.ToString();
        }

        public static ElementSearchQuery Deserialize(string json)
        {
            var jObj = JObject.Parse(json);
            var res = new ElementSearchQuery();
            if (jObj[nameof(ID)] != null) res.ID = jObj[nameof(ID)].ToObject<string>();
            if (jObj[nameof(ParentID)] != null) res.ParentID = jObj[nameof(ParentID)].ToObject<string>();
            if (jObj[nameof(Name)] != null)
            {
                var objName = jObj[nameof(Name)];
                foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                    if (objName[sc.ToString()] != null)
                        foreach (var el in objName[sc.ToString()])
                            res.Name.Add((sc, el.ToObject<string>()));
            }

            if (jObj[nameof(Opened)] != null)
            {
                var objName = jObj[nameof(Opened)];
                foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                {
                    if (objName[sc.ToString()] == null) continue;
                    foreach (var el in objName[sc.ToString()]) res.Opened.Add((sc, el.ToObject<DateTime>()));
                }
            }

            if (jObj[nameof(Modified)] != null)
            {
                var objName = jObj[nameof(Modified)];
                foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                {
                    if (objName[sc.ToString()] == null) continue;
                    foreach (var el in objName[sc.ToString()]) res.Modified.Add((sc, el.ToObject<DateTime>()));
                }
            }

            if (jObj[nameof(Created)] != null)
            {
                var objName = jObj[nameof(Created)];
                foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                {
                    if (objName[sc.ToString()] == null) continue;
                    foreach (var el in objName[sc.ToString()]) res.Created.Add((sc, el.ToObject<DateTime>()));
                }
            }

            if (jObj[nameof(Removed)] != null)
            {
                var objName = jObj[nameof(Removed)];
                foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                {
                    if (objName[sc.ToString()] == null) continue;
                    foreach (var el in objName[sc.ToString()]) res.Removed.Add((sc, el.ToObject<bool>()));
                }
            }

            if (jObj[nameof(Metadata)] != null)
            {
                var jMeta = jObj[nameof(Metadata)];
                if (jMeta[nameof(EMetadataKeys.Length)] != null)
                {
                    var jfield = jMeta[nameof(EMetadataKeys.Length)];
                    foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                    foreach (var val in jfield[sc.ToString()])
                        res.Metadata.Add((nameof(EMetadataKeys.Length), sc, val.ToObject<long>()));
                }

                if (jMeta[nameof(EMetadataKeys.PathNames)] != null)
                {
                    var jfield = jMeta[nameof(EMetadataKeys.PathNames)];
                    foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                    foreach (var val in jfield[sc.ToString()])
                        res.Metadata.Add((nameof(EMetadataKeys.PathNames), sc, val.ToObject<string>()));
                }

                if (jMeta[nameof(EMetadataKeys.PathIDs)] != null)
                {
                    var jfield = jMeta[nameof(EMetadataKeys.PathIDs)];
                    foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                    foreach (var val in jfield[sc.ToString()])
                        res.Metadata.Add((nameof(EMetadataKeys.PathIDs), sc, val.ToObject<string>()));
                }

                if (jMeta[nameof(EMetadataKeys.Deleted)] != null)
                {
                    var jfield = jMeta[nameof(EMetadataKeys.Deleted)];
                    foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                    foreach (var val in jfield[sc.ToString()])
                        res.Metadata.Add((nameof(EMetadataKeys.Deleted), sc, val.ToObject<DateTime>()));
                }
            }

            if (jObj[nameof(CustomMetadata)] != null)
            {
                var jMeta = jObj[nameof(CustomMetadata)];
                if (jMeta["Shared"] != null)
                {
                    var jfield = jMeta["Shared"];
                    foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                    foreach (var val in jfield[sc.ToString()])
                        res.CustomMetadata.Add(("Shared", sc, val.ToObject<bool>()));
                }

                if (jMeta["ShareID"] != null)
                {
                    var jfield = jMeta["ShareID"];
                    foreach (var sc in (ESearchCondition[]) Enum.GetValues(typeof(ESearchCondition)))
                    foreach (var val in jfield[sc.ToString()])
                        res.CustomMetadata.Add(("ShareID", sc, val.ToObject<string>()));
                }
            }

            return res;
        }
    }
}