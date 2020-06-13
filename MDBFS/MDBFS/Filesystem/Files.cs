﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MDBFS.Exceptions;
using MDBFS.Filesystem.AccessControl.Models;
using MDBFS.FileSystem.BinaryStorage;
using MDBFS.FileSystem.BinaryStorage.Models;
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
        private NamedReaderWriterLock _nrwl;
        
        public Files(IMongoCollection<Element> elements, int binaryStorageBufferLength = 1024, int chunkSize = 1048576)
        {
            _elements = elements;
            _nrwl = new NamedReaderWriterLock();
            _binaryStorage = new BinaryStorageClient(_nrwl, elements.Database, binaryStorageBufferLength, chunkSize);
            var tmp0 = _binaryStorage.CleanUpErrors();
            foreach (var map in tmp0) _elements.DeleteOne(x => x.ID == map.ID);
        }

        public static bool IsNameValid(string name)
        {
            return (!name.Contains('/') && name.Length > 0);
        }
        private Element PrepareElement(string parentId, string name)
        {
            // ReSharper disable once AccessToModifiedClosure
            var nDupSearch = _elements.Find(x => x.ParentID == parentId && x.Name == name).ToList();
            string validName = null;
            var count = 0;
            var tmp0 = name.Contains('.')?  name.Substring(0, name.LastIndexOf('.')):"";
            var tmp1 = name.Contains('.') ? name.Substring(name.LastIndexOf('.'), name.Length - name.LastIndexOf('.')):"";
            while (nDupSearch.Any())
            {
                validName = name.Contains('.') ? $"{tmp0}({count}){tmp1}" : $"{name}({count})";
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
            if(!IsNameValid(name)) throw new MdbfsInvalidNameException();
            var elemSearch = _elements.Find(x => x.ID == parentId && x.Removed == false).ToList();
            if (elemSearch.Count == 0) throw new MdbfsElementNotFoundException(); //parent does not exist
            var elem = PrepareElement(parentId, name);
            using (var stream = _binaryStorage.OpenUploadStream())
            {
                elem.ID = stream.Id;
                stream.Write(data, 0, data.Length);
                stream.Flush();
                elem.Metadata.Add(nameof(EMetadataKeys.Length), stream.Length);
            }

            _elements.InsertOne(elem);
            return elem;
        }

        public async Task<Element> CreateAsync(string parentId, string name, byte[] data)
        {
            if (!IsNameValid(name)) throw new MdbfsInvalidNameException();
            var elemSearch = (await _elements.FindAsync(x => x.ID == parentId && x.Removed == false)).ToList();
            if (elemSearch.Count == 0) throw new MdbfsElementNotFoundException(); //parent does not exist
            var elem = await PrepareElementAsync(parentId, name);
            await using (var stream = await _binaryStorage.OpenUploadStreamAsync())
            {
                elem.ID = stream.Id;
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
                elem.Metadata.Add(nameof(EMetadataKeys.Length), stream.Length);
            }

            await _elements.InsertOneAsync(elem);
            return elem;
        }

        private async Task<Element> PrepareElementAsync(string parentId, string name)
        {
            if (!IsNameValid(name)) throw new MdbfsInvalidNameException();
            // ReSharper disable once AccessToModifiedClosure
            var nDupSearch = (await _elements.FindAsync(x => x.ParentID == parentId && x.Name == name)).ToList();
            string validName = null;
            var count = 0;
            var tmp0 = name.Contains('.') ? name.Substring(0, name.LastIndexOf('.')) : "";
            var tmp1 = name.Contains('.') ? name.Substring(name.LastIndexOf('.'), name.Length - name.LastIndexOf('.')) : "";
            while (nDupSearch.Any())
            {
                validName = name.Contains('.') ? $"{tmp0}({count}){tmp1}" : $"{name}({count})";
                // ReSharper disable once AccessToModifiedClosure
                nDupSearch = (await _elements.FindAsync(x => x.ParentID == parentId && x.Name == validName)).ToList();
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
            if (!IsNameValid(name)) throw new MdbfsInvalidNameException();
            var elemSearch = _elements.Find(x => x.ID == parentId && x.Removed == false).ToList();
            if (elemSearch.Count == 0) throw new MdbfsElementNotFoundException(); //parent does not exist
            var elem = PrepareElement(parentId, name);
            var id = _binaryStorage.UploadFromStream(stream);
            elem.ID = id;
            elem.Metadata.Add(nameof(EMetadataKeys.Length), stream.Length);
            _elements.InsertOne(elem);
            return elem;
        }

        public async Task<Element> CreateAsync(string parentId, string name, Stream stream, bool streamSupportsAsync)
        {
            if (!IsNameValid(name)) throw new MdbfsInvalidNameException();
            var elemSearch = (await _elements.FindAsync(x => x.ID == parentId && x.Removed == false)).ToList();
            if (!elemSearch.Any()) throw new MdbfsElementNotFoundException(); //parent does not exist
            var elem = await PrepareElementAsync(parentId, name);
            var (id, _) = await _binaryStorage.UploadFromStreamAsync(stream, streamSupportsAsync);
            elem.ID = id;
            elem.Metadata.Add(nameof(EMetadataKeys.Length), stream.Length);
            await _elements.InsertOneAsync(elem);
            return elem;
        }

        public FileUploadStream OpenFileUploadStream(string parentId, string name)
        {
            if (!IsNameValid(name)) throw new MdbfsInvalidNameException();
            return new FileUploadStream(_binaryStorage.OpenUploadStream(), _elements,
                Element.Create(null, parentId, 1, name,
                    new Dictionary<string, object> { { nameof(EMetadataKeys.Length), 0L } }, null));
        }

        public async Task<FileUploadStream> OpenFileUploadStreamAsync(string parentId, string name)
        {
            if (!IsNameValid(name)) throw new MdbfsInvalidNameException();
            return new FileUploadStream(await _binaryStorage.OpenUploadStreamAsync(), _elements,
                Element.Create(null, parentId, 1, name,
                    new Dictionary<string, object> { { nameof(EMetadataKeys.Length), 0L } }, null));
        }

        public Element Get(string id)
        {
            var lId = _nrwl.AcquireReaderLock($"{nameof(Files)}.{id}");
            var elemSearch = _elements.Find(x => x.ID == id).ToList();
            _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);
            return !elemSearch.Any() ? null : elemSearch.First();
        }

        public async Task<Element> GetAsync(string id)
        {
            var lId = await _nrwl.AcquireReaderLockAsync($"{nameof(Files)}.{id}");
            var elemSearch = (await _elements.FindAsync(x => x.ID == id)).ToList();
            await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
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
            var lId = _nrwl.AcquireWriterLock($"{nameof(Files)}.{id}");
            Element f = null;
            if (permanently)
            {
                _elements.FindOneAndDelete(x => x.ID == id);
                var lId2 = _nrwl.AcquireWriterLock($"{nameof(ChunkMap)}.{id}");
                _binaryStorage._maps.UpdateOne(x => x.ID == id, Builders<ChunkMap>.Update.Set(x => x.Removed, true));
                _nrwl.ReleaseLock($"{nameof(ChunkMap)}.{id}", lId2);
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

                        e = parentSearch.First();
                        originalLocationNames = e.Name + '/' + originalLocationNames;
                        originalLocationIDs = e.ID + '/' + originalLocationIDs;
                    } while (e.ParentID != null);

                    f.Opened = deleted;
                    f.Modified = deleted;
                    f.Removed = true;
                    f.Metadata[nameof(EMetadataKeys.PathNames)] = originalLocationNames;
                    f.Metadata[nameof(EMetadataKeys.PathIDs)] = originalLocationIDs;
                    f.Metadata[nameof(EMetadataKeys.Deleted)] = deleted;
                    _elements.FindOneAndReplace(x => x.ID == id, f);
                }
            }

            _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);
            return f;
        }

        public async Task<Element> RemoveAsync(string id, bool permanently)
        {
            var lId = await _nrwl.AcquireWriterLockAsync($"{nameof(Files)}.{id}");
            Element f = null;
            try
            {
                if (permanently)
                {
                    await _elements.FindOneAndDeleteAsync(x => x.ID == id);
                    var lId2 = await _nrwl.AcquireWriterLockAsync($"{nameof(ChunkMap)}.{id}");
                    await _binaryStorage._maps.UpdateOneAsync(x => x.ID == id, Builders<ChunkMap>.Update.Set(x => x.Removed, true));
                    await _nrwl.ReleaseLockAsync($"{nameof(ChunkMap)}.{id}", lId2);
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
                            {

                                await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
                                throw new MdbfsElementNotFoundException(nameof(id));
                            }
                            e = parentSearch.First();
                            originalLocationNames = e.Name + '/' + originalLocationNames;
                            originalLocationIDs = e.ID + '/' + originalLocationIDs;
                        } while (e.ParentID != null);

                        f.Opened = deleted;
                        f.Modified = deleted;
                        f.Removed = true;
                        f.Metadata[nameof(EMetadataKeys.PathNames)] = originalLocationNames;
                        f.Metadata[nameof(EMetadataKeys.PathIDs)] = originalLocationIDs;
                        f.Metadata[nameof(EMetadataKeys.Deleted)] = deleted;
                        await _elements.FindOneAndReplaceAsync(x => x.ID == id, f);
                    }
                }
            }
            catch (Exception)
            {
                await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
            }

            await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
            return f;
        }

        public Element Restore(string id)
        {
            var lId = _nrwl.AcquireWriterLock($"{nameof(Files)}.{id}");
            var elemSearch = _elements.Find(x => x.ID == id && x.Removed).ToList();
            if (elemSearch.Count == 0)
            {
                _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            } //element not found

            var element = elemSearch.First();

            var alterSearch = _elements.Find(x =>
                x.ParentID == element.ParentID && x.Name == element.Name && x.Removed == false).ToList();
            if (alterSearch.Any())
                element.Name = $"{element.Name}_restored_{DateTime.Now:yyyy_MM_dd_H:mm:ss:fff}";

            var originalLocationNames =
                (string)element.Metadata[nameof(EMetadataKeys.PathNames)];
            var originalLocationIDs = (string)element.Metadata[nameof(EMetadataKeys.PathIDs)];
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
            element.Metadata.Remove(nameof(EMetadataKeys.PathNames));
            element.Metadata.Remove(nameof(EMetadataKeys.PathIDs));
            element.Metadata.Remove(nameof(EMetadataKeys.Deleted));
            _elements.FindOneAndReplace(x => x.ID == id, element);
            _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);

            return element;
        }

        public async Task<Element> RestoreAsync(string id)
        {
            var lId = await _nrwl.AcquireWriterLockAsync($"{nameof(Files)}.{id}");
            Element f = null;
            try
            {
                var elemSearch = (await _elements.FindAsync(x => x.ID == id)).ToList();
                if (elemSearch.Count == 0)
                {
                    await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
                    throw new MdbfsElementNotFoundException();
                } //element not found
                f = elemSearch.First();
                if (f.Removed == false)
                {
                    await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
                    throw new MdbfsElementNotFoundException();
                } // element is not removed

                var originalLocationNames = (string)f.Metadata[nameof(EMetadataKeys.PathNames)];
                var originalLocationIDs = (string)f.Metadata[nameof(EMetadataKeys.PathIDs)];
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
                f.Metadata.Remove(nameof(EMetadataKeys.PathNames));
                f.Metadata.Remove(nameof(EMetadataKeys.PathIDs));
                f.Metadata.Remove(nameof(EMetadataKeys.Deleted));
                await _elements.FindOneAndReplaceAsync(x => x.ID == id, f);
            }
            catch (Exception)
            {
                await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
            }
            await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
            return f;
        }

        public Element Copy(string id, string parentId)
        {
            var lId = _nrwl.AcquireWriterLock($"{nameof(Files)}.{id}");
            if (!_elements.Find(x => x.ID == parentId && x.Removed == false).Any()) throw new MdbfsElementNotFoundException(); //parent not found
            var eleSearch = _elements.Find(x => x.ID == id && x.Removed == false).ToList();
            if (eleSearch.Count == 0)
            {
                _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            } //element not found
            var mElem = eleSearch.First();

            _elements.UpdateOne(x => x.ID == id, Builders<Element>.Update.Set(x => x.Opened, DateTime.Now));

            var nId = _binaryStorage.Duplicate(id);
            if (nId == null)
            {
                _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            }
            var date = DateTime.Now;
            var nName = mElem.Name;

            var tmp0 = nName.Contains('.') ? nName.Substring(0, nName.LastIndexOf('.')) : "";
            var tmp1 = nName.Contains('.') ? nName.Substring(nName.LastIndexOf('.'), nName.Length - nName.LastIndexOf('.')) : "";

            // ReSharper disable once AccessToModifiedClosure
            if (_elements.Find(x => x.ParentID == parentId && x.Name == nName).Any())
            {
                long counter = 0;
                var validName = nName.Contains('.') ? $"{tmp0}_Copy{tmp1}" : $"{nName}({nName})";
                // ReSharper disable once AccessToModifiedClosure
                if (_elements.Find(x => x.ParentID == parentId && x.Name == validName).Any())
                {
                    // ReSharper disable once AccessToModifiedClosure
                    while (_elements.Find(x => x.ParentID == parentId && x.Name == nName).Any())
                    {
                        validName = nName.Contains('.') ? $"{tmp0}_Copy({counter}){tmp1}" : $"{nName}({nName})";
                        if (!_elements.Find(x => x.ParentID == parentId && x.Name == validName).Any())
                        {
                            nName = validName;
                            break;
                        }

                        counter++;
                    }
                    nName = validName;
                }
                else
                    nName = validName;
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
                Metadata = mElem.Metadata,
                CustomMetadata = mElem.CustomMetadata,
            };
            _elements.InsertOne(f);
            _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);
            return f;
        }

        public async Task<Element> CopyAsync(string id, string parentId)
        {
            var lId = await _nrwl.AcquireWriterLockAsync($"{nameof(Files)}.{id}");
            if (!await (await _elements.FindAsync(x => x.ID == parentId && x.Removed == false)).AnyAsync())
            {
                await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            } //parent not found
            var eleSearch = (await _elements.FindAsync(x => x.ID == id && x.Removed == false)).ToList();
            if (!eleSearch.Any())
            {
                await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            } //element not found
            var mElem = eleSearch.First();

            await _elements.UpdateOneAsync(x => x.ID == id, Builders<Element>.Update.Set(x => x.Opened, DateTime.Now));

            var nId = await _binaryStorage.DuplicateAsync(id);
            if (nId == null)
            {
                await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            }
            var date = DateTime.Now;

            var nName = mElem.Name;

            var tmp0 = nName.Contains('.') ? nName.Substring(0, nName.LastIndexOf('.')) : "";
            var tmp1 = nName.Contains('.') ? nName.Substring(nName.LastIndexOf('.'), nName.Length - nName.LastIndexOf('.')) : "";

            // ReSharper disable once AccessToModifiedClosure
            if (await _elements.Find(x => x.ParentID == parentId && x.Name == nName).AnyAsync())
            {
                long counter = 0;
                var validName = nName.Contains('.') ? $"{tmp0}_Copy{tmp1}" : $"{nName}({nName})";
                // ReSharper disable once AccessToModifiedClosure
                if (await _elements.Find(x => x.ParentID == parentId && x.Name == validName).AnyAsync())
                {
                    // ReSharper disable once AccessToModifiedClosure
                    while (await _elements.Find(x => x.ParentID == parentId && x.Name == nName).AnyAsync())
                    {
                        validName = nName.Contains('.') ? $"{tmp0}_Copy({counter}){tmp1}" : $"{nName}({nName})";
                        if (!await _elements.Find(x => x.ParentID == parentId && x.Name == validName).AnyAsync())
                        {
                            nName = validName;
                            break;
                        }

                        counter++;
                    }
                    nName = validName;
                }
                else
                    nName = validName;
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
                Metadata = mElem.Metadata,
                CustomMetadata = mElem.CustomMetadata,
            };
            await _elements.InsertOneAsync(f);
            await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
            return f;
        }

        public Element Move(string id, string nParentId)
        {
            var lId = _nrwl.AcquireWriterLock($"{nameof(Files)}.{id}");
            var parSearch = _elements.Find(x => x.ID == nParentId && x.Removed == false).ToList();
            if (!parSearch.Any())
            {
                _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            } //parent not found

            var elemSearch = _elements.Find(x => x.ID == id && x.Removed == false);
            if (!elemSearch.Any())
            {
                _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            } //element not found

            var f = elemSearch.First();
            f.Opened = f.Modified = DateTime.Now;
            f.ParentID = nParentId;
            _elements.FindOneAndReplace(x => x.ID == id, f);
            _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);
            return f;
        }

        public async Task<Element> MoveAsync(string id, string nParentId)
        {
            var lId = await _nrwl.AcquireWriterLockAsync($"{nameof(Files)}.{id}");
            var parSearch = (await _elements.FindAsync(x => x.ID == nParentId && x.Removed == false)).ToList();
            if (!parSearch.Any())
            {
                await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            }//parent not found

            var elemSearch = (await _elements.FindAsync(x => x.ID == id && x.Removed == false)).ToList();
            if (!elemSearch.Any())
            {
                await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            } //element not found

            var f = elemSearch.First();
            f.Opened = f.Modified = DateTime.Now;
            f.ParentID = nParentId;
            await _elements.FindOneAndReplaceAsync(x => x.ID == id, f);
            await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
            return f;
        }

        public Element Rename(string id, string nameNew)
        {
            var lId = _nrwl.AcquireWriterLock($"{nameof(Files)}.{id}");
            var search = _elements.Find(x => x.ID == id && x.Removed == false).ToList();
            if (!search.Any())
            {
                _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            }
            var elem = search.First();
            var nDupSearch = _elements.Find(x => x.ParentID == elem.ParentID && x.Name == nameNew).ToList();
            string validName = null;
            var count = 0;
            var tmp0 = nameNew.Contains('.') ? nameNew.Substring(0, nameNew.LastIndexOf('.')) : "";
            var tmp1 = nameNew.Contains('.') ? nameNew.Substring(nameNew.LastIndexOf('.'), nameNew.Length - nameNew.LastIndexOf('.')) : "";
            while (nDupSearch.Any())
            {
                validName = nameNew.Contains('.') ? $"{tmp0}({count}){tmp1}" : $"{nameNew}({count})";
                // ReSharper disable once AccessToModifiedClosure
                nDupSearch = _elements.Find(x => x.ParentID == elem.ParentID && x.Name == validName).ToList();
                count++;
            }
            elem.Name = validName;
            _elements.UpdateOne(x => x.ID == id, Builders<Element>.Update.Set(x => x.Name, nameNew));
            _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);
            return elem;
        }
        public Element SetCustomMetadata(string id, string fieldName, object fieldValue)
        {
            var lId = _nrwl.AcquireWriterLock($"{nameof(Files)}.{id}");
            var search = _elements.Find(x => x.ID == id).ToList();
            if (!search.Any())
            {
                _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            }
            _elements.UpdateOne(x => x.ID == id,
                Builders<Element>.Update.Set(x => x.CustomMetadata[fieldName], fieldValue));
            List<Element> search2;
            _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);
            return (search2 = _elements.Find(x => x.ID == id).ToList()).Any() ? search2.First() : null;
        }

        public Element RemoveCustomMetadata(string id, string fieldName)
        {
            var lId = _nrwl.AcquireWriterLock($"{nameof(Files)}.{id}");
            var search = _elements.Find(x => x.ID == id).ToList();
            if (!search.Any())
            {
                _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            }
            _elements.UpdateOne(x => x.ID == id,
                Builders<Element>.Update.PullFilter(x => x.CustomMetadata, x => x.Key == fieldName));
            List<Element> search2;
            _nrwl.ReleaseLock($"{nameof(Files)}.{id}", lId);
            return (search2 = _elements.Find(x => x.ID == id).ToList()).Any() ? search2.First() : null;
        }

        public async Task<Element> RenameAsync(string id, string nameNew)
        {
            var lId = await _nrwl.AcquireWriterLockAsync($"{nameof(Files)}.{id}");
            var search = (await _elements.FindAsync(x => x.ID == id && x.Removed == false)).ToList();
            if (!search.Any())
            {
                await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            }
            var elem = search.First(); string validName = null;

            var nDupSearch = _elements.Find(x => x.ParentID == elem.ParentID && x.Name == nameNew).ToList();
            var count = 0;
            var tmp0 = nameNew.Contains('.') ? nameNew.Substring(0, nameNew.LastIndexOf('.')) : "";
            var tmp1 = nameNew.Contains('.') ? nameNew.Substring(nameNew.LastIndexOf('.'), nameNew.Length - nameNew.LastIndexOf('.')) : "";
            while (nDupSearch.Any())
            {
                validName = nameNew.Contains('.') ? $"{tmp0}({count}){tmp1}" : $"{nameNew}({count})";
                // ReSharper disable once AccessToModifiedClosure
                nDupSearch = _elements.Find(x => x.ParentID == elem.ParentID && x.Name == validName).ToList();
                count++;
            }
            elem.Name = validName;
            await _elements.UpdateOneAsync(x => x.ID == id, Builders<Element>.Update.Set(x => x.Name, nameNew));
            await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
            return elem;
        }

        public async Task<Element> SetCustomMetadataAsync(string id, string fieldName, object fieldValue)
        {
            var lId = await _nrwl.AcquireWriterLockAsync($"{nameof(Files)}.{id}");
            var search = (await _elements.FindAsync(x => x.ID == id)).ToList();
            if (!search.Any())
            {
                await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            }
            await _elements.UpdateOneAsync(x => x.ID == id,
                Builders<Element>.Update.Set(x => x.CustomMetadata[fieldName], fieldValue));
            List<Element> search2;
            await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
            return (search2 = (await _elements.FindAsync(x => x.ID == id)).ToList()).Any() ? search2.First() : null;
        }

        public async Task<Element> RemoveCustomMetadataAsync(string id, string fieldName)
        {
            var lId = await _nrwl.AcquireWriterLockAsync($"{nameof(Files)}.{id}");
            var search = (await _elements.FindAsync(x => x.ID == id)).ToList();
            if (!search.Any())
            {
                await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
                throw new MdbfsElementNotFoundException();
            }
            await _elements.UpdateOneAsync(x => x.ID == id,
                Builders<Element>.Update.PullFilter(x => x.CustomMetadata, x => x.Key == fieldName));
            List<Element> search2;
            await _nrwl.ReleaseLockAsync($"{nameof(Files)}.{id}", lId);
            return (search2 = (await _elements.FindAsync(x => x.ID == id)).ToList()).Any() ? search2.First() : null;
        }
    }
}