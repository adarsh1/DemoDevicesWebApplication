using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.Azure.Devices;

namespace DemoDevicesWebApplication.Controllers
{
    public class IotHubController : Controller
    {
        [HttpGet]
        public async Task<string> DeviceConnection(string hubConnectionString, string deviceId)
        {
            IotHubConnectionStringBuilder connection = IotHubConnectionStringBuilder.Create(hubConnectionString);
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(connection.ToString());

            Device device;
            try
            {
                device = await registryManager.AddDeviceAsync(new Device(deviceId));
            }
            catch (Exception)
            {
                device = await registryManager.GetDeviceAsync(deviceId);
            }

            return "HostName=" + connection.HostName + ";DeviceId=" + deviceId + ";SharedAccessKey=" + device.Authentication.SymmetricKey.PrimaryKey;
        }
    }
}