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
using Newtonsoft.Json.Linq;

namespace WitDrive.Controllers
{
    [Route("api/[controller]")]
    [AllowAnonymous]
    [ApiController]
    public class SharedController : ControllerBase
    {
        private readonly IConfiguration config;
        private readonly IFilesService filesService;
        private readonly FileSystemClient fsc;
        public SharedController(IFilesService filesService, IConfiguration config)
        {
            this.config = config;
            this.filesService = filesService;
            var mongoClient = new MongoDB.Driver.MongoClient(config.GetConnectionString("MongoDbConnection"));
            var database = mongoClient.GetDatabase(nameof(WitDrive));
            this.fsc = new FileSystemClient(database, chunkSize: 32768);
        }

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
                if (!await fsc.AccessControl.CheckPermissionsWithTokenAsync(shareInfo.ElementId, shareInfo.ShareId, false, true, false, false))
                {
                    return Unauthorized();
                }

                var element = await fsc.AccessControl.GetAccessControlAsync(shareInfo.ElementId);

                if (element.Type == 1)
                {
                    JArray jArray = new JArray();
                    jArray.Add(element.FileToJObject());
                    return Ok(jArray.ToString());
                }
                else if(element.Type == 2)
                {
                    JArray jArray = new JArray();
                    var subElments = await fsc.Directories.GetSubelementsAsync(shareInfo.ElementId);
                    foreach (var item in subElments)
                    {

                        if (item.Type == 1)
                        {
                            jArray.Add(item.FileToJObject());
                        }
                    }
                    return Ok(jArray.ToString());
                }
                else
                {
                    throw new Exception("Not possible in this universe");
                }
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException e)
            {
                return NotFound("Element not found");
            }
            catch (Exception e)
            {
                return BadRequest("Failed to retrieve share info");
            }
        }

        //[HttpGet("{shareId}")]
        //public async Task<IActionResult> DownloadSharedFile(string shareId)
        //{
        //    var shareInfo = await filesService.GetByShareId(shareId);

        //    if (shareInfo == null)
        //    {
        //        return BadRequest("Failed to retrieve share info");
        //    }

        //    try
        //    {
        //        if (!await fsc.AccessControl.CheckPermissionsWithTokenAsync(shareInfo.ElementId, shareInfo.ShareId, false, true, false, false))
        //        {
        //            return Unauthorized();
        //        }

        //        var element = await fsc.AccessControl.GetAccessControlAsync(shareInfo.ElementId);

        //        if (element.Type == 1)
        //        {
        //            byte[] file = new byte[0];
        //            using (var stream = await fsc.Files.OpenFileDownloadStreamAsync(element.ID))
        //            {
        //                byte[] buffer = new byte[4096];
        //                int count = await stream.ReadAsync(buffer, 0, buffer.Length);
        //                while (count > 0)
        //                {
        //                    file = file.Append(buffer.SubArray(0, count));
        //                    count = await stream.ReadAsync(buffer, 0, buffer.Length);
        //                }
        //            }

        //            return File(file, MimeTypes.GetMimeType(element.Name), element.Name);
        //        }
        //        else if(element.Type == 2)
        //        {
        //            return BadRequest("Illegal operation");
        //        }
        //        else
        //        {
        //            throw new Exception("Not possible in this universe");
        //        }
        //    }
        //    catch (MDBFS.Exceptions.MdbfsElementNotFoundException)
        //    {
        //        return NotFound("File not found");
        //    }

        //}

        [HttpGet("{shareId}")]
        public async Task<IActionResult> DownloadSharedF(string shareId, [FromQuery] string fileId)
        {
            var shareInfo = await filesService.GetByShareId(shareId);

            if (shareInfo == null)
            {
                return BadRequest("Failed to retrieve share info");
            }

            try
            {

                if (!await fsc.AccessControl.CheckPermissionsWithTokenAsync(fileId, shareInfo.ShareId, false, true, false, false))
                {
                    return Unauthorized();
                }

                var element = await fsc.AccessControl.GetAccessControlAsync(fileId);

                if (element.Type == 1)
                {
                    var (bytes, elem) = fsc.Files.Download(fileId);
                    return File(bytes, MimeTypes.GetMimeType(elem.Name), elem.Name);
                }
                else if (element.Type == 2)
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
            catch (Exception e)
            {
                return BadRequest("Failed to download file");
            }

        }
    }
}
