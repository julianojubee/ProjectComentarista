using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Controllers
{
    public class AdminController : Controller
    {
        private readonly FutebolContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(FutebolContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ── Mapeamento: variantes de grafia → nome canônico da Copa do Mundo 2026 ──
        // 48 seleções da Copa do Mundo FIFA 2026
        private static readonly Dictionary<string, string> _variantesParaCanonical = new(StringComparer.OrdinalIgnoreCase)
        {
            // ── CONMEBOL ─────────────────────────────────────────────────────────
            ["brasil"]                        = "Brasil",
            ["brazil"]                        = "Brasil",
            ["argentina"]                     = "Argentina",
            ["uruguai"]                       = "Uruguai",
            ["uruguay"]                       = "Uruguai",
            ["colômbia"]                      = "Colômbia",
            ["colombia"]                      = "Colômbia",
            ["equador"]                       = "Equador",
            ["ecuador"]                       = "Equador",
            ["venezuela"]                     = "Venezuela",
            // ── CONCACAF ─────────────────────────────────────────────────────────
            ["estados unidos"]                = "Estados Unidos",
            ["usa"]                           = "Estados Unidos",
            ["united states"]                 = "Estados Unidos",
            ["canadá"]                        = "Canadá",
            ["canada"]                        = "Canadá",
            ["méxico"]                        = "México",
            ["mexico"]                        = "México",
            ["panamá"]                        = "Panamá",
            ["panama"]                        = "Panamá",
            ["honduras"]                      = "Honduras",
            ["jamaica"]                       = "Jamaica",
            // ── UEFA ─────────────────────────────────────────────────────────────
            ["frança"]                        = "França",
            ["france"]                        = "França",
            ["alemanha"]                      = "Alemanha",
            ["germany"]                       = "Alemanha",
            ["deutschland"]                   = "Alemanha",
            ["espanha"]                       = "Espanha",
            ["spain"]                         = "Espanha",
            ["españa"]                        = "Espanha",
            ["portugal"]                      = "Portugal",
            ["inglaterra"]                    = "Inglaterra",
            ["england"]                       = "Inglaterra",
            ["holanda"]                       = "Holanda",
            ["netherlands"]                   = "Holanda",
            ["países baixos"]                 = "Holanda",
            ["bélgica"]                       = "Bélgica",
            ["belgica"]                       = "Bélgica",
            ["belgium"]                       = "Bélgica",
            ["suíça"]                         = "Suíça",
            ["suiça"]                         = "Suíça",
            ["switzerland"]                   = "Suíça",
            ["croácia"]                       = "Croácia",
            ["croatia"]                       = "Croácia",
            ["sérvia"]                        = "Sérvia",
            ["serbia"]                        = "Sérvia",
            ["dinamarca"]                     = "Dinamarca",
            ["denmark"]                       = "Dinamarca",
            ["áustria"]                       = "Áustria",
            ["austria"]                       = "Áustria",
            ["geórgia"]                       = "Geórgia",
            ["georgia"]                       = "Geórgia",
            ["escócia"]                       = "Escócia",
            ["scotland"]                      = "Escócia",
            ["hungria"]                       = "Hungria",
            ["hungary"]                       = "Hungria",
            ["turquia"]                       = "Turquia",
            ["turkey"]                        = "Turquia",
            ["türkiye"]                       = "Turquia",
            ["polônia"]                       = "Polônia",
            ["poland"]                        = "Polônia",
            ["polonia"]                       = "Polônia",
            ["polónia"]                       = "Polônia",
            ["romênia"]                       = "Romênia",
            ["romania"]                       = "Romênia",
            ["república checa"]               = "República Checa",
            ["czech republic"]                = "República Checa",
            ["czechia"]                       = "República Checa",
            ["rep. tcheca"]                   = "República Checa",
            ["eslováquia"]                    = "Eslováquia",
            ["slovakia"]                      = "Eslováquia",
            ["eslovênia"]                     = "Eslovênia",
            ["slovenia"]                      = "Eslovênia",
            ["albânia"]                       = "Albânia",
            ["albania"]                       = "Albânia",
            ["ucrânia"]                       = "Ucrânia",
            ["ukraine"]                       = "Ucrânia",
            // ── CAF ──────────────────────────────────────────────────────────────
            ["marrocos"]                      = "Marrocos",
            ["morocco"]                       = "Marrocos",
            ["senegal"]                       = "Senegal",
            ["nigéria"]                       = "Nigéria",
            ["nigeria"]                       = "Nigéria",
            ["egito"]                         = "Egito",
            ["egypt"]                         = "Egito",
            ["camarões"]                      = "Camarões",
            ["cameroon"]                      = "Camarões",
            ["costa do marfim"]               = "Costa do Marfim",
            ["ivory coast"]                   = "Costa do Marfim",
            ["côte d'ivoire"]                 = "Costa do Marfim",
            ["cote d'ivoire"]                 = "Costa do Marfim",
            ["república democrática do congo"] = "República Democrática do Congo",
            ["democratic republic of the congo"] = "República Democrática do Congo",
            ["rd do congo"]                   = "República Democrática do Congo",
            ["rdc"]                           = "República Democrática do Congo",
            ["áfrica do sul"]                 = "África do Sul",
            ["south africa"]                  = "África do Sul",
            ["argélia"]                       = "Argélia",
            ["algeria"]                       = "Argélia",
            ["mali"]                          = "Mali",
            // ── AFC ──────────────────────────────────────────────────────────────
            ["japão"]                         = "Japão",
            ["japan"]                         = "Japão",
            ["coreia do sul"]                 = "Coreia do Sul",
            ["south korea"]                   = "Coreia do Sul",
            ["korea republic"]                = "Coreia do Sul",
            ["irã"]                           = "Irã",
            ["iran"]                          = "Irã",
            ["austrália"]                     = "Austrália",
            ["australia"]                     = "Austrália",
            ["arábia saudita"]                = "Arábia Saudita",
            ["saudi arabia"]                  = "Arábia Saudita",
            ["uzbequistão"]                   = "Uzbequistão",
            ["uzbekistan"]                    = "Uzbequistão",
            ["indonésia"]                     = "Indonésia",
            ["indonesia"]                     = "Indonésia",
            ["qatar"]                         = "Qatar",
            ["catar"]                         = "Qatar",
        };

        [HttpPost]
        public async Task<IActionResult> LimparNacionalidades()
        {
            var todas = await _context.Nacionalidades.ToListAsync();

            // ── 1. Mapeia cada nacionalidade para o nome canônico (ou null se não é WC) ──
            var grupos = new Dictionary<string, List<Nacionalidade>>(StringComparer.OrdinalIgnoreCase);

            foreach (var n in todas)
            {
                if (_variantesParaCanonical.TryGetValue(n.Nome.Trim(), out var canonical))
                {
                    if (!grupos.ContainsKey(canonical))
                        grupos[canonical] = new List<Nacionalidade>();
                    grupos[canonical].Add(n);
                }
                // else: não é Copa do Mundo 2026 → será deletada
            }

            int atualizados = 0, deletados = 0;

            // ── 2. Para cada grupo, o menor ID é o canônico; os demais são duplicatas ──
            foreach (var (canonicalNome, lista) in grupos)
            {
                lista.Sort((a, b) => a.Id.CompareTo(b.Id));
                var canonical = lista[0];

                // Garante que o nome canônico está correto
                if (canonical.Nome != canonicalNome)
                {
                    canonical.Nome = canonicalNome;
                    atualizados++;
                }

                // Reatribui jogadores das duplicatas para o canônico
                for (int i = 1; i < lista.Count; i++)
                {
                    var dupl = lista[i];
                    var jogadoresDupl = await _context.Jogadores
                        .Where(j => j.NacionalidadeId == dupl.Id).ToListAsync();
                    foreach (var j in jogadoresDupl)
                    {
                        j.NacionalidadeId = canonical.Id;
                        atualizados++;
                    }
                }
            }

            await _context.SaveChangesAsync();

            // ── 3. Identifica IDs a deletar (não canônicos + fora da Copa do Mundo) ──
            var idsCanonicosWC = grupos.Values
                .Select(lista => lista[0].Id)
                .ToHashSet();

            var paraDeletetar = todas
                .Where(n => !idsCanonicosWC.Contains(n.Id))
                .ToList();

            // Limpa FK dos jogadores que apontam para entradas a deletar
            foreach (var n in paraDeletetar)
            {
                await _context.Jogadores
                    .Where(j => j.NacionalidadeId == n.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(j => j.NacionalidadeId, (int?)null));
            }

            _context.Nacionalidades.RemoveRange(paraDeletetar);
            await _context.SaveChangesAsync();
            deletados = paraDeletetar.Count;

            _logger.LogInformation("[Admin] LimparNacionalidades: {D} deletadas, {A} jogadores atualizados, {W} países mantidos.",
                deletados, atualizados, idsCanonicosWC.Count);

            TempData["Sucesso"] = $"Limpeza concluída: {idsCanonicosWC.Count} países mantidos, {deletados} entradas deletadas, {atualizados} jogadores atualizados.";
            return RedirectToAction("Index", "Relatorios");
        }

        // Método público usado pelo serviço para resolver o nome canônico
        public static string? ResolverNomeCanonical(string? nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return null;
            return _variantesParaCanonical.TryGetValue(nome.Trim(), out var canonical)
                ? canonical : null;
        }
    }
}
