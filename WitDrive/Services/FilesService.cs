using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WitDrive.Data;
using WitDrive.Interfaces;
using WitDrive.Models;
using Microsoft.EntityFrameworkCore;

namespace WitDrive.Services
{
    public class FilesService : IFilesService
    {
        private readonly DataContext context;
        public FilesService(DataContext context)
        {
            this.context = context;
        }

        public void Add<T>(T entity) where T : class
        {
            context.Add(entity);
        }

        public void Delete<T>(T entity) where T : class
        {
            context.Remove(entity);
        }

        public void Update<T>(T entity) where T : class
        {
            context.Update(entity);
        }

        public async Task<bool> SaveAll()
        {
            return await context.SaveChangesAsync() > 0;
        }

        public async Task<ShareMap> GetByShareId(string shareId)
        {
            var id = await context.ShareLinks.FirstOrDefaultAsync(id => id.ShareId == shareId);
            return id;
        }

        public byte[] ConvertToByteArray(IFormFile file)
        {
            List<byte> bytes = new List<byte>();
            using (var stream = file.OpenReadStream())
            {
                byte[] buffor = new byte[128];
                var length = stream.Read(buffor, 0, buffor.Length);
                while (length > 0)
                {
                    for (int i = 0; i < length; i++) bytes.Add(buffor[i]);
                    length = stream.Read(buffor, 0, buffor.Length);
                }
                return bytes.ToArray();
            }
        }
    }
}
