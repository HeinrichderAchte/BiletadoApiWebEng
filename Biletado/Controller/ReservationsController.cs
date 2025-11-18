using Microsoft.AspNetCore.Mvc;

namespace Biletado;

public class ReservationsController : ControllerBase
{
    // GET
    public IActionResult Index()
    {
        return View();
    }
}