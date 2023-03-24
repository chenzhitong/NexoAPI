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
    var file = Path.Combine(AppContext.BaseDirectory, "NexoAPI.xml");  // xml�ĵ�����·��
    var path = Path.Combine(AppContext.BaseDirectory, file); // xml�ĵ�����·��
    c.IncludeXmlComments(path, true); // true : ��ʾ��������ע��
    c.OrderActionsBy(o => o.RelativePath); // ��action�����ƽ�����������ж�����Ϳ��Կ���Ч���ˡ�

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

