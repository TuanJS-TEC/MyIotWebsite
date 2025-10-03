using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using MyIotWebsite.Data;
using MyIotWebsite.Hubs;
using MyIotWebsite.Models;
using MyIotWebsite.Services;

namespace MyIotWebsite.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IotApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly MqttClientService _mqttService;
        private readonly IWebHostEnvironment _env;

        public IotApiController(ApplicationDbContext context, MqttClientService mqttService)
        {
            _context = context;
            _mqttService = mqttService;
            _env = _env;
        }

        // --- API CHO SENSOR DATA ---

        // GET: api/IotApi/sensordata/latest
        [HttpGet("sensordata/latest")]
        public async Task<ActionResult<SensorData>> GetLatestSensorData()
        {
            var latestData = await _context.SensorData
                                           .OrderByDescending(s => s.Timestamp)
                                           .FirstOrDefaultAsync();

            if (latestData == null)
            {
                return NotFound();
            }

            return Ok(latestData);
        }

        // GET: api/IotApi/sensordata/history
        [HttpGet("sensordata/history")]
        public async Task<ActionResult<IEnumerable<SensorData>>> GetSensorDataHistory()
        {
            var historyData = await _context.SensorData
                .OrderByDescending(s => s.Timestamp)
                .Take(30)
                .ToListAsync();
            
            historyData.Reverse();
            
            return Ok(historyData);
        }
        
        // GET: api/IotApi/sensordata/all
        [HttpGet("sensordata/search")]
        public async Task<ActionResult<PaginatedResponse<SensorData>>> SearchAllSensors(
            [FromQuery] string? searchTerm,
            [FromQuery] string searchType = "smart",
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = _context.SensorData.AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                if (searchType == "smart")
                {
                    // --- LOGIC CHO TÌM KIẾM "SMART" ---
                    if (DateTime.TryParse(searchTerm, out var searchDate))
                    {
                        var startDate = searchDate.Date.ToUniversalTime();
                        var endDate = startDate.AddDays(1);
                        query = query.Where(s => s.Timestamp >= startDate && s.Timestamp < endDate);
                    }
                    else if (searchTerm.StartsWith(">") || searchTerm.StartsWith("<") || searchTerm.StartsWith("="))
                    {
                        var op = searchTerm.Substring(0, 1);
                        if (double.TryParse(searchTerm.Substring(1), out var value))
                        {
                            if (op == ">") query = query.Where(s => s.Temperature > value || s.Humidity > value || s.Light > value);
                            else if (op == "<") query = query.Where(s => s.Temperature < value || s.Humidity < value || s.Light < value);
                            else if (op == "=") query = query.Where(s => s.Temperature == value || s.Humidity == value || s.Light == value);
                        }
                    }
                    else if (long.TryParse(searchTerm, out var numericValue))
                    {
                        double tolerance = 5; 
                        query = query.Where(s => 
                            // TÌM KIẾM THEO ID (khớp chính xác)
                            s.Id == numericValue || 
                    
                            (s.Temperature >= numericValue - tolerance && s.Temperature <= numericValue + tolerance) ||
                            (s.Humidity >= numericValue - tolerance && s.Humidity <= numericValue + tolerance) ||
                            (s.Light >= numericValue - tolerance && s.Light <= numericValue + tolerance)
                        );
                    }
                }
                else 
                {
                    // --- LOGIC CHO TÌM KIẾM THEO TỪNG LOẠI CỤ THỂ ---
                    if (double.TryParse(searchTerm, out var numericValue)) 
                    {
                        double tolerance = 5;
                        switch (searchType)
                        {
                            case "temperature":
                                query = query.Where(s => s.Temperature >= numericValue - tolerance && s.Temperature <= numericValue + tolerance);
                                break;
                            case "humidity":
                                query = query.Where(s => s.Humidity >= numericValue - tolerance && s.Humidity <= numericValue + tolerance);
                                break;
                            case "light":
                                query = query.Where(s => s.Light >= numericValue - tolerance && s.Light <= numericValue + tolerance);
                                break;
                        }
                    }
                }
            }

            query = query.OrderByDescending(s => s.Timestamp);
    
            // Logic phân trang (giữ nguyên)
            var totalRecords = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            var pagedData = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

            var response = new PaginatedResponse<SensorData>
            {
                Data = pagedData,
                PageNumber = pageNumber,
                TotalPages = totalPages
            };
    
            return Ok(response);
        }
        
        // --- API CHO THIẾT BỊ  ---

        // GET: api/IotApi/devicestates
        [HttpGet("devicestates")]
        public async Task<ActionResult<IEnumerable<ActionHistory>>> GetDeviceStates()
        {
            var latestStates = await _context.ActionHistories
                .GroupBy(h => h.DeviceName)
                .Select(g => g.OrderByDescending(h => h.Timestamp).FirstOrDefault())
                .ToListAsync();

            return Ok(latestStates);
        }

        // POST: api/IotApi/devices/{deviceName}/toggle
        [HttpPost("devices/{deviceName}/toggle")]
        public async Task<IActionResult> ToggleDevice(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
            {
                return BadRequest("Device name cannot be empty.");
            }

            var lastState = await _context.ActionHistories
                .Where(h => h.DeviceName.ToLower() == deviceName.ToLower())
                .OrderByDescending(h => h.Timestamp)
                .FirstOrDefaultAsync();

            bool currentStateIsOn = lastState?.IsOn ?? false;
            
            var newAction = new ActionHistory
            {
                DeviceName = deviceName,
                IsOn = !currentStateIsOn,
                Timestamp = DateTime.UtcNow
            };

            _context.ActionHistories.Add(newAction);
            await _context.SaveChangesAsync(); 

            try
            {
                string deviceControlName = deviceName.ToLower() == "light" ? "bulb" : deviceName.ToLower();

                string payload = $"{deviceControlName}_{(newAction.IsOn ? "on" : "off")}";
                
                await _mqttService.PublishAsync("control/led", payload);
                
                Console.WriteLine($"Published MQTT message to 'control/led': {payload}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error publishing MQTT message: {ex.Message}");
            }

            return Ok(newAction);
        }
        
        // GET: api/IotApi/actionhistory
        [HttpGet("actionhistory")]
        public async Task<ActionResult<PaginatedResponse<ActionHistory>>> GetActionHistory(
            [FromQuery] string? deviceName,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10) 
        {
            var query = _context.ActionHistories.AsQueryable();

            if (!string.IsNullOrEmpty(deviceName))
            {
                query = query.Where(h => h.DeviceName.ToLower().Contains(deviceName.ToLower()));
            }

            query = query.OrderByDescending(h => h.Timestamp);
    
            var totalRecords = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            var pagedData = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var response = new PaginatedResponse<ActionHistory>
            {
                Data = pagedData,
                PageNumber = pageNumber,
                TotalPages = totalPages
            };
    
            return Ok(response);
        }
        
        //Post: api/IotApi/profile/avatar
        [HttpPost("profile/avatar")]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Avatar file cannot be empty.");
            }
            
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            
            var uploadPath = Path.Combine(_env.WebRootPath, "images");

            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }
            
            var filePath = Path.Combine(uploadPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            var publicPaht = $"/images/{fileName}";
            return Ok(new { path = publicPaht });
        }
    }
}