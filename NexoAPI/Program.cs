using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NexoAPI;
using NexoAPI.Data;
using System.ComponentModel;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<NexoAPIContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NexoAPIContext") ?? throw new InvalidOperationException("Connection string 'NexoAPIContext' not found.")));

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
    options.Filters.Add<GlobalExceptionFilter>();
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
    var file = Path.Combine(AppContext.BaseDirectory, "NexoAPI.xml");  // xml文档绝对路径
    var path = Path.Combine(AppContext.BaseDirectory, file); // xml文档绝对路径
    c.IncludeXmlComments(path, true); // true : 显示控制器层注释
    c.OrderActionsBy(o => o.RelativePath); // 对action的名称进行排序，如果有多个，就可以看见效果了。

    // 添加 JWT 鉴权
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<BackgroundTask>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllDomain",
        builder => builder
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader());
});
var app = builder.Build();
app.UseMiddleware<NotFoundMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseAuthorization();
app.UseCors("AllowAllDomain");
app.MapControllers();
app.Run();
