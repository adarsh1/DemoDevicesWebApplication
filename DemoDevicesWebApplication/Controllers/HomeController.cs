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
    }
}
