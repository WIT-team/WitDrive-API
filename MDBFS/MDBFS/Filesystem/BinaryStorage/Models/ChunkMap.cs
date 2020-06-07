using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace MDBFS.FileSystem.BinaryStorage.Models
{
    public class ChunkMap
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        // ReSharper disable once InconsistentNaming
        public string ID { get; set; }
        public long Length { get; set; }
        public List<string> ChunksIDs { get; set; }
        public bool Removed { get; set; }

        public ChunkMap()
        {
            Removed = true;
        }
    }
}
