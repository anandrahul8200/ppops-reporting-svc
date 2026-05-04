using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton<GenerationClient>();
builder.Services.AddSingleton<SensorClient>();
builder.Services.AddSingleton<NotificationClient>();
var app = builder.Build();
app.MapControllers();
app.MapGet("/health", () => "healthy");
app.Run();

namespace ReportingSvc
{
    public class GenerationClient
    {
        private readonly HttpClient _http = new();
        private readonly string _baseUrl = Environment.GetEnvironmentVariable("GENERATION_SVC_URL") ?? "http://localhost:8081";
        public async Task<string> GetSummaryAsync() => await _http.GetStringAsync($"{_baseUrl}/api/generation/summary");
        public async Task<string> GetLiveAsync(int plantId) => await _http.GetStringAsync($"{_baseUrl}/api/generation/live?plantId={plantId}");
    }

    public class SensorClient
    {
        private readonly HttpClient _http = new();
        private readonly string _baseUrl = Environment.GetEnvironmentVariable("SENSOR_SVC_URL") ?? "http://localhost:8082";
        public async Task<string> GetReadingAsync(int equipmentId) => await _http.GetStringAsync($"{_baseUrl}/api/sensor/{equipmentId}");
        public async Task<string> GetBulkAsync(string ids) => await _http.GetStringAsync($"{_baseUrl}/api/sensor/bulk?ids={ids}");
    }

    public class NotificationClient
    {
        private readonly HttpClient _http = new();
        private readonly string _baseUrl = Environment.GetEnvironmentVariable("NOTIFICATION_SVC_URL") ?? "http://localhost:8083";
        public async Task<HttpResponseMessage> SendAlertAsync(string recipient, string subject, string body) =>
            await _http.PostAsJsonAsync($"{_baseUrl}/api/notification/alert", new { Recipient = recipient, Subject = subject, Body = body, AlertType = "report" });
    }
}

namespace ReportingSvc.Controllers
{
    [ApiController]
    [Route("api/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly GenerationClient _generation;
        private readonly SensorClient _sensor;
        private readonly NotificationClient _notification;

        public ReportsController(GenerationClient generation, SensorClient sensor, NotificationClient notification)
        { _generation = generation; _sensor = sensor; _notification = notification; }

        [HttpGet("daily-generation")]
        public async Task<IActionResult> DailyGeneration()
        {
            var summary = await _generation.GetSummaryAsync();
            return Ok(new { Report = "DailyGeneration", Data = summary, GeneratedAt = DateTime.UtcNow });
        }

        [HttpGet("equipment-health/{equipmentId}")]
        public async Task<IActionResult> EquipmentHealth(int equipmentId)
        {
            var reading = await _sensor.GetReadingAsync(equipmentId);
            return Ok(new { Report = "EquipmentHealth", EquipmentId = equipmentId, SensorData = reading, GeneratedAt = DateTime.UtcNow });
        }

        [HttpPost("send-report")]
        public async Task<IActionResult> SendReport([FromBody] SendReportRequest request)
        {
            var summary = await _generation.GetSummaryAsync();
            await _notification.SendAlertAsync(request.Recipient, "Daily Report", summary);
            return Ok(new { Status = "sent", Recipient = request.Recipient });
        }
    }
    public class SendReportRequest { public string Recipient { get; set; } }
}
