﻿using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Iserv.IdentityServer4.BusinessLogic.Settings;
using Iserv.IdentityServer4.BusinessLogic.ExceptionHandling;
using Iserv.IdentityServer4.BusinessLogic.Models;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Iserv.IdentityServer4.BusinessLogic.Services
{
    public class PortalService : IPortalService
    {
        public const string PortalCode = "portal";

        private readonly AuthPortalOptions _options;
        private readonly IHttpClientFactory _clientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<PortalService> _logger;

        public PortalService(AuthPortalOptions options, IHttpClientFactory clientFactory, IMemoryCache memoryCache, ILogger<PortalService> logger)
        {
            _options = options;
            _clientFactory = clientFactory;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        private async Task<string> ReadResponseAsStringAsync(HttpResponseMessage response)
        {
            var result = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode) return result;
            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    return "Portal. Unauthorized";
                case HttpStatusCode.BadRequest:
                {
                    if (!string.IsNullOrWhiteSpace(result)) _logger.LogInformation(result);
                    return "Portal. " + result;
                }
                default:
                    throw new PortalException(result);
            }
        }

        public async Task UpdateSessionAsync()
        {
            var handler = new HttpClientHandler {CookieContainer = new CookieContainer()};
            var client = new HttpClient(handler) {BaseAddress = new Uri(_options.RootAddress)};
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{_options.Login}:{_options.Password}")));
            await client.GetAsync("tehprisEE_lookups");
            var cookie = handler.CookieContainer.GetCookies(new Uri(_options.RootAddress)).FirstOrDefault();
            if (cookie != null)
            {
                _memoryCache.Set(PortalCode, cookie.Value);
            }
        }

        public string GetCookie()
        {
            return _memoryCache.Get(PortalCode)?.ToString();
        }

        public async Task<PortalResult<Guid>> GetUserIdByAuthAsync(string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new ValidationException("Логин пользователя не указан");
            if (string.IsNullOrWhiteSpace(password))
                throw new ValidationException("Пароль пользователя не указан");
            var client = _clientFactory.CreateClient(PortalCode);
            var response = await client.PostAsync("tehprisEE_auth/signin", new StringContent(JsonConvert.SerializeObject(new {email = userName, password}), Encoding.UTF8));
            var txt = await ReadResponseAsStringAsync(response);
            if (!response.IsSuccessStatusCode) return new PortalResult<Guid>() {IsError = true, Message = txt};
            var result = JsonConvert.DeserializeObject<PortalAuthResultSuccess>(txt);
            return result != null ? new PortalResult<Guid>() {Value = result.UserId} : new PortalResult<Guid>() {IsError = true, Message = txt};
        }

        public async Task<PortalResult<Dictionary<string, object>>> GetUserAsync(Guid idext)
        {
            if (idext.Equals(Guid.Empty))
                throw new ValidationException("Внешний Id пользователя на портале пустой");
            var client = _clientFactory.CreateClient(PortalCode);
            var response = await client.GetAsync("tehprisEE_profiles/" + idext);
            var txt = await ReadResponseAsStringAsync(response);
            if (!response.IsSuccessStatusCode) return new PortalResult<Dictionary<string, object>>() {IsError = true, Message = txt};
            try
            {
                return new PortalResult<Dictionary<string, object>>() {Value = JsonConvert.DeserializeObject<Dictionary<string, object>>(txt)};
            }
            catch
            {
                return new PortalResult<Dictionary<string, object>>() {IsError = true, Message = txt};
            }
        }

        public async Task<PortalResult<Guid>> RegisterAsync(PortalRegistrationData userProfile)
        {
            var client = _clientFactory.CreateClient(PortalCode);
            var response = await client.PostAsync("tehprisEE_auth/register", new StringContent(JsonConvert.SerializeObject(userProfile), Encoding.UTF8));
            var txt = await ReadResponseAsStringAsync(response);
            if (!response.IsSuccessStatusCode) return new PortalResult<Guid>() {IsError = true, Message = txt};
            var result = JsonConvert.DeserializeObject<PortalAuthResultSuccess>(txt);
            return result != null ? new PortalResult<Guid>() {Value = result.UserId} : new PortalResult<Guid>() {IsError = true, Message = txt};
        }

        public async Task<PortalResult> UpdateUserAsync(Guid idext, Dictionary<string, object> values, IEnumerable<FileModel> files = null)
        {
            var client = _clientFactory.CreateClient(PortalCode);
            var multiForm = new MultipartFormDataContent {{new StringContent(JsonConvert.SerializeObject(values), Encoding.UTF8), "attributes"}};
            foreach (var file in files)
            {
                var imageContent = new ByteArrayContent(file.FileData);
                imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
                multiForm.Add(imageContent, file.Tag, file.Name);
            }

            var response = await client.PostAsync("tehprisEE_profiles/" + idext, multiForm);
            var txt = await ReadResponseAsStringAsync(response);
            return response.IsSuccessStatusCode ? new PortalResult() {Message = txt} : new PortalResult() {IsError = true, Message = txt};
        }

        public async Task<PortalResult> UpdatePasswordAsync(Guid idext, string password)
        {
            var client = _clientFactory.CreateClient(PortalCode);
            var response = await client.PostAsync("tehprisEE_profiles/" + idext, new StringContent(JsonConvert.SerializeObject(new {password}), Encoding.UTF8));
            var txt = await ReadResponseAsStringAsync(response);
            return !response.IsSuccessStatusCode ? new PortalResult() {IsError = true, Message = txt} : new PortalResult();
        }

        public async Task<PortalResult> RestorePasswordByEmailAsync(string email)
        {
            var client = _clientFactory.CreateClient(PortalCode);
            var response = await client.PostAsync("tehprisEE_auth/restore", new StringContent(JsonConvert.SerializeObject(new {email}), Encoding.UTF8));
            var txt = await ReadResponseAsStringAsync(response);
            return !response.IsSuccessStatusCode ? new PortalResult() {IsError = true, Message = txt} : new PortalResult();
        }
    }
}