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
        private const string MqttServer = "172.20.10.4";
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
                await _mqttClient.StartAsync(_mqttOptions);
                Console.WriteLine("MQTT client started successfully.");

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
                    
                    if (parts.Length == 5 &&
                        double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double temp) &&
                        double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double hum) &&
                        double.TryParse(parts[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double light) &&
                        double.TryParse(parts[3], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dust) &&
                        double.TryParse(parts[4], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double co2))
                    {
                        var sensorData = new SensorData
                        {
                            Temperature = temp,
                            Humidity = hum,
                            Light = light,
                            Dust = dust,
                            Co2 = co2,
                            Timestamp = DateTime.UtcNow
                        };

                        dbContext.SensorData.Add(sensorData);
                        await dbContext.SaveChangesAsync();
                        await hubContext.Clients.All.SendAsync("ReceiveSensorData", sensorData);

                        Console.WriteLine("Sensor data (5 values) saved to DB and pushed via SignalR.");

                        // --- LOGIC CẢNH BÁO MỚI ---
                        try
                        {
                            // Ngưỡng: Dust > 500 (50% của 1000) VÀ Co2 > 50 (50% của 100)
                            if (dust > 500 && co2 > 50)
                            {
                                // Gửi lệnh bật LED cảnh báo
                                await PublishAsync("control/alarm", "alarm_on");
                                Console.WriteLine("ALARM TRIGGERED: Dust or CO2 exceeded threshold. Sent 'alarm_on'.");
                            }
                            else
                            {
                                // Gửi lệnh tắt LED cảnh báo (nếu không vượt ngưỡng)
                                await PublishAsync("control/alarm", "alarm_off");
                                Console.WriteLine("ALARM OFF: Levels are normal. Sent 'alarm_off'.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error publishing alarm MQTT message: {ex.Message}");
                        }
                        // --- KẾT THÚC LOGIC CẢNH BÁO ---
                    }
                    else
                    {
                        Console.WriteLine($"Failed to parse sensor data payload. Expected 5 parts, but got {parts.Length}. Payload: {payload}");
                    }
                }
                else if (topic == "status/device")
                {
                    // (Giữ nguyên không thay đổi)
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