using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace MDBFS.Filesystem.AccessControl.Models
{
    public enum EUserRole
    {
        User,
        Admin,
    }
    public class User
    {
        public User()
        {
            MemberOf = new List<string>();
            Permissions = new List<string>();
        }

#pragma warning disable IDE1006
        // ReSharper disable once InconsistentNaming
        protected string _id { get; set; }

#pragma warning restore IDE1006
        [BsonId]
        public string Username
        {
            get => _id;
            set => _id = value;
        }
        public string RootDirectory { get; set; }
        public EUserRole Role { get; set; }
        public List<string> MemberOf { get; set; }
        public List<string> Permissions { get; set; }
    }
}
