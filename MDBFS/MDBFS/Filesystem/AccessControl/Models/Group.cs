using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace MDBFS.Filesystem.AccessControl.Models
{
    public class Group
    {
#pragma warning disable IDE1006
        // ReSharper disable once InconsistentNaming
        // ReSharper disable once MemberCanBePrivate.Global
        protected string _id { get; set; }

#pragma warning restore IDE1006
        public List<string> Members { get; set; }
        [BsonId]
        public string Name
        {
            get => _id;
            set => _id = value;
        }
    }
}
