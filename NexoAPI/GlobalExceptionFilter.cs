using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace NexoAPI
{
    public class GlobalExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            context.HttpContext.Response.StatusCode = 500;
            context.HttpContext.Response.ContentType = "application/json";

            context.Result = new ObjectResult(new
            {
                Code = context.Exception.GetType().ToString(),
                Message = context.Exception.Message,
                Data = context.Exception.StackTrace.Trim()
            });
        }
    }
}
