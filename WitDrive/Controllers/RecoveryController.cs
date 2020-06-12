using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using WitDrive.Dto;
using WitDrive.Interfaces;
using WitDrive.Models;
using Microsoft.AspNetCore.Http;
using MDBFS_Lib;
using System.Net.Http;
using System.Net;
using Microsoft.Extensions.Configuration;
using MDBFS.Filesystem;
using MDBFS.Misc;
using Newtonsoft.Json.Linq;
using WitDrive.Infrastructure.Extensions;



namespace WitDrive.Controllers
{
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class RecoveryController : ControllerBase
    {
        private readonly IEmailSender emailSender;
        private readonly UserManager<User> userManager;
        public RecoveryController(IEmailSender emailSender, UserManager<User> userManager)
        {
            this.emailSender = emailSender;
            this.userManager = userManager;
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto forgotPasswordDto)
        {
            var user = await userManager.FindByEmailAsync(forgotPasswordDto.Email);
            if (user == null)
            {
                return NotFound();
            }

            var code = await userManager.GeneratePasswordResetTokenAsync(user);

            var message = new Message(new string[] { forgotPasswordDto.Email }, "Go to https://localhost:8080/passwordReset and enter your code: ", code);
            await emailSender.SendEmailAsync(message);

            return Ok();
        }

        [ValidateAntiForgeryToken]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto resetPasswordDto)
        {
            var user = await userManager.FindByEmailAsync(resetPasswordDto.Email);

            if (user == null)
            {
                return NotFound();
            }

            var result = await userManager.ResetPasswordAsync(user, resetPasswordDto.Code, resetPasswordDto.Password);

            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return Ok();
        }
    }
}
