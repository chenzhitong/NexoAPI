using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using NexoAPI.Data;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<NexoAPIContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NexoAPIContext") ?? throw new InvalidOperationException("Connection string 'NexoAPIContext' not found.")));

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "�����ĵ�����",
        Version = "�ĵ��汾���",
        Description = "�ĵ�����"
    });
    var file = Path.Combine(AppContext.BaseDirectory, "NexoAPI.xml");  // xml�ĵ�����·��
    var path = Path.Combine(AppContext.BaseDirectory, file); // xml�ĵ�����·��
    c.IncludeXmlComments(path, true); // true : ��ʾ��������ע��
    c.OrderActionsBy(o => o.RelativePath); // ��action�����ƽ�����������ж�����Ϳ��Կ���Ч���ˡ�

});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
