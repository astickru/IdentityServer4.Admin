using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Iserv.IdentityServer4.BusinessLogic.Settings;
using Microsoft.Extensions.Logging;

namespace Iserv.IdentityServer4.BusinessLogic.TokenValidators
{
    public class OkTokenValidator : IOkTokenValidator
    {
        private readonly string _accessTokenEndpoint;
        private const string AccessTokenReplacement = "{token}";
        private const string SigReplacement = "{sig}";
        private readonly SocialParams _okParams;
        private readonly ILogger<OkTokenValidator> _logger;

        public OkTokenValidator(SocialOptions socialOptions, ILogger<OkTokenValidator> logger)
        {
            _okParams = socialOptions.OkParams;
            _accessTokenEndpoint =
                // $"https://api.ok.ru/fb.do?application_key=COIGFJJGDIHBABABA&format=json&method=users.getCurrentUser&sig=818ad8b3f0812a56da5b92df8ef7f663&access_token=
                $"https://api.ok.ru/fb.do?application_key={socialOptions.OkParams.WebClientPublic}&format=json&method=users.getCurrentUser&sig={SigReplacement}&access_token={AccessTokenReplacement}";
            _logger = logger;
        }

        public async Task<TokenValidationResult> ValidateAccessTokenAsync(string token, string expectedScope = null)
        {
            if (string.IsNullOrWhiteSpace(token)) return new TokenValidationResult {IsError = true, ErrorDescription = "Код авторизации не указан"};
            var md5Hasher = MD5.Create();
            var hash = md5Hasher.ComputeHash(Encoding.Default.GetBytes($"{token}{_okParams.WebClientSecret}"));
            var sBuilder = new StringBuilder();
            foreach (var t in hash)
            {
                sBuilder.Append(t.ToString("x2"));
            }

            var sessionKey = sBuilder.ToString().ToLower();

            hash = md5Hasher.ComputeHash(Encoding.Default.GetBytes($"application_key={_okParams.WebClientPublic}format=jsonmethod=users.getCurrentUser{sessionKey}"));
            sBuilder.Clear();
            foreach (var t in hash)
            {
                sBuilder.Append(t.ToString("x2"));
            }

            var sig = sBuilder.ToString().ToLower();
            using var client = new HttpClient();
            var response = await client.GetAsync(_accessTokenEndpoint.Replace(AccessTokenReplacement, token).Replace(SigReplacement, sig));
            if (!response.IsSuccessStatusCode)
            {
                const string msg = "Не удалось получить access_token Одноклассники";
                _logger.LogWarning(msg + ". " + response.ReasonPhrase);
                return new TokenValidationResult {IsError = true, ErrorDescription = msg};
            }

            var jsonDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var claimList = new List<Claim>();
            var item = jsonDocument.RootElement.EnumerateObject().FirstOrDefault();
            if (item.Name == "error_code")
            {
                _logger.LogWarning(item.Value.GetRawText());
                return new TokenValidationResult {IsError = true, ErrorDescription = "Код ошибки Одноклассники: " + item.Value.GetRawText()};
            }

            foreach (var prop in jsonDocument.RootElement.EnumerateObject())
            {
                if (prop.Name == "uid") claimList.Add(new Claim("id", prop.Value.ToString()));
                else if (prop.Name == "email") claimList.Add(new Claim("email", prop.Value.ToString()));
                else if (prop.Name == "first_name") claimList.Add(new Claim("FirstName", prop.Value.ToString()));
                else if (prop.Name == "last_name") claimList.Add(new Claim("LastName", prop.Value.ToString()));
            }

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