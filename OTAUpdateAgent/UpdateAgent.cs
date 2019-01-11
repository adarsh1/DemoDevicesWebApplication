using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;

namespace OTAUpdateAgent
{
    public enum UpdateStatus
    {
        Ready,
        UpdatePending,
        RebootPending,
        Updating,
        RollingBack,
        UpdateFailed,
        Idle,
        Busy
    }
    public class UpdateAgent
    {
        IUpdateableDevice managedDevice;
        UpdateStatus Status { get; set; }

        private const string SupportedMethodsProperty = "SupportedMethods";
        private const string UpdateStatusProperty = nameof(UpdateStatus);

        private DeviceClient client;
        private string DeviceId = "";

        string update;
        string previousFirmware;
        public UpdateAgent(string connectionString, IUpdateableDevice device)
        {
            managedDevice = device;
            Status = UpdateStatus.Idle;
            client = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Amqp);
            var connectionStringBuilder = IotHubConnectionStringBuilder.Create(connectionString);
            DeviceId = connectionStringBuilder.DeviceId;
        }

        public async Task Initialize()
        {
            var twin = await client.GetTwinAsync();
            TwinCollection properties = new TwinCollection();
            properties[SupportedMethodsProperty] = nameof(PrepareForUpdate) + "," + nameof(DownloadUpdate) + "," + nameof(InstallUpdate) + "," + nameof(Rollback) + "," + nameof(CanUpdate) + "," + nameof(Reboot);
            properties[UpdateStatusProperty] = Status.ToString();
            try
            {
                await client.UpdateReportedPropertiesAsync(properties);
                await SetupMethodCallBacks();
            }
            catch (Exception e)
            {
            }
        }

        async Task SetupMethodCallBacks()
        {
            await client.SetMethodHandlerAsync(nameof(PrepareForUpdate), PrepareForUpdate, null);
            await client.SetMethodHandlerAsync(nameof(DownloadUpdate), DownloadUpdate, null);
            await client.SetMethodHandlerAsync(nameof(InstallUpdate), InstallUpdate, null);
            await client.SetMethodHandlerAsync(nameof(Rollback), Rollback, null);
            await client.SetMethodHandlerAsync(nameof(CanUpdate), CanUpdate, null);
            await client.SetMethodHandlerAsync(nameof(Reboot), Reboot, null);
        }

        async Task UpdateReportedStatus(UpdateStatus newStatus)
        {
            Status = newStatus;
            TwinCollection properties = new TwinCollection();
            properties[UpdateStatusProperty] = Status.ToString();
            try
            {
                await client.UpdateReportedPropertiesAsync(properties);
            }
            catch (Exception e)
            {
            }
        }

        async Task<MethodResponse> PrepareForUpdate(MethodRequest methodRequest, object userContext)
        {
            await Task.Delay(1000);
            await UpdateReportedStatus(UpdateStatus.Ready);
            return new MethodResponse(200);
        }

        async Task<MethodResponse> DownloadUpdate(MethodRequest methodRequest, object userContext)
        {
            await Task.Delay(5000);
            update = methodRequest.DataAsJson;
            await UpdateReportedStatus(UpdateStatus.UpdatePending);
            return new MethodResponse(200);
        }

        async Task<MethodResponse> InstallUpdate(MethodRequest methodRequest, object userContext)
        {
            previousFirmware = managedDevice.Firmware;
            await Task.Delay(3000);
            if(DateTime.Now.Ticks % 10 == 0)
            {
                await UpdateReportedStatus(UpdateStatus.RollingBack);
            }
            else
            {
                await managedDevice.UpdateFirmware(update);
                update = "";
                await UpdateReportedStatus(UpdateStatus.RebootPending);
            }

            return new MethodResponse(200);
        }

        async Task<MethodResponse> Reboot(MethodRequest methodRequest, object userContext)
        {
            await Task.Delay(3000);
            await UpdateReportedStatus(UpdateStatus.Idle);
            return new MethodResponse(200);
        }

        Task<MethodResponse> CanUpdate(MethodRequest methodRequest, object userContext)
        {
            return Task.FromResult(new MethodResponse(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(managedDevice.CanUpdate())), 200));
        }

        async Task<MethodResponse> Rollback(MethodRequest methodRequest, object userContext)
        {
            await Task.Delay(3000);
            await managedDevice.UpdateFirmware(previousFirmware);
            if(update == "")
            {
                await UpdateReportedStatus(UpdateStatus.Idle);
            }
            else
            {
                await UpdateReportedStatus(UpdateStatus.UpdateFailed);
            }
            return new MethodResponse(200);
        }

        public async Task Dispose()
        {
            if (client != null)
            {
                await client.CloseAsync();
                client?.Dispose();
            }
        }
    }
}
