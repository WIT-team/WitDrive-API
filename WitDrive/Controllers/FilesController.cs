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
        private readonly FileSystemClient fsc;
        private readonly long space;
        public FilesController(IFilesService filesService, IConfiguration config)
        {
            this.config = config;
            this.filesService = filesService;
            var mongoClient = new MongoDB.Driver.MongoClient(config.GetConnectionString("MongoDbConnection"));
            var database = mongoClient.GetDatabase(nameof(WitDrive));
            this.fsc = new FileSystemClient(database, chunkSize: 32768);
            this.space = long.Parse(config.GetSection("DiskSpace").GetSection("Space").Value);
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
                var usr = await fsc.AccessControl.GetUserAsync(userId.ToString());
                var dir = await fsc.Directories.GetAsync(usr.RootDirectory);
                var subDirs = await fsc.Directories.GetSubelementsAsync(usr.RootDirectory);

                return Ok(dir.DirToJson(subDirs));
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException e)
            {
                return BadRequest("Directory not found");
            }
            catch (Exception e)
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
                byte[] data = filesService.ConvertToByteArray(file);

                if (await fsc.AccessControl.CalculateDiskUsageAsync(userId.ToString()) + data.Length > space)
                {
                    return Unauthorized("Not enough space");
                }
                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(directoryId, userId.ToString(), false, true, true, false))
                {
                    return Unauthorized();
                }

                var f = await fsc.Files.CreateAsync(directoryId, file.FileName, data);

                await fsc.AccessControl.CreateAccessControlAsync(f.ID, userId.ToString());
                var r1 = await fsc.Directories.SetCustomMetadataAsync(f.ID, "Shared", false);
                var r2 = await fsc.Directories.SetCustomMetadataAsync(f.ID, "ShareID", String.Empty);

                return Ok();
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException e)
            {
                return BadRequest("Directory not found");
            }
            catch (Exception e)
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
                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(fileId, userId.ToString(), false, true, false, false))
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

        [HttpPatch("rename/{fileId}")]
        public async Task<IActionResult> RenameFile(int userId, string fileId, [FromQuery] string name)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            try
            {                       
                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(fileId, userId.ToString(), false, true, true, false))
                {
                    return Unauthorized();
                }

                var tmpDeb = await fsc.Files.RenameAsync(fileId, name);

                return Ok();
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException e)
            {
                return BadRequest("File not found");
            }
            catch (Exception e)
            {
                return BadRequest("Failed to change file name");
            }

        }

        [HttpPatch("move")]
        public async Task<IActionResult> MoveFile([FromQuery] string fileId, [FromQuery] string dirId, int userId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }
            if (dirId == fsc.Directories.Root)
            {
                return BadRequest("Invalid operation");
            }
            try
            {
                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(fileId, userId.ToString(), false, true, true, false))
                {
                    return Unauthorized();
                }

                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(dirId, userId.ToString(), false, true, true, true))
                {
                    return Unauthorized();
                }

                var tmpDeb = await fsc.Files.MoveAsync(fileId, dirId);
                return Ok();
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException e)
            {
                return BadRequest("File not found");
            }
            catch (Exception e)
            {
                return BadRequest("Failed to move file");
            }
        }

        [HttpPut("copy")]
        public async Task<IActionResult> CopyFile([FromQuery] string fileId, [FromQuery] string dirId, int userId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }
            if (dirId == fsc.Directories.Root)
            {
                return BadRequest("Invalid operation");
            }
            try
            {
                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(fileId, userId.ToString(), false, true, true, false))
                {
                    return Unauthorized();
                }

                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(dirId, userId.ToString(), false, true, true, true))
                {
                    return Unauthorized();
                }

                var file = await fsc.Files.GetAsync(fileId);
                var length = (long)file.Metadata[nameof(EMetadataKeys.Length)];

                if (await fsc.AccessControl.CalculateDiskUsageAsync(userId.ToString()) + length > space)
                {
                    return Unauthorized("Not enough space");
                }

                var deb = await fsc.Files.CopyAsync(fileId, dirId);

                return Ok();
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException e)
            {
                return BadRequest("File not found");
            }
            catch (Exception e)
            {
                return BadRequest("Failed to move file");
            }
        }

        [HttpPatch("share/{fileId}")]
        public async Task<IActionResult> EnableFileSharing(int userId, string fileId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            try
            {
                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(fileId, userId.ToString(), true, false, false, false))
                {
                    return Unauthorized();
                }
                var shareId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                var fileInfo = await fsc.AccessControl.AuthorizeTokenAsync(fileId, shareId, true, true, true);

                if (fileInfo.Type == 2)
                {
                    var subElems = await fsc.Directories.GetSubelementsAsync(fileInfo.ID);
                    foreach (var item in subElems)
                    {
                        var itemShareId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                        var itemInfo = await fsc.AccessControl.AuthorizeTokenAsync(item.ID, itemShareId, true, true, true);

                        ShareMap tmp0 = new ShareMap();
                        tmp0.ElementId = item.ID;
                        tmp0.Type = item.Type;
                        tmp0.ShareId = itemShareId;
                        tmp0.Active = true;

                        filesService.Add<ShareMap>(tmp0);

                        await fsc.Files.SetCustomMetadataAsync(item.ID, "ShareID", itemShareId);
                        await fsc.Files.SetCustomMetadataAsync(item.ID, "Shared", true);
                    }
                }

                ShareMap f = new ShareMap()
                {
                    ElementId = fileInfo.ID,
                    Type = fileInfo.Type,
                    ShareId = shareId,
                    Active = true
                };

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
                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(fileId, userId.ToString(), true, false, false, false))
                {
                    return Unauthorized();
                }

                var fileInfo = await fsc.Files.GetAsync(fileId);
                var shrId = (string)fileInfo.CustomMetadata["ShareID"];
                fileInfo = await fsc.AccessControl.AuthorizeTokenAsync(fileId, shrId, false, false, false);

                if (fileInfo.Type == 2)
                {
                    var subElems = await fsc.Directories.GetSubelementsAsync(fileInfo.ID);
                    foreach (var item in subElems)
                    {
                        var cstMta = (string)item.CustomMetadata["ShareID"];
                        var itemInfo = await fsc.AccessControl.AuthorizeTokenAsync(item.ID, cstMta, false, false, false);

                        ShareMap tmp = new ShareMap()
                        {
                            ElementId = item.ID,
                            Type = item.Type,
                            ShareId = cstMta,
                            Active = false
                        };

                        filesService.Update<ShareMap>(tmp);

                        await fsc.Files.SetCustomMetadataAsync(item.ID, "ShareID", String.Empty);
                        await fsc.Files.SetCustomMetadataAsync(item.ID, "Shared", false);
                    }
                }
                await fsc.Files.SetCustomMetadataAsync(fileId, "ShareID", String.Empty);
                await fsc.Files.SetCustomMetadataAsync(fileId, "Shared", false);


                ShareMap f = new ShareMap()
                {
                    ElementId = fileInfo.ID,
                    Type = fileInfo.Type,
                    ShareId = shrId,
                    Active = false
                };

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
                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(fileId, userId.ToString(), false, false, true, false))
                {
                    return Unauthorized();
                }

                await fsc.Files.RemoveAsync(fileId, true); //todo

                return Ok();
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException)
            {
                return BadRequest("Unknown file id");
            }
            catch (Exception e)
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
                var usr = await fsc.AccessControl.GetUserAsync(userId.ToString());

                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(usr.RootDirectory, userId.ToString(), false, true, false, false))
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
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException e)
            {
                return BadRequest("Failed to get shared list");
            }
            catch (Exception e)
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

            var shareInfo = await filesService.GetByShareId(shareId);

            if (!shareInfo.Active)
            {
                return BadRequest("Failed to retrieve file info");
            }

            try
            {
                var usr = await fsc.AccessControl.GetUserAsync(userId.ToString());

                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(usr.RootDirectory, userId.ToString(), false, true, false, false))
                {
                    return Unauthorized();
                }

                var query = new ElementSearchQuery();
                query.CustomMetadata.Add(("ShareID", ESearchCondition.Eq, shareInfo.ElementId));
                var search = await fsc.Directories.FindAsync(usr.RootDirectory, query);
                var trueSearch = fsc.AccessControl.ModerateSearch(userId.ToString(), search);

                if (trueSearch.Count() == 0)
                {
                    return BadRequest("Failed to retrieve file info");
                }
                else
                {
                    var element = trueSearch.First();
                    if (element.Type == 2)
                    {
                        return BadRequest("Failed to retrieve file info");
                    }
                    return Ok(element.FileToJson());
                }
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException)
            {
                return BadRequest("Failed to retrieve file info");
            }
            catch (Exception)
            {
                return BadRequest("Failed to retrieve file info");
            }
        }

    }
}
