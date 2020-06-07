using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MDBFS_Lib;
using WitDrive.Interfaces;
using Json.Net;
using WitDrive.Models;
using Newtonsoft.Json;

namespace WitDrive.Controllers
{
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class SharedController : ControllerBase
    {
        private readonly IConfiguration config;
        private readonly IFilesService filesService;
        private readonly FileRepository repo;
        public SharedController(IFilesService filesService, IConfiguration config)
        {
            this.config = config;
            this.filesService = filesService;
            this.repo = new FileRepository(config.GetConnectionString("MongoDbConnection"));
        }

        [HttpGet("file-info/{shareId}")]
        public async Task<IActionResult> GetSharedFile(string shareId)
        {
            var res = await repo.GetFileFromShareAsync(null, shareId);

            if (res.success)
            {
                return Ok(res.result);
            }

            return BadRequest("Failed to retrieve file info");
        }

        [HttpGet("{shareId}")]
        public async Task<IActionResult> DownloadSharedFile(string shareId)
        {
            var res = await repo.GetFileFromShareAsync(null, shareId);
            if (res.success)
            {
                var items = JsonConvert.DeserializeObject<SharedFile>(res.result);
                var fileId = items.ID;

                MDBFS_Lib.Util.AsyncResult<KeyValuePair<string, byte[]>?> downloadRes = await repo.DownloadFileAsync(null, fileId);

                if (downloadRes.success)
                {
                    string fileName = downloadRes.result.Value.Key;
                    byte[] data = downloadRes.result.Value.Value;
                    return File(data, MimeTypes.GetMimeType(fileName), fileName);
                }
            }

            return BadRequest("This file does not exist or is not shared");
        }

    }
}
