using FieldSuite.Web.Models;

namespace FieldSuite.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
