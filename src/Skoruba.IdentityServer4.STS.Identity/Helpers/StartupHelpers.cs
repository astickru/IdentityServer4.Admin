﻿using System;
using System.Globalization;
using IdentityServer4.EntityFramework.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SendGrid;
using Skoruba.IdentityServer4.STS.Identity.Configuration;
using Skoruba.IdentityServer4.STS.Identity.Configuration.ApplicationParts;
using Skoruba.IdentityServer4.STS.Identity.Configuration.Constants;
using Skoruba.IdentityServer4.STS.Identity.Configuration.Interfaces;
using Skoruba.IdentityServer4.STS.Identity.Helpers.Localization;
using Skoruba.IdentityServer4.STS.Identity.Services;
using System.Linq;
using Skoruba.IdentityServer4.Admin.EntityFramework.Interfaces;
using Skoruba.IdentityServer4.Admin.EntityFramework.MySql.Extensions;
using Skoruba.IdentityServer4.Admin.EntityFramework.PostgreSQL.Extensions;
using Skoruba.IdentityServer4.Admin.EntityFramework.Shared.Configuration;
using Skoruba.IdentityServer4.Admin.EntityFramework.SqlServer.Extensions;
using Skoruba.IdentityServer4.Admin.EntityFramework.Helpers;
using IdentityServer4;
using Microsoft.Extensions.Caching.Memory;
using Iserv.IdentityServer4.BusinessLogic.Services;
using Iserv.IdentityServer4.BusinessLogic.Sms;
using Iserv.IdentityServer4.BusinessLogic.Settings;
using Skoruba.IdentityServer4.Admin.EntityFramework.Shared.Entities.Identity;
using System.Net.Http;
using System.Net;
using IdentityServer4.Services;
using Iserv.IdentityServer4.BusinessLogic.Interfaces;
using Iserv.IdentityServer4.BusinessLogic.Providers;
using Iserv.IdentityServer4.BusinessLogic.Senders;
using Iserv.IdentityServer4.BusinessLogic.TokenValidators;
using Iserv.IdentityServer4.BusinessLogic.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ISmsService = Iserv.IdentityServer4.BusinessLogic.Sms.ISmsService;

namespace Skoruba.IdentityServer4.STS.Identity.Helpers
{
    public static class StartupHelpers
    {
        /// <summary>
        /// Register services for MVC and localization including available languages
        /// </summary>
        /// <param name="services"></param>
        public static IMvcBuilder AddMvcWithLocalization<TUser, TKey>(this IServiceCollection services, IConfiguration configuration)
            where TUser : IdentityUser<TKey>
            where TKey : IEquatable<TKey>
        {
            services.AddLocalization(opts => { opts.ResourcesPath = ConfigurationConsts.ResourcesPath; });

            services.TryAddTransient(typeof(IGenericControllerLocalizer<>), typeof(GenericControllerLocalizer<>));

            var mvcBuilder = services.AddControllersWithViews(o => { o.Conventions.Add(new GenericControllerRouteConvention()); })
                .AddViewLocalization(
                    LanguageViewLocationExpanderFormat.Suffix,
                    opts => { opts.ResourcesPath = ConfigurationConsts.ResourcesPath; })
                .AddDataAnnotationsLocalization()
                .ConfigureApplicationPartManager(m => { m.FeatureProviders.Add(new GenericTypeControllerFeatureProvider<TUser, TKey>()); });

            var cultureConfiguration = configuration.GetSection(nameof(CultureConfiguration)).Get<CultureConfiguration>();
            services.Configure<RequestLocalizationOptions>(
                opts =>
                {
                    // If cultures are specified in the configuration, use them (making sure they are among the available cultures),
                    // otherwise use all the available cultures
                    var supportedCultureCodes = (cultureConfiguration?.Cultures?.Count > 0
                        ? cultureConfiguration.Cultures.Intersect(CultureConfiguration.AvailableCultures)
                        : CultureConfiguration.AvailableCultures).ToArray();

                    if (!supportedCultureCodes.Any()) supportedCultureCodes = CultureConfiguration.AvailableCultures;
                    var supportedCultures = supportedCultureCodes.Select(c => new CultureInfo(c)).ToList();

                    // If the default culture is specified use it, otherwise use CultureConfiguration.DefaultRequestCulture ("en")
                    var defaultCultureCode = string.IsNullOrEmpty(cultureConfiguration?.DefaultCulture)
                        ? CultureConfiguration.DefaultRequestCulture
                        : cultureConfiguration?.DefaultCulture;

                    // If the default culture is not among the supported cultures, use the first supported culture as default
                    if (!supportedCultureCodes.Contains(defaultCultureCode)) defaultCultureCode = supportedCultureCodes.FirstOrDefault();

                    opts.DefaultRequestCulture = new RequestCulture(defaultCultureCode);
                    opts.SupportedCultures = supportedCultures;
                    opts.SupportedUICultures = supportedCultures;
                });

            return mvcBuilder;
        }

        /// <summary>
        /// Using of Forwarded Headers and Referrer Policy
        /// </summary>
        /// <param name="app"></param>
        public static void UseSecurityHeaders(this IApplicationBuilder app)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions()
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseHsts(options => options.MaxAge(days: 365));
            app.UseReferrerPolicy(options => options.NoReferrer());
        }

        /// <summary>
        /// Add email senders - configuration of sendgrid, smtp senders
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        public static void AddEmailSenders(this IServiceCollection services, IConfiguration configuration)
        {
            var smtpConfiguration = configuration.GetSection(nameof(SmtpConfiguration)).Get<SmtpConfiguration>();
            var sendGridConfiguration = configuration.GetSection(nameof(SendgridConfiguration)).Get<SendgridConfiguration>();

            if (sendGridConfiguration != null && !string.IsNullOrWhiteSpace(sendGridConfiguration.ApiKey))
            {
                services.AddSingleton<ISendGridClient>(_ => new SendGridClient(sendGridConfiguration.ApiKey));
                services.AddSingleton(sendGridConfiguration);
                services.AddTransient<IEmailSender, SendgridEmailSender>();
            }
            else if (smtpConfiguration != null && !string.IsNullOrWhiteSpace(smtpConfiguration.Host))
            {
                services.AddSingleton(smtpConfiguration);
                services.AddTransient<IEmailSender, SmtpEmailSender>();
            }
            else
            {
                services.AddSingleton<IEmailSender, EmailSender>();
            }
        }

        /// <summary>
        /// Register DbContexts for IdentityServer ConfigurationStore and PersistedGrants and Identity
        /// Configure the connection strings in AppSettings.json
        /// </summary>
        /// <typeparam name="TConfigurationDbContext"></typeparam>
        /// <typeparam name="TPersistedGrantDbContext"></typeparam>
        /// <typeparam name="TIdentityDbContext"></typeparam>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        public static void RegisterDbContexts<TIdentityDbContext, TConfigurationDbContext, TPersistedGrantDbContext>(this IServiceCollection services, IConfiguration configuration)
            where TIdentityDbContext : DbContext
            where TPersistedGrantDbContext : DbContext, IAdminPersistedGrantDbContext
            where TConfigurationDbContext : DbContext, IAdminConfigurationDbContext
        {
            var databaseProvider = configuration.GetSection(nameof(DatabaseProviderConfiguration)).Get<DatabaseProviderConfiguration>();

            var identityConnectionString = configuration.GetConnectionString(ConfigurationConsts.IdentityDbConnectionStringKey);
            var configurationConnectionString = configuration.GetConnectionString(ConfigurationConsts.ConfigurationDbConnectionStringKey);
            var persistedGrantsConnectionString = configuration.GetConnectionString(ConfigurationConsts.PersistedGrantDbConnectionStringKey);

            switch (databaseProvider.ProviderType)
            {
                case DatabaseProviderType.SqlServer:
                    services.RegisterSqlServerDbContexts<TIdentityDbContext, TConfigurationDbContext, TPersistedGrantDbContext>(identityConnectionString,
                        configurationConnectionString, persistedGrantsConnectionString);
                    break;
                case DatabaseProviderType.PostgreSQL:
                    services.RegisterNpgSqlDbContexts<TIdentityDbContext, TConfigurationDbContext, TPersistedGrantDbContext>(identityConnectionString,
                        configurationConnectionString, persistedGrantsConnectionString);
                    break;
                case DatabaseProviderType.MySql:
                    services.RegisterMySqlDbContexts<TIdentityDbContext, TConfigurationDbContext, TPersistedGrantDbContext>(identityConnectionString, configurationConnectionString,
                        persistedGrantsConnectionString);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(databaseProvider.ProviderType),
                        $@"The value needs to be one of {string.Join(", ", Enum.GetNames(typeof(DatabaseProviderType)))}.");
            }
        }

        /// <summary>
        /// Register InMemory DbContexts for IdentityServer ConfigurationStore and PersistedGrants and Identity
        /// Configure the connection strings in AppSettings.json
        /// </summary>
        /// <typeparam name="TConfigurationDbContext"></typeparam>
        /// <typeparam name="TPersistedGrantDbContext"></typeparam>
        /// <typeparam name="TIdentityDbContext"></typeparam>
        /// <param name="services"></param>
        public static void RegisterDbContextsStaging<TIdentityDbContext, TConfigurationDbContext, TPersistedGrantDbContext>(
            this IServiceCollection services)
            where TIdentityDbContext : DbContext
            where TPersistedGrantDbContext : DbContext, IAdminPersistedGrantDbContext
            where TConfigurationDbContext : DbContext, IAdminConfigurationDbContext
        {
            var identityDatabaseName = Guid.NewGuid().ToString();
            services.AddDbContext<TIdentityDbContext>(optionsBuilder => optionsBuilder.UseInMemoryDatabase(identityDatabaseName));

            var configurationDatabaseName = Guid.NewGuid().ToString();
            var operationalDatabaseName = Guid.NewGuid().ToString();

            services.AddConfigurationDbContext<TConfigurationDbContext>(options => { options.ConfigureDbContext = b => b.UseInMemoryDatabase(configurationDatabaseName); });

            services.AddOperationalDbContext<TPersistedGrantDbContext>(options => { options.ConfigureDbContext = b => b.UseInMemoryDatabase(operationalDatabaseName); });
        }

        /// <summary>
        /// Add services for authentication, including Identity model, IdentityServer4 and external providers
        /// </summary>
        /// <typeparam name="TIdentityDbContext">DbContext for Identity</typeparam>
        /// <typeparam name="TUserIdentity">User Identity class</typeparam>
        /// <typeparam name="TUserIdentityRole">User Identity Role class</typeparam>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        public static void AddAuthenticationServices<TIdentityDbContext, TUserIdentity, TUserIdentityRole>(this IServiceCollection services, IConfiguration configuration)
            where TIdentityDbContext : DbContext
            where TUserIdentity : IdentityUser<string>, new()
            where TUserIdentityRole : IdentityRole<string>
        {
            var loginConfiguration = GetLoginConfiguration(configuration);
            var registrationConfiguration = GetRegistrationConfiguration(configuration);

            services
                .AddSingleton(registrationConfiguration)
                .AddSingleton(loginConfiguration)
                .AddScoped<UserResolver<TUserIdentity>>()
                .AddIdentity<TUserIdentity, TUserIdentityRole>(options => { options.User.RequireUniqueEmail = false; })
                .AddUserStore<CustomUserStore<TUserIdentity, TUserIdentityRole, TIdentityDbContext, string, UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin,
                    UserIdentityUserToken, UserIdentityRoleClaim>>()
                .AddEntityFrameworkStores<TIdentityDbContext>()
                .AddDefaultTokenProviders();

            services.Configure<IISOptions>(iis =>
            {
                iis.AuthenticationDisplayName = "Windows";
                iis.AutomaticAuthentication = false;
            });

            var authenticationBuilder = services.AddAuthentication();

            services.AddExternalProviders(authenticationBuilder, configuration);
        }

        /// <summary>
        /// Get configuration for login
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns></returns>
        private static LoginConfiguration GetLoginConfiguration(IConfiguration configuration)
        {
            var loginConfiguration = configuration.GetSection(nameof(LoginConfiguration)).Get<LoginConfiguration>();

            // Cannot load configuration - use default configuration values
            if (loginConfiguration == null)
            {
                return new LoginConfiguration();
            }

            return loginConfiguration;
        }

        /// <summary>
        /// Get configuration for registration
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns></returns>
        private static RegisterConfiguration GetRegistrationConfiguration(IConfiguration configuration)
        {
            var registerConfiguration = configuration.GetSection(nameof(RegisterConfiguration)).Get<RegisterConfiguration>();

            // Cannot load configuration - use default configuration values
            if (registerConfiguration == null)
            {
                return new RegisterConfiguration();
            }

            return registerConfiguration;
        }

        /// <summary>
        /// Add configuration for IdentityServer4
        /// </summary>
        /// <typeparam name="TConfigurationDbContext"></typeparam>
        /// <typeparam name="TPersistedGrantDbContext"></typeparam>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        public static IIdentityServerBuilder AddIdentityServer<TConfigurationDbContext, TPersistedGrantDbContext>(
            this IServiceCollection services,
            IConfiguration configuration)
            where TPersistedGrantDbContext : DbContext, IAdminPersistedGrantDbContext
            where TConfigurationDbContext : DbContext, IAdminConfigurationDbContext
        {
            var builder = services.AddIdentityServer(options =>
                {
                    options.Events.RaiseErrorEvents = true;
                    options.Events.RaiseInformationEvents = true;
                    options.Events.RaiseFailureEvents = true;
                    options.Events.RaiseSuccessEvents = true;
                })
                .AddConfigurationStore<TConfigurationDbContext>()
                .AddOperationalStore<TPersistedGrantDbContext>()
                .AddAspNetIdentity<UserIdentity>()
                .AddResourceOwnerValidator<ResourceOwnerValidatorPassword<UserIdentity, string>>()
                .AddDelegationGrant<UserIdentity, string>() // Register the extension grant 
                .AddDefaultSocialLoginValidators()
                .AddTokenValidators(options =>
                {
                    options.AddValidator<IYandexTokenValidator>("yandex");
                    options.AddValidator<IVkTokenValidator>("vk");
                    options.AddValidator<IOkTokenValidator>("ok");
                });

            services.AddScoped<IYandexTokenValidator, YandexTokenValidator>();
            services.AddScoped<IVkTokenValidator, VkTokenValidator>();
            services.AddScoped<IOkTokenValidator, OkTokenValidator>();
            
            services.AddTransient<IProfileService, IdentityClaimsProfileService>();

            builder.AddCustomSigningCredential(configuration);
            builder.AddCustomValidationKey(configuration);
            return builder;
        }

        /// <summary>
        /// Add external providers
        /// </summary>
        /// <param name="authenticationBuilder"></param>
        /// <param name="configuration"></param>
        private static void AddExternalProviders(this IServiceCollection services, AuthenticationBuilder authenticationBuilder, IConfiguration configuration)
        {
            var externalProviderConfiguration = configuration.GetSection(nameof(ExternalProvidersConfiguration)).Get<ExternalProvidersConfiguration>();

            if (externalProviderConfiguration.UseGitHubProvider)
            {
                authenticationBuilder.AddGitHub(options =>
                {
                    options.ClientId = externalProviderConfiguration.GitHubClientId;
                    options.ClientSecret = externalProviderConfiguration.GitHubClientSecret;
                    options.Scope.Add("user:email");
                });
            }

            var socialOptions = configuration.GetSection(nameof(SocialOptions)).Get<SocialOptions>();
            services.AddSingleton(socialOptions);
            authenticationBuilder.AddGoogle(googleOptions =>
            {
                googleOptions.ClientId = socialOptions.GoogleParams.WebClientId;
                googleOptions.ClientSecret = socialOptions.GoogleParams.WebClientSecret;
                googleOptions.Scope.Add("openid");
                googleOptions.Scope.Add("email");
            }).AddYandex(yandexOptions =>
            {
                yandexOptions.ClientId = socialOptions.YandexParams.WebClientId;
                ;
                yandexOptions.ClientSecret = socialOptions.YandexParams.WebClientSecret;
            });
        }

        /// <summary>
        /// Register middleware for localization
        /// </summary>
        /// <param name="app"></param>
        public static void UseMvcLocalizationServices(this IApplicationBuilder app)
        {
            var options = app.ApplicationServices.GetService<IOptions<RequestLocalizationOptions>>();
            app.UseRequestLocalization(options.Value);
        }

        /// <summary>
        /// Add authorization policies
        /// </summary>
        /// <param name="services"></param>
        /// <param name="rootConfiguration"></param>
        public static void AddAuthorizationPolicies(this IServiceCollection services,
            IRootConfiguration rootConfiguration)
        {
            services.AddLocalApiAuthentication();
            services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthorizationConsts.SealedPolicy, policy =>
                {
                    policy.AddAuthenticationSchemes(IdentityServerConstants.LocalApi.AuthenticationScheme);
                    policy.RequireScope(rootConfiguration.AdminConfiguration.LocalApiResource);
                });
                options.AddPolicy(AuthorizationConsts.PortalPolicy, policy =>
                {
                    policy.AddAuthenticationSchemes(IdentityServerConstants.LocalApi.AuthenticationScheme);
                    policy.RequireScope(rootConfiguration.AdminConfiguration.PortalApiResource);
                });
                options.AddPolicy(AuthorizationConsts.AdministrationPolicy, policy => policy.RequireRole(rootConfiguration.AdminConfiguration.AdministrationRole));
                options.AddPolicy(IdentityServerConstants.LocalApi.PolicyName, policy =>
                {
                    policy.AddAuthenticationSchemes(IdentityServerConstants.LocalApi.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                    policy.RequireScope(rootConfiguration.AdminConfiguration.ExternalApiResource);
                });
            });
        }

        public static void AddIdSHealthChecks<TConfigurationDbContext, TPersistedGrantDbContext, TIdentityDbContext>(this IServiceCollection services, IConfiguration configuration)
            where TConfigurationDbContext : DbContext, IAdminConfigurationDbContext
            where TPersistedGrantDbContext : DbContext, IAdminPersistedGrantDbContext
            where TIdentityDbContext : DbContext
        {
            var configurationDbConnectionString = configuration.GetConnectionString(ConfigurationConsts.ConfigurationDbConnectionStringKey);
            var persistedGrantsDbConnectionString = configuration.GetConnectionString(ConfigurationConsts.PersistedGrantDbConnectionStringKey);
            var identityDbConnectionString = configuration.GetConnectionString(ConfigurationConsts.IdentityDbConnectionStringKey);

            var healthChecksBuilder = services.AddHealthChecks()
                .AddDbContextCheck<TConfigurationDbContext>("ConfigurationDbContext")
                .AddDbContextCheck<TPersistedGrantDbContext>("PersistedGrantsDbContext")
                .AddDbContextCheck<TIdentityDbContext>("IdentityDbContext");

            var serviceProvider = services.BuildServiceProvider();
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            using (var scope = scopeFactory.CreateScope())
            {
                var configurationTableName = DbContextHelpers.GetEntityTable<TConfigurationDbContext>(scope.ServiceProvider);
                var persistedGrantTableName = DbContextHelpers.GetEntityTable<TPersistedGrantDbContext>(scope.ServiceProvider);
                var identityTableName = DbContextHelpers.GetEntityTable<TIdentityDbContext>(scope.ServiceProvider);

                var databaseProvider = configuration.GetSection(nameof(DatabaseProviderConfiguration)).Get<DatabaseProviderConfiguration>();
                switch (databaseProvider.ProviderType)
                {
                    case DatabaseProviderType.SqlServer:
                        healthChecksBuilder
                            .AddSqlServer(configurationDbConnectionString, name: "ConfigurationDb",
                                healthQuery: $"SELECT TOP 1 * FROM dbo.[{configurationTableName}]")
                            .AddSqlServer(persistedGrantsDbConnectionString, name: "PersistentGrantsDb",
                                healthQuery: $"SELECT TOP 1 * FROM dbo.[{persistedGrantTableName}]")
                            .AddSqlServer(identityDbConnectionString, name: "IdentityDb",
                                healthQuery: $"SELECT TOP 1 * FROM dbo.[{identityTableName}]");

                        break;
                    case DatabaseProviderType.PostgreSQL:
                        healthChecksBuilder
                            .AddNpgSql(configurationDbConnectionString, name: "ConfigurationDb",
                                healthQuery: $"SELECT * FROM {configurationTableName} LIMIT 1")
                            .AddNpgSql(persistedGrantsDbConnectionString, name: "PersistentGrantsDb",
                                healthQuery: $"SELECT * FROM {persistedGrantTableName} LIMIT 1")
                            .AddNpgSql(identityDbConnectionString, name: "IdentityDb",
                                healthQuery: $"SELECT * FROM {identityTableName} LIMIT 1");
                        break;
                    case DatabaseProviderType.MySql:
                        healthChecksBuilder
                            .AddMySql(configurationDbConnectionString, name: "ConfigurationDb")
                            .AddMySql(persistedGrantsDbConnectionString, name: "PersistentGrantsDb")
                            .AddMySql(identityDbConnectionString, name: "IdentityDb");
                        break;
                    default:
                        throw new NotImplementedException($"Health checks not defined for database provider {databaseProvider.ProviderType}");
                }
            }
        }

        public static void AddAccountService<TIdentityDbContext>(this IServiceCollection services, IConfiguration configuration)
            where TIdentityDbContext : IdentityDbContext<UserIdentity, UserIdentityRole, string, UserIdentityUserClaim, UserIdentityUserRole, UserIdentityUserLogin,
                UserIdentityRoleClaim, UserIdentityUserToken>
        {
            var repairPwdSmsExpiration = 300;
            var cacheSetting = configuration.GetSection("Cache");
            if (cacheSetting != null)
            {
                repairPwdSmsExpiration = int.Parse(cacheSetting["RepairPwdSMSExpiration"]);
            }

            var smsSetting = new SmsSetting();
            var smsSettingSection = configuration.GetSection("SmsSetting");
            smsSettingSection.Bind(smsSetting);

            var portalOptions = new AuthPortalOptions();
            var portalOptionsSection = configuration.GetSection("Portal");
            portalOptionsSection.Bind(portalOptions);

            var messageTemplates = new MessageTemplates();
            var messageTemplatesSection = configuration.GetSection("MessageTemplates");
            messageTemplatesSection.Bind(messageTemplates);

            services.AddScoped(provider => portalOptions);
            services.AddScoped(provider => messageTemplates);

            services.AddMemoryCache();
            services.AddScoped<IPortalService, PortalService>();
            services.AddHttpClient(PortalService.PortalCode, (provider, client) =>
            {
                client.BaseAddress = new Uri(portalOptions.RootAddress);
                client.DefaultRequestHeaders.Add("cache-control", "no-cache");
                client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.Add("Accept", "*/*");
            }).ConfigurePrimaryHttpMessageHandler(provider =>
            {
                var memoryCache = provider.GetRequiredService<IMemoryCache>();
                var cookie = memoryCache.Get(PortalService.PortalCode)?.ToString();
                var logger = provider.GetService<ILogger<Startup>>();
                if (string.IsNullOrWhiteSpace(cookie))
                {
                    var portalService = provider.GetRequiredService<IPortalService>();
                    var result = portalService.UpdateSessionAsync().Result;
                    if (result.IsError)
                    {
                        logger.LogError("Ошибка получения интеграционного токена. Ответ портала: " + result.Message);
                    }
                    else
                    {
                        cookie = memoryCache.Get(PortalService.PortalCode)?.ToString();
                    }
                }

                var handler = new HttpClientHandler();
                handler.CookieContainer = new CookieContainer();
                handler.CookieContainer.Add(new Uri(portalOptions.RootAddress), new Cookie("JSESSIONID", cookie));
                return handler;
            });
            services.AddScoped<ISmsService>(provider =>
            {
                if (smsSetting.Provider == ESmsProviders.Devino)
                    return new SmsSenderDevino(smsSetting, provider.GetService<ILogger<SmsSenderDevino>>());
                else
                    return new SmsSenderTwilio(smsSetting, provider.GetService<ILogger<SmsSenderTwilio>>());
            });
            services.AddScoped<IConfirmService>(provider =>
                new ConfirmService(TimeSpan.FromSeconds(repairPwdSmsExpiration), provider.GetRequiredService<IMemoryCache>(), provider.GetRequiredService<IEmailSender>(),
                    provider.GetRequiredService<ISmsService>()));
            services
                .AddScoped<IAccountService<UserIdentity, string>, AccountService<TIdentityDbContext, UserIdentity, UserIdentityRole, string, UserIdentityUserClaim,
                    UserIdentityUserRole, UserIdentityUserLogin, UserIdentityRoleClaim, UserIdentityUserToken>>();
            services.AddHostedService<AuthPortalHostedService>();
            services.AddSingleton<ISender, Sender>();
        }
    }
}