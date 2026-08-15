using Microsoft.AspNetCore.Mvc;

namespace PlantSense.Controllers
{
    public class DocsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
