﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using OTAUpdateAgent;
using SimulatedDevice;

namespace DemoDevicesWebApplication.Controllers
{
    public class ThermostatController : Controller
    {
        static Thermostat thermostat = null;
        static PnPUpdateAgent updateAgent = null;

        [HttpPost]
        public async Task<ActionResult> Initialize(string connectionString, int transportType)
        {
            await Uninitialize(connectionString);
            thermostat = new Thermostat(connectionString, transportType);
            await thermostat.Initialize();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<ActionResult> InitializeWithUpdate(string connectionString,string updateAgentConnectionString, int transportType)
        {
            await Initialize(connectionString, transportType);
            updateAgent = new PnPUpdateAgent(updateAgentConnectionString, thermostat);
            await updateAgent.Initialize();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<ActionResult> InitializeX509(string deviceId, string hostName, string certPath, int transportType)
        {
            try
            {
                await Uninitialize(deviceId);
                thermostat = new Thermostat(deviceId, certPath, hostName, transportType);
                await thermostat.Initialize();
                return Json(new { success = true });
            }
            catch(Exception ex)
            {
                return Json(new { success = false, error = ex.ToString() , path = System.Web.HttpRuntime.AppDomainAppPath });
            }
        }

        [HttpPost]
        public async Task<ActionResult> Uninitialize(string connectionString)
        {
            if (thermostat != null)
            {
                var temp = Interlocked.Exchange(ref thermostat, null);
                await temp?.Dispose();
                if (updateAgent != null)
                {
                    var temp2 = Interlocked.Exchange(ref updateAgent, null);
                    await temp2?.Dispose();
                }
            }
            
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<ActionResult> UploadStatistics()
        {
            if (thermostat != null)
            {
                await thermostat.UploadStatistics();
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
        public string Firmware()
        {
            return thermostat?.Firmware ?? "0.0.0.0";
        }

        [HttpGet]
        public string Status()
        {
            return thermostat?.Status ?? "Not Connected";
        }

        [HttpGet]
        public ActionResult Message()
        {
            Tuple<string, Thermostat.MessageType> message = null;
            if (thermostat != null && thermostat.Messages.Count != 0)
            {
                thermostat.Messages.TryDequeue(out message);
            }

            if(message == null)
            {
                return null;
            }

            return Json(new {
                Message = message.Item1,
                Type = message.Item2.ToString()
            }, JsonRequestBehavior.AllowGet);
        }
    }
}