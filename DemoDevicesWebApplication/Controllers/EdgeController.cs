using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using SimulatedDevice;

namespace DemoDevicesWebApplication.Controllers
{
    public class EdgeController : Controller
    {
        static DeviceTelemetryReader reader = null;
        const string temperatureSensorTelemetrySchema =
@"{
  '$schema': 'http://json-schema.org/draft-04/schema#',
  'type': 'object',
  'required': [
    'machine',
    'ambient',
    'timeCreated'
  ],
  'properties': {
    'machine': {
      '$ref': '#/definitions/Machine'
    },
    'ambient': {
      '$ref': '#/definitions/Ambient'
    },
    'timeCreated': {
      'type': 'string',
      'format': 'date-time'
    }
  },
  'definitions': {
    'Machine': {
      'type': 'object',
      'required': [
        'temperature',
        'pressure'
        ],
      'properties': {
        'temperature': {
          'type': 'number'
        },
        'pressure': {
          'type': 'number'
        }
      }
    },
    'Ambient': {
      'type': 'object',
      'required': [
        'temperature',
        'humidity'
        ],
      'properties': {
        'temperature': {
          'type': 'number'
        },
        'humidity': {
          'type': 'integer'
        }
      }
    }
  }
}";

        [HttpPost]
        public async Task<ActionResult> Initialize(string connectionString, string hubName)
        {
            reader = new DeviceTelemetryReader(connectionString, hubName);
            await reader.Start("$Default", temperatureSensorTelemetrySchema);
            return Json(new { success = true });
        }

        [HttpPost]
        public ActionResult Uninitialize(string connectionString)
        {
            if (reader != null)
            {
                var temp = Interlocked.Exchange(ref reader, null);
                temp?.Dispose();
            }
            
            return Json(new { success = true });
        }

        [HttpGet]
        public ActionResult Messages(int count)
        {
            IEnumerable<KeyValuePair<string, string>> result;
            result = reader?.GetMessageBatch(count).ToList();
            if (!(result?.Any()??false))
            {
                return null;
            }

            return Json(result.Select(x=>new { Key = x.Key, Value =x.Value}), JsonRequestBehavior.AllowGet);
        }
    }
}