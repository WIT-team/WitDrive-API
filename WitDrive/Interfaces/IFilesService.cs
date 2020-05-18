using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WitDrive.Interfaces
{
    public interface IFilesService
    {
        public byte[] ConvertToByteArray(IFormFile file);
    }
}
