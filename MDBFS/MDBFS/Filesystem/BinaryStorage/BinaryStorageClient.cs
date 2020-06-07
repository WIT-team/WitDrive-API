using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MDBFS.Exceptions;
using MDBFS.FileSystem.BinaryStorage.Models;
using MDBFS.FileSystem.BinaryStorage.Streams;
using MDBFS.Misc;
using MongoDB.Driver;

namespace MDBFS.FileSystem.BinaryStorage
{
    public class BinaryStorageClient
    {
        private readonly IMongoCollection<Chunk> _chunks;
        private readonly IMongoCollection<ChunkMap> _maps;
        private readonly int _bufforLength;
        private readonly int _maxChunkLenght;
        private readonly NamedReaderWriterLock _namedReaderWriterLock;

        public BinaryStorageClient(NamedReaderWriterLock namedReaderWriterLock, IMongoDatabase database,
            int bufforLength = 1024, int maxChunkLength = 1048576)
        {
            //set database

            //set collections
            _chunks = database.GetCollection<Chunk>(nameof(MDBFS) + '.' + nameof(Filesystem) + '.' +
                                                    nameof(BinaryStorageClient) + nameof(_chunks));
            _maps = database.GetCollection<ChunkMap>(nameof(MDBFS) + '.' + nameof(Filesystem) + '.' +
                                                     nameof(BinaryStorageClient) + nameof(_maps));

            _bufforLength = bufforLength;
            _maxChunkLenght = maxChunkLength;
            _namedReaderWriterLock = namedReaderWriterLock;

        }

        public List<ChunkMap> CleanUpErrors()
        {
            var searchDeleted = _maps.Find(x => x.Removed).ToList();
            foreach (var map in searchDeleted)
            {
                Remove(map.ID);
            }

            return searchDeleted;
        }
        public BinaryUploadStream OpenUploadStream()
        {
            var (success, stream) =
                BinaryUploadStream.Open(_maps, _chunks, _maxChunkLenght, null, _namedReaderWriterLock);
            return success ? stream : null;
        }

        public BinaryUploadStream OpenUploadStream(string id)
        {
            var (success, stream) =
                BinaryUploadStream.Open(_maps, _chunks, _maxChunkLenght, id, _namedReaderWriterLock);
            return success ? stream : throw new MdbfsDuplicateKeyException("Document with specified ID already exists");
        }

        public async Task<BinaryUploadStream> OpenUploadStreamAsync()
        {
            var (success, stream) =
                await BinaryUploadStream.OpenAsync(_maps, _chunks, _maxChunkLenght, null, _namedReaderWriterLock);
            return success ? stream : null;
        }

        public async Task<BinaryUploadStream> OpenUploadStreamAsync(string id)
        {
            var (success, stream) =
                await BinaryUploadStream.OpenAsync(_maps, _chunks, _maxChunkLenght, id, _namedReaderWriterLock);
            return success ? stream : throw new MdbfsDuplicateKeyException("Document with specified ID already exists");
        }

        public string  UploadFromStream(Stream stream)
        {
            string id;
            using var binS = OpenUploadStream();
            id = binS.Id;
            var buff = new byte[_bufforLength];
            var len = stream.Read(buff, 0, buff.Length);
            while (len > 0)
            {
                binS.Write(buff.SubArray(0, len), 0, len);
                len = stream.Read(buff, 0, buff.Length);
            }

            return id;
        }

        public async Task<(string id, long length)> UploadFromStreamAsync(Stream stream, bool streamSupportsAsync)
        {
            string id;
            long length = 0;
            await using (var binS = await OpenUploadStreamAsync())
            {
                int len;
                id = binS.Id;
                var buff = new byte[_bufforLength];
                if (streamSupportsAsync) len = await stream.ReadAsync(buff, 0, buff.Length);
                else len = await stream.ReadAsync(buff, 0, buff.Length);

                length += len;
                while (len > 0)
                {
                    await binS.WriteAsync(buff.SubArray(0, len), 0, len);
                    if (streamSupportsAsync) len = await stream.ReadAsync(buff, 0, buff.Length);
                    else len = await stream.ReadAsync(buff, 0, buff.Length);
                    length += len;
                }
            }

            return (id, length);
        }

        public string Upload(byte[] data)
        {
            string id;
            using var binS = OpenUploadStream();
            id = binS.Id;
            binS.Write(data, 0, data.Length);

            return id;
        }

        public async Task<string> UploadAsync(byte[] data)
        {
            await using var binS = await OpenUploadStreamAsync();
            var id = binS.Id;
            await binS.WriteAsync(data, 0, data.Length);

            return id;
        }

        public void UploadFromStream(Stream stream, string id)
        {
            using var binS = OpenUploadStream(id);
            var buff = new byte[_bufforLength];
            var len = stream.Read(buff, 0, buff.Length);
            while (len > 0)
            {
                binS.Write(buff.SubArray(0, len));
                len = stream.Read(buff, 0, buff.Length);
            }
        }

        public async Task UploadFromStreamAsync(Stream stream, string id)
        {
            await using var binS = await OpenUploadStreamAsync(id);
            var buff = new byte[_bufforLength];
            var len = await stream.ReadAsync(buff, 0, buff.Length);
            while (len > 0)
            {
                await binS.WriteAsync(buff.SubArray(0, len));
                len = await stream.ReadAsync(buff, 0, buff.Length);
            }
        }

        public void Upload(byte[] data, string id)
        {
            using var binS = OpenUploadStream(id);
            binS.Write(data, 0, data.Length);
        }

        public async Task UploadAsync(byte[] data, string id)
        {
            await using var binS = await OpenUploadStreamAsync(id);
            await binS.WriteAsync(data, 0, data.Length);
        }


        public BinaryDownloadStream OpenDownloadStream(string id)
        {
            var (success, stream) =
                BinaryDownloadStream.Open(_maps, _chunks, _maxChunkLenght, id, _namedReaderWriterLock);
            return success ? stream : null;
        }

        public async Task<BinaryDownloadStream> OpenDownloadStreamAsync(string id)
        {
            var (success, stream) =
                await BinaryDownloadStream.OpenAsync(_maps, _chunks, _maxChunkLenght, id, _namedReaderWriterLock);
            return success ? stream : null;
        }

        public void DownloadToStream(Stream stream, string id)
        {
            using var binD = OpenDownloadStream(id);
            var buff = new byte[_bufforLength];
            var len = binD.Read(buff, 0, buff.Length);
            while (len > 0)
            {
                stream.Write(buff, 0, buff.Length);
                len = binD.Read(buff, 0, buff.Length);
            }
        }

        public async Task DownloadToStreamAsync(Stream stream, bool streamSupportsAsync, string id)
        {
            await using var binD = await OpenDownloadStreamAsync(id);
            var buff = new byte[_bufforLength];
            var len = await binD.ReadAsync(buff, 0, buff.Length);
            while (len > 0)
            {
                if (streamSupportsAsync) await stream.WriteAsync(buff, 0, buff.Length);
                else await stream.WriteAsync(buff, 0, buff.Length);
                len = await binD.ReadAsync(buff, 0, buff.Length);
            }
        }

        public byte[] Download(string id)
        {
            byte[] buff;
            using var binD = OpenDownloadStream(id);
            buff = new byte[binD.Length];
            binD.Read(buff, 0, buff.Length);

            return buff;
        }

        public async Task<byte[]> DownloadAsync(string id)
        {
            byte[] buff;
            await using var binD = await OpenDownloadStreamAsync(id);
            buff = new byte[binD.Length];
            await binD.ReadAsync(buff, 0, buff.Length);

            return buff;
        }

        public string Duplicate(string id)
        {
            var mapSearch = _maps.Find(x => x.ID == id).ToList();
            if (!mapSearch.Any()) return null; //not found
            var map = mapSearch.First();
            var nMap = new ChunkMap {ChunksIDs = new List<string>(), Length = map.Length};
            var chunksSearch = _chunks.Find(Builders<Chunk>.Filter.Where(x => map.ChunksIDs.Contains(x.ID)));
            var nChunks = new List<Chunk>();
            foreach (var ch in chunksSearch.ToEnumerable()) nChunks.Add(new Chunk {Bytes = ch.Bytes});
            _chunks.InsertMany(nChunks);
            nChunks.ForEach(x => nMap.ChunksIDs.Add(x.ID));
            _maps.InsertOne(nMap);
            return nMap.ID;
        }

        internal async Task<string> DuplicateAsync(string id)
        {
            var mapSearch = (await _maps.FindAsync(x => x.ID == id)).ToList();
            if (!mapSearch.Any()) return null; //not found
            var map = mapSearch.First();
            var nMap = new ChunkMap {ChunksIDs = new List<string>(), Length = map.Length};
            var chunksSearch = await _chunks.FindAsync(Builders<Chunk>.Filter.Where(x => map.ChunksIDs.Contains(x.ID)));
            var nChunks = new List<Chunk>();

            foreach (var ch in chunksSearch.ToEnumerable()) nChunks.Add(new Chunk {Bytes = ch.Bytes});

            await _chunks.InsertManyAsync(nChunks);
            nChunks.ForEach(x => nMap.ChunksIDs.Add(x.ID));
            await _maps.InsertOneAsync(nMap);
            return nMap.ID;
        }

        public void Remove(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            _namedReaderWriterLock.AcquireReaderLock(id);
            var search = _maps.Find(x => x.ID == id).ToList();
            _namedReaderWriterLock.ReleaseReaderLock(id);
            if (search.Any())
            {
                _namedReaderWriterLock.AcquireWriterLock(id);
                _maps.UpdateOne(x => x.ID == id, Builders<ChunkMap>.Update.Set(x => x.Removed, true));
                var map = search.First();
                foreach (var chunkId in map.ChunksIDs)
                {
                    _chunks.DeleteOne(x => x.ID == chunkId);
                }

                _maps.DeleteOne(x => x.ID == id);
            }
            _namedReaderWriterLock.ReleaseWriterLock(id);
        }
        public async Task RemoveAsync(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            await _namedReaderWriterLock.AcquireReaderLockAsync(id);
            var search = _maps.Find(x => x.ID == id).ToList();
            await _namedReaderWriterLock.ReleaseReaderLockAsync(id);
            if (search.Any())
            {
                await _namedReaderWriterLock.AcquireWriterLockAsync(id);
                await _maps.UpdateOneAsync(x => x.ID == id, Builders<ChunkMap>.Update.Set(x => x.Removed, true));
                var map = search.First();
                foreach (var chunkId in map.ChunksIDs)
                {
                    await _chunks.DeleteOneAsync(x => x.ID == chunkId);
                }

                await _maps.DeleteOneAsync(x => x.ID == id);
            }
            _namedReaderWriterLock.ReleaseWriterLock(id);
        }
    }
}