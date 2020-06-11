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
    public class DirController : ControllerBase
    {
        private readonly IConfiguration config;
        private readonly IDirService dirService;
        private readonly FileSystemClient fsc;
        private readonly long space;
        public DirController(IDirService dirService, IConfiguration config)
        {
            this.config = config;
            this.dirService = dirService;
            var mongoClient = new MongoDB.Driver.MongoClient(config.GetConnectionString("MongoDbConnection"));
            var database = mongoClient.GetDatabase(nameof(WitDrive));
            this.fsc = new FileSystemClient(database, chunkSize: 32768);
            this.space = long.Parse(config.GetSection("DiskSpace").GetSection("Space").Value);
        }

        [HttpGet("space")]
        public async Task<IActionResult> Space(int userId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var usedDiskSpace = await fsc.AccessControl.CalculateDiskUsageAsync(userId.ToString());

            JObject jObject = new JObject();
            jObject["Used"] = usedDiskSpace;
            jObject["Available"] = space;
            jObject["Left"] = space - usedDiskSpace;

            return Ok(jObject.ToString());
        }

        [HttpPost("create/{dirId}/{name}")]
        public async Task<IActionResult> CreateDirectory(string name, string dirId, int userId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            try
            {
                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(dirId, userId.ToString(), false, true, true, true))
                {
                    return Unauthorized();
                }

                var dir = await fsc.Directories.CreateAsync(dirId, name);

                await fsc.AccessControl.CreateAccessControlAsync(dir.ID, userId.ToString());

                await fsc.Files.SetCustomMetadataAsync(dir.ID, "ShareID", String.Empty);
                await fsc.Files.SetCustomMetadataAsync(dir.ID, "Shared", true);

                return Ok();
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException e)
            {
                return BadRequest("Directory not found");
            }
            catch (Exception e)
            {
                return BadRequest("Failed to create new directory");
            }
        }

        [HttpGet("{dirId}")]
        public async Task<IActionResult> GetDirectory(string name, string dirId, int userId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            try
            {
                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(dirId, userId.ToString(), false, true, false, true))
                {
                    return Unauthorized();
                }

                var dir = await fsc.Directories.GetAsync(dirId);
                var subDirs = await fsc.Directories.GetSubelementsAsync(dirId);
                return Ok(dir.DirToJson(subDirs));
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException e)
            {
                return BadRequest("Directory not found");
            }
            catch (Exception e)
            {
                return BadRequest("Failed to retrieve directory data");
            }
        }

        [HttpPatch("rename/{dirId}/{name}")]
        public async Task<IActionResult> RenameDirectory(string name, string dirId, int userId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            if (name.Length <= 0)
            {
                return BadRequest("Directory name is too short");
            }

            if (dirId == fsc.Directories.Root)
            {
                return Unauthorized();
            }

            try
            {
                var tmp = await fsc.Directories.GetAsync(dirId);
                if (tmp.ParentID == fsc.Directories.Root)
                {
                    return BadRequest("Can not change root directory name");
                }

                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(dirId, userId.ToString(), false, true, true, false))
                {
                    return Unauthorized();
                }

                await fsc.Directories.RenameAsync(dirId, name);

                return Ok();
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException e)
            {
                return BadRequest("Directory not found");
            }
            catch (Exception e)
            {
                return BadRequest("Failed to rename directory");
            }
        }

        [HttpDelete("{dirId}")]
        public async Task<IActionResult> DeleteDirectory(string dirId, int userId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }
            if (dirId == fsc.Directories.Root)
            {
                return Unauthorized();
            }
            try
            {
                var tmp = await fsc.Directories.GetAsync(dirId);
                if (tmp.ParentID == fsc.Directories.Root)
                {
                    return BadRequest("Can not delete root directory");
                }

                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(dirId, userId.ToString(), false, true, true, true))
                {
                    return Unauthorized();
                }

                await fsc.Directories.RemoveAsync(dirId, true);

                return Ok();
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException e)
            {
                return BadRequest("Directory not found");
            }
            catch (Exception e)
            {
                return BadRequest("Failed to delete directory");
            }
        }

        [HttpPatch("move")]
        public async Task<IActionResult> MoveDirectory([FromQuery] string srcDirId, [FromQuery] string dstDirId, int userId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            if (srcDirId == fsc.Directories.Root)
            {
                return Unauthorized();
            }

            if (dstDirId == fsc.Directories.Root)
            {
                return Unauthorized();
            }

            try
            {
                var tmp = await fsc.Directories.GetAsync(srcDirId);
                if (tmp.ParentID == fsc.Directories.Root)
                {
                    return BadRequest("Can not move root directory");
                }

                if (!await fsc.AccessControl.CheckPermissionsWithUsernameAsync(srcDirId, userId.ToString(), false, true, true, false)
                    && !await fsc.AccessControl.CheckPermissionsWithUsernameAsync(dstDirId, userId.ToString(), false, true, true, true))
                {
                    return Unauthorized();
                }

                var tmpDeb = await fsc.Directories.MoveAsync(srcDirId, dstDirId);

                return Ok();
            }
            catch (MDBFS.Exceptions.MdbfsElementNotFoundException e)
            {
                return BadRequest("Directory not found");
            }
            catch (Exception e)
            {
                return BadRequest("Failed to move directory");
            }
        }
    }
}