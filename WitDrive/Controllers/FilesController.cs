using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using WitDrive.Interfaces;
using MDBFS_Lib;
using System.Net.Http;
using System.Net;
using Microsoft.Extensions.Configuration;
using MDBFS.Filesystem;
using MDBFS.Misc;
using Newtonsoft.Json.Linq;
using WitDrive.Infrastructure.Extensions;
using WitDrive.Models;

namespace WitDrive.Controllers
{
    [Route("api/u/{userId}/[controller]")]
    [ApiController]
    [Authorize]
    public class FilesController : ControllerBase
    {
        private readonly IConfiguration config;
        private readonly IFilesService filesService;
        //readonly FileRepository repo;
        private readonly FileSystemClient fsc;
        public FilesController(IFilesService filesService, IConfiguration config)
        {
            this.config = config;
            this.filesService = filesService;
            //this.repo = new FileRepository(config.GetConnectionString("MongoDbConnection"));
            var mongoClient = new MongoDB.Driver.MongoClient(config.GetConnectionString("MongoDbConnection"));
            var database = mongoClient.GetDatabase(nameof(WitDrive));
            this.fsc = new FileSystemClient(database, chunkSize: 32768);
        }

        [HttpGet("root")]
        public async Task<IActionResult> GetRootDir(int userId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            try
            {
                var usr = fsc.AccessControl.GetUser(userId.ToString());
                var dir = await fsc.Directories.GetAsync(usr.RootDirectory);
                var subDirs = await fsc.Directories.GetSubelementsAsync(usr.RootDirectory);
                return Ok(dir.DirToJson(subDirs));
            }
            catch (Exception)
            {
                return BadRequest("Failed to retrieve root data");
            }

        }

        [HttpPost("upload")]
        public async Task<IActionResult> FileUpload(int userId, string directoryId, [FromForm] IFormFile file)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            try
            {
                var perm = fsc.AccessControl.CheckPermissionsWithUsername(directoryId, userId.ToString(), false, true, true, false);
                if (!perm)
                {
                    return Unauthorized();
                }

                var f = await fsc.Files.CreateAsync(directoryId, file.FileName, filesService.ConvertToByteArray(file));

                fsc.AccessControl.CreateAccessControl(f.ID, userId.ToString());

                return Ok();
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException)
            {
                return BadRequest("Directory not found");
            }
            catch (Exception)
            {
                return BadRequest("Failed to upload file");
            }
        }

        [HttpGet("download/{fileId}")]
        public async Task<IActionResult> FileDownload(int userId, string fileId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            try
            {
                if (fsc.AccessControl.CheckPermissionsWithUsername(fileId, userId.ToString(), false, true, false, true))
                {
                    return Unauthorized();
                }

                byte[] file = new byte[0];
                using (var stream = await fsc.Files.OpenFileDownloadStreamAsync(fileId))
                {
                    byte[] buffer = new byte[4096];
                    int count = await stream.ReadAsync(buffer, 0, buffer.Length);
                    while (count > 0)
                    {
                        file = file.Append(buffer.SubArray(0, count));
                        count = await stream.ReadAsync(buffer, 0, buffer.Length);
                    }
                }

                var fileInfo = await fsc.Files.GetAsync(fileId);

                return File(file, MimeTypes.GetMimeType(fileInfo.Name), fileInfo.Name);
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException)
            {
                return NotFound("File not found");
            }

        }

        //[HttpPost("download/{fileId}")]
        //public async Task<IActionResult> FileDownloadTwo(int userId, string fileId)
        //{
        //    if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
        //    {
        //        return Unauthorized();
        //    }

        //    MDBFS_Lib.Util.AsyncResult<KeyValuePair<string, byte[]>?> res = await repo.DownloadFileAsync(Convert.ToString(userId), fileId);

        //    if (res.success)
        //    {
        //        string fileName = res.result.Value.Key;
        //        byte[] data = res.result.Value.Value;
        //        return File(data, MimeTypes.GetMimeType(fileName), fileName);
        //    }

        //    return BadRequest("Failed to download file");
        //}


        [HttpPatch("share/{fileId}")]
        public async Task<IActionResult> EnableFileSharing(int userId, string fileId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            try
            {
                var perm = fsc.AccessControl.CheckPermissionsWithUsername(fileId, userId.ToString(), true, false, false, false);
                if (!perm)
                {
                    return Unauthorized();
                }
                var shareId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                var fileInfo = fsc.AccessControl.AuthorizeToken(fileId, shareId, true, true, true);

                ShareMap f = new ShareMap();
                f.ElementId = fileInfo.ID;
                f.Type = fileInfo.Type;
                f.ShareId = shareId;
                f.Active = true;

                filesService.Add<ShareMap>(f);

                await fsc.Files.SetCustomMetadataAsync(fileId, "ShareID", shareId);
                await fsc.Files.SetCustomMetadataAsync(fileId, "Shared" , true);

                if (await filesService.SaveAll())
                {
                    return Ok(shareId);
                }

                return BadRequest("Failed to share file");
            }
            catch(MDBFS.Exceptions.MdbfsElementNotFoundException)
            {
                return BadRequest("Unknown file id");
            }
            catch (Exception)
            {
                return BadRequest("Failed to share file");
            }
        }

        [HttpPatch("disable-sharing/{fileId}")]
        public async Task<IActionResult> DisableFileSharing(int userId, string fileId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            try
            {
                var perm = fsc.AccessControl.CheckPermissionsWithUsername(fileId, userId.ToString(), true, false, false, false);
                if (!perm)
                {
                    return Unauthorized();
                }
                var fileInfo = await fsc.Files.GetAsync(fileId);
                var shrId = (string)fileInfo.CustomMetadata["ShareID"];
                fileInfo = fsc.AccessControl.AuthorizeToken(fileId, shrId, false, false, false);

                await fsc.Files.SetCustomMetadataAsync(fileId, "ShareID", String.Empty);
                await fsc.Files.SetCustomMetadataAsync(fileId, "Shared", false);

                ShareMap f = new ShareMap();
                f.ElementId = fileInfo.ID;
                f.Type = fileInfo.Type;
                f.ShareId = shrId;
                f.Active = false;

                filesService.Update<ShareMap>(f);

                if (await filesService.SaveAll())
                {
                    return Ok();
                }

                return BadRequest("Failed to disable file sharing");
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException)
            {
                return BadRequest("Unknown file id");
            }
            catch (Exception)
            {
                return BadRequest("Failed to disable file sharing");
            }
        }

        [HttpDelete("{fileId}")]
        public async Task<IActionResult> FileDelete(int userId, string fileId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }
            try
            {
                var perm = fsc.AccessControl.CheckPermissionsWithUsername(fileId, userId.ToString(), false, false, true, false);
                if (!perm)
                {
                    return Unauthorized();
                }
                await fsc.Files.RemoveAsync(fileId, false);
                return Ok();
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException)
            {
                return BadRequest("Unknown file id");
            }
            catch (Exception)
            {
                return BadRequest("Failed to delete file");
            }
        }

        [HttpGet("get-shared-list")]
        public async Task<IActionResult> GetSharedList(int userId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }
            try
            {
                var usr = fsc.AccessControl.GetUser(userId.ToString());
                var perm = fsc.AccessControl.CheckPermissionsWithUsername(usr.RootDirectory, userId.ToString(), false, false, true, false);
                if (!perm)
                {
                    return Unauthorized();
                }
                var query = new ElementSearchQuery();
                query.CustomMetadata.Add(("Shared", ESearchCondition.Eq, true));
                var search = await fsc.Directories.FindAsync(usr.RootDirectory, query);
                var trueSearch = fsc.AccessControl.ModerateSearch(userId.ToString(), search);

                JArray jArray = new JArray();
                foreach (var item in trueSearch)
                {
                    jArray.Add(item.ElementToJObject());
                }
                return Ok(jArray.ToString());
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException)
            {
                return BadRequest("Failed to get shared list");
            }
            catch (Exception)
            {
                return BadRequest("Failed to get shared list");
            }
        }

        [HttpGet("get-shared-file/{shareId}")]
        public async Task<IActionResult> GetSharedFile(int userId, string shareId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }
            
            var res = await repo.GetFileFromShareAsync(Convert.ToString(userId), shareId);

            if (res.success)
            {
                return Ok(res.result);
            }

            return BadRequest("Failed to retrieve file info");
        }

    }
}
