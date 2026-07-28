# Escopo de Projeto — Canal no YouTube "Comentarista"

Documento de escopo para criação e operação de um canal no YouTube com dois eixos:
**(1) tutoriais do sistema** e **(2) conteúdo de futebol da rodada** produzido *dentro* do sistema.

Restrição central de produção: **sem imagem pessoal**. Apenas captura de tela do sistema + narração
em voz. Todo o formato abaixo foi desenhado em cima dessa restrição — ela não é uma limitação, é o
posicionamento do canal ("o canal onde os dados aparecem, não o apresentador").

---

## 1. Objetivo

| Objetivo | Como se mede |
|---|---|
| Gerar demanda qualificada para o Comentarista (analistas, comissões técnicas, scouts, torcedor-analista) | Cliques no link da bio / cadastros com origem YouTube |
| Reduzir o custo de suporte e onboarding | Nº de tutoriais que respondem dúvidas recorrentes; queda de perguntas repetidas |
| Construir autoridade em análise de dados de futebol em pt-BR | Inscritos, retenção média, comentários pedindo análise de time X |
| Criar biblioteca perene (evergreen) de uso do produto | Views acumuladas dos tutoriais após 90 dias |

**Não é objetivo:** virar canal de notícia/rumor de futebol, competir com canais de resenha, ou
transmitir/reagir a lances de jogos.

---

## 2. Posicionamento e público

- **Público primário:** analista de desempenho, auxiliar técnico, olheiro/scout, coordenador de base,
  gestor de clube amador/base — gente que hoje resolve isso em planilha.
- **Público secundário:** torcedor-analista, fantasy game (Cartola), criador de conteúdo de futebol,
  estudante de Ciências do Esporte.
- **Promessa do canal:** "Futebol explicado por dados que você mesmo consegue montar."
- **Diferencial:** toda análise mostrada é reproduzível — sai de uma tela real do sistema, não de um
  slide bonito. Cada vídeo de futebol é, implicitamente, uma demo de produto.

---

## 3. Pilares de conteúdo

### Pilar A — Tutoriais do sistema (evergreen)
Formato didático, tela + voz, mostrando *como fazer*. Base da biblioteca do canal.

Trilha inicial (mapeada 1:1 com o `manual.html`):
1. O que é o Comentarista — tour de 3 minutos por tudo
2. Primeiro acesso: criar conta, perfil e preferências
3. Cadastrando seu time: escudo, cores, uniformes
4. Cadastrando jogadores: posição, idade automática, nacionalidade, seleção
5. Treinadores e histórico de passagens
6. Registrando um jogo do zero (placar, estádio, árbitro, pênaltis)
7. Escalação tática visual: montando o 4-3-3 arrastando no campo
8. Gols, assistências, cartões e substituições ligados a cada jogador
9. Analisar Partida: a tela mais poderosa do sistema
10. Observações da partida com tags e menções
11. Sistema de notas: critérios por peso vs. nota manual
12. Estatísticas do jogador: lendo os gráficos (gols por intervalo etc.)
13. Relatório Geral: acumulados por competição e temporada
14. Scout de Mercado: achar o jogador certo por filtros
15. Transferências e controle de mercado
16. Competições: Brasileirão, Copa do Mundo, Libertadores, Champions, Sul-Americana, Copa do Brasil
17. Fases de competição, grupos e mata-mata com desempate automático
18. Importação por JSON e integrações (API-Football / Transfermarkt)
19. Jogos de Hoje e acompanhamento ao vivo
20. App Android: o que dá pra fazer no celular

### Pilar B — Futebol da rodada (recorrente)
O sistema como ferramenta de análise. Sempre com a tela do Comentarista em cena.

- **Resumo da rodada em números** — resultados, tabela atualizada, destaques por nota
- **Nota dos jogadores da rodada** — top 5 / flop 5, com o critério de nota exposto na tela
- **Duelo de dados** — comparação lado a lado de 2 jogadores ou 2 times
- **Raio-X do time** — 1 clube por episódio: aproveitamento, escalação mais usada, artilharia interna
- **Scout da semana** — "achamos um lateral sub-23 com nota ≥ 7 usando 3 filtros"
- **Pré-rodada** — o que os dados dizem antes dos jogos
- **Especiais de competição** — chaveamento, cenários de classificação, contas da tabela

### Pilar C — Curtos / Shorts (distribuição)
Cortes verticais de 20–50s: um número forte, um gráfico, um filtro do Scout resolvendo uma pergunta.
Função: alcance e topo de funil. Sempre com CTA para o vídeo longo.

**Proporção alvo:** 40% Pilar A, 45% Pilar B, 15% Pilar C (em quantidade de vídeos longos + shorts à parte).

---

## 4. Formatos e duração

| Formato | Duração | Frequência | Esforço |
|---|---|---|---|
| Tutorial curto (1 tela / 1 tarefa) | 3–6 min | 1x/semana | Baixo |
| Tutorial completo (módulo inteiro) | 10–18 min | 1x/mês | Médio |
| Resumo da rodada | 8–12 min | 1x/semana (pós-rodada) | Médio |
| Duelo de dados / Raio-X | 6–10 min | 1x/quinzena | Médio |
| Short vertical | 20–50 s | 3x/semana | Baixo |
| Especial de competição | 12–20 min | Por evento | Alto |

---

## 5. Produção — sem imagem pessoal

### Stack técnica
- **Captura:** OBS Studio (gratuito), cena única em 1920×1080 @ 30fps, captura de janela do navegador.
- **Zoom/destaque:** OBS com fonte redimensionada ou pós no editor — a tela do sistema tem muita
  informação; sem zoom o espectador de celular não lê.
- **Cursor:** realce de cursor + clique visível (ativar no OBS ou no editor). Essencial em tutorial.
- **Edição:** DaVinci Resolve (gratuito) ou CapCut. Cortes secos, sem transição enfeitada.
- **Áudio:** microfone dinâmico/condensador USB + tratamento (noise gate + compressor + normalização
  para -14 LUFS). **Áudio é o único canal humano do vídeo — é onde o orçamento deve ir.**
- **Alternativa de voz:** narração por TTS de qualidade (ElevenLabs/Azure) se não quiser gravar voz
  própria. Decidir cedo e manter consistente — trocar de voz no meio quebra a identidade do canal.
- **Música:** biblioteca de áudio do YouTube ou Epidemic Sound, volume ≤ -22 dB sob a narração.

### Ambiente de gravação (importante)
Criar uma **base de dados de demonstração** dedicada: times, jogadores, jogos e notas preenchidos,
sem dado real de usuário. Motivos: evita vazar dados de terceiros, evita telas vazias e permite
regravar qualquer episódio com o mesmo estado. Versionar um dump/seed dessa base.

Checklist pré-gravação:
- [ ] Navegador em janela limpa (sem abas, favoritos ocultos, extensões desligadas)
- [ ] Zoom do navegador em 110–125% (legibilidade em celular)
- [ ] Modo/tema fixo (sempre o mesmo — claro ou escuro, não alternar entre vídeos)
- [ ] Notificações do sistema operacional silenciadas
- [ ] Logado com o usuário de demonstração
- [ ] Dados da rodada já sincronizados

### Identidade visual
- Logo/favicon do sistema (`wwwroot/favicon.svg`) como marca d'água discreta no canto.
- Paleta e tipografia herdadas do próprio sistema — o canal deve parecer uma extensão do produto.
- Abertura de ≤ 3 segundos (aberturas longas matam retenção).
- Lower third padrão para nome de jogador/time e para o número em destaque.
- Templates reutilizáveis no editor: intro, lower third, card de estatística, tela final com CTA.

---

## 6. Direitos autorais — regra dura

**Não usar** imagens de jogos, transmissões, gols, fotos de agências ou áudio de narração de TV.
Isso é o principal risco de strike/desmonetização do canal e não é negociável.

Permitido e suficiente:
- Telas do próprio sistema (gráficos, tabelas, escalação no campo) — a estrela do canal
- Escudos e logos: uso nominativo/editorial, com cautela; preferir os que já estão no sistema
- Fotos de jogadores: apenas de bancos com licença explícita (ou evitar e usar silhueta/avatar)
- Motion gráfico próprio, mapas de calor e campos desenhados

Quando precisar ilustrar um lance: **descrever com narração + reconstruir com o gráfico do sistema**.
Isso é, inclusive, mais alinhado ao posicionamento do canal do que reprisar o vídeo do gol.

---

## 7. Calendário editorial

Ritmo sustentável de partida (revisar após 8 semanas):

| Dia | Publicação |
|---|---|
| Segunda | Resumo da rodada em números (Pilar B) |
| Quarta | Tutorial curto (Pilar A) |
| Sexta | Duelo de dados / Scout da semana (Pilar B) — alternando quinzenalmente |
| Ter/Qui/Sáb | 1 Short cada |

**Batching:** gravar tutoriais em lote (4–6 por sessão). Conteúdo de rodada é o único que não pode
ser adiantado — reservar uma janela fixa de 3h pós-rodada.

**Banco de segurança:** manter 4 tutoriais prontos e não publicados antes do lançamento do canal,
para cobrir semanas ruins.

---

## 8. SEO e empacotamento

- **Títulos:** intenção de busca no Pilar A ("Como montar a escalação tática — Comentarista"),
  curiosidade + número no Pilar B ("O dado que explica a derrota do [time] na rodada 19").
- **Thumbnail:** 1 número gigante + 1 escudo/elemento + ≤ 3 palavras. Legível a 120px de largura.
  Modelo fixo por pilar (o espectador reconhece o tipo de vídeo antes de ler).
- **Descrição:** 2 linhas de resumo → link do sistema → capítulos com timestamps → links relacionados.
- **Capítulos obrigatórios** em qualquer vídeo > 5 min.
- **Playlists** por pilar e por módulo do sistema (a trilha de tutoriais vira um curso completo).
- **Primeiros 15 segundos:** mostrar o resultado final antes de ensinar o caminho.

---

## 9. Funil e conversão

1. **Short / vídeo de rodada** → topo: pessoa interessada no futebol, não no sistema
2. **Vídeo de rodada** → meio: "isso foi feito com uma ferramenta"
3. **Tutorial** → fundo: "eu consigo fazer isso"
4. **CTA** → link na descrição, fixado no comentário e em tela final de 8s

Regra de CTA: **um só por vídeo**, no fim. Pedir inscrição no meio do tutorial derruba retenção.
Nos vídeos de rodada, o CTA é a demonstração em si — não precisa de discurso de venda.

---

## 10. Métricas e metas

| Métrica | Meta 90 dias | Meta 180 dias |
|---|---|---|
| Vídeos publicados (longos) | 30 | 70 |
| Inscritos | 500 | 2.000 |
| Retenção média | ≥ 40% | ≥ 45% |
| CTR de thumbnail | ≥ 4% | ≥ 6% |
| Cliques no link do sistema | 300 | 1.500 |
| Cadastros com origem YouTube | 40 | 250 |

Revisão mensal: cortar formato com retenção < 30% em 3 vídeos seguidos; dobrar no formato de melhor
CTR × retenção.

---

## 11. Fases do projeto

**Fase 0 — Preparação (2 semanas)**
Base de demonstração populada e versionada; identidade visual do canal (avatar, banner, templates);
stack de gravação testada; decisão sobre voz própria vs. TTS; canal criado e configurado.
*Entregável:* 4 tutoriais gravados no banco de segurança.

**Fase 1 — Lançamento (semanas 3–6)**
Publicar o tour geral + trilha de tutoriais 1–8. Foco em consistência, não em alcance.
*Entregável:* playlist "Começando no Comentarista" completa.

**Fase 2 — Recorrência (semanas 7–14)**
Entra o Pilar B (rodada) e os Shorts. Ritmo de 3 longos/semana estabilizado.
*Entregável:* formato de resumo de rodada validado e replicável em ≤ 3h.

**Fase 3 — Escala (a partir da semana 15)**
Especiais de competição, séries temáticas, possíveis parcerias com canais de análise tática.
Avaliar automação: gerar cards/relatórios da rodada direto do sistema para reduzir o tempo de edição.

---

## 12. Riscos e mitigação

| Risco | Mitigação |
|---|---|
| Strike por uso de imagem de jogo | Política de zero footage de terceiros (seção 6) |
| Cansaço / queda de ritmo | Batching + banco de 4 vídeos de segurança |
| Vídeo de rodada envelhece rápido | Manter 40% do catálogo em tutoriais evergreen |
| Dados reais de usuário aparecendo em tela | Base de demonstração dedicada, nunca ambiente de produção com dados de cliente |
| Bug do sistema aparecendo na gravação | Gravar sempre em versão estável, nunca em branch de desenvolvimento |
| Canal virar só demo de produto e perder audiência | Regra: todo vídeo do Pilar B precisa entregar uma conclusão sobre futebol que valha por si só |

---

## 13. Checklist por episódio

**Pré:** roteiro em bullets → dados carregados → ambiente limpo (seção 5) → thumbnail esboçada
**Gravação:** tela primeiro (sem áudio) → narração por cima → 2 takes de áudio nos trechos críticos
**Pós:** corte → zooms/destaques → lower thirds → música → normalização -14 LUFS → legendas
**Publicação:** título → thumbnail → descrição com link e capítulos → playlist → CTA fixado
**Depois:** responder 100% dos comentários nas primeiras 24h → anotar retenção no dia 7

---

## 14. Backlog inicial — primeiros 12 vídeos

1. O Comentarista em 3 minutos (tour geral)
2. Como criar sua conta e configurar o perfil
3. Cadastrando seu primeiro time do zero
4. Cadastro de jogadores: tudo que o sistema calcula sozinho
5. Registrando um jogo completo em 5 minutos
6. Escalação tática visual: montando o time no campo
7. Analisar Partida — a tela que substitui sua planilha
8. Sistema de notas: como configurar seus próprios critérios
9. Resumo da rodada em números (primeiro do Pilar B)
10. Duelo de dados: [jogador A] x [jogador B]
11. Scout de Mercado: achando um jogador em 3 filtros
12. Relatório Geral: lendo as estatísticas da temporada
