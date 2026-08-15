using Microsoft.AspNetCore.Mvc;

namespace PlantSense.Controllers
{
    public class DevicesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
