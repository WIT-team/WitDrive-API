using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WitDrive.Dto;
using WitDrive.Interfaces;
using WitDrive.Models;

namespace WitDrive.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly IEmailSender emailSender;
        private readonly UserManager<User> userManager;

        public AccountController(IEmailSender emailSender, UserManager<User> userManager)
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

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var callback = Url.RouteUrl("ResetPasswordModel", new { token, email = user.Email }, Request.Scheme);

            var message = new Message(new string[] { forgotPasswordDto.Email }, "Link to reset your password", callback);
            await emailSender.SendEmailAsync(message);

            return Ok();
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> ResetPassword(ResetPasswordDto resetPasswordDto)
        //{

        //    var user = await userManager.FindByEmailAsync(resetPasswordDto.Email);
        //    if (user == null)
        //    {
        //        return NotFound();
        //    }

        //    var result = await userManager.ResetPasswordAsync(user, resetPasswordDto.Token, resetPasswordDto.Password);
        //    if (!result.Succeeded)
        //    {
        //        return BadRequest(result.Errors);
        //    }

        //    return Ok();
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPasswordxD(ResetPasswordDto resetPasswordModel)
        {
            if (!ModelState.IsValid)
                return View(resetPasswordModel);

            var user = await userManager.FindByEmailAsync(resetPasswordModel.Email);
            if (user == null)
                RedirectToAction(nameof(ResetPasswordConfirmation));

            var resetPassResult = await userManager.ResetPasswordAsync(user, resetPasswordModel.Token, resetPasswordModel.Password);
            if (!resetPassResult.Succeeded)
            {
                foreach (var error in resetPassResult.Errors)
                {
                    ModelState.TryAddModelError(error.Code, error.Description);
                }

                return View();
            }

            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }


        [HttpGet("reset-password-model", Name = "ResetPasswordModel")]
        public IActionResult ResetPassword(string token, string email)
        {
            var model = new ResetPasswordDto { Token = token, Email = email };
            return View(model);
        }

    }
}
