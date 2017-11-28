using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;

namespace FaceAPI_MVC.Web.Controllers
{
    public class AutoScaleController : Controller
    {
        // GET: AutoScale
        public ActionResult Index()
        {
            this.SimulateAutoScale();
            return View();
        }

        private void SimulateAutoScale()
        {
            int percentage = 80;
            Stopwatch timeToRun = new Stopwatch();
            timeToRun.Start();

            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                (new Thread(() =>
                {
                    Stopwatch watch = new Stopwatch();
                    watch.Start();

                    // Run for 10 minutes and then stop.
                    while (timeToRun.ElapsedMilliseconds <= 600000)
                    {
                        // Make the loop go on for "percentage" milliseconds then sleep the 
                        // remaining percentage milliseconds. So 80% utilization means work 80ms and sleep 20ms
                        if (watch.ElapsedMilliseconds > percentage)
                        {
                            Thread.Sleep(100 - percentage);
                            watch.Reset();
                            watch.Start();
                        }
                    }
                })).Start();
            }
        }
    }
}