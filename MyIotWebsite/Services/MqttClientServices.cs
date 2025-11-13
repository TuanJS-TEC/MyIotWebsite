using System.Text;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MyIotWebsite.Data;
using MyIotWebsite.Hubs;
using MyIotWebsite.Models;
using System.Text.Json;

namespace MyIotWebsite.Services
{
    public class MqttClientService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<SensorHub> _hubContext;
        private readonly IManagedMqttClient _mqttClient;
        private readonly ManagedMqttClientOptions _mqttOptions; 

        // Thông tin MQTT Broker
        private const string MqttServer = "192.168.0.107";
        private const int MqttPort = 1883;
        private const string MqttUser = "HoangMinhTuan";
        private const string MqttPassword = "123";
        private const string Topic = "sensor/data";

        public MqttClientService(IServiceProvider serviceProvider, IHubContext<SensorHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;

            _mqttOptions = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(new MqttClientOptionsBuilder()
                    .WithTcpServer(MqttServer, MqttPort) 
                    .WithCredentials(MqttUser, MqttPassword) 
                    .Build())
                .Build();

            _mqttClient = new MqttFactory().CreateManagedMqttClient();
            _mqttClient.ApplicationMessageReceivedAsync += OnMqttMessageReceived;

        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                // 1. Kết nối tới broker
                await _mqttClient.StartAsync(_mqttOptions);
                Console.WriteLine("MQTT client started successfully.");

                // 2. Subcribe các topic
                await _mqttClient.SubscribeAsync("sensor/data");
                await _mqttClient.SubscribeAsync("status/device");
                Console.WriteLine("MQTT client subscribed to topics.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL ERROR] Failed to start or subscribe MQTT client. Application will stop.");
                Console.WriteLine($"[FATAL ERROR] Details: {ex.Message}");
                throw; 
            }
            
            // catch (Exception ex)
            // {
            //     Console.WriteLine($"Failed to start or subscribe MQTT client: {ex.Message}");
            // }
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
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            Console.WriteLine("Received message on topic '{0}': {1}", topic, payload);

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<SensorHub>>();

                if (topic == "sensor/data")
                {
                    var parts = payload.Split(',');
                    if (parts.Length == 3 &&
                        double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double temp) &&
                        double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double hum) &&
                        double.TryParse(parts[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double light))
                    {
                        var sensorData = new SensorData
                        {
                            Temperature = temp,
                            Humidity = hum,
                            Light = light,
                            Timestamp = DateTime.UtcNow
                        };

                        dbContext.SensorData.Add(sensorData);
                        await dbContext.SaveChangesAsync();
                        await hubContext.Clients.All.SendAsync("ReceiveSensorData", sensorData);

                        Console.WriteLine("Sensor data saved to DB and pushed via SignalR.");
                    }
                    else
                    {
                        Console.WriteLine("Failed to parse sensor data payload.");
                    }
                }
                else if (topic == "status/device")
                {
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(payload))
                        {
                            JsonElement root = doc.RootElement;

                            if (root.TryGetProperty("deviceName", out JsonElement deviceNameElement) &&
                                root.TryGetProperty("isOn", out JsonElement isOnElement))
                            {
                                string deviceName = deviceNameElement.GetString();
                                bool isOn = isOnElement.GetBoolean();

                                if (!string.IsNullOrEmpty(deviceName))
                                {
                                    var newAction = new ActionHistory
                                    {
                                        DeviceName = deviceName,
                                        IsOn = isOn,
                                        Timestamp = DateTime.UtcNow
                                    };

                                    dbContext.ActionHistories.Add(newAction);
                                    await dbContext.SaveChangesAsync();

                                    await hubContext.Clients.All.SendAsync("ReceiveActionHistory", newAction);

                                    Console.WriteLine("Device status feedback saved to DB and pushed via SignalR.");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing device status feedback: {ex.Message}");
                    }
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_mqttClient != null)
            {
                await _mqttClient.StopAsync();
            }
            Console.WriteLine("MQTT Client Service stopped.");
        }
    }
}