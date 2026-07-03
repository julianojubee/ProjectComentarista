# Planejamento de Refatoração Estrutural

> Documento de planejamento — nenhuma alteração de código foi aplicada ainda.
> Objetivo: melhorar manutenibilidade sem alterar comportamento (sem mudança de schema do banco).

## Diagnóstico atual (medido no código)

| Área | Situação | Evidência |
|------|----------|-----------|
| `JogosController` | Inchado: 1829 linhas, 31 actions, 69 `ViewBag` | Responsabilidades misturadas (CRUD, escalação, eventos, cronômetro, análise) |
| `ViewBag`/`ViewData` | 200+ usos em controllers; tipos perdidos em runtime | Frágil — erros só aparecem na view, sem IntelliSense |
| Controllers de competição | ~~Lógica duplicada~~ — resolvido na Fase 1 via `TemporadaHelper` e `ClassificacaoCalculator` | Libertadores, SulAmericana, CopaDoMundo, ChampionsLeague, CopaBrasil |
| Acesso a dados | 23 controllers acessam `FutebolContext` direto | Sem camada de repositório; queries repetidas (ex.: 5 includes de escalação no `JogosController`) |
| ViewModels | Pasta `Models/ViewModels/` existe com 11 VMs | Padrão **parcialmente** adotado — falta consistência |

---

## Princípios

1. **Sem big-bang.** Refatorar por fatias verticais, sempre com build verde entre passos.
2. **Sem mudança de schema.** Nada de migrations nesta fase.
3. **Comportamento idêntico.** Refatoração estrutural, não funcional.
4. **Aproveitar o que existe.** Já há `Models/ViewModels/` e `Helpers/` — estender, não recriar.

---

## Fase 1 — Eliminar duplicação dos controllers de competição (maior ganho, menor risco) ✅ CONCLUÍDA

Os controllers de competição eram quase idênticos, variando apenas pelo `CompeticaoId`.

**Feito:**
- `Helpers/TemporadaHelper.cs` — `Resolver(context, competicaoId, temporada)` centraliza o
  bloco repetido de seleção de temporada (`temporadasDisponiveis` + `temporadaSel`).
  Usado pelos 5 controllers: `LibertadoresController`, `SulAmericanaController`,
  `ChampionsLeagueController`, `CopaDoMundoController`, `CopaBrasilController`.
- `Helpers/ClassificacaoCalculator.cs` — `Calcular(List<Jogo>)` centraliza o cálculo de
  classificação round-robin (pontos corridos). Usado por `LibertadoresController`,
  `SulAmericanaController` e `ChampionsLeagueController`.

**Mantido intencionalmente fora do helper (não é duplicação, é regra de negócio distinta):**
- `CopaDoMundoController.CalcularClassificacaoGrupo` continua próprio — aplica os critérios
  de desempate FIFA (confronto direto + índice de fair play) que `ClassificacaoCalculator`
  não cobre. Documentado no XML comment do helper.
- `CopaBrasilController` é mata-mata (confrontos ida/volta), não usa classificação de grupo
  — não há lógica de classificação a extrair ali.
- `EhFaseDeGrupos` (Libertadores) e `EhFaseDeLiga`/`OrdemFase` (ChampionsLeague) são usados
  uma única vez cada, em controllers diferentes com critérios de normalização distintos —
  não configuram duplicação real, então não foram extraídos.

**Verificado:** `dotnet build` limpo (0 erros/avisos) com o estado atual.

**Arquivos afetados:** `LibertadoresController`, `SulAmericanaController`, `CopaDoMundoController`, `ChampionsLeagueController`, `CopaBrasilController`.

---

## Fase 2 — ViewModels no lugar de ViewBag (começar pelo `Analisar`)

**`Analisar` ✅ já concluído** — `Models/ViewModels/AnalisarViewModel.cs` existe com todas as
propriedades tipadas (`Jogo`, `GolsPorJogador`, `AssistsPorJogador`, escalações, formações,
treinadores, etc.). `JogosController.Analisar` monta e retorna `View(vm)`; `Analisar.cshtml`
usa `@model AnalisarViewModel` e só tem 1 `ViewData[...]` restante, que é o padrão legítimo
de título de página (`ViewData["TitleOverride"]`), não dado de tela. Verificado com
`dotnet build` limpo.

**`JogadoresController.Index` ✅ concluído** — `Models/ViewModels/JogadoresIndexViewModel.cs`
criado com filtros, combos (`SelectList`), paginação e a lista de jogadores (`Itens`).
`Index.cshtml` migrada para `@model JogadoresIndexViewModel`. Os parâmetros de ordenação
(`NomeSortParam` etc.) foram mantidos na VM mesmo não sendo lidos por nenhuma view hoje —
eram código morto via ViewBag já antes da migração; preservados 1:1 para não mudar
comportamento. `RedirectToAction(nameof(Index))` em outras actions do mesmo controller não
é afetado (sempre re-executa a action, não passa o ViewBag adiante). Build limpo após a mudança.

**Ainda pendente em `JogadoresController` (telas separadas, fora do escopo desta etapa):**
`Create`/`Edit` (ViewBag.TimeId/NacionalidadeId — dropdowns padrão de formulário) e
`Estatisticas` (ViewBag.Competicoes/CompeticaoId).

**`TreinadoresController.Index` ✅ concluído** — `Models/ViewModels/TreinadoresIndexViewModel.cs`
criado com filtros multi-seleção (competições/times/nacionalidades), listas para os
tag-selectors e paginação. `Index.cshtml` migrada para `@model TreinadoresIndexViewModel`.
Build limpo após a mudança.

**Ainda pendente em `TreinadoresController` (telas separadas, fora do escopo desta etapa):**
`Create`/`Edit` (ViewBag.Times/Nacionalidades — dropdowns padrão de formulário),
`ConsultarHistorico` (ViewBag.Treinador) e `PreVisualizarHistorico`/`PreVisualizarHistoricoUrl`
(ViewBag.Treinador/Historico/ProfileUrl/FotoUrl, view `HistoricoPreVisualizacao`).

**Ainda pendente (outros controllers, levantamento atual):**

| Controller | Usos de ViewBag/ViewData |
|---|---|
| `JogosController` (outras actions, fora do `Analisar`) | 22 |
| `AnotacoesTimeController` | 7 |
| `TabelaBrasileiraoController` | 6 |
| demais (Times, CopaBrasil, Formacoes, Competicoes, CriteriosNota, ImportacaoJson, Brasileirao, Account, TransfermarktLogs) | 1–4 cada |

**Migração incremental:** uma view por vez, maior impacto primeiro:
demais actions do `JogosController` → resto.

**Resultado esperado:** erros pegos em tempo de compilação; IntelliSense nas views.

---

## Fase 3 — Camada de Repositório / Query Objects

Hoje queries complexas (ex.: os 5 blocos de `Include(...).ThenInclude(...)` de escalação
no `JogosController`) estão espalhadas e repetidas.

**Ação (pragmática, não dogmática):**
- Criar `Repositories/IJogoRepository.cs` + `JogoRepository.cs` com métodos de leitura
  reutilizados: `ObterParaAnaliseAsync(int id)`, `ObterEscalacaoCompletaAsync(...)`,
  `GolsPorJogadorNaCompeticaoAsync(int competicaoId)`, etc.
- Registrar no DI (`Program.cs`): `builder.Services.AddScoped<IJogoRepository, JogoRepository>()`.
- Controllers passam a depender da interface, não do `FutebolContext` diretamente
  (para as queries já extraídas).

**Escopo:** começar só por `Jogo` (o mais crítico). Não criar repositório para tudo de uma vez.

---

## Fase 4 — Quebrar o `JogosController`

Com VMs e repositório no lugar, dividir por responsabilidade:

| Novo controller | Actions movidas |
|-----------------|-----------------|
| `JogosController` (CRUD) | `Index`, `Details`, `Create`, `Edit`, `Delete`, `Hoje` |
| `EscalacaoController` | `EditEscalacao`, `SalvarEscalacao`, `LimparEscalacoes`, `ReimportarEscalacao`, `Analisar` |
| `EventosJogoController` | `RegistrarGol`, `RemoverGol`, `RegistrarCartao`, `RemoverCartao`, `AtualizarPlacar`, `BuscarEventos` |
| `CronometroController` | `CronometroEstado`, `CronometroAcao` |
| `FaseTaticaController` | `SalvarFaseTatica`, `ExcluirFaseTatica` |

> Atenção: mudança de controller altera URLs (`/Jogos/RegistrarGol` → `/EventosJogo/RegistrarGol`).
> Verificar todas as chamadas JS/`asp-controller` nas views antes de mover, ou usar
> `[Route("Jogos/[action]")]` para preservar rotas.

---

## Ordem de execução recomendada

1. ~~**Fase 1** (duplicação de competição) — isolado, baixo risco, ganho imediato.~~ ✅ concluída.
2. **Fase 2** começando por `Analisar` — área ativa, valida o padrão de VM.
3. **Fase 3** repositório de `Jogo` — habilita a Fase 4.
4. **Fase 4** split do `JogosController` — maior, fazer por último com rotas preservadas.

Cada fase: branch própria + build verde + teste manual da(s) tela(s) afetada(s) antes de seguir.

---

## Itens menores pendentes (limpeza, fora das fases)

- Remover `_variantesParaCanonical` de `AdminController.cs` (já coberto por `CountryHelper`).
- Avaliar remoção de `_traducaoTimes` em `ApiFootballService.cs` (verificar entradas exclusivas antes).
- Aplicar a migration `observacoes` no banco de produção (via psql).
