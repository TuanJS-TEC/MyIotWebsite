using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using MyIotWebsite.Data;
using MyIotWebsite.Hubs;
using MyIotWebsite.Models;
using MyIotWebsite.Services;
using System.Globalization;
using System.Linq.Expressions;

namespace MyIotWebsite.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IotApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly MqttClientService _mqttService;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<SensorHub> _hubContext;

        public IotApiController(
            ApplicationDbContext context, 
            MqttClientService mqttService, 
            IWebHostEnvironment env,
            IHubContext<SensorHub> hubContext) 
        {
            _context = context;
            _mqttService = mqttService;
            _env = env;
            _hubContext = hubContext;
        }
        
        // --- API CHO SENSOR DATA ---
        [HttpGet("sensordata/latest")]
        public async Task<ActionResult<SensorData>> GetLatestSensorData()
        {
            var latestData = await _context.SensorData.OrderByDescending(s => s.Timestamp).FirstOrDefaultAsync();
            if (latestData == null) return NotFound();
            return Ok(latestData);
        }

        [HttpGet("sensordata/history")]
        public async Task<ActionResult<IEnumerable<SensorData>>> GetSensorDataHistory()
        {
            var historyData = await _context.SensorData.OrderByDescending(s => s.Timestamp).Take(30).ToListAsync();
            historyData.Reverse();
            return Ok(historyData);
        }
        
        [HttpGet("sensordata/search")]
        public async Task<ActionResult<PaginatedResponse<SensorData>>> SearchAllSensors(
            [FromQuery] string? searchTerm,
            [FromQuery] string searchType = "ALL",
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string sortBy = "timestamp",
            [FromQuery] string sortOrder = "desc")
        {
            var query = _context.SensorData.AsQueryable();
            // Phần 1: Lọc dữ liệu (Filtering)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                if (searchType == "ALL")
                {
                    string[] formats = {
                        "d/M/yyyy HH:mm:ss", "d/M/yyyy HH:mm", "d/M/yyyy HH",
                        "d/M/yy HH:mm:ss", "d/M/yy HH:mm", "d/M/yy HH",
                        "HH:mm:ss d/M/yyyy", "HH:mm d/M/yyyy", "HH d/M/yyyy", 
                        "d/M/yy", "d/M/yyyy", 
                        "yyyy-MM-dd", "yyyy-MM-dd HH", 
                        "HH:mm:ss", "HH:mm"
                    };
                    
                    if (DateTime.TryParseExact(searchTerm, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                    {
                        DateTime searchDate = !searchTerm.Contains('/') && !searchTerm.Contains('-') 
                            ? DateTime.Today.Add(parsedDate.TimeOfDay) 
                            : parsedDate;
            
                        DateTime localSearchDate = DateTime.SpecifyKind(searchDate, DateTimeKind.Local);
                
                        DateTime startDateUtc, endDateUtc;
                        var colonCount = searchTerm.Count(c => c == ':');
                        if (colonCount == 2) 
                        {
                            startDateUtc = localSearchDate.ToUniversalTime();
                            endDateUtc = startDateUtc.AddSeconds(1);
                        }
                        else if (colonCount == 1) 
                        {
                            var searchMinuteUtc = localSearchDate.ToUniversalTime();
                            startDateUtc = new DateTime(searchMinuteUtc.Year, searchMinuteUtc.Month, searchMinuteUtc.Day, 
                                searchMinuteUtc.Hour, searchMinuteUtc.Minute, 0, DateTimeKind.Utc);
                            endDateUtc = startDateUtc.AddMinutes(1);
                        }
                        else if (colonCount == 0 && (searchTerm.Contains('/') || searchTerm.Contains('-')) && searchTerm.Contains(' '))
                        {
                            var searchHourUtc = localSearchDate.ToUniversalTime();
                            startDateUtc = new DateTime(searchHourUtc.Year, searchHourUtc.Month, searchHourUtc.Day, 
                                searchHourUtc.Hour, 0, 0, DateTimeKind.Utc);
                            endDateUtc = startDateUtc.AddHours(1);
                        }
                        else 
                        {
                            var localStartDate = new DateTime(localSearchDate.Year, localSearchDate.Month, localSearchDate.Day, 0, 0, 0, DateTimeKind.Local);
                            var localEndDate = localStartDate.AddDays(1);
                            startDateUtc = localStartDate.ToUniversalTime();
                            endDateUtc = localEndDate.ToUniversalTime();
                        }

                        query = query.Where(s => s.Timestamp >= startDateUtc && s.Timestamp < endDateUtc);
                    }
                    else if (double.TryParse(searchTerm, NumberStyles.Any, CultureInfo.InvariantCulture,
                                 out var numericValue))
                    {
                        double tolerance = 0.1; 
                        long.TryParse(searchTerm, out var longId);

                        query = query.Where(s => 
                            (longId != 0 && s.Id == longId) || 
                            (s.Temperature >= numericValue - tolerance && s.Temperature <= numericValue + tolerance) ||
                            (s.Humidity >= numericValue - tolerance && s.Humidity <= numericValue + tolerance) ||
                            (s.Light >= numericValue - tolerance && s.Light <= numericValue + tolerance) ||
                            (s.Dust >= numericValue - tolerance && s.Dust <= numericValue + tolerance) ||
                            (s.Co2 >= numericValue - tolerance && s.Co2 <= numericValue + tolerance)  
                        );
                    }
                }
                else 
                {
                    if (double.TryParse(searchTerm, NumberStyles.Any, CultureInfo.InvariantCulture, out var numericValue))
                    {
                        double epsilon = 0.05; 
                        switch (searchType)
                        {
                            case "temperature": 
                                query = query.Where(s => Math.Abs(s.Temperature - numericValue) <= epsilon); 
                                break;
                            case "humidity": 
                                query = query.Where(s => Math.Abs(s.Humidity - numericValue) <= epsilon); 
                                break;
                            case "light": 
                                query = query.Where(s => s.Light == numericValue); 
                                break;
                            case "dust": 
                                query = query.Where(s => Math.Abs(s.Dust - numericValue) <= epsilon); 
                                break;
                            case "co2": 
                                query = query.Where(s => Math.Abs(s.Co2 - numericValue) <= epsilon); 
                                break;
                            default: 
                                query = query.Where(s => false); 
                                break;
                        }
                    }
                    else
                    {
                        query = query.Where(s => false);
                    }
                }
            }
            // Phần 2: Sắp xếp dữ liệu (Sorting)
            bool isAscending = sortOrder.ToLower() == "asc";

            var sortedQuery = (sortBy.ToLower(), isAscending) switch
            {
                ("id", true) => query.OrderBy(s => s.Id),
                ("id", false) => query.OrderByDescending(s => s.Id),
                ("temperature", true) => query.OrderBy(s => s.Temperature),
                ("temperature", false) => query.OrderByDescending(s => s.Temperature),
                ("humidity", true) => query.OrderBy(s => s.Humidity),
                ("humidity", false) => query.OrderByDescending(s => s.Humidity),
                ("light", true) => query.OrderBy(s => s.Light),
                ("light", false) => query.OrderByDescending(s => s.Light),
                ("dust", true) => query.OrderBy(s => s.Dust), 
                ("dust", false) => query.OrderByDescending(s => s.Dust), 
                ("co2", true) => query.OrderBy(s => s.Co2), 
                ("co2", false) => query.OrderByDescending(s => s.Co2), 
                ("timestamp", true) => query.OrderBy(s => s.Timestamp),
                _ => query.OrderByDescending(s => s.Timestamp) 
            };

            // Phần 3: Phân trang (Pagination)
            var totalRecords = await sortedQuery.CountAsync();

            var pagedData = await sortedQuery
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var response = new PaginatedResponse<SensorData>
            {
                Data = pagedData,
                PageNumber = pageNumber,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize)
            };
            return Ok(response);
        }

        [HttpDelete("sensordata/delete-old")]
        public async Task<IActionResult> DeleteOldSensorData(
            [FromQuery] string startDate, 
            [FromQuery] string endDate)
        {
            if (!DateTime.TryParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStartDate))
            {
                return BadRequest(new { message = "Định dạng Ngày Bắt Đầu không hợp lệ. Vui lòng sử dụng 'yyyy-MM-dd'." });
            }

            if (!DateTime.TryParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedEndDate))
            {
                return BadRequest(new { message = "Định dạng Ngày Kết Thúc không hợp lệ. Vui lòng sử dụng 'yyyy-MM-dd'." });
            }

            if (parsedStartDate > parsedEndDate)
            {
                return BadRequest(new { message = "Ngày Bắt Đầu phải trước hoặc bằng Ngày Kết Thúc." });
            }

            if (parsedEndDate >= DateTime.Today)
            {
                 return BadRequest(new { message = "Ngày Kết Thúc phải là một ngày trong quá khứ. Không thể xóa dữ liệu của ngày hôm nay hoặc tương lai." });
            }
            DateTime localStartDate = DateTime.SpecifyKind(parsedStartDate, DateTimeKind.Local);
            DateTime startDateUtc = localStartDate.ToUniversalTime();

            DateTime localEndDate = DateTime.SpecifyKind(parsedEndDate.AddDays(1), DateTimeKind.Local);
            DateTime endDateUtc = localEndDate.ToUniversalTime();
            try
            {
                var recordsDeleted = await _context.SensorData
                    .Where(s => s.Timestamp >= startDateUtc && s.Timestamp < endDateUtc)
                    .ExecuteDeleteAsync();

                return Ok(new { message = $"Đã xóa thành công {recordsDeleted} bản ghi dữ liệu cảm biến (từ {parsedStartDate:dd/MM/yyyy} đến {parsedEndDate:dd/MM/yyyy})." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi xóa dữ liệu theo khoảng ngày: {ex.Message}");
                return StatusCode(500, new { message = $"Lỗi máy chủ khi xóa dữ liệu. {ex.Message}" });
            }
        }
        
        // --- API CHO THIẾT BỊ ---
        [HttpGet("devicestates")]
        public async Task<ActionResult<IEnumerable<ActionHistory>>> GetDeviceStates()
        {
            try
            {
                var deviceNames = await _context.ActionHistories
                    .Select(h => h.DeviceName)
                    .Distinct()
                    .ToListAsync();
        
                var latestStates = new List<ActionHistory>();
        
                foreach (var name in deviceNames)
                {
                    var latest = await _context.ActionHistories
                        .Where(h => h.DeviceName == name)
                        .OrderByDescending(h => h.Timestamp)
                        .FirstOrDefaultAsync();
            
                    if (latest != null)
                    {
                        latestStates.Add(latest);
                    }
                }
        
                return Ok(latestStates.OrderBy(s => s.DeviceName));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetDeviceStates: {ex.Message}");
                return StatusCode(500, "Internal server error retrieving device states.");
            }
        }
        [HttpPost("devices/{deviceName}/toggle")]
        public async Task<IActionResult> ToggleDevice(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return BadRequest("Device name cannot be empty.");

            var lastState = await _context.ActionHistories
                .Where(h => h.DeviceName.ToLower() == deviceName.ToLower())
                .OrderByDescending(h => h.Timestamp).FirstOrDefaultAsync();
                
            bool currentStateIsOn = lastState?.IsOn ?? false;
            
            try
            {
                string deviceControlName = deviceName.ToLower() == "light" ? "bulb" : deviceName.ToLower();
                string payload = $"{deviceControlName}_{(!currentStateIsOn ? "on" : "off")}";
                await _mqttService.PublishAsync("control/led", payload);
                return Accepted();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error pub MQTT message: {ex.Message}");
            }
        }
        
        // --- API CHO LỊCH SỬ ---
        [HttpGet("actionhistory")]
        public async Task<ActionResult<PaginatedResponse<ActionHistory>>> GetActionHistory(
            [FromQuery] string? searchTerm,
            [FromQuery] string deviceName = "all",
            [FromQuery] string status = "all",
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = _context.ActionHistories.AsQueryable();

            if (deviceName != "all" && !string.IsNullOrEmpty(deviceName))
            {
                query = query.Where(h => h.DeviceName.ToLower() == deviceName.ToLower());
            }

            if (status.ToLower() == "on")
            {
                query = query.Where(h => h.IsOn == true);
            }
            else if (status.ToLower() == "off")
            {
                query = query.Where(h => h.IsOn == false);
            }
    
            if (!string.IsNullOrEmpty(searchTerm))
            {
                string[] formats = {
                    "d/M/yyyy HH:mm:ss", "d/M/yyyy HH:mm", "d/M/yyyy HH",
                    "d/M/yy HH:mm:ss", "d/M/yy HH:mm",
                    "HH:mm:ss d/M/yyyy", "HH:mm d/M/yyyy",
                    "d/M/yyyy", "yyyy-MM-dd", "HH:mm:ss", "HH:mm"
                };

                if (DateTime.TryParseExact(searchTerm, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    DateTime searchDate = !searchTerm.Contains("/") && !searchTerm.Contains("-") 
                        ? DateTime.Today.Add(parsedDate.TimeOfDay) 
                        : parsedDate;

                    DateTime localSearchDate = DateTime.SpecifyKind(searchDate, DateTimeKind.Local);
                    DateTime startDateUtc, endDateUtc;
    
                    var colonCount = searchTerm.Count(c => c == ':');

                    if (colonCount == 2) 
                    {
                        startDateUtc = localSearchDate.ToUniversalTime();
                        endDateUtc = startDateUtc.AddSeconds(1);
                    }
                    else if (colonCount == 1)
                    {
                        var searchMinuteUtc = localSearchDate.ToUniversalTime();
                        startDateUtc = new DateTime(searchMinuteUtc.Year, searchMinuteUtc.Month, searchMinuteUtc.Day,
                            searchMinuteUtc.Hour, searchMinuteUtc.Minute, 0, DateTimeKind.Utc);
                        endDateUtc = startDateUtc.AddMinutes(1);
                    }
                    else if (colonCount == 0 && (searchTerm.Contains('/') || searchTerm.Contains('-')) && searchTerm.Contains(' '))
                    {
                        var searchHourUtc = localSearchDate.ToUniversalTime();
                        startDateUtc = new DateTime(searchHourUtc.Year, searchHourUtc.Month, searchHourUtc.Day, 
                            searchHourUtc.Hour, 0, 0, DateTimeKind.Utc);
                        endDateUtc = startDateUtc.AddHours(1);
                    }
                    else 
                    {
                        var localStartDate = new DateTime(localSearchDate.Year, localSearchDate.Month, localSearchDate.Day, 0, 0, 0, DateTimeKind.Local);
                        var localEndDate = localStartDate.AddDays(1);

                        startDateUtc = localStartDate.ToUniversalTime();
                        endDateUtc = localEndDate.ToUniversalTime();
                    }
                    query = query.Where(h => h.Timestamp >= startDateUtc && h.Timestamp < endDateUtc);
                }
                else if (long.TryParse(searchTerm, out var id))
                {
                    query = query.Where(h => h.Id == id);
                }
                else
                {
                    query = query.Where(h => false); 
                }
            }
            query = query.OrderByDescending(h => h.Timestamp);
            var totalRecords = await query.CountAsync();
            var pagedData = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
    
            if (!pagedData.Any() && !string.IsNullOrEmpty(searchTerm))
            {
                return NotFound("Không tìm thấy giá trị bạn mong muốn.");
            }
            var response = new PaginatedResponse<ActionHistory>
            {
                Data = pagedData,
                PageNumber = pageNumber,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize)
            };

            return Ok(response);
        }
    }
}