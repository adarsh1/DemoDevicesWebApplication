using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.Azure.Devices;
using ServiceSimulation;
using SimulatedDevice;

namespace DemoDevicesWebApplication.Controllers
{
    public class ServiceController : Controller
    {
        static SimulatedService service = null;

        [HttpPost]
        public ActionResult Initialize(string connectionString, int transportType)
        {
            service = new SimulatedService(connectionString, transportType);
            return Json(new { success = true });
        }

        [HttpPost]
        public  ActionResult Uninitialize(string connectionString)
        {
            if (service != null)
            {
                var temp = Interlocked.Exchange(ref service, null);
                temp?.Dispose();
            }
            
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<ActionResult> SendMessage(string message, string deviceId)
        {
            if (service != null)
            {
                await service.SendMessage(message, deviceId);
            }

            return Json(new { success = true });
        }

        [HttpGet]
        public ActionResult Feedback()
        {
            int batchSize = 50;
            int count = 0;
            List<FeedbackRecord> feedbackRecords = new List<FeedbackRecord>();
            if (service != null && service.Feedback.Count != 0)
            {
                while(service.Feedback.TryDequeue(out FeedbackRecord feedback) && feedback !=null && ++count <= batchSize)
                {
                    feedbackRecords.Add(feedback);
                }
            }

            if(!feedbackRecords.Any())
            {
                return null;
            }

            return Json(feedbackRecords.Select(x=>new {
                Device = x.DeviceId,
                Status = x.StatusCode.ToString(),
            }), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult FileNotification()
        {
            int batchSize = 50;
            int count = 0;
            List<FileNotification> notifications = new List<FileNotification>();
            if (service != null && service.FileNotification.Count != 0)
            {
                while (service.FileNotification.TryDequeue(out FileNotification notification) && notification != null && ++count <= batchSize)
                {
                    notifications.Add(notification);
                }
            }

            if (!notifications.Any())
            {
                return null;
            }

            return Json(notifications.Select(x => new {
                Device = x.DeviceId,
                BlobName = x.BlobName,
                BlobSize = x.BlobSizeInBytes,
            }), JsonRequestBehavior.AllowGet);
        }
    }
}