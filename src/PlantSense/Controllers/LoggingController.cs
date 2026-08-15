using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PlantSense.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PlantSense.Controllers
{
    public class LoggingController : Controller
    {
        public IActionResult Index(string log = "app")
        {
            bool showSystem = log == "sys";
            ViewBag.ShowingSystem = showSystem;

            List<Log> lstLogs = new List<Log>();

            // Serilog writes daily rolling files (applog-YYYYMMDD.json); read the newest.
            // The plain names (applog.json) are matched too for older deployments.
            string pattern = showSystem ? "syslog*.json" : "applog*.json";
            string logPath = Directory.GetFiles(System.AppContext.BaseDirectory, pattern)
                .OrderByDescending(System.IO.File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (logPath != null)
            {
                // FileShare.ReadWrite — Serilog holds the current file open for writing
                using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        Log logentry = JsonConvert.DeserializeObject<Log>(line);
                        if (logentry != null)
                        {
                            logentry.Source = showSystem
                                ? "System"
                                : (string.IsNullOrEmpty(logentry.Properties?.Source) ? "App" : logentry.Properties.Source);
                            lstLogs.Add(logentry);
                        }
                    }
                    catch (JsonException)
                    {
                        // Skip malformed log lines
                    }
                }
            }

            return View(lstLogs);
        }
    }
}
