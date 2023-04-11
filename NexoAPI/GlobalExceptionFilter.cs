using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;

namespace NexoAPI
{
    public class GlobalExceptionFilter : IExceptionFilter
    {
        public Logger _logger;
        public void OnException(ExceptionContext context)
        {
            _logger = LogManager.LoadConfiguration("nlog.config").GetCurrentClassLogger();
            context.HttpContext.Response.StatusCode = 500;
            context.HttpContext.Response.ContentType = "application/json";
            _logger.Error($"{context.Exception.GetType()}\t{context.Exception.Message}\t{context.Exception?.StackTrace?.Trim()}");
            context.Result = new ObjectResult(new
            {
                Code = "InternalError",
                Message = $"{context.Exception?.GetType()} {context.Exception?.Message}",
                Data = context.Exception?.StackTrace?.Trim()
            });
        }
    }
}
