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
using MDBFS.Filesystem;
using WitDrive.Infrastructure.Extensions;
using MDBFS.Misc;

namespace WitDrive.Controllers
{
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class SharedController : ControllerBase
    {
        private readonly IConfiguration config;
        private readonly IFilesService filesService;
        //private readonly FileRepository repo;
        private readonly FileSystemClient fsc;
        public SharedController(IFilesService filesService, IConfiguration config)
        {
            this.config = config;
            this.filesService = filesService;
            //this.repo = new FileRepository(config.GetConnectionString("MongoDbConnection"));
            var mongoClient = new MongoDB.Driver.MongoClient(config.GetConnectionString("MongoDbConnection"));
            var database = mongoClient.GetDatabase(nameof(WitDrive));
            this.fsc = new FileSystemClient(database, chunkSize: 32768);
        }

        //[HttpGet("info/{shareId}")]
        //public async Task<IActionResult> GetSharedFile(string shareId)
        //{
        //    var res = await repo.GetFileFromShareAsync(null, shareId);

        //    if (res.success)
        //    {
        //        return Ok(res.result);
        //    }

        //    return BadRequest("Failed to retrieve file info");
        //}

        [HttpGet("info/{shareId}")]
        public async Task<IActionResult> GetSharedInfo(string shareId)
        {

            var shareInfo = await filesService.GetByShareId(shareId);

            if (!shareInfo.Active)
            {
                return BadRequest("Failed to retrieve share info");
            }

            try
            {
                var perm = await fsc.AccessControl.CheckPermissionsWithTokenAsync(shareInfo.ElementId, shareInfo.ShareId, false, true, false, false);
                if (!perm)
                {
                    return Unauthorized();
                }

                var element = await fsc.AccessControl.GetAccessControlAsync(shareInfo.ElementId);

                if (element.Type == 1)
                {
                    return Ok(element.FileToJson());
                }
                else if(element.Type == 2)
                {
                    var subElments = await fsc.Directories.GetSubelementsAsync(shareInfo.ElementId);
                    return Ok(element.DirToJson(subElments));
                }
                else
                {
                    throw new Exception("Not possible in this universe");
                }
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException)
            {
                return BadRequest("Failed to retrieve share info");
            }
            catch (Exception)
            {
                return BadRequest("Failed to retrieve share info");
            }
        }

        [HttpGet("{shareId}")]
        public async Task<IActionResult> DownloadSharedFile(string shareId)
        {
            var shareInfo = await filesService.GetByShareId(shareId);

            if (!shareInfo.Active)
            {
                return BadRequest("Failed to retrieve share info");
            }

            try
            {
                var perm = await fsc.AccessControl.CheckPermissionsWithTokenAsync(shareInfo.ElementId, shareInfo.ShareId, false, true, false, false);
                if (!perm)
                {
                    return Unauthorized();
                }

                var element = await fsc.AccessControl.GetAccessControlAsync(shareInfo.ElementId);

                if (element.Type == 1)
                {
                    byte[] file = new byte[0];
                    using (var stream = await fsc.Files.OpenFileDownloadStreamAsync(element.ID))
                    {
                        byte[] buffer = new byte[4096];
                        int count = await stream.ReadAsync(buffer, 0, buffer.Length);
                        while (count > 0)
                        {
                            file = file.Append(buffer.SubArray(0, count));
                            count = await stream.ReadAsync(buffer, 0, buffer.Length);
                        }
                    }

                    return File(file, MimeTypes.GetMimeType(element.Name), element.Name);
                }
                else if(element.Type == 2)
                {
                    return BadRequest("Illegal operation");
                }
                else
                {
                    throw new Exception("Not possible in this universe");
                }
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException)
            {
                return NotFound("File not found");
            }

        }

    }
}
