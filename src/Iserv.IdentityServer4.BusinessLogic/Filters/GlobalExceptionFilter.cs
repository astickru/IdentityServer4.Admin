using System;
using Iserv.IdentityServer4.BusinessLogic.ExceptionHandling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;

namespace Iserv.IdentityServer4.BusinessLogic.Filters
{
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<GlobalExceptionFilter> _logger;
        public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
        {
            _logger = logger;
        }

        private IActionResult getResult(HttpStatusCode status, Exception exception)
        {
            var title = "Россети - Мобильный личный кабинет";
            if (exception is PortalException) {
                title = "Россети - Портал ТП";
            }
            if (!(exception is ValidationException))
            {
                _logger.LogError(exception.Message, exception);
            }

            var msg = exception.Message;
            if (exception is HttpRequestException)
            {
                _logger.LogError(exception.Message, exception);
                msg = "Не удалось выполнить запрос.";
            }

            return new BadRequestObjectResult(new
            {
                status,
                title,
                text = msg
            });
        }
        
        public void OnException(ExceptionContext context)
        {
            var status = context.HttpContext.Response.StatusCode == HttpStatusCode.OK.GetHashCode() ? HttpStatusCode.BadRequest : (HttpStatusCode)context.HttpContext.Response.StatusCode;

            if (context.Exception is AggregateException)
            {
                ((AggregateException)context.Exception).Handle((x) =>
                {
                    context.Result = getResult(status, context.Exception);
                    return false;
                });
            }
            else
            {
                context.Result = getResult(status, context.Exception);
            }

            context.ExceptionHandled = true;
        }
    }
}
