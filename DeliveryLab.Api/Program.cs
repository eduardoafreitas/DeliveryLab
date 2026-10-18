using DeliveryLab.Api.Data;
using DeliveryLab.Api.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<DeliveryLabDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DeliveryLab")));
builder.Services.AddScoped<DeliveryService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapSwagger("/openapi/{documentName}.json");
app.MapScalarApiReference();
app.MapControllers();

app.Run();

// Permite iniciar a API com WebApplicationFactory nos testes.
public partial class Program { }
