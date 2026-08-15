using Microsoft.AspNetCore.Mvc;
using PlantSense.Helpers;
using PlantSense.Models;
using System.Collections.Generic;

namespace PlantSense.Controllers
{
    public class WateringViewController : Controller
    {
        public IActionResult Index()
        {
            SolutionSettings settings = SettingsManager.ReadFromSettingsFile();
            List<SolutionSettings> lstSettings = new List<SolutionSettings>();

            lstSettings.Add(settings);

            return View(lstSettings);
        }
    }
}