using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.Extensions.Logging;

namespace Iserv.IdentityServer4.BusinessLogic.TokenValidators
{
    public class VkTokenValidator : IVkTokenValidator
    {
        private const string UserInfoEndpoint = "https://api.vk.com/method/users.get?fields=nickname&access_token={token}&v=5.103";
        private const string AccessTokenReplacement = "{token}";
        private readonly ILogger<VkTokenValidator> _logger;

        public VkTokenValidator(ILogger<VkTokenValidator> logger)
        {
            _logger = logger;
        }

        public async Task<TokenValidationResult> ValidateAccessTokenAsync(string token, string expectedScope = null)
        {
            if (string.IsNullOrWhiteSpace(token)) return new TokenValidationResult {IsError = true, ErrorDescription = "Токен авторизации не указан"};
            var regex = new Regex("email=(.*@.*\\..*$)");
            if (!regex.IsMatch(token))
                return new TokenValidationResult {IsError = true, ErrorDescription = "Не указан в токене email"};
            var email = regex.Match(token).Groups[1].Value;
            if (string.IsNullOrWhiteSpace(email)) return new TokenValidationResult {IsError = true, ErrorDescription = "Не указан в токене email"};
            token = token.Replace("email=" + email, "");
            using var client = new HttpClient();
            var response = await client.GetAsync(UserInfoEndpoint.Replace(AccessTokenReplacement, token));
            if (!response.IsSuccessStatusCode)
            {
                const string msg = "Не удалось получить данные пользователя VK";
                _logger.LogWarning(msg + ". " + response.ReasonPhrase);
                return new TokenValidationResult {IsError = true, ErrorDescription = msg};
            }

            var jsonDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var claimList = new List<Claim> {new Claim("email", email)};
            var item = jsonDocument.RootElement.EnumerateObject().FirstOrDefault();
            if (item.Name == "error")
            {
                _logger.LogWarning(item.Value.GetRawText());
                return new TokenValidationResult {IsError = true, ErrorDescription = item.Value.GetRawText()};
            }

            if (item.Value.ValueKind != JsonValueKind.Array || item.Value.GetArrayLength() < 1)
                return new TokenValidationResult {IsError = true, ErrorDescription = response.ReasonPhrase};
            var props = item.Value[0];
            claimList.Add(new Claim("id", props.GetProperty("id").ToString()));
            claimList.Add(new Claim("FirstName", props.GetProperty("first_name").ToString()));
            claimList.Add(new Claim("LastName", props.GetProperty("last_name").ToString()));
            claimList.Add(new Claim("MiddleName", props.GetProperty("nickname").ToString()));
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