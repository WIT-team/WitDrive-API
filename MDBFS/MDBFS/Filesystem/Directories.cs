using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MDBFS.Exceptions;
using MDBFS.Filesystem.Models;
using MDBFS.Misc;
using MongoDB.Driver;

namespace MDBFS.Filesystem
{
    public class Directories
    {
        private readonly IMongoCollection<Element> _elements;
        private readonly Files _files;

        public Directories(IMongoCollection<Element> elements, Files files)
        {
            _elements = elements;
            _files = files;
            var rootSearch = elements.Find(x => x.ParentID == null).ToList();
            if (rootSearch.Count == 0)
            {
                var e = new Element
                {
                    ID = null,
                    ParentID = null,
                    Type = 2,
                    Name = "_",
                    Created = DateTime.MinValue,
                    Modified = DateTime.MinValue,
                    Opened = DateTime.MinValue,
                    Removed = false,
                    Metadata = new Dictionary<string, object>()
                };
                elements.InsertOne(e);
                Root = e.ID;
            }
            else
            {
                Root = rootSearch.First().ID;
            }
        }

        public string Root { get; }

        public Element Create(string parentId, string name)
        {
            var parSearch = _elements.Find(x => x.ID == parentId).ToList();
            if (!parSearch.Any()) throw new MdbfsElementNotFoundException("Parent element not found");


            var name1 = name;
            var nDupSearch = _elements.Find(x => x.ParentID == parentId && x.Name == name1).ToList();
            string validName = null;
            var count = 0;
            while (nDupSearch.Any())
            {
                validName = $"{name}({count})";
                var validName1 = validName;
                nDupSearch = _elements.Find(x => x.ParentID == parentId && x.Name == validName1).ToList();
                count++;
            }

            if (validName != null) name = validName;
            var date = DateTime.Now;
            var elem = new Element
            {
                ParentID = parentId,
                Type = 2,
                Name = name,
                Created = date,
                Modified = date,
                Opened = date,
                Removed = false
            };
            _elements.InsertOne(elem);
            return elem;
        }

        public Element Get(string id)
        {
            var elemSearch = _elements.Find(x => x.ID == id).ToList();
            if (!elemSearch.Any()) return null; //element not found
            _elements.FindOneAndUpdate(x => x.ID == id, Builders<Element>.Update.Set(x => x.Opened, DateTime.Now));
            var e = elemSearch.First();
            e.Opened = DateTime.Now;
            return e;
        }

        public Element[] GetSubelements(string id)
        {
            var elemSearch = _elements.Find(x => x.ID == id && x.Removed == false).ToList();
            if (!elemSearch.Any()) return new Element[0]; //element not found
            var subElSearch = _elements.Find(x => x.ParentID == id && x.Removed == false).ToList();
            if (!subElSearch.Any()) return new Element[0]; //no subelements
            _elements.FindOneAndUpdate(x => x.ID == id, Builders<Element>.Update.Set(x => x.Opened, DateTime.Now));
            return subElSearch.ToArray();
        }

        public Element Move(string id, string nParentId)
        {
            var nParentSearch = _elements.Find(x => x.ID == nParentId && x.Removed == false).ToList();
            if (!nParentSearch.Any()) return null; //nParent not found
            var elemSearch = _elements.Find(x => x.ID == id && x.Removed == false).ToList();
            if (!elemSearch.Any()) return null; //element not found
            var element = elemSearch.First();
            var element1 = element;
            var alterElemSearch = _elements
                .Find(x => x.ParentID == nParentId && x.Removed == false && x.Name == element1.Name).ToList();
            var date = DateTime.Now;
            if (alterElemSearch.Any())
            {
                element = alterElemSearch.First();
                _elements.UpdateMany(x => x.ParentID == element.ParentID && x.Removed == false,
                    Builders<Element>.Update.Combine(Builders<Element>.Update.Set(x => x.ParentID, element.ID),
                        Builders<Element>.Update.Set(x => x.Opened, date),
                        Builders<Element>.Update.Set(x => x.Modified, date)));
            }
            else
            {
                element.ParentID = nParentId;
            }

            element.Opened = element.Modified = date;
            _elements.FindOneAndUpdate(x => x.ID == element.ID,
                Builders<Element>.Update.Combine(Builders<Element>.Update.Set(x => x.ParentID, nParentId),
                    Builders<Element>.Update.Set(x => x.Opened, DateTime.Now),
                    Builders<Element>.Update.Set(x => x.Modified, DateTime.Now)));
            return element;
        }

        public Element Remove(string id, bool permanently)
        {
            var elemSearch = _elements.Find(x => x.ID == id && x.Removed == false).ToList();
            if (!elemSearch.Any()) return null; //not found or already removed
            var element = elemSearch.First();

            if (permanently)
            {
                var subelements = GetSubelements(id);
                foreach (var subelem in subelements)
                    if (subelem.Type == 2) Remove(subelem.ID, true);
                    else if (subelem.Type == 1) _files.Remove(subelem.ID, true);
                var tmp = _elements.DeleteOne(x => x.ID == id);
                return element;
            }

            if (element.ParentID != null)
            {
                var pareSearch = _elements.Find(x => x.ID == element.ParentID).ToList();
                if (pareSearch.Any())
                {
                    var parent = pareSearch.First();
                    var date = DateTime.Now;
                    _elements.FindOneAndUpdate(x => x.ID == parent.ID, Builders<Element>.Update.Combine(
                        Builders<Element>.Update.Set(x => x.Opened, date),
                        Builders<Element>.Update.Set(x => x.Modified, date)));
                }
            }

            var originalLocationNames = "";
            var originalLocationIDs = "";
            var deleted = DateTime.Now;
            var currElement = element;
            do
            {
                var element1 = currElement;
                var parentSearch = _elements.Find(x => x.ID == element1.ParentID).ToList();
                if (!parentSearch.Any()) throw new MdbfsElementNotFoundException("Parent element missing");
                currElement = parentSearch.First();
                originalLocationNames = currElement.Name + '/' + originalLocationNames;
                originalLocationIDs = currElement.ID + '/' + originalLocationIDs;
            } while (currElement.ParentID != null);

            element.Opened = deleted;
            element.Modified = deleted;
            element.Removed = true;
            element.Metadata[nameof(EMetadataKeys.PathNames)] = originalLocationNames;
            element.Metadata[nameof(EMetadataKeys.PathIDs)] = originalLocationIDs;
            element.Metadata[nameof(EMetadataKeys.Deleted)] = deleted;
            _elements.FindOneAndReplace(x => x.ID == id, element);

            return element;
        }

        public Element Restore(string id)
        {
            var elemSearch = _elements.Find(x => x.ID == id && x.Removed).ToList();
            if (!elemSearch.Any()) return null; //not found or already removed
            var element = elemSearch.First();

            var alterSearch = _elements.Find(x =>
                x.ParentID == element.ParentID && x.Name == element.Name && x.Removed == false).ToList();
            if (alterSearch.Any())
                element.Name = string.Format("{0}_restored_{1:yyyy_MM_dd_H:mm:ss:fff}", element.Name, DateTime.Now);
            var prevIDsStr = (string)element.Metadata[nameof(EMetadataKeys.PathIDs)];
            var prevNamesStr = (string)element.Metadata[nameof(EMetadataKeys.PathNames)];
            var prevIDs = prevIDsStr.Split('/'); //adds one empty string at the end 
            var prevNames = prevNamesStr.Split('/'); //adds one empty string at the end
            var currentElement = element;
            for (var it = 1; it < prevIDs.Length - 1; it++)
            {
                var it1 = it;
                var e = _elements.Find(x =>
                    x.ID == prevIDs[it1] && x.Removed == false || x.ParentID == prevIDs[it1 - 1] &&
                    x.Name == prevNames[it1] && x.Removed == false).ToList();
                if (!e.Any())
                {
                    currentElement = Create(prevIDs[it - 1], prevNames[it]);
                }
                else
                {
                    currentElement = e.First();
                    if (currentElement.ID != prevIDs[it]) prevIDs[it] = currentElement.ID;
                    currentElement.Opened = currentElement.Modified = DateTime.Now;
                    var element1 = currentElement;
                    _elements.FindOneAndReplace(x => x.ID == element1.ID, currentElement);
                }
            }

            element.ParentID = currentElement.ID;
            element.Removed = false;
            element.Opened = element.Modified = DateTime.Now;
            element.Metadata.Remove(nameof(EMetadataKeys.PathIDs));
            element.Metadata.Remove(nameof(EMetadataKeys.PathNames));
            element.Metadata.Remove(nameof(EMetadataKeys.Deleted));
            _elements.FindOneAndReplace(x => x.ID == element.ID, element);
            return element;
        }

        public Element Copy(string id, string nParentId)
        {
            var elemSearch = _elements.Find(x => x.ID == id && x.Removed == false).ToList();
            if (!elemSearch.Any()) return null;

            var parentId = nParentId;
            var nParentSearch = _elements.Find(x => x.ID == parentId && x.Removed == false).ToList();
            if (!nParentSearch.Any()) return null;
            var element = elemSearch.First();
            var parentId1 = nParentId;
            var parChild = _elements.Find(x => x.ParentID == parentId1 && x.Removed == false && x.Name == element.Name)
                .ToList();
            if (parChild.Any())
            {
                nParentId = parChild.First().ID;
            }
            else
            {
                var name = element.Name;
                var meta = element.Metadata;
                var element2 = new Element
                { Name = name, ParentID = nParentId, Removed = false, Type = 2, Metadata = meta };

                element2.Opened = element.Created = element.Modified = DateTime.Now;
                _elements.InsertOne(element2);
                nParentId = element2.ID;
            }

            var date = DateTime.Now;
            _elements.UpdateOne(x => x.ID == id,
                Builders<Element>.Update.Combine(Builders<Element>.Update.Set(x => x.Opened, date),
                    Builders<Element>.Update.Set(x => x.Modified, date)));
            _elements.UpdateOne(x => x.ID == nParentId,
                Builders<Element>.Update.Combine(Builders<Element>.Update.Set(x => x.Opened, date),
                    Builders<Element>.Update.Set(x => x.Modified, date)));

            var subelements = GetSubelements(element.ID);
            if (!subelements.Any()) return element;
            foreach (var subelement in subelements)
                if (subelement.Type == 2) Copy(subelement.ID, nParentId);
                else if (subelement.Type == 1) _files.Copy(subelement.ID, nParentId);

            return element;
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
            var elem = search.First();
            elem.CustomMetadata[fieldName] = fieldValue;
            _elements.FindOneAndReplace(x => x.ID == id, elem);
            return elem;
        }

        public Element RemoveCustomMetadata(string id, string fieldName)
        {
            var search = _elements.Find(x => x.ID == id).ToList();
            if (!search.Any()) return null;
            _elements.UpdateOne(x => x.ID == id,
                Builders<Element>.Update.PullFilter(x => x.CustomMetadata, x => x.Key == fieldName));
            List<Element> search2;
            return (search2 = _elements.Find(x => x.ID == id).ToList()).Any() ? search2.First() : null;
        }
        public IEnumerable<Element> Find(string searchRoot, ElementSearchQuery query)
        {
            if (query.ID != null) return _elements.Find(x => x.ID == query.ID).ToList().ToArray();
            if (query.ParentID != null) return _elements.Find(x => x.ParentID == query.ParentID).ToList().ToArray();
            var filters = new List<FilterDefinition<Element>>
            {
                Builders<Element>.Filter.Eq(x => x.ParentID, searchRoot)
            };

            var translated = GenerateElementFilter(query);
            var res = new List<Element>();
            if (translated.Count > 0)
            {
                filters.AddRange(translated);


                res = _elements.Find(Builders<Element>.Filter.And(filters)).ToList();
            }


            foreach (var subelem in GetSubelements(searchRoot))
                if (subelem.Type == 2)
                    res.AddRange(Find(subelem.ID, query.ID, query.ParentID, translated).ToList());

            return res.ToArray();
        }



        public async Task<Element> CreateAsync(string parentId, string name)
        {
            var parSearch = (await _elements.FindAsync(x => x.ID == parentId)).ToList();
            if (!parSearch.Any()) throw new MdbfsElementNotFoundException("Parent element not found");


            var name1 = name;
            var nDupSearch = (await _elements.FindAsync(x => x.ParentID == parentId && x.Name == name1)).ToList();
            string validName = null;
            var count = 0;
            while (nDupSearch.Any())
            {
                validName = $"{name}({count})";
                var validName1 = validName;
                nDupSearch = (await _elements.FindAsync(x => x.ParentID == parentId && x.Name == validName1)).ToList();
                count++;
            }

            if (validName != null) name = validName;
            var date = DateTime.Now;
            var elem = new Element
            {
                ParentID = parentId,
                Type = 2,
                Name = name,
                Created = date,
                Modified = date,
                Opened = date,
                Removed = false
            };
            await _elements.InsertOneAsync(elem);
            return elem;
        }

        public async Task<Element> GetAsync(string id)
        {
            var elemSearch = (await _elements.FindAsync(x => x.ID == id)).ToList();
            if (!elemSearch.Any()) return null; //element not found
            await _elements.FindOneAndUpdateAsync(x => x.ID == id, Builders<Element>.Update.Set(x => x.Opened, DateTime.Now));
            var e = elemSearch.First();
            e.Opened = DateTime.Now;
            return e;
        }

        public async Task<Element[]> GetSubelementsAsync(string id)
        {
            var elemSearch = (await _elements.FindAsync(x => x.ID == id && x.Removed == false)).ToList();
            if (!elemSearch.Any()) return new Element[0]; //element not found
            var subElSearch = (await _elements.FindAsync(x => x.ParentID == id && x.Removed == false)).ToList();
            if (!subElSearch.Any()) return new Element[0]; //no subelements
            await _elements.FindOneAndUpdateAsync(x => x.ID == id, Builders<Element>.Update.Set(x => x.Opened, DateTime.Now));
            return subElSearch.ToArray();
        }

        public async Task<Element> MoveAsync(string id, string nParentId)
        {
            var nParentSearch = (await _elements.FindAsync(x => x.ID == nParentId && x.Removed == false)).ToList();
            if (!nParentSearch.Any()) return null; //nParent not found
            var elemSearch = (await _elements.FindAsync(x => x.ID == id && x.Removed == false)).ToList();
            if (!elemSearch.Any()) return null; //element not found
            var element = elemSearch.First();
            var element1 = element;
            var alterElemSearch = (await _elements
                .FindAsync(x => x.ParentID == nParentId && x.Removed == false && x.Name == element1.Name)).ToList();
            var date = DateTime.Now;
            if (alterElemSearch.Any())
            {
                element = alterElemSearch.First();
                await _elements.UpdateManyAsync(x => x.ParentID == element.ParentID && x.Removed == false,
                    Builders<Element>.Update.Combine(Builders<Element>.Update.Set(x => x.ParentID, element.ID),
                        Builders<Element>.Update.Set(x => x.Opened, date),
                        Builders<Element>.Update.Set(x => x.Modified, date)));
            }
            else
            {
                element.ParentID = nParentId;
            }

            element.Opened = element.Modified = date;
            await _elements.FindOneAndUpdateAsync(x => x.ID == element.ID,
                Builders<Element>.Update.Combine(Builders<Element>.Update.Set(x => x.ParentID, nParentId),
                    Builders<Element>.Update.Set(x => x.Opened, DateTime.Now),
                    Builders<Element>.Update.Set(x => x.Modified, DateTime.Now)));
            return element;
        }

        public async Task<Element> RemoveAsync(string id, bool permanently)
        {
            if (permanently)
            {
                var subelements = await GetSubelementsAsync(id);
                foreach (var subelem in subelements)
                    if (subelem.Type == 2) await RemoveAsync(subelem.ID, true);
                    else if (subelem.Type == 1) await _files.RemoveAsync(subelem.ID, true);
                var tmp = await _elements.DeleteOneAsync(x => x.ID == id);
                return null;
            }

            var elemSearch =(await _elements.FindAsync(x => x.ID == id && x.Removed == false)).ToList();
            if (!elemSearch.Any()) return null; //not found or already removed
            var element = elemSearch.First();

            if (element.ParentID != null)
            {
                var pareSearch =(await _elements.FindAsync(x => x.ID == element.ParentID)).ToList();
                if (pareSearch.Any())
                {
                    var parent = pareSearch.First();
                    var date = DateTime.Now;
                    await _elements.FindOneAndUpdateAsync(x => x.ID == parent.ID, Builders<Element>.Update.Combine(
                        Builders<Element>.Update.Set(x => x.Opened, date),
                        Builders<Element>.Update.Set(x => x.Modified, date)));
                }
            }

            var originalLocationNames = "";
            var originalLocationIDs = "";
            var deleted = DateTime.Now;
            var currentElement = element;
            do
            {
                var element1 = currentElement;
                var parentSearch =(await _elements.FindAsync(x => x.ID == element1.ParentID)).ToList();
                if (!parentSearch.Any()) throw new MdbfsElementNotFoundException("Parent element missing");
                currentElement = parentSearch.First();
                originalLocationNames = currentElement.Name + '/' + originalLocationNames;
                originalLocationIDs = currentElement.ID + '/' + originalLocationIDs;
            } while (currentElement.ParentID != null);

            element.Opened = deleted;
            element.Modified = deleted;
            element.Removed = true;
            element.Metadata[nameof(EMetadataKeys.PathNames)] = originalLocationNames;
            element.Metadata[nameof(EMetadataKeys.PathIDs)] = originalLocationIDs;
            element.Metadata[nameof(EMetadataKeys.Deleted)] = deleted;
            await _elements.FindOneAndReplaceAsync(x => x.ID == id, element);

            return element;
        }

        public async Task<Element> RestoreAsync(string id)
        {
            var elemSearch =(await _elements.FindAsync(x => x.ID == id && x.Removed)).ToList();
            if (!elemSearch.Any()) return null; //not found or already removed
            var element = elemSearch.First();

            var alterSearch =(await _elements.FindAsync(x =>
                x.ParentID == element.ParentID && x.Name == element.Name && x.Removed == false)).ToList();
            if (alterSearch.Any())
                element.Name = $"{element.Name}_restored_{DateTime.Now:yyyy_MM_dd_H:mm:ss:fff}";
            var prevIDsStr = (string)element.Metadata[nameof(EMetadataKeys.PathIDs)];
            var prevNamesStr = (string)element.Metadata[nameof(EMetadataKeys.PathNames)];
            var prevIDs = prevIDsStr.Split('/'); //adds one empty string at the end 
            var prevNames = prevNamesStr.Split('/'); //adds one empty string at the end
            var currentElement = element;
            for (var it = 1; it < prevIDs.Length - 1; it++)
            {
                var it1 = it;
                var e =(await  _elements.FindAsync(x =>
                    x.ID == prevIDs[it1] && x.Removed == false || x.ParentID == prevIDs[it1 - 1] &&
                    x.Name == prevNames[it1] && x.Removed == false)).ToList();
                if (!e.Any())
                {
                    currentElement = await CreateAsync(prevIDs[it - 1], prevNames[it]);
                }
                else
                {
                    currentElement = e.First();
                    if (currentElement.ID != prevIDs[it]) prevIDs[it] = currentElement.ID;
                    currentElement.Opened = currentElement.Modified = DateTime.Now;
                    var element1 = currentElement;
                    await _elements.FindOneAndReplaceAsync(x => x.ID == element1.ID, currentElement);
                }
            }

            element.ParentID = currentElement.ID;
            element.Removed = false;
            element.Opened = element.Modified = DateTime.Now;
            element.Metadata.Remove(nameof(EMetadataKeys.PathIDs));
            element.Metadata.Remove(nameof(EMetadataKeys.PathNames));
            element.Metadata.Remove(nameof(EMetadataKeys.Deleted));
            await _elements.FindOneAndReplaceAsync(x => x.ID == element.ID, element);
            return element;
        }

        public async Task<Element> CopyAsync(string id, string nParentId)
        {
            var elemSearch =(await _elements.FindAsync(x => x.ID == id && x.Removed == false)).ToList();
            if (!elemSearch.Any()) return null;

            var parentId = nParentId;
            var nParentSearch =(await _elements.FindAsync(x => x.ID == parentId && x.Removed == false)).ToList();
            if (!nParentSearch.Any()) return null;
            var element = elemSearch.First();
            var parentId1 = nParentId;
            var parChild =(await _elements.FindAsync(x => x.ParentID == parentId1 && x.Removed == false && x.Name == element.Name)).ToList();
            if (parChild.Any())
            {
                nParentId = parChild.First().ID;
            }
            else
            {
                var name = element.Name;
                var meta = element.Metadata;
                var element2 = new Element
                {
                    Name = name,
                    ParentID = nParentId,
                    Removed = false,
                    Type = 2,
                    Metadata = meta,
                    Opened = element.Created = element.Modified = DateTime.Now
                };

                await _elements.InsertOneAsync(element2);
                nParentId = element2.ID;
            }

            var date = DateTime.Now;
            await _elements.UpdateOneAsync(x => x.ID == id,
                Builders<Element>.Update.Combine(Builders<Element>.Update.Set(x => x.Opened, date),
                    Builders<Element>.Update.Set(x => x.Modified, date)));
            await _elements.UpdateOneAsync(x => x.ID == nParentId,
                Builders<Element>.Update.Combine(Builders<Element>.Update.Set(x => x.Opened, date),
                    Builders<Element>.Update.Set(x => x.Modified, date)));

            var subelements = await GetSubelementsAsync(element.ID);
            if (!subelements.Any()) return element;
            foreach (var subelement in subelements)
                if (subelement.Type == 2) await CopyAsync(subelement.ID, nParentId);
                else if (subelement.Type == 1) await _files.CopyAsync(subelement.ID, nParentId);

            return element;
        }

        public async Task<Element> RenameAsync(string id, string newName)
        {
            var search =(await  _elements.FindAsync(x => x.ID == id && x.Removed == false)).ToList();
            if (!search.Any()) return null;
            var elem = search.First();
            elem.Name = newName;
            await _elements.UpdateOneAsync(x => x.ID == id, Builders<Element>.Update.Set(x => x.Name, newName));
            return elem;
        }

        public async Task<Element> SetCustomMetadataAsync(string id, string fieldName, object fieldValue)
        {
            var search = (await _elements.FindAsync(x => x.ID == id)).ToList();
            if (!search.Any()) return null;
            var elem = search.First();
            elem.CustomMetadata[fieldName] = fieldValue;
            await _elements.FindOneAndReplaceAsync(x => x.ID == id, elem);
            return elem;
        }

        public async Task<Element> RemoveCustomMetadataAsync(string id, string fieldName)
        {
            var search =(await  _elements.FindAsync(x => x.ID == id)).ToList();
            if (!search.Any()) return null;
            await _elements.UpdateOneAsync(x => x.ID == id,
                Builders<Element>.Update.PullFilter(x => x.CustomMetadata, x => x.Key == fieldName));
            List<Element> search2;
            return (search2 =(await _elements.FindAsync(x => x.ID == id)).ToList()).Any() ? search2.First() : null;
        }
        public async Task<IEnumerable<Element>> FindAsync(string searchRoot, ElementSearchQuery query)
        {
            if (query.ID != null) return( await _elements.FindAsync(x => x.ID == query.ID)).ToList();
            if (query.ParentID != null) return(await  _elements.FindAsync(x => x.ParentID == query.ParentID)).ToList();
            var filters = new List<FilterDefinition<Element>>
            {
                Builders<Element>.Filter.Eq(x => x.ParentID, searchRoot)
            };

            var translated = GenerateElementFilter(query);
            var res = new List<Element>();
            if (translated.Count > 0)
            {
                filters.AddRange(translated);


                res =(await _elements.FindAsync(Builders<Element>.Filter.And(filters))).ToList();
            }


            foreach (var subelem in await GetSubelementsAsync(searchRoot))
                if (subelem.Type == 2)
                    res.AddRange((await FindAsync(subelem.ID, query.ID, query.ParentID, translated)).ToList());

            return res.ToArray();
        }



        private async Task<IEnumerable<Element>> FindAsync(string searchRoot, string id, string parentId,
            List<FilterDefinition<Element>> translated)
        {
            if (searchRoot == null) throw new ArgumentNullException(nameof(searchRoot));
            if (translated == null) throw new ArgumentNullException(nameof(translated));

            if (id != null) return(await  _elements.FindAsync(x => x.ID == id)).ToList();
            if (parentId != null) return(await _elements.FindAsync(x => x.ParentID == parentId)).ToList();
            var filters = new List<FilterDefinition<Element>>
            {
                Builders<Element>.Filter.Eq(x => x.ParentID, searchRoot)
            };
            filters.AddRange(translated);
            var res = _elements.Find(Builders<Element>.Filter.And(filters)).ToList();

            foreach (var subElem in await GetSubelementsAsync(searchRoot))
                if (subElem.Type == 2)
                    res.AddRange((await FindAsync(subElem.ID, null, null, translated)).ToList());

            return res.ToArray();
        }

        private IEnumerable<Element> Find(string searchRoot, string id, string parentId,
            List<FilterDefinition<Element>> translated)
        {
            if (searchRoot == null) throw new ArgumentNullException(nameof(searchRoot));
            if (translated == null) throw new ArgumentNullException(nameof(translated));

            if (id != null) return _elements.Find(x => x.ID == id).ToList();
            if (parentId != null) return _elements.Find(x => x.ParentID == parentId).ToList();
            var filters = new List<FilterDefinition<Element>>
            {
                Builders<Element>.Filter.Eq(x => x.ParentID, searchRoot)
            };
            filters.AddRange(translated);
            var res = _elements.Find(Builders<Element>.Filter.And(filters)).ToList();

            foreach (var subelem in GetSubelements(searchRoot))
                if (subelem.Type == 2)
                    res.AddRange(Find(subelem.ID, null, null, translated).ToList());

            return res.ToArray();
        }

        private static List<FilterDefinition<Element>> GenerateElementFilter(ElementSearchQuery query)
        {
            var result = new List<FilterDefinition<Element>>();
            if (query.Name != null) result.AddRange(GenerateForName(query.Name));
            if (query.Opened != null) result.AddRange(GenerateForOpened(query.Opened));
            if (query.Modified != null) result.AddRange(GenerateForCreated(query.Modified));
            if (query.Created != null) result.AddRange(GenerateForModified(query.Created));
            if (query.Removed != null) result.AddRange(GenerateForRemoved(query.Removed));
            if (query.Metadata != null) result.AddRange(GenerateForMetadata(query.Metadata));
            if (query.CustomMetadata != null) result.AddRange(GenerateForCustomMetadata(query.CustomMetadata));
            return result;
        }

        private static IEnumerable<FilterDefinition<Element>> GenerateForName(
            IEnumerable<(ESearchCondition condition, string value)> cond)
        {
            var filters = new List<FilterDefinition<Element>>();
            foreach (var (condition, value) in cond)
                switch (condition)
                {
                    case ESearchCondition.Eq:
                        filters.Add(Builders<Element>.Filter.Eq(x => x.Name, value));
                        break;
                    case ESearchCondition.Ne:
                        filters.Add(Builders<Element>.Filter.Ne(x => x.Name, value));
                        break;
                    case ESearchCondition.Lt:
                        filters.Add(Builders<Element>.Filter.Lt(x => x.Name, value));
                        break;
                    case ESearchCondition.Lte:
                        filters.Add(Builders<Element>.Filter.Lte(x => x.Name, value));
                        break;
                    case ESearchCondition.Gt:
                        filters.Add(Builders<Element>.Filter.Gt(x => x.Name, value));
                        break;
                    case ESearchCondition.Gte:
                        filters.Add(Builders<Element>.Filter.Gte(x => x.Name, value));
                        break;
                    case ESearchCondition.Contains:
                        var filt = Builders<Element>.Filter.Where(x => x.Name.Contains(value));
                        filters.Add(filt);
                        break;
                }

            return filters;
        }

        private static IEnumerable<FilterDefinition<Element>> GenerateForOpened(
            IEnumerable<(ESearchCondition condition, DateTime value)> cond)
        {
            var filters = new List<FilterDefinition<Element>>();
            foreach (var (condition, value) in cond)
                switch (condition)
                {
                    case ESearchCondition.Eq:
                        filters.Add(Builders<Element>.Filter.Eq(x => x.Opened, value));
                        break;
                    case ESearchCondition.Ne:
                        filters.Add(Builders<Element>.Filter.Ne(x => x.Opened, value));
                        break;
                    case ESearchCondition.Lt:
                        filters.Add(Builders<Element>.Filter.Lt(x => x.Opened, value));
                        break;
                    case ESearchCondition.Lte:
                        filters.Add(Builders<Element>.Filter.Lte(x => x.Opened, value));
                        break;
                    case ESearchCondition.Gt:
                        filters.Add(Builders<Element>.Filter.Gt(x => x.Opened, value));
                        break;
                    case ESearchCondition.Gte:
                        filters.Add(Builders<Element>.Filter.Gte(x => x.Opened, value));
                        break;
                }

            return filters;
        }

        private static IEnumerable<FilterDefinition<Element>> GenerateForCreated(
            IEnumerable<(ESearchCondition condition, DateTime value)> cond)
        {
            var filters = new List<FilterDefinition<Element>>();
            foreach (var (condition, value) in cond)
                switch (condition)
                {
                    case ESearchCondition.Eq:
                        filters.Add(Builders<Element>.Filter.Eq(x => x.Created, value));
                        break;
                    case ESearchCondition.Ne:
                        filters.Add(Builders<Element>.Filter.Ne(x => x.Created, value));
                        break;
                    case ESearchCondition.Lt:
                        filters.Add(Builders<Element>.Filter.Lt(x => x.Created, value));
                        break;
                    case ESearchCondition.Lte:
                        filters.Add(Builders<Element>.Filter.Lte(x => x.Created, value));
                        break;
                    case ESearchCondition.Gt:
                        filters.Add(Builders<Element>.Filter.Gt(x => x.Created, value));
                        break;
                    case ESearchCondition.Gte:
                        filters.Add(Builders<Element>.Filter.Gte(x => x.Created, value));
                        break;
                }

            return filters;
        }

        private static IEnumerable<FilterDefinition<Element>> GenerateForModified(
            IEnumerable<(ESearchCondition condition, DateTime value)> cond)
        {
            var filters = new List<FilterDefinition<Element>>();
            foreach (var (condition, value) in cond)
                switch (condition)
                {
                    case ESearchCondition.Eq:
                        filters.Add(Builders<Element>.Filter.Eq(x => x.Modified, value));
                        break;
                    case ESearchCondition.Ne:
                        filters.Add(Builders<Element>.Filter.Ne(x => x.Modified, value));
                        break;
                    case ESearchCondition.Lt:
                        filters.Add(Builders<Element>.Filter.Lt(x => x.Modified, value));
                        break;
                    case ESearchCondition.Lte:
                        filters.Add(Builders<Element>.Filter.Lte(x => x.Modified, value));
                        break;
                    case ESearchCondition.Gt:
                        filters.Add(Builders<Element>.Filter.Gt(x => x.Modified, value));
                        break;
                    case ESearchCondition.Gte:
                        filters.Add(Builders<Element>.Filter.Gte(x => x.Modified, value));
                        break;
                }

            return filters;
        }

        private static IEnumerable<FilterDefinition<Element>> GenerateForRemoved(
            IEnumerable<(ESearchCondition condition, bool value)> cond)
        {
            var filters = new List<FilterDefinition<Element>>();
            foreach (var (condition, value) in cond)
                switch (condition)
                {
                    case ESearchCondition.Eq:
                        filters.Add(Builders<Element>.Filter.Eq(x => x.Removed, value));
                        break;
                    case ESearchCondition.Ne:
                        filters.Add(Builders<Element>.Filter.Ne(x => x.Removed, value));
                        break;
                }

            return filters;
        }

        private static IEnumerable<FilterDefinition<Element>> GenerateForMetadata(
            IEnumerable<(string fieldName, ESearchCondition condition, object value)> cond)
        {
            var filters = new List<FilterDefinition<Element>>();

            foreach (var (fieldName, condition, value) in cond)
                switch (condition)
                {
                    case ESearchCondition.Eq:
                        filters.Add(Builders<Element>.Filter.Eq(x => x.Metadata[fieldName], value));
                        break;
                    case ESearchCondition.Ne:
                        filters.Add(Builders<Element>.Filter.Ne(x => x.Metadata[fieldName], value));
                        break;
                    case ESearchCondition.Lt:
                        filters.Add(Builders<Element>.Filter.Lt(x => x.Metadata[fieldName], value));
                        break;
                    case ESearchCondition.Lte:
                        filters.Add(Builders<Element>.Filter.Lte(x => x.Metadata[fieldName], value));
                        break;
                    case ESearchCondition.Gt:
                        filters.Add(Builders<Element>.Filter.Gt(x => x.Metadata[fieldName], value));
                        break;
                    case ESearchCondition.Gte:
                        filters.Add(Builders<Element>.Filter.Gte(x => x.Metadata[fieldName], value));
                        break;
                }

            return filters;
        }

        private static IEnumerable<FilterDefinition<Element>> GenerateForCustomMetadata(
           IEnumerable<(string fieldName, ESearchCondition condition, object value)> cond)
        {
            var filters = new List<FilterDefinition<Element>>();

            foreach (var (fieldName, condition, value) in cond)
                switch (condition)
                {
                    case ESearchCondition.Eq:
                        filters.Add(Builders<Element>.Filter.Eq(x => x.CustomMetadata[fieldName], value));
                        break;
                    case ESearchCondition.Ne:
                        filters.Add(Builders<Element>.Filter.Ne(x => x.CustomMetadata[fieldName], value));
                        break;
                    case ESearchCondition.Lt:
                        filters.Add(Builders<Element>.Filter.Lt(x => x.CustomMetadata[fieldName], value));
                        break;
                    case ESearchCondition.Lte:
                        filters.Add(Builders<Element>.Filter.Lte(x => x.CustomMetadata[fieldName], value));
                        break;
                    case ESearchCondition.Gt:
                        filters.Add(Builders<Element>.Filter.Gt(x => x.CustomMetadata[fieldName], value));
                        break;
                    case ESearchCondition.Gte:
                        filters.Add(Builders<Element>.Filter.Gte(x => x.CustomMetadata[fieldName], value));
                        break;
                }

            return filters;
        }

    }
}