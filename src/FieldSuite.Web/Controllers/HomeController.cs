using FieldSuite.Web.Models;

namespace FieldSuite.Web.Controllers;

public class HomeController : Controller
{
    /// <summary>
    /// Displays error page with request ID.
    /// </summary>
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
