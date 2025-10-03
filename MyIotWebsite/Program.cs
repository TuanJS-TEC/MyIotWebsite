using Microsoft.EntityFrameworkCore;
using MQTTnet.Adapter;
using MyIotWebsite.Data;
using MyIotWebsite.Models;
using MyIotWebsite.Services;
using MyIotWebsite.Hubs;

var builder = WebApplication.CreateBuilder(args) ;
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddHostedService<MqttClientService>();
builder.Services.AddSingleton<MqttClientService>();
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<SensorHub>("/sensorhub");

app.Run();