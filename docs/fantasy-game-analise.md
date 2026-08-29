# Fantasy game — análise de viabilidade e proposta de implementação

Data: 20/08/2026. Base: código em `ControleFutebolWeb` no estado do branch
`trabalho/2026-08-14-treinadores-e-sync`.

## 1. Resumo

O sistema já tem tudo o que é caro num fantasy: linha estatística por (jogador,
jogo), posição real da partida e um motor de rating por posição calibrado contra
o rating do provedor. O que falta é o jogo em si — mercado, escalação por rodada,
apuração e classificação — mais um agendamento confiável de importação.

A recomendação central é **não inventar uma tabela de pontuação nova**. O
`RatingAutomaticoHelper` já resolve os três problemas que quebram um fantasy
caseiro (volume, erro, posição) e já é explicável jogada a jogada. Pontuação e
preço saem dele.

## 2. O que já existe e é reaproveitável

| Peça | Onde | Serve para |
|---|---|---|
| Rating automático 3,0–10,0 por posição, com z-score contra a média do grupo, saturação de eventos e compressão no topo | `Helpers/Rating/RatingAutomaticoHelper.cs` | **Pontuação da rodada** |
| Composição da nota (famílias, métricas, eventos, contexto) | `ComposicaoRating`, mesmo arquivo | Tela "por que fiz X pontos" |
| Régua calibrada sobre todo o histórico + aderência (correlação, média, desvio, P50/P90/P99 por posição) | `Services/RatingAutomaticoService.cs` (`CalibrarAsync`, `DiagnosticarAsync`) | **Calibrar escala de pontos e faixa de preço com dado real** |
| Cálculo em lote: `CalcularAsync(jogoIds, usuarioId)` → `(JogadorId, JogoId) → ComposicaoRating` | idem | Apuração da rodada é praticamente uma chamada |
| Estatística por jogador/jogo com marca de origem e cobertura declarada | `Models/EstatisticaJogador.cs`, `Models/FonteEstatistica.cs` | Evita tratar zero de dado ausente como zero de desempenho |
| Posição **daquela** partida (não a do cadastro) | `Helpers/LadoJogadorHelper.cs` | Precificação e pontuação por posição corretas |
| Formações + campinho tático + geração de escalação | `Models/Formacao.cs`, `PosicaoFormacao`, `Helpers/GerarEscalacao.cs` | UI de montar time |
| Padrão de entidade por usuário com slots em JSON | `Models/SelecaoCopaUsuario.cs` | Molde direto para `FantasyEscalacao` |
| Identity multiusuário + cobrança manual (`AcessoPagoAte`, `PagamentoUsuario`) | `Models/ApplicationUser.cs`, `Controllers/PagamentosController.cs` | Monetização já resolvida |
| API JWT do app Android | `Controllers/Api/` | Fantasy no celular sem retrabalho |
| Import de elenco para jogos futuros | `ApiFootballService.ImportarElencoAsync` | Mercado existe antes de haver escalação publicada |
| Transferências detectadas no import | `Models/Transferencia.cs` | Regra "máx. N por clube" acompanha o clube atual |

## 3. Lacunas e riscos reais

1. **Importação é manual.** Os dois hosted services de atualização estão
   comentados (`Program.cs:163-164`); automático hoje só roda transmissões e
   jogadores sem data. Fantasy exige janela previsível: mercado fecha antes da
   primeira bola da rodada, apuração roda depois da última. Sem agendador, o dono
   da liga vira o cron.
2. **Cobertura desigual entre fontes.** A ESPN não publica passes, desarmes,
   duelos, dribles nem pênaltis (`FonteEstatistica`). Para a nota isso é
   absorvido (o peso das famílias é redistribuído), mas num fantasy dois
   jogadores da **mesma rodada** seriam medidos por réguas diferentes. Pontuar
   pelo rating (que já normaliza) em vez de por evento cru mitiga; ainda assim, a
   rodada deve nascer provisória e ser reapurada se vier fonte completa.
3. **Não existe valor de mercado.** Nenhum campo de valor Transfermarkt no
   `Jogador`. O preço tem que ser 100% derivado do histórico de rating — que é o
   pedido original, mas significa que a fase de calibração não é opcional.
4. **Dados por usuário vs. globais.** `Escalacao`, `Nota` e `CriterioNota` têm
   `UsuarioId`; `Jogo` e `EstatisticaJogador` são globais. Toda a apuração do
   fantasy deve passar `usuarioId: null`, para que todo mundo veja a mesma
   pontuação. Um `usuarioId` vazando para o cálculo dá liga com placar diferente
   por participante.
5. **Reimportação muda o passado.** `ForcarReimportarEscalacaoAsync` e correções
   de estatística alteram pontos já apurados. Precisa de snapshot + política de
   congelamento.
6. **`Jogo.Data` é nullable.** O deadline da rodada depende dela; rodada sem data
   em todos os jogos não pode abrir.
7. **Sem minutos não há rating** (`Calcular` devolve `null`). É decisão de
   design, não bug: quem não entrou em campo faz 0 ponto.
8. **Volume de dados é a incógnita.** Quantos jogos por competição/temporada já
   têm `EstatisticaJogador` vinda da api-football (não da ESPN) decide se dá para
   abrir liga em uma competição só ou em várias. Medir antes de prometer.

## 4. Pontuação proposta

### Fórmula

```
Pontos(jogador, jogo) = (Nota − 6,0) × 10          // 6,0 é o neutro do motor
Pontos(capitão)       = Pontos × 1,5
Pontos(sem minutos)   = 0
```

A nota vem de `RatingAutomaticoHelper.Calcular(estatistica, posicaoNoJogo,
contexto, baseline)`, exatamente como hoje. Consequências:

- **Faixa**: −30 a +40 pontos por jogo, com o topo comprimido (o 10 é assíntota),
  então um hat-trick não decide a rodada sozinho.
- **Justiça por posição**: `PesosRating` dá a cada grupo um caminho próprio até a
  nota alta — zagueiro pontua por duelo, ponta por finalização. É exatamente o
  problema que todo fantasy artesanal erra.
- **Minutos**: a confiança já encolhe a parte contínua de quem entrou aos 80', em
  vez de dar piso artificial.
- **Explicabilidade**: `ComposicaoRating` entrega famílias, métricas com z-score,
  eventos e contexto. A tela "como fiz meus pontos" sai de graça.

### Alternativa descartada

Somar `CriterioNota × quantidade` (motor Clássico) seria mais parecido com o
Cartola, mas herda os três vícios que o próprio `RatingAutomaticoHelper`
documenta e é enviesado contra jogos importados da ESPN. Se um dia se quiser o
sabor "cartoleiro", o caminho é **exibir** os eventos (gol, assistência, jogo sem
sofrer gol) na composição, sem somá-los duas vezes.

### Ponto em aberto

A escala ×10 é chute até a Fase 0 rodar. `DiagnosticarAsync` devolve desvio e
percentis por posição — a escala deve ser fixada de modo que a mediana da rodada
fique perto de 0 e o P99 perto de +30.

## 5. Precificação proposta

Moeda: C$ (cartoletas). Orçamento inicial C$ 100, faixa de preço C$ 1,0 a
C$ 20,0 — a faixa é o que obriga escolha.

### Força do jogador

```
R  = Σ wᵢ·notaᵢ / Σ wᵢ ,  wᵢ = 0,75^i     // últimas 8 partidas, mais peso nas recentes
R̂  = (n·R + k·6,0) / (n + k) ,  k = 3      // encolhimento: 1 jogo bom não vira o mais caro
m  = minutos jogados / minutos possíveis   // últimas 5 rodadas, ∈ [0,1]
```

### Preço-alvo

```
Palvo = Pmin + (Pmax − Pmin) · σ((R̂ − 6,0)/s) · (0,4 + 0,6·m)
```

`σ` = logística; `s` = desvio típico do rating, **medido** por
`AderenciaRating.DesvioNosso` (por posição), não chutado. O fator de minutos é o
que faz reserva ficar barato — comportamento de mercado desejado.

### Variação por rodada

```
Pnovo = Pant + clamp(Palvo − Pant, ±15% de Pant)
```

Valorização gradual é o que cria o meta-jogo de patrimônio. O preço é **congelado
por rodada** num snapshot (`FantasyPrecoJogador`), imutável depois do
fechamento — sem isso, uma reimportação de estatística reescreveria compras já
feitas.

### Casos de borda

- Sem histórico (< 2 jogos): mediana de preço do grupo de posição.
- Aposentado (`Jogador.Aposentado`) ou transferido para fora da competição: sai
  do mercado; escalações da rodada aberta são invalidadas com aviso.

## 6. Modelo de dados

Tabelas novas, seguindo a convenção do `FutebolContext`:

| Tabela | Campos essenciais |
|---|---|
| `FantasyLiga` | Id, Nome, CompeticaoId, Temporada, CriadorUsuarioId, Publica, Codigo (convite), Orcamento, MaxPorClube, PermiteCapitao |
| `FantasyRodada` | Id, LigaId, Rodada, FechamentoEm, Status (ABERTA/FECHADA/APURADA), ApuradaEm |
| `FantasyPrecoJogador` | JogadorId, CompeticaoId, Temporada, Rodada, Preco, RatingPonderado, Jogos, FracaoMinutos, Delta |
| `FantasyEscalacao` | LigaId, UsuarioId, Rodada, FormacaoId, SlotsJson `[{x,y,jogadorId,capitao}]`, CustoTotal, Confirmada |
| `FantasyPontuacaoJogador` | JogadorId, JogoId, Rodada, Pontos, ComposicaoJson |
| `FantasyPontuacaoTime` | LigaId, UsuarioId, Rodada, Pontos, PontosAcumulados, Patrimonio, Posicao |

Índices únicos: `(LigaId, UsuarioId, Rodada)` na escalação e
`(CompeticaoId, Temporada, Rodada, JogadorId)` no preço. `SlotsJson` segue o
padrão de `SelecaoCopaUsuario` — evita tabela-filha e casa com o campinho que já
existe na UI.

## 7. Fluxo operacional da rodada

1. **Abertura** — jogos da rodada importados (`AtualizarJogosAgendadosAsync`) e
   elencos garantidos (`ImportarElencoAsync`). Preços da rodada calculados e
   gravados. `FechamentoEm = min(Data dos jogos) − 1h`.
2. **Mercado aberto** — usuário monta/edita o time. Validações: orçamento,
   formação, máx. por clube (clube no momento do fechamento), jogador ativo.
3. **Fechamento** — automático pelo horário; sem confirmação, vale a última
   escalação salva.
4. **Apuração** — depois do import das estatísticas de todos os jogos:
   `RatingAutomaticoService.CalcularAsync(jogoIdsDaRodada, usuarioId: null)` →
   pontos por jogador → soma por escalação → classificação e patrimônio.
   Reapuração permitida enquanto o status não for `APURADA`.
5. **Congelamento** — após X horas ou por ação do admin vira `APURADA`, e
   correções de estatística não mexem mais no placar.

## 8. Fases de implementação

**Fase 0 — calibração (obrigatória, ~1 dia).** Rodar `DiagnosticarAsync` sobre o
histórico e medir: quantos jogos por competição/temporada têm estatística da
api-football, distribuição de nota por posição, desvio e percentis. Saída: as
constantes da escala de pontos, o `s` da precificação e a decisão de quais
competições comportam uma liga. Sem isso, todo número deste documento é chute.

**Fase 1 — motor, sem UI.** Migração das seis tabelas, `FantasyPrecoService`,
`FantasyApuracaoService`, comandos manuais na tela `/Servicos` (abrir rodada,
apurar). Testes em `ControleFutebolWeb.Tests` no padrão dos existentes.

**Fase 2 — web.** Mercado (filtro por posição/clube/preço), montagem no campinho
reaproveitando o componente da Seleção da Copa, minha equipe, classificação e a
tela de composição dos pontos.

**Fase 3 — ligas.** Convite por código, ligas privadas, histórico e patrimônio.

**Fase 4 — Android.** Endpoints em `Controllers/Api/` no padrão JWT existente.

**Fase 5 — automação.** Reativar/reescrever o agendador de importação
(`Program.cs:163`), que é o que permite abrir e apurar rodada sem operador.

## 9. Decisões que dependem do dono do produto

- Liga pública única ou múltiplas ligas privadas desde o começo?
- Banco de reservas com substituição automática de quem não jogou — v1 ou v2?
- Trocas ilimitadas por rodada (estilo Cartola) ou orçamento de transferências?
- Fantasy entra no acesso pago (`AcessoPagoAte`) ou é isca gratuita?
