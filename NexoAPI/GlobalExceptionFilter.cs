using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace NexoAPI
{
    public class GlobalExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            var response = new _500
            { 
                Code = context.Exception.GetType().ToString(),
                Message = context.Exception.Message,
                Data = context.Exception.StackTrace
            };

            context.HttpContext.Response.StatusCode = 500;
            context.HttpContext.Response.ContentType = "application/json";
            context.Result = new ObjectResult(response);
        }
    }

    public class _500
    {
        public string Code { get; set; }

        public string Message { get; set; }

        public string Data { get; set; }
    }
}
