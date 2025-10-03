using System.Text;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MyIotWebsite.Data;
using MyIotWebsite.Hubs;
using MyIotWebsite.Models;

namespace MyIotWebsite.Services
{
    public class MqttClientService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<SensorHub> _hubContext;
        private IManagedMqttClient _mqttClient;

        // Thông tin MQTT Broker
        private const string MqttServer = "192.168.0.112"; 
        private const int MqttPort = 1883;
        private const string MqttUser = "HoangMinhTuan"; 
        private const string MqttPassword = "123";
        private const string Topic = "sensor/data";

        public MqttClientService(IServiceProvider serviceProvider, IHubContext<SensorHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
            
            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(new MqttClientOptionsBuilder()
                    .WithTcpServer(MqttServer, 1883) 
                    .WithCredentials("HoangMinhTuan", "123") 
                    .Build())
                .Build();

            _mqttClient = new MqttFactory().CreateManagedMqttClient();
            _mqttClient.ApplicationMessageReceivedAsync += OnMqttMessageReceived;
            _mqttClient.StartAsync(options).GetAwaiter().GetResult();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
           _mqttClient.SubscribeAsync("sensor/data").GetAwaiter().GetResult();
           Console.WriteLine("MQTT client subscribed to sensor/data");
           return Task.CompletedTask;
        }

        public async Task PublishAsync(string topic, string payload)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .Build();
            await _mqttClient.EnqueueAsync(message);
        }
        private async Task OnMqttMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            Console.WriteLine("Received MQTT message on topic '{0}': {1}", e.ApplicationMessage.Topic, payload);

            var parts = payload.Split(',');
            if (parts.Length == 3 && 
                double.TryParse(parts[0], out double temp) &&
                double.TryParse(parts[1], out double hum) &&
                double.TryParse(parts[2], out double light))
            {
                // Sử dụng IServiceProvider để tạo một scope mới, lấy DbContext và lưu dữ liệu
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    
                    var sensorData = new SensorData
                    {
                        Temperature = temp,
                        Humidity = hum,
                        Light = light,
                        Timestamp = DateTime.UtcNow
                    };

                    dbContext.SensorData.Add(sensorData);
                    await dbContext.SaveChangesAsync();
                    
                    await _hubContext.Clients.All.SendAsync("ReceiveSensorData", sensorData);
                    Console.WriteLine("Successfully saved sensor data to database.");
                }
            }
            else
            {
                Console.WriteLine("Failed to parse MQTT payload.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _mqttClient?.StopAsync().GetAwaiter().GetResult();
            Console.WriteLine("MQTT Client Service stopped.");
            return Task.CompletedTask;
        }
    }
}       