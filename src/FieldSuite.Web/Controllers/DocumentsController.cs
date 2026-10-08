using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace FieldSuite.Web.Controllers;

[Authorize]
public class DocumentsController : Controller
{
    private static readonly Dictionary<string, (string title, string? videoUrl)> DocMeta = new()
    {
        ["FieldSuite-User-Guide.pdf"] = ("FieldSuite User Guide", "https://www.youtube.com/results?search_query=FieldSuite+User+Guide+AI+explainer+video"),
        ["01-Dashboard.pdf"] = ("Dashboard", "https://www.youtube.com/results?search_query=FieldSuite+Dashboard+KPI+Smart+Nudges+explainer"),
        ["02-Projects.pdf"] = ("Projects", "https://www.youtube.com/results?search_query=FieldSuite+Projects+module+explainer"),
        ["03-Safety-EHS.pdf"] = ("Safety & EHS", "https://www.youtube.com/results?search_query=FieldSuite+Safety+EHS+incidents+corrective+actions+toolbox+talks"),
        ["04-Quality.pdf"] = ("Quality", "https://www.youtube.com/results?search_query=FieldSuite+Quality+Snags+Inspections+NCRs"),
        ["05-Work-Permits-ePTW.pdf"] = ("Work Permits (ePTW)", "https://www.youtube.com/results?search_query=FieldSuite+ePTW+Work+Permits+digital+workflow"),
        ["06-Estimation-BOQ.pdf"] = ("Estimation & BOQ", "https://www.youtube.com/results?search_query=FieldSuite+Estimation+BOQ+rate+suggestions+proposal"),
        ["07-Workforce.pdf"] = ("Workforce", "https://www.youtube.com/results?search_query=FieldSuite+Workforce+Attendance+Workers+Contractors"),
        ["08-AI-Assistant.pdf"] = ("AI Assistant", "https://www.youtube.com/results?search_query=FieldSuite+AI+Assistant+Ask+Draft+offline")
    };

    private readonly IWebHostEnvironment _env;

    public DocumentsController(IWebHostEnvironment env) => _env = env;

public IActionResult Index()
    {
        var docsPath = Path.Combine(_env.ContentRootPath, "wwwroot", "docs");
        var files = Directory.Exists(docsPath)
            ? Directory.GetFiles(docsPath, "*.pdf").OrderBy(f => f).ToList()
            : new List<string>();

        var model = files.Select(f =>
        {
            var name = Path.GetFileName(f);
            var info = new FileInfo(f);
            var meta = DocMeta.TryGetValue(name, out var m) ? m : (name[..^4].Replace('-', ' '), (string?)null);
            return new DocumentItem
            {
                FileName = name,
                Title = meta.Item1,
                SizeKb = (int)Math.Ceiling(info.Length / 1024.0),
                ModifiedUtc = info.LastWriteTimeUtc,
                VideoUrl = meta.Item2
            };
        }).ToList();

        return View(model);
    }
}

public class DocumentItem
{
    public string FileName { get; set; } = "";
    public string Title { get; set; } = "";
    public int SizeKb { get; set; }
    public DateTime ModifiedUtc { get; set; }
    public string? VideoUrl { get; set; }
}