﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text;

namespace NexoAPI
{
    public class ValidationFilter : IResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            var msg = new StringBuilder();

            context.ModelState.Where(p => p.Value?.Errors.Count > 0).ToList().ForEach(error => error.Value?.Errors.ToList().ForEach(p => msg.Append($"{p.ErrorMessage} ")));

            context.Result = new BadRequestObjectResult(new { Code = "InvalidParameter", Message = msg.ToString().Trim(), Data = string.Empty });

            return;
        }

        public void OnResultExecuted(ResultExecutedContext context) { }
    }
}
