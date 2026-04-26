using ControleFutebolWeb.Services;

public class TimesController : Controller
{
    private readonly ITeamImportService _importService;

    public TimesController(ITeamImportService importService)
    {
        _importService = importService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportSerieA()
    {
        var imported = await _importService.ImportSerieATeamsAsync();
        TempData["Message"] = $"{imported} times importados.";
        return RedirectToAction("Index");
    }
}