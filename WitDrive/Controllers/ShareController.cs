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
    public class ShareController : ControllerBase
    {
        private readonly IConfiguration config;
        private readonly IFilesService filesService;
        private readonly FileSystemClient fsc;
        private readonly long space;
        public ShareController(IFilesService filesService, IConfiguration config)
        {
            this.config = config;
            this.filesService = filesService;
            var mongoClient = new MongoDB.Driver.MongoClient(config.GetConnectionString("MongoDbConnection"));
            var database = mongoClient.GetDatabase(nameof(WitDrive));
            this.fsc = new FileSystemClient(database, chunkSize: 32768);
            this.space = long.Parse(config.GetSection("DiskSpace").GetSection("Space").Value);
        }

        [HttpPatch("enable")]
        public async Task<IActionResult> EnableElementSharing(int userId, [FromQuery] string elementId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            try
            {
                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(elementId, userId.ToString(), true, false, false, false))
                {
                    return Unauthorized();
                }
                var shareId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                shareId = shareId.Replace("/", "t");
                var fileInfo = await fsc.AccessControl.AuthorizeTokenAsync(elementId, shareId, true, true, true);

                if (fileInfo.Type == 2)
                {
                    var subElems = await fsc.Directories.GetSubelementsAsync(fileInfo.ID);
                    foreach (var item in subElems)
                    {
                        if (item.Type == 2)
                        {
                            continue;
                        }
                     
                        var itemInfo = await fsc.AccessControl.AuthorizeTokenAsync(item.ID, shareId, true, true, true);
                        await fsc.Files.SetCustomMetadataAsync(item.ID, "ShareID", String.Empty);
                        await fsc.Files.SetCustomMetadataAsync(item.ID, "Shared", false);
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

                await fsc.Files.SetCustomMetadataAsync(elementId, "ShareID", shareId);
                await fsc.Files.SetCustomMetadataAsync(elementId, "Shared", true);

                if (await filesService.SaveAll())
                {
                    return Ok(shareId);
                }

                return BadRequest("Failed to share element");
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException)
            {
                return NotFound("Unknown element id");
            }
            catch (Exception)
            {
                return BadRequest("Failed to share element");
            }
        }

        [HttpPatch("disable")]
        public async Task<IActionResult> DisableElementSharing(int userId, [FromQuery] string elementId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            try
            {
                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(elementId, userId.ToString(), true, false, false, false))
                {
                    return Unauthorized();
                }

                var fileInfo = await fsc.Files.GetAsync(elementId);
                var shrId = (string)fileInfo.CustomMetadata["ShareID"];
                fileInfo = await fsc.AccessControl.AuthorizeTokenAsync(elementId, shrId, false, false, false);

                if (fileInfo.Type == 2)
                {
                    var subElems = await fsc.Directories.GetSubelementsAsync(fileInfo.ID);
                    foreach (var item in subElems)
                    {
                        if (item.Type == 2)
                        {
                            continue;
                        }
                        var itemInfo = await fsc.AccessControl.AuthorizeTokenAsync(item.ID, shrId, false, false, false);

                        await fsc.Files.SetCustomMetadataAsync(item.ID, "ShareID", String.Empty);
                        await fsc.Files.SetCustomMetadataAsync(item.ID, "Shared", false);
                    }
                }
                await fsc.Files.SetCustomMetadataAsync(elementId, "ShareID", String.Empty);
                await fsc.Files.SetCustomMetadataAsync(elementId, "Shared", false);

                var shrMapFile = await filesService.GetByShareId(shrId);
                filesService.Delete<ShareMap>(shrMapFile);


                if (await filesService.SaveAll())
                {
                    return Ok();
                }

                return BadRequest("Failed to disable element sharing");
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException)
            {
                return BadRequest("Unknown element id");
            }
            catch (Exception)
            {
                return BadRequest("Failed to disable element sharing");
            }
        }

        [HttpGet("shared-list")]
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
    }
}
