using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimulatedDevices
{
    public class Thermostat
    {
        const string targetTempProperty = "targetTempProperty";
        const string typeProperty = "Type";
        const string firmwareProperty = "Firmware";
        const string desiredFirmwareProperty = "DesiredFirmware";
        const string colorPaletteProperty = "colorPalette";

        private string deviceConnectionString;

        private DeviceClient client;
        private string DeviceId = "";
        private SemaphoreSlim deviceSemaphore;

        public float Temperature { get; private set; }
        public int TargetTemperature => _targetTemperature;

        int _targetTemperature;

        public string DesiredColorPalette { get; private set; }

        public string Firmware { get; private set; }

        public string Status { get; private set; }

        CancellationTokenSource cancelationTokenSource;
        public ConcurrentQueue<string> Messages { get; }

        Task TelemetryTask = null;
        Task UpdateTask = null;
        Task MessagesTask = null;

        public Thermostat(string connectionString)
        {
            deviceConnectionString = connectionString;
            client = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Amqp);
            var connectionStringBuilder = IotHubConnectionStringBuilder.Create(deviceConnectionString);
            DeviceId = connectionStringBuilder.DeviceId;
            cancelationTokenSource = new CancellationTokenSource();
            deviceSemaphore = new SemaphoreSlim(1, 1);
            Temperature = 19;
            Status = "Normal";
            DesiredColorPalette = "inferno";
            Firmware = "0.0.0.0";
            _targetTemperature = 19;
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
            properties[typeProperty] = "Thermostat";
            properties["SupportedMethods"] = "Extinguish,Increment,Decrement";
            properties["Telemetry"] = new {
                TemperatureSchema = new {
                    Interval = "00:00:01",
                    MessageTemplate = "{\"temperature\":${temperature}}",
                    MessageSchema = new {
                        Name = "temperatureSchema",
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

            }
        }

        async Task SetupCallBacks()
        {
            await client.SetMethodHandlerAsync("Extinguish", Extinguish, null);
            await client.SetMethodHandlerAsync("Increment", IncrementCloud, null);
            await client.SetMethodHandlerAsync("Decrement", DecrementCloud, null);
            await client.SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback, null);
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

        async Task<MethodResponse> Extinguish(MethodRequest methodRequest, object userContext)
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
                message.Properties.Add("$$MessageSchema", "temperatureSchema");
                message.Properties.Add("$$ContentType", "JSON");
                await client.SendEventAsync(message);
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
                catch (Exception) { };
            }
        }

        async Task UpdateTemperature(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await deviceSemaphore.WaitAsync();
                try
                {
                    string status = "Normal";
                    if (Math.Abs(Temperature - TargetTemperature) >= 0.2f)
                    {
                        if (Temperature > TargetTemperature)
                        {
                            Temperature -= 0.2f;
                            status = "Cooling";
                        }
                        else if (Temperature < TargetTemperature)
                        {
                            Temperature += 0.2f;
                            status = "Heating";
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
