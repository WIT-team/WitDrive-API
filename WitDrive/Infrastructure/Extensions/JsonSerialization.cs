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
            jObject[nameof(dir.Created)] = dir.Created;
            jObject[nameof(dir.Modified)] = dir.Modified;
            jObject[nameof(dir.Opened)] = dir.Opened;
            jObject[nameof(dir.Type)] = dir.Type;
            jObject[nameof(dir.Name)] = dir.Name;

            if (dir.CustomMetadata.ContainsKey("Shared"))
            {
                jObject["Shared"] = (bool)dir.CustomMetadata["Shared"];
            }
            if (dir.CustomMetadata.ContainsKey("ShareID"))
            {
                jObject["ShareID"] = (string)dir.CustomMetadata["ShareID"];
            }

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
            jObject[nameof(file.Created)] = file.Created;
            jObject[nameof(file.Modified)] = file.Modified;
            jObject[nameof(file.Opened)] = file.Opened;
            jObject[nameof(file.Type)] = file.Type;
            jObject[nameof(file.Name)] = file.Name;

            if (file.CustomMetadata.ContainsKey("Shared"))
            {
                jObject["Shared"] = (bool)file.CustomMetadata["Shared"];
            }
            if (file.CustomMetadata.ContainsKey("ShareID"))
            {
                jObject["ShareID"] = (string)file.CustomMetadata["ShareID"];
            }
            if (file.Metadata.ContainsKey(nameof(EMetadataKeys.Length)))
            {
                jObject[nameof(EMetadataKeys.Length)] = (long)file.Metadata[nameof(EMetadataKeys.Length)];
            }
            if (file.Metadata.ContainsKey(nameof(EMetadataKeys.Deleted)))
            {
                jObject[nameof(EMetadataKeys.Deleted)] = (DateTime)file.Metadata[nameof(EMetadataKeys.Deleted)];
            }
            if (file.Metadata.ContainsKey(nameof(EMetadataKeys.PathIDs)))
            {
                jObject[nameof(EMetadataKeys.PathIDs)] = (DateTime)file.Metadata[nameof(EMetadataKeys.PathIDs)];
            }
            if (file.Metadata.ContainsKey(nameof(EMetadataKeys.PathNames)))
            {
                jObject[nameof(EMetadataKeys.Deleted)] = (DateTime)file.Metadata[nameof(EMetadataKeys.PathNames)];
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
            if (element.Type == 2)
            {
                return element.DirToJObject();
            }
            else if (element.Type == 1)
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
