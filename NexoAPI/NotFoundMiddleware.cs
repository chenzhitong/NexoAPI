using Azure;
using Newtonsoft.Json;
using NLog;
using System.Net;
using System.Text;

namespace NexoAPI
{
    public class NotFoundMiddleware
    {
        private readonly RequestDelegate _next;

        public readonly Logger _logger;

        public NotFoundMiddleware(RequestDelegate next)
        {
            _next = next;
            _logger = LogManager.LoadConfiguration("nlog.config").GetCurrentClassLogger();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
                if (context.Response.StatusCode == 404)
                {
                    context.Response.ContentType = "application/json";
                    context.Response.StatusCode = 400;
                    var json = JsonConvert.SerializeObject(new { code = "NotFound", message = "404 NotFound", data = $"Path: {context.Request.Path}" });
                    await context.Response.WriteAsync(json);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
            }
        }
    }
}
