using System;
using System.Collections.Generic;

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
        Contains,
    }
    public class ElementSearchQuery
    {
        public ElementSearchQuery()
        {
            Opened = new List<(ESearchCondition condition, DateTime value)>();
            Created = new List<(ESearchCondition condition, DateTime value)>();
            Modified = new List<(ESearchCondition condition, DateTime value)>();
            Removed = new List<(ESearchCondition condition, bool value)>();
            Metadata = new List<(string fieldName, ESearchCondition condition, object value)>();
            CustomMetadata = new List<(string fieldName, ESearchCondition condition, object value)>();
            Name = new List<(ESearchCondition condition, string value)>();
        }

        // ReSharper disable once InconsistentNaming
        public string ID { get; set; }
        // ReSharper disable once InconsistentNaming
        public string ParentID { get; set; }
        public List<(ESearchCondition condition, string value)> Name { get; set; }
        public List<(ESearchCondition condition, DateTime value)> Opened { get; set; }
        public List<(ESearchCondition condition, DateTime value)> Created { get; set; }
        public List<(ESearchCondition condition, DateTime value)> Modified { get; set; }
        public List<(ESearchCondition condition, bool value)> Removed { get; set; }
        public List<(string fieldName,ESearchCondition condition,object value)> Metadata { get; set; }
        public List<(string fieldName,ESearchCondition condition,object value)> CustomMetadata { get; set; }
    }
}