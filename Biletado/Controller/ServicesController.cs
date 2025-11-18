using Microsoft.AspNetCore.Mvc;

namespace Biletado;

public class ServicesController : ControllerBase
{
    // GET
    public IActionResult Index()
    {
        return View();
    }
}