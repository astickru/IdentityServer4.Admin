using IdentityServer4.AspNetIdentity;
using IdentityServer4.Services;
using IdentityServer4.Validation;
using Iserv.IdentityServer4.BusinessLogic.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Skoruba.IdentityServer4.Admin.EntityFramework.Shared.Entities.Identity;
using System;
using System.Threading.Tasks;
using IdentityModel;
using Iserv.IdentityServer4.BusinessLogic.Interfaces;
using Iserv.IdentityServer4.BusinessLogic.Models;
using Microsoft.Extensions.Configuration;

namespace Iserv.IdentityServer4.BusinessLogic.Validators
{
    public class ResourceOwnerValidatorPassword<TUser, TKey> : ResourceOwnerPasswordValidator<TUser>
        where TUser : UserIdentity<TKey>, new()
        where TKey : IEquatable<TKey>
    {
        private readonly string _appName;
        private readonly UserManager<TUser> _userManager;
        private readonly SignInManager<TUser> _signInManager;
        private readonly IAccountService<TUser, TKey> _accountService;
        private readonly IPortalService _portalService;
        private readonly ISender _sender;
        private readonly ILogger<ResourceOwnerPasswordValidator<TUser>> _logger;

        public ResourceOwnerValidatorPassword(IConfiguration configuration, UserManager<TUser> userManager, SignInManager<TUser> signInManager, IAccountService<TUser, TKey> accountService,
            IPortalService portalService, ISender sender, IEventService events, ILogger<ResourceOwnerPasswordValidator<TUser>> logger)
            : base(userManager, signInManager, events, logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _accountService = accountService;
            _portalService = portalService;
            _sender = sender;
            _logger = logger;
            _appName = configuration["AppName"];
        }

        private async Task<bool> ValidateAsync(ResourceOwnerPasswordValidationContext context, ELoginTypes loginTypes)
        {
            TUser user = null;
            if (loginTypes == ELoginTypes.Email)
            {
                user = await _accountService.FindByEmailAsync(context.UserName);
            }
            else
            {
                user = await _accountService.FindByPhoneAsync(context.UserName);
            }

            if (user == null) return false;
            var result = await _signInManager.CheckPasswordSignInAsync(user, context.Password, true);
            if (!result.Succeeded) return false;
            context.Result = new GrantValidationResult(user.Id.ToString(), OidcConstants.AuthenticationMethods.Password);
            var messageToken = context.Request.Raw.Get("messageToken");
            if (string.IsNullOrWhiteSpace(messageToken)) return true;
            try
            {
                await _sender.SendNotificationAsync(messageToken, _appName, "Произведен вход в Ваш личный кабинет компании Россети.");
            }
            catch (Exception e)
            {
                _logger.LogError("Не удалось отправить Push уведомление на устройство.", e);
            }

            return true;
        }

        public override async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
        {
            if (string.IsNullOrWhiteSpace(context.UserName))
                return;
            context.UserName = context.UserName.ToLower();
            var loginTypes = context.UserName[0] == '+' ? ELoginTypes.Phone : ELoginTypes.Email;
            var result = await ValidateAsync(context, loginTypes);
            if (result) return;
            try
            {
                var resultPortalId = await _portalService.GetUserIdByAuthAsync(loginTypes, context.UserName, context.Password);
                if (resultPortalId.IsError)
                    return;
                var user = loginTypes == ELoginTypes.Email ? await _accountService.FindByEmailAsync(context.UserName) : await _accountService.FindByPhoneAsync(context.UserName);
                if (user != null)
                {
                    var tokenPassword = await _userManager.GeneratePasswordResetTokenAsync(user);
                    await _userManager.ResetPasswordAsync(user, tokenPassword, context.Password);
                    await ValidateAsync(context, loginTypes);
                }
                else
                {
                    if ((await _accountService.CreateUserFromPortalAsync(resultPortalId.Value, context.Password)).Succeeded)
                    {
                        await ValidateAsync(context, loginTypes);
                    }
                }
            }
            catch (Exception err)
            {
                context.Result.IsError = true;
                context.Result.ErrorDescription = err.Message;
                _logger.LogError(err.Message, err);
            }
        }
    }
}