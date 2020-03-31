using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Iserv.AccountService.Infrastucture.Tracing
{
    public sealed class TracingMiddleware
    {
		private readonly RequestDelegate _next;

		private readonly ILogger<TracingMiddleware> _logger;

		public TracingMiddleware(RequestDelegate next, ILogger<TracingMiddleware> logger)
		{
			_next = next;
			_logger = logger;
		}

		public async Task Invoke(HttpContext httpContext)
		{
			var activeSpan = OpenTracing.Util.GlobalTracer.Instance?.ActiveSpan;
			var originalBodyStream = httpContext.Response.Body;

			try
			{
				{
					var request = httpContext.Request;

					var header = string.Join("; ", request.Headers.Select(h => h.Key + "=" + string.Join(", ", h.Value)));
					activeSpan.SetTag("http.request.header", header);

					var body = await GetRequestBody(httpContext);
					activeSpan.SetTag("http.request.body", body);

					_logger.LogInformation("Начало обработки запроса");
				}

				using (var responseBody = new MemoryStream())
				{
					var response = httpContext.Response;

					response.Body = responseBody;

					await _next(httpContext);

					var header = string.Join("; ", response.Headers.Select(h => h.Key + "=" + string.Join(", ", h.Value)));
					activeSpan.SetTag("http.response.header", header);

					var body = await GetResponseBody(response);

					await responseBody.CopyToAsync(originalBodyStream);

					response.Body = originalBodyStream;

					activeSpan.SetTag("http.response.result", body);

					_logger.LogInformation("Завершение обработки запроса");
				}
			}
			catch (Exception exc)
			{
				activeSpan.SetTag("error", true);

				activeSpan.Log(new Dictionary<string, object>
				{
					{"error.kind", "Exception"},
					{"error.object", exc.GetType().FullName},
					{"message", exc.Message},
					{"stack", exc.StackTrace},
				});

				_logger.LogError(exc, "Ошибка");

				throw;
			}
		}

		private static async Task<string> GetResponseBody(HttpResponse response)
		{
			response.Body.Seek(0, SeekOrigin.Begin);

			var body = await new StreamReader(response.Body).ReadToEndAsync();

			response.Body.Seek(0, SeekOrigin.Begin);

			return body;
		}

		private static async Task<string> GetRequestBody(HttpContext context)
		{
			context.Request.EnableRewind();

			var bufferSize = context.Request.ContentLength.HasValue && context.Request.ContentLength > 0 && context.Request.ContentLength < 1024 ? 
				Convert.ToInt32(context.Request.ContentLength) : 1024;
			
     		var body = await new StreamReader(context.Request.Body, System.Text.Encoding.UTF8, true, bufferSize, true).ReadToEndAsync();

			context.Request.Body.Seek(0, SeekOrigin.Begin);

			return body;
		}
	}

	public static class TracingMiddlewareExtensions
	{
		public static IApplicationBuilder UseTracingMiddleware(this IApplicationBuilder builder)
		{
			return builder.UseMiddleware<TracingMiddleware>().UseSystemVersion();
		}
	}
}
