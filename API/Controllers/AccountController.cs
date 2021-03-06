using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using API.Constants;
using API.DTOs;
using API.Entities;
using API.Errors;
using API.Extensions;
using API.Interfaces;
using API.Interfaces.Services;
using AutoMapper;
using Kavita.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Controllers
{
    /// <summary>
    /// All Account matters
    /// </summary>
    public class AccountController : BaseApiController
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AccountController> _logger;
        private readonly IMapper _mapper;

        /// <inheritdoc />
        public AccountController(UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            ITokenService tokenService, IUnitOfWork unitOfWork,
            ILogger<AccountController> logger,
            IMapper mapper)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _mapper = mapper;
        }

        /// <summary>
        /// Update a user's password
        /// </summary>
        /// <param name="resetPasswordDto"></param>
        /// <returns></returns>
        [HttpPost("reset-password")]
        public async Task<ActionResult> UpdatePassword(ResetPasswordDto resetPasswordDto)
        {
            _logger.LogInformation("{UserName} 正修改 {ResetUser}的密码", User.GetUsername(), resetPasswordDto.UserName);
            var user = await _userManager.Users.SingleAsync(x => x.UserName == resetPasswordDto.UserName);

            if (resetPasswordDto.UserName != User.GetUsername() && !User.IsInRole(PolicyConstants.AdminRole))
                return Unauthorized("您无权进行此操作.");

            // Validate Password
            foreach (var validator in _userManager.PasswordValidators)
            {
                var validationResult = await validator.ValidateAsync(_userManager, user, resetPasswordDto.Password);
                if (!validationResult.Succeeded)
                {
                    return BadRequest(
                        validationResult.Errors.Select(e => new ApiException(400, e.Code, e.Description)));
                }
            }

            var result = await _userManager.RemovePasswordAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError("无法更新密码");
                return BadRequest(result.Errors.Select(e => new ApiException(400, e.Code, e.Description)));
            }


            result = await _userManager.AddPasswordAsync(user, resetPasswordDto.Password);
            if (!result.Succeeded)
            {
                _logger.LogError("无法更新密码");
                return BadRequest(result.Errors.Select(e => new ApiException(400, e.Code, e.Description)));
            }

            _logger.LogInformation("{User}的密码已重置", resetPasswordDto.UserName);
            return Ok();
        }

        /// <summary>
        /// Register a new user on the server
        /// </summary>
        /// <param name="registerDto"></param>
        /// <returns></returns>
        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register(RegisterDto registerDto)
        {
            try
            {
                if (await _userManager.Users.AnyAsync(x => x.NormalizedUserName == registerDto.Username.ToUpper()))
                {
                    return BadRequest("用户名已被使用.");
                }

                var user = _mapper.Map<AppUser>(registerDto);
                user.UserPreferences ??= new AppUserPreferences();
                user.ApiKey = HashUtil.ApiKey();

                var result = await _userManager.CreateAsync(user, registerDto.Password);

                if (!result.Succeeded) return BadRequest(result.Errors);

                var role = registerDto.IsAdmin ? PolicyConstants.AdminRole : PolicyConstants.PlebRole;
                var roleResult = await _userManager.AddToRoleAsync(user, role);

                if (!roleResult.Succeeded) return BadRequest(result.Errors);

                // When we register an admin, we need to grant them access to all Libraries.
                if (registerDto.IsAdmin)
                {
                    _logger.LogInformation("{UserName} 正在注册为管理员。 授予对所有库的访问权限",
                        user.UserName);
                    var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesAsync()).ToList();
                    foreach (var lib in libraries)
                    {
                        lib.AppUsers ??= new List<AppUser>();
                        lib.AppUsers.Add(user);
                    }

                    if (libraries.Any() && !await _unitOfWork.CommitAsync())
                        _logger.LogError("授予书库访问权限时出现问题。 请手动执行此操作");
                }

                return new UserDto
                {
                    Username = user.UserName,
                    Token = await _tokenService.CreateToken(user),
                    ApiKey = user.ApiKey,
                    Preferences = _mapper.Map<UserPreferencesDto>(user.UserPreferences)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "注册用户时出现问题");
                await _unitOfWork.RollbackAsync();
            }

            return BadRequest("注册用户时出现问题");
        }

        /// <summary>
        /// Perform a login. Will send JWT Token of the logged in user back.
        /// </summary>
        /// <param name="loginDto"></param>
        /// <returns></returns>
        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
        {
            var user = await _userManager.Users
                .Include(u => u.UserPreferences)
                .SingleOrDefaultAsync(x => x.NormalizedUserName == loginDto.Username.ToUpper());

            if (user == null) return Unauthorized("无效的用户名");

            var result = await _signInManager
                .CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded) return Unauthorized("您的凭据不正确.");

            // Update LastActive on account
            user.LastActive = DateTime.Now;
            user.UserPreferences ??= new AppUserPreferences();

            _unitOfWork.UserRepository.Update(user);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("{UserName} 登录于 {Time}", user.UserName, user.LastActive);

            return new UserDto
            {
                Username = user.UserName,
                Token = await _tokenService.CreateToken(user),
                ApiKey = user.ApiKey,
                Preferences = _mapper.Map<UserPreferencesDto>(user.UserPreferences)
            };
        }

        /// <summary>
        /// Get All Roles back. See <see cref="PolicyConstants"/>
        /// </summary>
        /// <returns></returns>
        [HttpGet("roles")]
        public ActionResult<IList<string>> GetRoles()
        {
            return typeof(PolicyConstants)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string))
                .ToDictionary(f => f.Name,
                    f => (string) f.GetValue(null)).Values.ToList();
        }

        /// <summary>
        /// Sets the given roles to the user.
        /// </summary>
        /// <param name="updateRbsDto"></param>
        /// <returns></returns>
        [HttpPost("update-rbs")]
        public async Task<ActionResult> UpdateRoles(UpdateRbsDto updateRbsDto)
        {
            var user = await _userManager.Users
                .Include(u => u.UserPreferences)
                .SingleOrDefaultAsync(x => x.NormalizedUserName == updateRbsDto.Username.ToUpper());
            if (updateRbsDto.Roles.Contains(PolicyConstants.AdminRole) ||
                updateRbsDto.Roles.Contains(PolicyConstants.PlebRole))
            {
                return BadRequest("无效角色");
            }

            var existingRoles = (await _userManager.GetRolesAsync(user))
                .Where(s => s != PolicyConstants.AdminRole && s != PolicyConstants.PlebRole)
                .ToList();

            // Find what needs to be added and what needs to be removed
            var rolesToRemove = existingRoles.Except(updateRbsDto.Roles);
            var result = await _userManager.AddToRolesAsync(user, updateRbsDto.Roles);

            if (!result.Succeeded)
            {
                await _unitOfWork.RollbackAsync();
                return BadRequest("出了点问题，无法更新用户的角色");
            }
            if ((await _userManager.RemoveFromRolesAsync(user, rolesToRemove)).Succeeded)
            {
                return Ok();
            }

            await _unitOfWork.RollbackAsync();
            return BadRequest("出了点问题，无法更新用户的角色");

        }

        /// <summary>
        /// Resets the API Key assigned with a user
        /// </summary>
        /// <returns></returns>
        [HttpPost("reset-api-key")]
        public async Task<ActionResult<string>> ResetApiKey()
        {
            var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());

            user.ApiKey = HashUtil.ApiKey();

            if (_unitOfWork.HasChanges() && await _unitOfWork.CommitAsync())
            {
                return Ok(user.ApiKey);
            }

            await _unitOfWork.RollbackAsync();
            return BadRequest("出了点问题，无法重置密钥");

        }
    }
}
