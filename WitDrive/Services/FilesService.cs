using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WitDrive.Interfaces;

namespace WitDrive.Services
{
    public class FilesService : IFilesService
    {
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
