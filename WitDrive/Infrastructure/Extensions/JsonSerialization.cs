using MDBFS.Filesystem.Models;
using MDBFS.Misc;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WitDrive.Infrastructure.Extensions
{
    public static class JsonSerialization
    {
        public static JObject DirToJObject(this Element dir)
        {
            if (dir.Type != 2)
            {
                throw new Exception("Not a directory");
            }
            JObject jObject = new JObject();

            jObject[nameof(dir.ID)] = dir.ID;
            jObject[nameof(dir.ParentID)] = dir.ParentID;
            jObject[nameof(dir.Created)] = dir.ID;
            jObject[nameof(dir.Modified)] = dir.Modified;
            jObject[nameof(dir.Opened)] = dir.Opened;
            jObject[nameof(dir.Type)] = dir.Type;

            jObject["Shared"] = (bool)dir.CustomMetadata["Shared"];
            jObject["ShareID"] = (bool)dir.CustomMetadata["ShareID"];

            return jObject;
        }

        public static JObject FileToJObject(this Element file)
        {
            if (file.Type != 1)
            {
                throw new Exception("Not a file");
            }
            JObject jObject = new JObject();

            jObject[nameof(file.ID)] = file.ID;
            jObject[nameof(file.ParentID)] = file.ParentID;
            jObject[nameof(file.Created)] = file.ID;
            jObject[nameof(file.Modified)] = file.Modified;
            jObject[nameof(file.Opened)] = file.Opened;
            jObject[nameof(file.Type)] = file.Type;

            jObject["Shared"] = (bool)file.CustomMetadata["Shared"];
            jObject["ShareID"] = (string)file.CustomMetadata["ShareID"];
            jObject[nameof(EMatadataKeys.Length)] = (long)file.CustomMetadata[nameof(EMatadataKeys.Length)];

            if (file.Metadata.ContainsKey(nameof(EMatadataKeys.Deleted)))
            {
                jObject[nameof(EMatadataKeys.Deleted)] = (DateTime)file.Metadata[nameof(EMatadataKeys.Deleted)];
            }
            if (file.Metadata.ContainsKey(nameof(EMatadataKeys.PathIDs)))
            {
                jObject[nameof(EMatadataKeys.PathIDs)] = (DateTime)file.Metadata[nameof(EMatadataKeys.PathIDs)];
            }
            if (file.Metadata.ContainsKey(nameof(EMatadataKeys.PathNames)))
            {
                jObject[nameof(EMatadataKeys.Deleted)] = (DateTime)file.Metadata[nameof(EMatadataKeys.PathNames)];
            }

            return jObject;
        }

        public static JObject DirToJObject(this Element dir, Element[] subDirs)
        {
            JObject jObject = dir.DirToJObject();
            JArray dirs = new JArray();
            JArray files = new JArray();
            foreach (var item in subDirs)
            {
                if (item.Type == 2)
                {
                    dirs.Add(item.DirToJObject());
                }

                if (item.Type == 1)
                {
                    files.Add(item.FileToJObject());
                }
            }
            jObject["directories"] = dirs;

            jObject["files"] = files;
            return jObject;
        }

        public static JObject ElementToJObject(this Element element)
        {
            if (element.Type == 1)
            {
                return element.DirToJObject();
            }
            else if (element.Type == 2)
            {
                return element.FileToJObject();
            }
            else
            {
                throw new Exception("Not possible in this universe");
            }
        }

        public static string DirToJson(this Element dir) => dir.DirToJObject().ToString();
        public static string FileToJson(this Element file) => file.FileToJObject().ToString();
        public static string DirToJson(this Element dir, Element[] subDirs) => dir.DirToJObject(subDirs).ToString();
    }
}
