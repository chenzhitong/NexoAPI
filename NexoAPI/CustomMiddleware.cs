using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NuGet.Protocol;

namespace NexoAPI
{
    public class CustomMiddleware
    {
        private readonly RequestDelegate _next;

        public CustomMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            if (context.Response.StatusCode == 400)
            {
                string responseBody;
                using var streamReader = new StreamReader(context.Response.Body);
                responseBody = streamReader.ReadToEnd();

                var response = new _400
                {
                    Code = "InvalidParameter",
                    Message = "",
                    Data = responseBody
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(response.ToJson());
            }
        }
    }

    public class _400
    {
        public string Code { get; set; }

        public string Message { get; set; }

        public string Data { get; set; }
    }
}
