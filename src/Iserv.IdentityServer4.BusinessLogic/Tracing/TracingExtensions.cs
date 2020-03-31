using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Iserv.IdentityServer4.BusinessLogic.Tracing
{
    public static class TracingExtensions
    {
        private static string _sysVer = Environment.GetEnvironmentVariable("SYSTEM_VERSION") ?? "0.0";
        
        public static void InitTraceIdRenderer()
        {
            AccountService.Infrastucture.Tracing.TracingExtensions.InitTraceIdRenderer();
        }

        public static void AddTracing(this IServiceCollection services, IConfiguration configuration)
        {
            AccountService.Infrastucture.Tracing.TracingExtensions.AddTracing(services, configuration);
        }

        public static void AddTracingFilters(this MvcOptions options)
        {
            options.Filters.Add(typeof(TracingRequestFilter));
            options.Filters.Add(typeof(TracingExceptionFilter));
        }
        
        public static IApplicationBuilder UseSystemVersion(this IApplicationBuilder builder)
        {
            builder.Use((context, next) =>
            {
                if (!context.Response.Headers.TryAdd("SYSVER", _sysVer))
                    context.Response.Headers["SYSVER"] = _sysVer;

                return next();
            });

            return builder;
        }
    }
}