using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using SimulatedDevices;

namespace DemoDevicesWebApplication.Controllers
{
    public class ThermostatController : Controller
    {
        static Thermostat thermostat = null;

        [HttpPost]
        public ActionResult Initialize(string connectionString)
        {
            thermostat = new Thermostat(connectionString);
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<ActionResult> Uninitialize(string connectionString)
        {
            var temp = Interlocked.Exchange(ref thermostat, null);
            if (temp != null)
            {
                await temp?.Dispose();
            }
            
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<ActionResult> IncrementTargetTemperature()
        {
            if (thermostat != null)
            {
                await thermostat.Increment();
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<ActionResult> DecrementTargetTemperature()
        {
            if (thermostat != null)
            {
                await thermostat.Decrement();
            }

            return Json(new { success = true });
        }

        [HttpGet]
        public float Temperature()
        {
            return thermostat?.Temperature ?? 19.0f;
        }

        [HttpGet]
        public int TargetTemperature()
        {
            return thermostat?.TargetTemperature ?? 19;
        }

        [HttpGet]
        public string ColorPalette()
        {
            return thermostat?.DesiredColorPalette ?? string.Empty;
        }

        [HttpGet]
        public string Status()
        {
            return thermostat?.Status ?? "Not Connected";
        }

        [HttpGet]
        public string Message()
        {
            string message = null;
            if (thermostat != null && thermostat.Messages.Count != 0)
            {
                thermostat.Messages.TryDequeue(out message);
            }
            return message;
        }
    }
}