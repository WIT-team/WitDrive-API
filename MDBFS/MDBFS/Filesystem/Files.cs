﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MDBFS.Exceptions;
using MDBFS.Filesystem.AccessControl.Models;
using MDBFS.FileSystem.BinaryStorage;
using MDBFS.Filesystem.Models;
using MDBFS.Filesystem.Streams;
using MDBFS.Misc;
using MongoDB.Driver;

namespace MDBFS.Filesystem
{
    public class Files
    {
        private readonly IMongoCollection<Element> _elements;
        private readonly BinaryStorageClient _binaryStorage;

        public Files(IMongoCollection<Element> elements, int binaryStorageBufferLength = 1024, int chunkSize = 1048576)
        {
            _elements = elements;
            var rwLock = new NamedReaderWriterLock();
            _binaryStorage = new BinaryStorageClient(rwLock, elements.Database, binaryStorageBufferLength, chunkSize);
            var tmp0 = _binaryStorage.CleanUpErrors();
            foreach (var map in tmp0) _elements.DeleteOne(x => x.ID == map.ID);
        }

        private Element PrepareElement(string parentId, string name)
        {
            // ReSharper disable once AccessToModifiedClosure
            var nDupSearch = _elements.Find(x => x.ParentID == parentId && x.Name == name).ToList();
            string validName = null;
            var count = 0;
            while (nDupSearch.Any())
            {
                validName = $"{name}({count})";
                // ReSharper disable once AccessToModifiedClosure
                nDupSearch = _elements.Find(x => x.ParentID == parentId && x.Name == validName).ToList();
                count++;
            }

            if (validName != null) name = validName;
            var date = DateTime.Now;
            var elem = new Element
            {
                ParentID = parentId,
                Type = 1,
                Name = name,
                Created = date,
                Modified = date,
                Opened = date,
                Removed = false,
                Metadata = new Dictionary<string, object>()
            };
            return elem;
        }

        public Element Create(string parentId, string name, byte[] data)
        {
            var elemSearch = _elements.Find(x => x.ID == parentId && x.Removed == false).ToList();
            if (elemSearch.Count == 0) return null; //parent does not exist
            var elem = PrepareElement(parentId, name);
            using (var stream = _binaryStorage.OpenUploadStream())
            {
                elem.ID = stream.Id;
                stream.Write(data, 0, data.Length);
                stream.Flush();
                elem.Metadata.Add(nameof(EMatadataKeys.Length), stream.Length);
            }

            _elements.InsertOne(elem);
            return elem;
        }

        public async Task<Element> CreateAsync(string parentId, string name, byte[] data)
        {
            var elemSearch =(await  _elements.FindAsync(x => x.ID == parentId && x.Removed == false)).ToList();
            if (elemSearch.Count == 0) return null; //parent does not exist
            var elem =await PrepareElementAsync(parentId, name);
            await using (var stream = await _binaryStorage.OpenUploadStreamAsync())
            {
                elem.ID = stream.Id;
                await stream.WriteAsync(data, 0, data.Length);
                elem.Metadata.Add(nameof(EMatadataKeys.Length), stream.Length);
            }

            await _elements.InsertOneAsync(elem);
            return elem;
        }

        private async Task<Element> PrepareElementAsync(string parentId, string name)
        {
            // ReSharper disable once AccessToModifiedClosure
            var nDupSearch =(await  _elements.FindAsync(x => x.ParentID == parentId && x.Name == name)).ToList();
            string validName = null;
            var count = 0;
            while (nDupSearch.Any())
            {
                validName = $"{name}({count})";
                // ReSharper disable once AccessToModifiedClosure
                nDupSearch =(await _elements.FindAsync(x => x.ParentID == parentId && x.Name == validName)).ToList();
                count++;
            }

            if (validName != null) name = validName;
            var date = DateTime.Now;
            var elem = new Element
            {
                ParentID = parentId,
                Type = 1,
                Name = name,
                Created = date,
                Modified = date,
                Opened = date,
                Removed = false,
                Metadata = new Dictionary<string, object>()
            };
            return elem;
        }

        public Element Create(string parentId, string name, Stream stream)
        {
            var elemSearch = _elements.Find(x => x.ID == parentId && x.Removed == false).ToList();
            if (elemSearch.Count == 0) return null; //parent does not exist
            var elem = PrepareElement(parentId, name);
            var id = _binaryStorage.UploadFromStream(stream);
            elem.ID = id;
            elem.Metadata.Add(nameof(EMatadataKeys.Length), stream.Length);
            _elements.InsertOne(elem);
            return elem;
        }

        public async Task<Element> CreateAsync(string parentId, string name, Stream stream, bool streamSupportsAsync)
        {
            var elemSearch =(await  _elements.FindAsync(x => x.ID == parentId && x.Removed == false)).ToList();
            if (!elemSearch.Any()) return null; //parent does not exist
            var elem = await PrepareElementAsync(parentId, name);
            var (id, _) = await _binaryStorage.UploadFromStreamAsync(stream, streamSupportsAsync);
            elem.ID = id;
            elem.Metadata.Add(nameof(EMatadataKeys.Length), stream.Length);
            await _elements.InsertOneAsync(elem);
            return elem;
        }

        public FileUploadStream OpenFileUploadStream(string parentId, string name)
        {
            return new FileUploadStream(_binaryStorage.OpenUploadStream(), _elements,
                Element.Create(null, parentId, 1, name,
                    new Dictionary<string, object> {{nameof(EMatadataKeys.Length), 0L}}, null));
        }

        public async Task<FileUploadStream> OpenFileUploadStreamAsync(string parentId, string name)
        {
            return new FileUploadStream(await _binaryStorage.OpenUploadStreamAsync(), _elements,
                Element.Create(null, parentId, 1, name,
                    new Dictionary<string, object> {{nameof(EMatadataKeys.Length), 0L}}, null));
        }

        public Element Get(string id)
        {
            var elemSearch = _elements.Find(x => x.ID == id).ToList();
            return !elemSearch.Any() ? null : elemSearch.First();
        }

        public async Task<Element> GetAsync(string id)
        {
            var elemSearch = (await _elements.FindAsync(x => x.ID == id)).ToList();
            return elemSearch.Count == 0 ? null : elemSearch.First();
        }

        public FileDownloadStream OpenFileDownloadStream(string id)
        {
            return new FileDownloadStream(_binaryStorage.OpenDownloadStream(id), _elements, id);
        }

        public async Task<FileDownloadStream> OpenFileDownloadStreamAsync(string id)
        {
            return new FileDownloadStream(await _binaryStorage.OpenDownloadStreamAsync(id), _elements, id);
        }

        public Element Remove(string id, bool permanently)
        {
            Element f = null;
            if (permanently)
            {
                _elements.FindOneAndDelete(x => x.ID == id);
            }
            else
            {
                var elemSearch = _elements.Find(x => x.ID == id).ToList();
                if (elemSearch.Count != 0)
                {
                    var e = f = elemSearch.First();
                    var originalLocationNames = "";
                    var originalLocationIDs = "";
                    var deleted = DateTime.Now;
                    do
                    {
                        // ReSharper disable once AccessToModifiedClosure
                        var parentSearch = _elements.Find(x => x.ID == e.ParentID).ToList();
                        if (!parentSearch.Any())
                            throw new MdbfsElementNotFoundException("Parent element missing");
                        e = parentSearch.First();
                        originalLocationNames = e.Name + '/' + originalLocationNames;
                        originalLocationIDs = e.ID + '/' + originalLocationIDs;
                    } while (e.ParentID != null);

                    f.Opened = deleted;
                    f.Modified = deleted;
                    f.Removed = true;
                    f.Metadata[nameof(EMatadataKeys.PathNames)] = originalLocationNames;
                    f.Metadata[nameof(EMatadataKeys.PathIDs)] = originalLocationIDs;
                    f.Metadata[nameof(EMatadataKeys.Deleted)] = deleted;
                    _elements.FindOneAndReplace(x => x.ID == id, f);
                }
            }

            return f;
        }

        public async Task<Element> RemoveAsync(string id, bool permanently)
        {
            Element f = null;
            try
            {
                if (permanently)
                {
                    await _elements.FindOneAndDeleteAsync(x => x.ID == id);
                }
                else
                {
                    var elemSearch = (await _elements.FindAsync(x => x.ID == id)).ToList();
                    if (elemSearch.Count != 0) //element found
                    {
                        var e = f = elemSearch.First();
                        var originalLocationNames = "";
                        var originalLocationIDs = "";
                        var deleted = DateTime.Now;
                        do
                        {
                            var parentSearch = (await _elements.FindAsync(x => x.ID == e.ParentID)).ToList();
                            if (!parentSearch.Any())
                                throw new MdbfsElementNotFoundException(nameof(id));
                            e = parentSearch.First();
                            originalLocationNames = e.Name + '/' + originalLocationNames;
                            originalLocationIDs = e.ID + '/' + originalLocationIDs;
                        } while (e.ParentID != null);

                        f.Opened = deleted;
                        f.Modified = deleted;
                        f.Removed = true;
                        f.Metadata[nameof(EMatadataKeys.PathNames)] = originalLocationNames;
                        f.Metadata[nameof(EMatadataKeys.PathIDs)] = originalLocationIDs;
                        f.Metadata[nameof(EMatadataKeys.Deleted)] = deleted;
                        await _elements.FindOneAndReplaceAsync(x => x.ID == id, f);
                    }
                }
            }
            catch (Exception)
            {
                //throw;
            }

            return f;
        }

        public Element Restore(string id)
        {
            var elemSearch = _elements.Find(x => x.ID == id && x.Removed).ToList();
            if (elemSearch.Count == 0) return null; //element not found
            var element = elemSearch.First();

            var alterSearch = _elements.Find(x =>
                x.ParentID == element.ParentID && x.Name == element.Name && x.Removed == false).ToList();
            if (alterSearch.Any())
                element.Name = $"{element.Name}_restored_{DateTime.Now:yyyy_MM_dd_H:mm:ss:fff}";

            var originalLocationNames =
                (string) element.Metadata[nameof(EMatadataKeys.PathNames)];
            var originalLocationIDs = (string) element.Metadata[nameof(EMatadataKeys.PathIDs)];
            var names = originalLocationNames.Trim().Split('/');
            var ids = originalLocationIDs.Trim().Split('/');
            var pId = "";
            for (var itD = 1; itD < names.Length - 1; itD++)
            {
                // ReSharper disable once AccessToModifiedClosure
                var parElemSearch = _elements.Find(x => x.ID == ids[itD]).ToList();
                if (!parElemSearch.Any())
                {
                    // ReSharper disable once AccessToModifiedClosure
                    var d = itD;
                    var searchAlter = _elements.Find(x => x.ParentID == ids[d - 1] && x.Name == names[d])
                        .ToList();
                    if (!searchAlter.Any())
                    {
                        var date = DateTime.Now;
                        var elem = new Element
                        {
                            ParentID = ids[itD - 1],
                            Type = 2,
                            Name = names[itD],
                            Created = date,
                            Modified = date,
                            Opened = date,
                            Removed = false
                        };
                        _elements.InsertOne(elem);
                        if (itD == 1) pId = elem.ID;
                    }
                    else
                    {
                        ids[itD] = searchAlter.First().ID;
                    }
                }
            }

            element.Removed = false;
            if (pId != "") element.ParentID = pId;
            element.Metadata.Remove(nameof(EMatadataKeys.PathNames));
            element.Metadata.Remove(nameof(EMatadataKeys.PathIDs));
            element.Metadata.Remove(nameof(EMatadataKeys.Deleted));
            _elements.FindOneAndReplace(x => x.ID == id, element);

            return element;
        }

        public async Task<Element> RestoreAsync(string id)
        {
            Element f = null;
            try
            {
                var elemSearch = (await _elements.FindAsync(x => x.ID == id)).ToList();
                if (elemSearch.Count == 0) return null; //element not found
                f = elemSearch.First();
                if (f.Removed == false) return null; // element is not removed

                var originalLocationNames = (string) f.Metadata[nameof(EMatadataKeys.PathNames)];
                var originalLocationIDs = (string) f.Metadata[nameof(EMatadataKeys.PathIDs)];
                var names = originalLocationNames.Trim().Split('/');
                var ids = originalLocationIDs.Trim().Split('/');
                var pId = "";
                for (var itD = 1; itD < names.Length - 1; itD++)
                {
                    var parElemSearch = (await _elements.FindAsync(x => x.ID == ids[itD])).ToList();
                    if (parElemSearch.Any()) continue;
                    var searchAlter =
                        (await _elements.FindAsync(x => x.ParentID == ids[itD - 1] && x.Name == names[itD])).ToList();
                    if (!searchAlter.Any())
                    {
                        var date = DateTime.Now;
                        var elem = new Element
                        {
                            ParentID = ids[itD - 1],
                            Type = 2,
                            Name = names[itD],
                            Created = date,
                            Modified = date,
                            Opened = date,
                            Removed = false
                        };
                        await _elements.InsertOneAsync(elem);
                        if (itD == 1) pId = elem.ID;
                    }
                    else
                    {
                        ids[itD] = searchAlter.First().ID;
                    }
                }

                f.Removed = false;
                if (pId != "") f.ParentID = pId;
                f.Metadata.Remove(nameof(EMatadataKeys.PathNames));
                f.Metadata.Remove(nameof(EMatadataKeys.PathIDs));
                f.Metadata.Remove(nameof(EMatadataKeys.Deleted));
                await _elements.FindOneAndReplaceAsync(x => x.ID == id, f);
            }
            catch (Exception)
            {
                //throw;
            }

            return f;
        }

        public Element Copy(string id, string parentId)
        {
            if (!_elements.Find(x => x.ID == parentId && x.Removed == false).Any()) return null; //parent not found
            var eleSearch = _elements.Find(x => x.ID == id && x.Removed == false).ToList();
            if (eleSearch.Count == 0) return null; //element not found
            var mElem = eleSearch.First();

            _elements.UpdateOne(x => x.ID == id, Builders<Element>.Update.Set(x => x.Opened, DateTime.Now));

            var nId = _binaryStorage.Duplicate(id);
            if (nId == null) return null;
            var date = DateTime.Now;
            var nName = mElem.Name;

            // ReSharper disable once AccessToModifiedClosure
            if (_elements.Find(x => x.ParentID == parentId && x.Name == nName).Any())
            {
                long counter = 0;
                // ReSharper disable once AccessToModifiedClosure
                if (_elements.Find(x => x.ParentID == parentId && x.Name == $"{nName}_Copy").Any())
                    // ReSharper disable once AccessToModifiedClosure
                    while (_elements.Find(x => x.ParentID == parentId && x.Name == nName).Any())
                    {
                        var nName2 = $"{nName}_Copy({counter})";
                        if (!_elements.Find(x => x.ParentID == parentId && x.Name == nName2).Any())
                        {
                            nName = nName2;
                            break;
                        }

                        counter++;
                    }
                else
                    nName = $"{nName}_Copy";
            }

            var f = new Element
            {
                ID = nId,
                ParentID = parentId,
                Type = 1,
                Name = nName,
                Created = date,
                Modified = date,
                Opened = date,
                Removed = false,
                Metadata = mElem.Metadata
            };
            _elements.InsertOne(f);
            return f;
        }

        public async Task<Element> CopyAsync(string id, string parentId)
        {
            if (!await (await _elements.FindAsync(x => x.ID == parentId && x.Removed == false)).AnyAsync()) return null; //parent not found
            var eleSearch = (await _elements.FindAsync(x => x.ID == id && x.Removed == false)).ToList();
            if (!eleSearch.Any()) return null; //element not found
            var mElem = eleSearch.First();

            await _elements.UpdateOneAsync(x => x.ID == id, Builders<Element>.Update.Set(x => x.Opened, DateTime.Now));

            var nId = await _binaryStorage.DuplicateAsync(id);
            if (nId == null) return null;
            var date = DateTime.Now;

            var nName = mElem.Name;

            if (await (await _elements.FindAsync(x => x.ParentID == parentId && x.Name == nName)).AnyAsync())
            {
                long counter = 0;
                if (await (await _elements.FindAsync(x => x.ParentID == parentId && x.Name == $"{nName}_Copy")).AnyAsync())
                    while (await (await _elements.FindAsync(x => x.ParentID == parentId && x.Name == nName)).AnyAsync())
                    {
                        var nName2 = $"{nName}_Copy({counter})";
                        if (!await (await _elements.FindAsync(x => x.ParentID == parentId && x.Name == nName2)).AnyAsync())
                        {
                            nName = nName2;
                            break;
                        }

                        counter++;
                    }
                else
                    nName = $"{nName}_Copy";
            }

            var f = new Element
            {
                ID = nId,
                ParentID = parentId,
                Type = 1,
                Name = nName,
                Created = date,
                Modified = date,
                Opened = date,
                Removed = false,
                Metadata = mElem.Metadata
            };
            await _elements.InsertOneAsync(f);
            return f;
        }

        public Element Move(string id, string nParentId)
        {
            var parSearch = _elements.Find(x => x.ID == nParentId && x.Removed == false).ToList();
            if (!parSearch.Any()) return null; //parent not found

            var elemSearch = _elements.Find(x => x.ID == id && x.Removed == false);
            if (!elemSearch.Any()) return null; //element not found

            var f = elemSearch.First();
            f.Opened = f.Modified = DateTime.Now;
            f.ParentID = nParentId;
            _elements.FindOneAndReplace(x => x.ID == id, f);
            return f;
        }

        public async Task<Element> MoveAsync(string id, string nParentId)
        {
            var parSearch = (await _elements.FindAsync(x => x.ID == nParentId && x.Removed == false)).ToList();
            if (!parSearch.Any()) return null; //parent not found

            var elemSearch = (await _elements.FindAsync(x => x.ID == id && x.Removed == false)).ToList();
            if (!elemSearch.Any()) return null; //element not found

            var f = elemSearch.First();
            f.Opened = f.Modified = DateTime.Now;
            f.ParentID = nParentId;
            await _elements.FindOneAndReplaceAsync(x => x.ID == id, f);
            return f;
        }

        public Element Rename(string id, string newName)
        {
            var search = _elements.Find(x => x.ID == id && x.Removed == false).ToList();
            if (!search.Any()) return null;
            var elem = search.First();
            elem.Name = newName;
            _elements.UpdateOne(x => x.ID == id, Builders<Element>.Update.Set(x => x.Name, newName));
            return elem;
        }

        public Element SetCustomMetadata(string id, string fieldName, object fieldValue)
        {
            var search = _elements.Find(x => x.ID == id).ToList();
            if (!search.Any()) return null;
            _elements.UpdateOne(x => x.ID == id,
                Builders<Element>.Update.Set(x => x.CustomMetadata[fieldName], fieldValue));
            List<Element> search2;
            return (search2 = _elements.Find(x => x.ID == id).ToList()).Any() ? search2.First() : null;
        }

        public Element RemoveCustomMetadata(string id, string fieldName)
        {
            var search = _elements.Find(x => x.ID == id).ToList();
            if (!search.Any()) return null;
            _elements.UpdateOne(x => x.ID == id,
                Builders<Element>.Update.PullFilter(x => x.CustomMetadata,x=>x.Key==fieldName));
            List<Element> search2;
            return (search2 = _elements.Find(x => x.ID == id).ToList()).Any() ? search2.First() : null;
        }
       
        public async  Task<Element> RenameAsync(string id, string newName)
        {
            var search = (await _elements.FindAsync(x => x.ID == id && x.Removed == false)).ToList();
            if (!search.Any()) return null;
            var elem = search.First();
            elem.Name = newName;
            await _elements.UpdateOneAsync(x => x.ID == id, Builders<Element>.Update.Set(x => x.Name, newName));
            return elem;
        }

        public async Task<Element> SetCustomMetadataAsync(string id, string fieldName, object fieldValue)
        {
            var search = _elements.Find(x => x.ID == id).ToList();
            if (!search.Any()) return null;
            await _elements.UpdateOneAsync(x => x.ID == id,
                Builders<Element>.Update.Set(x => x.CustomMetadata[fieldName], fieldValue));
            List<Element> search2;
            return (search2 =(await _elements.FindAsync(x => x.ID == id)).ToList()).Any() ? search2.First() : null;
        }

        public async Task<Element> RemoveCustomMetadataAsync(string id, string fieldName)
        {
            var search = (await _elements.FindAsync(x => x.ID == id)).ToList();
            if (!search.Any()) return null;
            await _elements.UpdateOneAsync(x => x.ID == id,
                Builders<Element>.Update.PullFilter(x => x.CustomMetadata, x => x.Key == fieldName));
            List<Element> search2;
            return (search2 = (await _elements.FindAsync(x => x.ID == id)).ToList()).Any() ? search2.First() : null;
        }
    }
}