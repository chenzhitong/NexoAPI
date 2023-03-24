using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using NexoAPI;
using NexoAPI.Data;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<NexoAPIContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NexoAPIContext") ?? throw new InvalidOperationException("Connection string 'NexoAPIContext' not found.")));

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.Filters.Add(typeof(GlobalExceptionFilter));
}).AddNewtonsoftJson().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new NexoAPI.DateTimeConverter("yyyy-MM-ddTHH:mm:ssZ"));
});
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Nexo API",
        Version = "1.0",
        Description = "Nexo API Swagger Doc"
    });
    //c.OperationFilter<CustomHeaderSwaggerAttribute>();
    var file = Path.Combine(AppContext.BaseDirectory, "NexoAPI.xml");  // xml文档绝对路径
    var path = Path.Combine(AppContext.BaseDirectory, file); // xml文档绝对路径
    c.IncludeXmlComments(path, true); // true : 显示控制器层注释
    c.OrderActionsBy(o => o.RelativePath); // 对action的名称进行排序，如果有多个，就可以看见效果了。

});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<BackgroundTask>();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();

