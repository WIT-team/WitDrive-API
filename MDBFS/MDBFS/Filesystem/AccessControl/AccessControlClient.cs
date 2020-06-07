﻿using System;
using System.Collections.Generic;
using System.Linq;
using MDBFS.Filesystem.AccessControl.Models;
using MDBFS.Filesystem.Models;
using MongoDB.Driver;
using static MDBFS.Filesystem.AccessControl.Models.EAccesControlFields;

namespace MDBFS.Filesystem.AccessControl
{
    public class AccessControlClient
    {
        private readonly Directories _directories;
        private readonly IMongoCollection<Element> _elements;
        private readonly Files _files;
        private readonly IMongoCollection<Group> _groups;
        private readonly IMongoCollection<User> _users;

        public AccessControlClient(IMongoDatabase database, IMongoCollection<Element> elements, Files files, Directories directories)
        {
            _users = database.GetCollection<User>(nameof(MDBFS) + '.' + nameof(Filesystem) + '.' +
                                                 nameof(AccessControl) +  nameof(_users));
            _groups = database.GetCollection<Group>(nameof(MDBFS) + '.' + nameof(Filesystem) + '.' +
                                                   nameof(AccessControl) +  nameof(_groups));
            this._elements = elements;
            this._files = files;
            this._directories = directories;
            if (!_users.Find(x => x.Username == "").Any()) _users.InsertOne(new User() { Username = "", Role = EUserRole.Admin, MemberOf = new List<string>(), RootDirectory = directories.Root });
            CreateAccessControl(directories.Root, "");

        }

        public User CreateUser(string username, bool admin)
        {
            var search = _users.Find(x => x.Username == username).ToList();
            if (search.Any()) return null;

            var elem = _directories.Create(_directories.Root, username);
            elem = CreateAccessControl(elem.ID, username);

            var usr = new User
            {
                MemberOf = new List<string>(),
                Role = admin ? EUserRole.Admin : EUserRole.User,
                RootDirectory = elem.ID,
                Username = username
            };
            _users.InsertOne(usr);
            return usr;
        }

        public User GetUser(string username)
        {
            var search = _users.Find(x => x.Username == username).ToList();
            if (!search.Any()) return null;
            return search.First();
        }
        public Element CreateAccessControl(string id, string username)
        {
            var search = _elements.Find(x => x.ID == id).ToList();
            if (!search.Any()) return null;
            var element = search.First();
            element.Metadata[nameof(OwnerId)] = username;
            element.Metadata[nameof(OtherUsers)] = 0b00000000;
            element.Metadata[nameof(Groups)] = new Dictionary<string, byte>();
            element.Metadata[nameof(Users)] = new Dictionary<string, byte>();
            _elements.FindOneAndReplace(x => x.ID == id, element);
            return element;
        }

        public Element GetAccessControl(string id)
        {
            var search = _elements.Find(x => x.ID == id).ToList();
            return !search.Any() ? null : search.First();
        }

        public Element RemoveAccessControl(string id)
        {
            var search = _elements.Find(x => x.ID == id).ToList();
            if (!search.Any()) return null;
            var element = search.First();
            if (element.Metadata.ContainsKey(nameof(OwnerId)))
                element.Metadata.Remove(nameof(OwnerId));
            if (element.Metadata.ContainsKey(nameof(OtherUsers)))
                element.Metadata.Remove(nameof(OtherUsers));
            if (element.Metadata.ContainsKey(nameof(Groups)))
                element.Metadata.Remove(nameof(Groups));
            if (element.Metadata.ContainsKey(nameof(Users)))
                element.Metadata.Remove(nameof(Users));
            _elements.FindOneAndReplace(x => x.ID == id, element);
            return element;
        }

        public void ClearRecycleBin(string username)
        {
            foreach (var elem in GetUserRecycleBin(username))
            {
                if (elem.Type == 1) _files.Remove(elem.ID, true);
                else if (elem.Type == 2) _directories.Remove(elem.ID, true);
            }
        }
        public Element[] GetUserRecycleBin(string username)
        {
            var usr = GetUser(username);
            var tmp = GetRemoved(usr.RootDirectory);
            return tmp.Count == 0 ? new Element[0] : tmp.ToArray();
        }

        private List<Element> GetRemoved(string id)
        {
            var res = new List<Element>();
            var search = _elements.Find(x => x.ParentID == id).ToList();
            foreach (var elem in search)
            {
                if (elem.Removed) res.Add(elem);
                else
                {
                    res.AddRange(GetRemoved(elem.ID));
                }
            }

            return res;
        }
        public Group CreateGroup(string name)
        {
            if (_groups.Find(x => x.Name == name).Any()) return null;
            var group = new Group
            {
                Name = name,
                Members = new List<string>()
            };
            _groups.InsertOne(group);
            return group;
        }

        public void RemoveGroup(string groupName)
        {
            if (!_groups.Find(x => x.Name == groupName).Any()) return;

            var filterUsrs = Builders<User>.Filter.Where(x => x.MemberOf.Contains(groupName));
            var updateUsrs = Builders<User>.Update.Pull(x => x.MemberOf, groupName);

            var filter = Builders<Element>.Filter.Where(x =>
                ((Dictionary<string, byte>)x.Metadata[nameof(Groups)]).ContainsKey(groupName));
            var update = Builders<Element>.Update.PullFilter(
                x => (Dictionary<string, byte>)x.Metadata[nameof(Groups)],
                x => x.Key == groupName);
            _users.UpdateMany(filterUsrs, updateUsrs);
            _elements.UpdateMany(filter, update);
            _groups.DeleteOne(x => x.Name == groupName);
        }

        public void RemoveUser(string username)
        {
            var searchUsr = _users.Find(x => x.Username == username).ToList();
            if (!searchUsr.Any()) return;
            var usr = searchUsr.First();
            var filterGroups = Builders<Group>.Filter.Where(x => x.Members.Contains(username));
            var updateGroups = Builders<Group>.Update.Pull(x => x.Members, username);

            var filter = Builders<Element>.Filter.Where(x =>
                ((Dictionary<string, byte>)x.Metadata[nameof(Users)]).ContainsKey(username));
            var update = Builders<Element>.Update.PullFilter(
                x => (Dictionary<string, byte>)x.Metadata[nameof(Users)], x => x.Key == username);

            _groups.UpdateMany(filterGroups, updateGroups);
            _elements.UpdateMany(filter, update);
            _directories.Remove(usr.RootDirectory, true);
        }

        public Group AddUserToGroup(string groupName, string username)
        {
            var search = _groups.Find(x => x.Name == groupName).ToList();
            if (!search.Any()) return null;
            var searchUsr = _users.Find(x => x.Username == username).ToList();
            if (!searchUsr.Any()) return null;

            _users.UpdateOne(x => x.Username == username, Builders<User>.Update.Push(x => x.MemberOf, groupName));
            _groups.UpdateOne(x => x.Name == groupName, Builders<Group>.Update.Push(x => x.Members, username));
            var res = search.First();
            res.Members.Add(username);
            return res;
        }
        public Group AddUserToGroupAddMisssing(string groupName, string username)
        {
            var search = _groups.Find(x => x.Name == groupName).ToList();
            if (!search.Any()) return null;
            var searchUsr = _users.Find(x => x.Username == username).ToList();
            if (!searchUsr.Any())
            {
                var usr = new User()
                {
                    MemberOf = new List<string>(){groupName},
                    Role = EUserRole.User,
                    RootDirectory = "",
                    Username = username,
                };
                _users.InsertOne(usr);
            }
            else
            {
                _users.UpdateOne(x => x.Username == username, Builders<User>.Update.Push(x => x.MemberOf, groupName));
            }
            _groups.UpdateOne(x => x.Name == groupName, Builders<Group>.Update.Push(x => x.Members, username));
            var res = search.First();
            res.Members.Add(username);
            return res;
        }

        public Group RemoveUserFromGroup(string groupName, string username)
        {
            var search = _groups.Find(x => x.Name == groupName).ToList();
            if (!search.Any()) return null;
            var searchUsr = _users.Find(x => x.Username == username).ToList();
            if (!searchUsr.Any()) return null;

            if (searchUsr.First().MemberOf.Contains(groupName))
                _users.UpdateOne(x => x.Username == username, Builders<User>.Update.Pull(x => x.MemberOf, groupName));
            if (search.First().Members.Contains(username))
                _groups.UpdateOne(x => x.Name == groupName, Builders<Group>.Update.Pull(x => x.Members, username));
            var res = search.First();
            if (search.First().Members.Contains(username)) res.Members.Remove(username);
            return res;
        }

        public Element AuthorizeUser(string id, string username, bool changeRights, bool read, bool write, bool execute)
        {
            var search = _elements.Find(x => x.ID == id).ToList();
            if (!search.Any()) return null;
            var elem = search.First();
            byte rights = 0;
            if (changeRights) rights = (byte)(rights | 0b00001000);
            if (read) rights = (byte)(rights | 0b00000100);
            if (write) rights = (byte)(rights | 0b00000010);
            if (execute) rights = (byte)(rights | 0b00000001);
            ((Dictionary<string, byte>)elem.Metadata[nameof(Users)])[username] = rights;
            _elements.FindOneAndReplace(x => x.ID == elem.ID, elem);
            return elem;
        }

        public Element AuthorizeGroup(string id, string groupName, bool changeRights, bool read, bool write, bool execute)
        {
            var search = _elements.Find(x => x.ID == id).ToList();
            if (!search.Any()) return null;
            var elem = search.First();
            byte rights = 0;
            if (changeRights) rights = (byte)(rights | 0b00001000);
            if (read) rights = (byte)(rights | 0b00000100);
            if (write) rights = (byte)(rights | 0b00000010);
            if (execute) rights = (byte)(rights | 0b00000001);
            ((Dictionary<string, byte>)elem.Metadata[nameof(Groups)])[groupName] = rights;
            _elements.FindOneAndReplace(x => x.ID == elem.ID, elem);
            return elem;
        }

        public bool CheckPermissions(string id, string username, bool changeRights, bool read, bool write, bool execute)
        {
            var search = _elements.Find(x => x.ID == id).ToList();
            if (!search.Any()) throw new Exception("Element does not exist");
            var searchUsr = _users.Find(x => x.Username == username).ToList();
            if (!searchUsr.Any()) throw new Exception("User does not exist");
            var elem = search.First();
            var user = searchUsr.First();
            if (user.Role == EUserRole.Admin) return true;
            var elemOwnerId = (string)elem.Metadata[nameof(OwnerId)];
            var elemGroups = (Dictionary<string, byte>)elem.Metadata[nameof(Groups)];
            var elemUsrs = (Dictionary<string, byte>)elem.Metadata[nameof(Users)];
            var otherUsersInt = (int)(elem.Metadata[nameof(OtherUsers)]);
            var otherUsers = (byte)(otherUsersInt);
            var userGroups = user.MemberOf;
            if (elemOwnerId == username) return true;

            var groupUnion = userGroups.Union(elemGroups.Keys);
            var grRights = groupUnion.Aggregate(0b10001111, (current, gr) => (byte)(current & elemGroups[gr]));

            byte rights = (byte)((grRights & 0b1000000) == 0 ? (otherUsers) : grRights);
            if (elemUsrs.ContainsKey(username)) rights = elemUsrs[username];
            if (changeRights)
                if ((rights & 0b00001000) == 0)
                    return false;
            if (read)
                if ((rights & 0b00000100) == 0)
                    return false;
            if (write)
                if ((rights & 0b00000010) == 0)
                    return false;
            if (execute)
                if ((rights & 0b00000001) == 0)
                    return false;

            return true;
        }

        public IEnumerable<Element> ModerateSearch(string username, IEnumerable<Element> searchResult) => searchResult.Where(element => CheckPermissions(element.ID, username, false, true, false, false)).ToList();

        public long CalculateDiskUsage(string username)
        {
            var search = _elements.Find(Builders<Element>.Filter.Where(x =>x.Type==1&& ((string)x.Metadata[nameof(OwnerId)])==username)).ToList();
            if (!search.Any()) return 0;
            long sum = 0;
            foreach (var e in search)
            {

                sum += (long) e.Metadata["Length"];
            }

            return sum;

        }
    }
}