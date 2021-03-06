﻿using Microsoft.AspNetCore.Authorization;
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
                var parent = await fsc.AccessControl.GetAccessControlAsync(directoryId);
                if((bool)parent.CustomMetadata["Shared"])
                {
                    await fsc.AccessControl.AuthorizeTokenAsync(f.ID, (string)parent.CustomMetadata["ShareID"], true, true, true);
                }
                await fsc.AccessControl.CreateAccessControlAsync(f.ID, userId.ToString());
                var r1 = await fsc.Directories.SetCustomMetadataAsync(f.ID, "Shared", false);
                var r2 = await fsc.Directories.SetCustomMetadataAsync(f.ID, "ShareID", String.Empty);

                return Ok();
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException e)
            {
                return NotFound("Directory not found");
            }
            catch (Exception e)
            {
                return BadRequest("Failed to upload file");
            }
        }

        [HttpGet("{fileId}")]
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
                var (bytes, elem) = fsc.Files.Download(fileId);
                return File(bytes, MimeTypes.GetMimeType(elem.Name), elem.Name);
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException)
            {
                return NotFound("File not found");
            }

        }

        [HttpPatch("rename")]
        public async Task<IActionResult> RenameFile(int userId,[FromQuery] string fileId, [FromQuery] string name)
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
                return NotFound("File not found");
            }
            catch (Exception e)
            {
                return BadRequest("Failed to change file name");
            }

        }

        [HttpPatch("move")]
        public async Task<IActionResult> MoveFile(int userId, [FromQuery] string fileId, [FromQuery] string dirId)
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

                var fileNew = await fsc.Files.MoveAsync(fileId, dirId);
                var parent = await fsc.AccessControl.GetAccessControlAsync(fileNew.ParentID);
                if ((bool)parent.CustomMetadata["Shared"])
                {
                    await fsc.AccessControl.AuthorizeTokenAsync(fileNew.ID, (string)parent.CustomMetadata["ShareID"], true, true, true);
                }
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
        public async Task<IActionResult> CopyFile(int userId, [FromQuery] string fileId, [FromQuery] string dirId)
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

                var fileNew = await fsc.Files.CopyAsync(fileId, dirId);
                var parent = await fsc.AccessControl.GetAccessControlAsync(fileNew.ParentID);
                if ((bool)parent.CustomMetadata["Shared"])
                {
                    await fsc.AccessControl.AuthorizeTokenAsync(fileNew.ID, (string)parent.CustomMetadata["ShareID"], true, true, true);
                }
                return Ok(fileNew.FileToJson());
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
                return NotFound("Unknown file id");
            }
            catch (Exception e)
            {
                return BadRequest("Failed to delete file");
            }
        }

    }
}
