using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Filters;
using OpenTracing;

namespace Iserv.IdentityServer4.BusinessLogic.Tracing
{
    public class TracingExceptionFilter : IExceptionFilter
    {
        private readonly ITracer _tracer;

        public TracingExceptionFilter(ITracer tracer)
        {
            _tracer = tracer;
        }

        public void OnException(ExceptionContext context)
        {
            _tracer.ActiveSpan.SetTag("error", true);

            _tracer.ActiveSpan.Log(new Dictionary<string, object>
            {
                {"error.kind", "Exception"},
                {"error.object", context.Exception.GetType().FullName},
                {"message", context.Exception.Message},
                {"stack", context.Exception.StackTrace},
            });
        }
    }
}
