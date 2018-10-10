using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace DemoDevicesWebApplication.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Home Page";

            return View();
        }

        public ActionResult Thermostat()
        {
            ViewBag.Title = "Thermostat Simulation";
            ViewBag.Message = "Thermostat.";

            return View();
        }

        public ActionResult Edge()
        {
            ViewBag.Title = "Edge Simulation";
            ViewBag.Message = "Edge.";

            return View();
        }

        public ActionResult Service()
        {
            ViewBag.Title = "Service Simulation";
            ViewBag.Message = "Service.";

            return View();
        }
    }
}
