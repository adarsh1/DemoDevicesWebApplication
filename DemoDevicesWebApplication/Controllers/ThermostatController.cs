using System;
using System.Collections.Generic;
using System.Linq;
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
        public async Task<ActionResult> Initialize(string connectionString)
        {
            thermostat = new Thermostat(connectionString);
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<ActionResult> IncrementTargetTemperature()
        {
            await thermostat.Increment();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<ActionResult> DecrementTargetTemperature()
        {
            await thermostat.Decrement();
            return Json(new { success = true });
        }

        [HttpGet]
        public float Temperature()
        {
            return thermostat.Temperature;
        }

        [HttpGet]
        public int TargetTemperature()
        {
            return thermostat.TargetTemperature;
        }

        [HttpGet]
        public string ColorPalette()
        {
            return thermostat.DesiredColorPalette;
        }

        [HttpGet]
        public string Status()
        {
            return thermostat.Status;
        }

        [HttpGet]
        public string Message()
        {
            string message = null;
            if (thermostat.Messages.Count != 0)
            {
                thermostat.Messages.TryDequeue(out message);
            }
            return message;
        }
    }
}