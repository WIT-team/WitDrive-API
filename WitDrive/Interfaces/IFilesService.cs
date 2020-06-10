using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WitDrive.Models;

namespace WitDrive.Interfaces
{
    public interface IFilesService
    {
        public byte[] ConvertToByteArray(IFormFile file);
        void Add<T>(T entity) where T : class;
        void Delete<T>(T entity) where T : class;
        void Update<T>(T entity) where T : class;
        Task<ShareMap> GetByShareId(string shareId);
        Task<bool> SaveAll();
    }
}
