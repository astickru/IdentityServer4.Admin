using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Iserv.IdentityServer4.BusinessLogic.Services;
using Microsoft.Extensions.Logging;
using Skoruba.IdentityServer4.Admin.EntityFramework.Shared.Entities.Identity;

namespace Iserv.IdentityServer4.BusinessLogic.TokenValidators
{
    public class DeviceIdTokenValidator<TUser, TKey> : IDeviceIdTokenValidator
        where TUser : UserIdentity<TKey>, new()
        where TKey : IEquatable<TKey>
    {
        private const string Separator = "_device_id_";
        private readonly IAccountService<TUser, TKey> _accountService;
        private readonly ILogger<DeviceIdTokenValidator<TUser, TKey>> _logger;

        public DeviceIdTokenValidator(IAccountService<TUser, TKey> accountService, ILogger<DeviceIdTokenValidator<TUser, TKey>> logger)
        {
            _accountService = accountService;
            _logger = logger;
        }

        public async Task<TokenValidationResult> ValidateAccessTokenAsync(string token, string expectedScope = null)
        {
            if (string.IsNullOrWhiteSpace(token)) return new TokenValidationResult {IsError = true, ErrorDescription = "Токен устройства не указан"};
            var tokenParams = token.Split(Separator);
            if (tokenParams.Length != 2)
                return new TokenValidationResult {IsError = true, ErrorDescription = "Невозможно разделить deviceId от token. Делитель равняется " + Separator};
            var deviceId = tokenParams[0];
            var deviceToken = tokenParams[1];
            if(string.IsNullOrWhiteSpace(deviceId)) return new TokenValidationResult {IsError = true, ErrorDescription = "Id устройства не указано"};
            if(string.IsNullOrWhiteSpace(deviceToken)) return new TokenValidationResult {IsError = true, ErrorDescription = "Токен устройства не указан"};
            var user = await _accountService.FindByDeviceIdAsync(deviceId, deviceToken);
            if (user == null)return new TokenValidationResult {IsError = true, ErrorDescription = "Неверный токен подтверждения устройства"};

            var claimList = new List<Claim>();
            claimList.Add(new Claim("id", user.Id.ToString()));
            claimList.Add(new Claim("email", user.Email));

            return new TokenValidationResult {IsError = false, Claims = claimList};
        }

        public Task<TokenValidationResult> ValidateIdentityTokenAsync(string token, string clientId = null, bool validateLifetime = true)
        {
            throw new NotImplementedException();
        }

        public Task<TokenValidationResult> ValidateRefreshTokenAsync(string token, Client client = null)
        {
            throw new NotImplementedException();
        }
    }
}