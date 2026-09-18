using Microsoft.EntityFrameworkCore;
using PulseWatch.Data;
using PulseWatch.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient<WebsiteMonitorService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHostedService<WebsiteMonitoringWorker>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
