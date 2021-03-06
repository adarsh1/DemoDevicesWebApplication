﻿using System.Web;
using System.Web.Optimization;

namespace DemoDevicesWebApplication
{
    public class BundleConfig
    {
        // For more information on bundling, visit https://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at https://modernizr.com to pick only the tests you need.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/treant").Include(
                        "~/Scripts/vendor/raphael.js",
                        "~/Scripts/vendor/jquery.easing.js",
                        "~/Scripts/Treant.js"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                      "~/Scripts/umd/popper.min.js",
                      "~/Scripts/bootstrap.min.js",
                      "~/Scripts/respond.min.js"));

            bundles.Add(new ScriptBundle("~/bundles/jsoneditor").Include(
                      "~/Scripts/jquery.json-editor.min.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap.min.css",
                      "~/Content/site.css"));

            bundles.Add(new StyleBundle("~/Content/Thermostat/css").Include(
                      "~/Content/Thermostat/thermostat.css"));

            bundles.Add(new StyleBundle("~/Content/Treant/css").Include(
                      "~/Content/Treant.css"));

            bundles.Add(new ScriptBundle("~/bundles/colorMaps").Include(
                      "~/Scripts/colormaps.js"));

            bundles.Add(new ScriptBundle("~/bundles/plotly").Include(
                      "~/Scripts/plotly-latest.min.js"));
        }
    }
}
