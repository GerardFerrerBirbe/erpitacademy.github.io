﻿using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ITAcademyERP.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ITAcademyERP.Data;
using Microsoft.EntityFrameworkCore;

namespace ITAcademyERP.Controllers
{
    [Produces("application/json")]
    [Route("api/Account")]
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<Person> _userManager;
        private readonly SignInManager<Person> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly ITAcademyERPContext _context;

        public AccountController(
            RoleManager<IdentityRole> roleManager,
            UserManager<Person> userManager,
            SignInManager<Person> signInManager,
            IConfiguration configuration,
            ITAcademyERPContext context)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _signInManager = signInManager;
            this._configuration = configuration;
            _context = context;
        }

        [Route("Create")]
        [HttpPost]
        public async Task<IActionResult> CreatePassword([FromBody] UserInfo model)
        {
            if (ModelState.IsValid)
            {
                var user = _context.People.SingleOrDefault(p => p.Email == model.Email);

                if (user == default)
                {
                    ModelState.AddModelError(string.Empty, "Email not registered. Please contact with admin responsible");
                    return BadRequest(ModelState);
                }
                
                if (user.PasswordHash != null)
                {
                    ModelState.AddModelError(string.Empty, "User already registered. Please sign in");
                    return BadRequest(ModelState);
                }

                var hasher = new PasswordHasher<IdentityUser>();

                user.PasswordHash = hasher.HashPassword(user, model.Password);

                _context.Entry(user).State = EntityState.Modified;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    var roles = await _userManager.GetRolesAsync(user);

                    return BuildToken(model, roles);
                }
                else
                {
                    return BadRequest(ModelState);
                }
            }
            else
            {
                return BadRequest(ModelState);
            }
        }                

        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login([FromBody] UserInfo userInfo)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(userInfo.Email, userInfo.Password, isPersistent: false, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    var user = await _userManager.FindByEmailAsync(userInfo.Email);

                    var roles = await _userManager.GetRolesAsync(user);
                    
                    return BuildToken(userInfo, roles);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return BadRequest(ModelState);
                }
            }
            else
            {
                return BadRequest(ModelState);
            }
        }

        private IActionResult BuildToken(UserInfo userInfo, IList<string> roles)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.UniqueName, userInfo.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
                        
            foreach (var role in roles)
            {                
                claims.Add(new Claim(ClaimTypes.Role, role));                
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Llave_super_secreta"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expiration = DateTime.UtcNow.AddDays(7);

            JwtSecurityToken token = new JwtSecurityToken(
               issuer: "yourdomain.com",
               audience: "yourdomain.com",
               claims: claims,
               expires: expiration,
               signingCredentials: creds);

            var user = _context.People.FirstOrDefault(p => p.Email == userInfo.Email);
            var userName = user.FirstName + ' ' + user.LastName;
            var isAdminUser = roles.Contains("Admin");
            
            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiration = expiration,
                userName = userName,
                isAdminUser = isAdminUser
            });
        }        
    }
}
