using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace FieldSuite.Web.Controllers;

[Authorize]
public class DocumentsController : Controller
{
    private static readonly Dictionary<string, string> Titles = new()
    {
        ["FieldSuite-User-Guide.pdf"] = "FieldSuite User Guide",
        ["01-Dashboard.pdf"] = "Dashboard",
        ["02-Projects.pdf"] = "Projects",
        ["03-Safety-EHS.pdf"] = "Safety & EHS",
        ["04-Quality.pdf"] = "Quality",
        ["05-Work-Permits-ePTW.pdf"] = "Work Permits (ePTW)",
        ["06-Estimation-BOQ.pdf"] = "Estimation & BOQ",
        ["07-Workforce.pdf"] = "Workforce",
        ["08-AI-Assistant.pdf"] = "AI Assistant"
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
            return new DocumentItem
            {
                FileName = name,
                Title = Titles.TryGetValue(name, out var t) ? t : name[..^4].Replace('-', ' '),
                SizeKb = (int)Math.Ceiling(info.Length / 1024.0),
                ModifiedUtc = info.LastWriteTimeUtc
            };
        }).ToList();

        ViewBag.Debug = $"{_env.ContentRootPath} | {_env.WebRootPath} | {docsPath} | exists={Directory.Exists(docsPath)} | count={files.Count}";

        return View(model);
    }
}

public class DocumentItem
{
    public string FileName { get; set; } = "";
    public string Title { get; set; } = "";
    public int SizeKb { get; set; }
    public DateTime ModifiedUtc { get; set; }
}