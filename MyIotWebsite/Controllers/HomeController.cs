using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyIotWebsite.Models;

namespace MyIotWebsite.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        _logger.LogInformation("Home/Index was called at {time}", DateTime.Now);
        return View();
    }

    public IActionResult SensorData()
    {
        return View();
    }
    
    public IActionResult History()
    {
        return View();
    }

    public IActionResult Profile()
    {
        return View();
    }
    
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}