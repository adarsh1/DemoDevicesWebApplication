using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimulatedDevices
{
    public class Thermostat
    {
        const string targetTempProperty = "targetTempProperty";
        const string typeProperty = "Type";
        const string transportTypeProperty = "TransportType";
        const string firmwareProperty = "Firmware";
        const string desiredFirmwareProperty = "DesiredFirmware";
        const string colorPaletteProperty = "colorPalette";
        const string StandByStatus = "StandBy";
        private const string HeatingStatus = "Heating";
        private const string CoolingStatus = "Cooling";
        private const string DeviceType = "Thermostat";
        private const string MessageSchema = "temperatureSchema";
        private const string MessageTemplate = "{\"temperature\":${temperature}}";
        private const string SupportedMethodsProperty = "SupportedMethods";
        private const string TelemetryProperty = "Telemetry";
        private string deviceConnectionString;

        private DeviceClient client;
        private string DeviceId = "";
        private SemaphoreSlim deviceSemaphore;

        private Statistics stats;

        public float Temperature { get; private set; }
        public int TargetTemperature => _targetTemperature;

        TransportType transportType;

        int _targetTemperature;

        public string DesiredColorPalette { get; private set; }

        public string Firmware { get; private set; }

        public string Status { get; private set; }

        CancellationTokenSource cancelationTokenSource;
        public ConcurrentQueue<string> Messages { get; }

        Task TelemetryTask = null;
        Task UpdateTask = null;
        Task MessagesTask = null;

        class Statistics
        {
            public Statistics()
            {
                creationTime = DateTime.UtcNow;
            }

            private DateTime creationTime;
            private long exceptionsEncountered;
            private string log;

            public long ExceptionsEncountered => exceptionsEncountered;

            public TimeSpan Uptime => DateTime.UtcNow-creationTime;

            public void IncrementExceptionsEncountered()
            {
                Interlocked.Increment(ref exceptionsEncountered);
            }

            [JsonIgnore]
            public string Json => JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public Thermostat(string connectionString, int transportTypeInt)
        {
            deviceConnectionString = connectionString;
            this.transportType = (TransportType)transportTypeInt;
            client = DeviceClient.CreateFromConnectionString(deviceConnectionString, this.transportType);
            var connectionStringBuilder = IotHubConnectionStringBuilder.Create(deviceConnectionString);
            DeviceId = connectionStringBuilder.DeviceId;
            cancelationTokenSource = new CancellationTokenSource();
            deviceSemaphore = new SemaphoreSlim(1, 1);
            Temperature = 19;
            Status = StandByStatus;
            DesiredColorPalette = "inferno";
            Firmware = "0.0.0.0";
            _targetTemperature = 19;
            stats = new Statistics();
            Messages = new ConcurrentQueue<string>();
            TelemetryTask = Task.Run(() => SendTelemetry(cancelationTokenSource.Token));
            UpdateTask = Task.Run(() => UpdateTemperature(cancelationTokenSource.Token));
            MessagesTask = Task.Run(() => RecieveMessages(cancelationTokenSource.Token));
        }

        public async Task Initialize()
        {
            await SetupCallBacks();
            var twin = await client.GetTwinAsync();
            await DesiredPropertyUpdateCallback(twin.Properties.Desired, null);

            TwinCollection properties = new TwinCollection();
            properties[transportTypeProperty] = this.transportType.ToString();
            properties[typeProperty] = DeviceType;
            properties[SupportedMethodsProperty] = nameof(AirConditioning)+ "," + nameof(IncrementCloud) + "," + nameof(DecrementCloud);
            properties[TelemetryProperty] = new {
                TemperatureSchema = new {
                    Interval = "00:00:01",
                    MessageTemplate = Thermostat.MessageTemplate,
                    MessageSchema = new {
                        Name = MessageSchema,
                        Format = "JSON",
                        Fields = new {
                            temperature = "Double" 
                        }
                    }
                }
            };
            properties[firmwareProperty] = Firmware;
            try
            {
                await client.UpdateReportedPropertiesAsync(properties);
            }
            catch (Exception)
            {
                stats.IncrementExceptionsEncountered();
            }
        }

        async Task SetupCallBacks()
        {
            await client.SetMethodHandlerAsync(nameof(AirConditioning), AirConditioning, null);
            await client.SetMethodHandlerAsync(nameof(IncrementCloud), IncrementCloud, null);
            await client.SetMethodHandlerAsync(nameof(DecrementCloud), DecrementCloud, null);
            await client.SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback, null);
        }

        public async Task UploadStatistics()
        {
            string fileName = $"{DeviceId}_Stats/{DateTime.UtcNow.ToString("yyyy/MMMM/dd/hh:MM tt")}.json";

            using (var sourceData = new MemoryStream(System.Text.Encoding.Default.GetBytes(stats.Json)))
            {
                try
                {
                    await client.UploadToBlobAsync(fileName, sourceData);
                    Messages.Enqueue("Device data uploaded successfully");
                }
                catch (Exception)
                {
                    stats.IncrementExceptionsEncountered();
                    throw;
                }
            }

        }

        async Task DesiredPropertyUpdateCallback(TwinCollection desiredProperties, object userContext)
        {
            foreach(KeyValuePair<string, object> property in desiredProperties)
            {
                switch (property.Key)
                {
                    case colorPaletteProperty: DesiredColorPalette = desiredProperties[colorPaletteProperty];
                        break;
                    case desiredFirmwareProperty:
                        if(!Firmware.Equals(desiredProperties[desiredFirmwareProperty]))
                        {
                            await deviceSemaphore.WaitAsync();
                            try
                            {
                                var tempStatus = Status;
                                Status = "Updating Firmware...";
                                await Task.Delay(5000);
                                Firmware = desiredProperties[desiredFirmwareProperty];
                                Status = tempStatus;
                                TwinCollection properties = new TwinCollection();
                                properties[desiredFirmwareProperty] = Firmware;
                                try
                                {
                                    await client.UpdateReportedPropertiesAsync(properties);
                                }
                                catch (Exception)
                                {
                                    stats.IncrementExceptionsEncountered();
                                }
                            }
                            finally
                            {
                                deviceSemaphore.Release();
                            }
                        }
                        break;
                    default: Console.WriteLine(property.ToString());
                        break;
                }
            }

        }

        async Task<MethodResponse> AirConditioning(MethodRequest methodRequest, object userContext)
        {
            Messages.Enqueue("Critical temperature reached. Cooling Requested");
            await SetTargetInternal(16);
            return new MethodResponse(200);
        }

        async Task SetTargetInternal(int target)
        {
            Interlocked.Exchange(ref _targetTemperature, target);
            await UpdateTargetTemperature();
        }

        async Task<MethodResponse> IncrementCloud(MethodRequest methodRequest, object userContext)
        {
            await Increment();
            return new MethodResponse(200);
        }

        async Task<MethodResponse> DecrementCloud(MethodRequest methodRequest, object userContext)
        {
            await Decrement();
            return new MethodResponse(200);
        }

        public async Task Increment()
        {
            Interlocked.Increment(ref _targetTemperature);
            await UpdateTargetTemperature();
        }

        public async Task Decrement()
        {
            Interlocked.Decrement(ref _targetTemperature);
            await UpdateTargetTemperature();
        }

        private async Task UpdateTargetTemperature()
        {
            TwinCollection properties = new TwinCollection();
            properties[targetTempProperty] = TargetTemperature;
            try
            {
                await client.UpdateReportedPropertiesAsync(properties);
            }
            catch(Exception)
            {
                stats.IncrementExceptionsEncountered();
            }
        }

        async Task SendTelemetry(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var telemetryDataPoint = new
                {
                    temperature = Temperature
                };
                var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
                var message = new Message(Encoding.UTF8.GetBytes(messageString));
                message.Properties.Add("$$CreationTimeUtc", DateTime.UtcNow.ToString());
                message.Properties.Add("$$MessageSchema", MessageSchema);
                message.Properties.Add("$$ContentType", "JSON");
                message.Properties.Add("$ThresholdExceeded", telemetryDataPoint.temperature>25?"True":"False");
                try
                {
                    await client.SendEventAsync(message);
                }
                catch (Exception)
                {
                    stats.IncrementExceptionsEncountered();
                }

                await Task.Delay(1000);
            }
        }

        async Task RecieveMessages(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var message = await client.ReceiveAsync();
                    if (message != null)
                    {
                        Messages.Enqueue(Encoding.ASCII.GetString(message.GetBytes()));
                        await client.CompleteAsync(message);
                    }
                }
                catch (Exception)
                {
                    stats.IncrementExceptionsEncountered();
                }
            }
        }

        async Task UpdateTemperature(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await deviceSemaphore.WaitAsync();
                try
                {
                    string status = StandByStatus;
                    if (Math.Abs(Temperature - TargetTemperature) >= 0.2f)
                    {
                        if (Temperature > TargetTemperature)
                        {
                            Temperature -= 0.2f;
                            status = CoolingStatus;
                        }
                        else if (Temperature < TargetTemperature)
                        {
                            Temperature += 0.2f;
                            status = HeatingStatus;
                        }
                    }
                    Status = status;

                    await Task.Delay(350);
                }
                finally{
                    deviceSemaphore.Release();
                }
            }
        }

        public async Task Dispose()
        {
            cancelationTokenSource.Cancel();
            if (client != null)
            {
                await client.CloseAsync();
                client?.Dispose();
                deviceSemaphore?.Dispose();
            }
        }
    }
}
