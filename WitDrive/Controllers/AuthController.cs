﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WitDrive.Dto;
using WitDrive.Infrastructure.Helpers;
using WitDrive.Interfaces;
using WitDrive.Models;
using MDBFS_Lib;

namespace WitDrive.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration config;
        private readonly IMapper mapper;
        private readonly IAuthService service;
        private readonly UserManager<User> userManager;
        private readonly SignInManager<User> signInManager;
        private readonly FileRepository repo;

        public AuthController(IConfiguration config, IMapper mapper, IAuthService service,
            UserManager<User> userManager, SignInManager<User> signInManager)
        {
            this.config = config;
            this.mapper = mapper;
            this.service = service;
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.repo = new FileRepository("mongodb+srv://App:8aOnnxaohkXlFyZn@witdbcluster0-cchqy.mongodb.net/test?retryWrites=true&w=majority");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(UserForLoginDto userLoginDto)
        {
            if (!RequestValidation.IsRequestValid<UserForLoginDto>(userLoginDto))
            {
                return BadRequest("Invalid request");
            }

            var user = await userManager.FindByNameAsync(userLoginDto.Username);

            if (user == null)
            {
                return Unauthorized();
            }

            var result = await signInManager.CheckPasswordSignInAsync(user, userLoginDto.Password, false);

            if (result.Succeeded)
            {
                var appUser = mapper.Map<UserForListDto>(user);
                return Ok(new
                {
                    token = service.GenerateJwtToken(user, userManager, config).Result,
                    user = appUser
                });
            }
            return Unauthorized();
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(UserForRegisterDto userForRegisterDto)
        {
            if (!RequestValidation.IsRequestValid<UserForRegisterDto>(userForRegisterDto))
            {
                return BadRequest("Invalid request");
            }

            var newUser = mapper.Map<User>(userForRegisterDto);

            var result = await userManager.CreateAsync(newUser, userForRegisterDto.Password);

            if (result.Succeeded)
            {
                var usr = await userManager.FindByNameAsync(newUser.UserName);
                if (await repo.InitializeUserAsync(Convert.ToString(usr.Id)))
                {
                    return StatusCode(201);
                }
            }
            return BadRequest(result.Errors);
        }
    }
}
