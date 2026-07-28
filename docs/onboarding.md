# Onboarding — ControleFutebolWeb

Plano de aprendizado do sistema, da tela mais simples à mais complexa, para quem vai
dar manutenção sem depender de alguém mais experiente a cada chamado.

**Pré-requisitos:** C# básico (classe, método, `async`/`await`) e SQL básico (`SELECT`,
`JOIN`). O plano não ensina a linguagem — ensina *este* sistema.

**O que é o sistema:** aplicação ASP.NET Core 9 (MVC + Razor) com PostgreSQL, para
análise de partidas de futebol: escalações, eventos, estatísticas e relatórios. Tem
também uma API REST consumida por um aplicativo Android.

| | |
|---|---|
| Telas (pastas de view) | 26 |
| Controllers | 39 |
| Tabelas mapeadas | 44 |
| Helpers de cálculo | 14 |
| Testes automatizados | 31 |

> As contagens de linha citadas ao longo do documento refletem o estado do repositório
> quando o plano foi escrito. Elas mudam conforme o código evolui; a **ordem de
> aprendizado**, não.

Cada nível tem: o que se aprende, arquivos reais para abrir, um exercício e um critério
objetivo de conclusão. A regra da progressão é que **cada nível só usa conceitos dos
anteriores** — vale seguir a ordem.

---

## Sumário

| Nível | Assunto | Tempo estimado |
|---|---|---|
| [00](#nível-00--antes-de-tocar-em-qualquer-código) | Antes de tocar em qualquer código | meio período |
| [01](#nível-01--anatomia-de-uma-tela) | Anatomia de uma tela | 1 dia |
| [02](#nível-02--tela-de-leitura-pura) | Tela de leitura pura | 1 dia |
| [03](#nível-03--crud-completo) | CRUD completo | 3 a 4 dias |
| [04](#nível-04--listagens-de-verdade-filtros-paginação-e-listas-suspensas) | Filtros, paginação e listas suspensas | 2 dias |
| [05](#nível-05--banco-de-dados-relações-e-migrações) | Banco de dados, relações e migrações | 3 dias |
| [06](#nível-06--telas-que-conversam-com-o-servidor-sem-recarregar) | Telas com AJAX | 3 dias |
| [07](#nível-07--a-tela-analisar) | A tela Analisar | 1 semana |
| [08](#nível-08--serviços-integrações-e-o-que-roda-sozinho) | Serviços e integrações externas | 1 semana |
| [09](#nível-09--infraestrutura-segurança-e-publicação) | Infraestrutura e segurança | 3 dias |
| [10](#casos-reais-deste-sistema) | Casos reais deste sistema | — |
| [11](#regras-da-casa) | Regras da casa | — |
| [12](#o-que-você-já-pode-resolver-sozinho) | O que você já pode resolver sozinho | — |

---

## Nível 00 — Antes de tocar em qualquer código

Ninguém aprende sistema lendo. Aprende rodando, quebrando e vendo quebrar. O primeiro
objetivo é subir a aplicação e abrir uma tela.

```bash
dotnet build ControleFutebolWeb -v q --nologo
```

```bash
dotnet run --project ControleFutebolWeb --urls https://localhost:5057
```

### Duas armadilhas do primeiro dia

1. **Não use `--launch-profile https`.** Esse perfil não existe. Sem o perfil padrão o
   ambiente não entra em Development, a aplicação tenta o Postgres de *produção* e morre
   com erro de senha.
2. **Build falhando ao copiar `ControleFutebolWeb.exe`** significa que a aplicação ainda
   está rodando de antes. Mate o processo e builde de novo.

As migrações do banco são aplicadas sozinhas ao subir (`Database.Migrate()` no
`Program.cs`) — não há passo manual de banco.

### Leia estes três, nesta ordem

| Arquivo | Por quê |
|---|---|
| `.claude/skills/verify/SKILL.md` | Manual de como subir e dirigir o sistema, inclusive autenticado por linha de comando. Comece aqui. |
| `ControleFutebolWeb/Program.cs` (327 linhas) | Tudo que a aplicação liga ao iniciar. Você não vai entender metade agora — volte no nível 09. |
| `ControleFutebolWeb/appsettings.json` | Configuração. As senhas são placeholders: os valores reais vêm de user-secrets / variáveis de ambiente. Nunca escreva segredo aqui. |

**Concluído quando:** você sobe o sistema, faz login no navegador e abre a tela de Jogos,
sem ajuda.

---

## Nível 01 — Anatomia de uma tela

Toda tela segue o mesmo caminho. Entendido o fluxo, o resto é volume.

```
URL  →  Controller  →  action  →  EF Core  →  banco
                                     ↓
HTML  ←  arquivo .cshtml  ←  model / ViewModel
```

A rota padrão é `{controller}/{action}/{id?}`. Logo, `/Jogos/Analisar/17660` chama
`Analisar(17660)` em `JogosController`, que busca dados, monta um objeto e entrega para
`Views/Jogos/Analisar.cshtml`.

### As quatro pastas que importam

| Pasta | Papel |
|---|---|
| `Controllers/` | Recebem a requisição e decidem o que fazer. É onde quase toda investigação começa. |
| `Views/` | O HTML. A pasta tem o nome do controller: `Views/Times/` serve o `TimesController`. |
| `Models/` | As 44 classes que viram tabelas, mais os 25 ViewModels em `Models/ViewModels/`. |
| `Data/FutebolContext.cs` (351 linhas) | A ponte com o PostgreSQL: qual classe é qual tabela e como se relacionam. |

### Model x ViewModel

Um **Model** é uma linha do banco. Um **ViewModel** é o que a tela precisa mostrar —
normalmente junta dados de várias tabelas mais valores calculados. Quando uma tela fica
lenta ou confusa, com frequência é porque se entregou o Model cru onde devia haver um
ViewModel.

### Exercício

Abra `/Jogos` no navegador. Encontre no código: qual arquivo produziu a página, qual
método executou e qual arquivo gerou o HTML. Depois altere o título da tela, builde, suba
e confirme a mudança. Reverta.

**Concluído quando:** dada qualquer URL do sistema, você acha controller, action e view em
menos de um minuto, sem busca por texto.

---

## Nível 02 — Tela de leitura pura

A tela mais simples do sistema são os logs de sincronização: 61 linhas ao todo, já com o
padrão que se repete em quase tudo.

| Arquivo | Conteúdo |
|---|---|
| `Controllers/TransfermarktLogsController.cs` (61 linhas) | Um `Index` que filtra e lista, e um POST que limpa. O "olá mundo" do sistema. |
| `Controllers/ServicosController.cs` (67 linhas) | Painel de status dos serviços de fundo. Mesmo padrão, outro assunto. |

### O que observar nesse código

- `_context.TransfermarktSincronizacaoLogs` — a tabela virou propriedade C#.
- `.AsNoTracking()` — "só vou ler, não vou salvar". Deixa a consulta mais leve; use sempre
  que for apenas exibir.
- `.Where(...)` condicional — o filtro só entra se o usuário informou algo. A consulta
  ainda **não** foi ao banco.
- `.ToListAsync()` — **aqui** o banco é consultado. Tudo antes foi montar a pergunta.
- `ViewBag` — jeito rápido de enviar dado extra à tela. Funciona, mas não tem checagem do
  compilador; em tela nova, prefira ViewModel.

> **O conceito mais importante do EF Core:** enquanto você encadeia `.Where`, está
> escrevendo SQL sem executar. É em `ToList`/`First`/`Count` que a consulta roda. Quase
> todo problema de lentidão nasce de não perceber isso.

### Exercício

Na tela de logs o limite é de 300 registros (`.Take(300)`). Adicione um filtro por data —
apenas logs dos últimos N dias, com N vindo da URL — seguindo o estilo dos filtros
existentes.

**Concluído quando:** você adiciona um filtro numa listagem sem copiar de outro lugar, e
explica por que `AsNoTracking` está ali.

---

## Nível 03 — CRUD completo

Telas que escrevem no banco. O padrão é sempre o par: um `GET` que mostra o formulário e
um `POST` de mesmo nome que recebe o preenchimento.

| Arquivo | Conteúdo |
|---|---|
| `Controllers/CriteriosNotaController.cs` (165 linhas) | Index, Create, Edit, Delete e um Resetar. CRUD limpo. |
| `Controllers/AnotacoesTimeController.cs` (179 linhas) | Mesmo padrão, actions em português (Nova, Editar, Excluir), vinculado a um time. |
| `Controllers/FormacoesController.cs` (171 linhas) | Formações táticas e suas posições. Introduz relação pai-filho. |

### Os cinco elementos de um POST seguro

| Elemento | Para que serve |
|---|---|
| `[HttpPost]` | Só responde a envio de formulário, não a acesso pela URL. |
| `ModelState.IsValid` | Confere as regras declaradas no Model. Se falhar, devolve o formulário com os erros. |
| `_context.SaveChangesAsync()` | Só aqui a alteração vai ao banco. Sem isso, você mudou o objeto em memória e nada aconteceu. |
| `RedirectToAction` | Depois de salvar, sempre redirecione — evita que F5 salve de novo. |
| `TempData["Mensagem"]` | Recado que sobrevive ao redirect e vira o aviso no canto da tela. |

Sobre CSRF: você verá `[ValidateAntiForgeryToken]` em alguns métodos, mas neste projeto a
validação é **automática em todo POST** (ligada globalmente no `Program.cs`). Os atributos
espalhados são redundância inofensiva.

### Exercício

Em Anotações de Time, acrescente um campo novo (por exemplo, uma marcação de
"importante"). Exige o caminho completo: alterar o Model, criar a migração, ajustar as
telas de criar e editar, exibir na listagem. É o exercício mais valioso desta fase — faça
inteiro.

**Concluído quando:** você leva um campo novo do banco até a tela sozinho, e sabe dizer por
que se redireciona depois de salvar.

---

## Nível 04 — Listagens de verdade: filtros, paginação e listas suspensas

A listagem de jogos é o modelo a seguir quando a tabela é grande: filtro por time,
competição, situação e período, com paginação de 50 em 50.

| Arquivo | Conteúdo |
|---|---|
| `Controllers/JogosController.cs` → método `Index` | O jeito certo de fazer listagem grande aqui. Leia o comentário que explica por que os relacionamentos pesados *não* são carregados. |
| `Controllers/TimesController.cs` (672 linhas) | Listagem com busca e upload de escudo; mostra `Helpers/UploadHelper.cs` em uso. |

### Três coisas para levar desta tela

- **Filtro condicional** — a consulta é montada por partes e só executa no fim.
- **Paginação** — `.Skip().Take()` mais a contagem total para desenhar os números de página.
- **SelectList** — como se preenche um `<select>` no Razor: campo do id, campo do texto, e
  qual vem selecionado.

### O erro que se comete aqui

Precisar de um dado a mais e resolver com um `.Include()` extra. Se a tela lista 50 jogos e
cada um tem 30 escalações, você trouxe 1.500 linhas para mostrar um nome. Traga só o que a
tela desenha; se precisa de um campo de outra tabela, prefira um `.Select()` com
exatamente esses campos.

### Exercício

Acrescente um filtro de "só jogos com escalação salva", sem carregar as escalações. Dica:
dá para perguntar ao banco se existe alguma, em vez de trazê-las.

**Concluído quando:** você olha uma listagem e diz quantas consultas ela dispara e quanto
traz do banco, só lendo o código.

---

## Nível 05 — Banco de dados, relações e migrações

Agora vale abrir o `FutebolContext` de verdade. Ele tem particularidades que você precisa
conhecer **antes** de mexer em qualquer tabela.

| Arquivo | Conteúdo |
|---|---|
| `Data/FutebolContext.cs` (351 linhas) | As 44 tabelas, relacionamentos, índices e três regras próprias. |
| `Migrations/` | Histórico de mudanças do banco. Nunca edite arquivo antigo — sempre crie migração nova. |

### As três regras próprias deste banco

| Regra | Por quê |
|---|---|
| Tabelas e colunas em minúsculas | Convenção do PostgreSQL, aplicada automaticamente no fim do `OnModelCreating`. Você não faz nada, mas precisa saber que acontece. |
| Datas sempre em UTC | Há conversão automática na gravação e na leitura. Gravar hora local gera diferença de 3 horas: jogo às 21h aparece como 00h do dia seguinte. |
| Cascata com exceções | Apagar um jogo apaga gols e cartões. Apagar um time **não** apaga o histórico de transferências — o campo só fica nulo. Leia os `OnDelete` antes de apagar qualquer coisa. |

### Carregar dados relacionados

```csharp
// traz o jogo e, junto, os dois times
.Include(j => j.TimeCasa)
.Include(j => j.TimeVisitante)

// traz as escalações e, dentro de cada uma, o jogador
.Include(j => j.Escalacoes).ThenInclude(e => e.Jogador)
```

Sem `Include`, a propriedade vem nula e a tela quebra com referência nula. Com `Include`
demais, a consulta fica pesada. O equilíbrio é julgamento — e se erra para os dois lados.

### Migrações

Altere a classe do Model, rode `dotnet ef migrations add NomeDescritivo` e **leia o arquivo
gerado** antes de subir. O EF acerta quase sempre, mas quando erra — renomear coluna, por
exemplo — ele apaga a antiga e cria vazia. Com dados de produção, isso é perda
irreversível.

> Migração é a única coisa neste sistema que vale mostrar a alguém mais experiente antes de
> aplicar, até você ter feito umas cinco.

**Concluído quando:** você cria e revisa uma migração sozinho, e explica o que acontece com
os dados relacionados ao apagar um jogo.

---

## Nível 06 — Telas que conversam com o servidor sem recarregar

Até aqui toda ação recarregava a página. Agora as telas que atualizam pedaços via
JavaScript. O padrão: o JS chama uma URL, o controller devolve JSON, o JS desenha.

| Arquivo | Conteúdo |
|---|---|
| `Controllers/JogosCronometroController.cs` (89 linhas) | O menor deles — leia primeiro para pegar o formato. |
| `Controllers/JogosEventosController.cs` (377 linhas) | Gols, cartões, placar. Comece por `RegistrarGol` e siga até o JS. |
| `Controllers/JogosObservacoesController.cs` (161 linhas) | Observações com tag e menções a jogador escritas com `@` no texto. |
| `wwwroot/js/site.js` (222 linhas) | Funções compartilhadas por todas as telas. Veja como o token anti-CSRF entra sozinho nas chamadas. |

### Detalhe de roteamento que confunde

Esses controllers têm `[Route("Jogos/[action]/{id?}")]` no topo. Isso mantém as URLs como
`/Jogos/RegistrarGol` mesmo com o código morando em `JogosEventosController` — foi assim
que o código foi separado sem quebrar o JavaScript.

**Se você mover uma action de controller, a URL muda** e o front para de achar. Confira
sempre se existe rota declarada.

### Um caso especial: por que existe o MediaProxy

As fotos de jogadores e escudos vêm de um serviço externo que exige chave de autenticação.
Se a página apontasse direto para lá, a chave ficaria exposta no HTML. Então o servidor
busca a imagem, guarda em memória e serve — é o `MediaProxyController` (169 linhas). Vale
ler: é curto e mostra cache, lista de domínios permitidos e tratamento de falha de rede.

### Exercício

Acompanhe um clique inteiro: registre um cartão amarelo na tela de análise com a aba de
rede do navegador aberta. Veja a URL chamada, o que foi enviado, o que voltou. Depois ache
esses três pontos no código.

**Concluído quando:** você cria um endpoint JSON novo e o consome no JS, com o token
anti-CSRF correto.

---

## Nível 07 — A tela Analisar

A tela principal do produto: campo com jogadores, arrastar e soltar, escalação inicial e
final, fases táticas, setas de movimentação, mapa de calor, cronômetro, eventos e painéis
de pré e pós-jogo. É onde mais se mexe e onde mais se quebra.

| Arquivo | Conteúdo |
|---|---|
| `Views/Jogos/Analisar.cshtml` (1.447 linhas) | Só o HTML e um bloco de dados. Já foi 6.029 linhas — CSS e JS foram separados. |
| `wwwroot/js/analisar.js` (2.872 linhas) | Todo o comportamento. Lê os dados do jogo pelo objeto `window.ANALISAR`, montado na view. |
| `wwwroot/css/analisar.css` (1.755 linhas) | O visual. Só as cores das camisas ficaram na view, porque mudam a cada partida. |

### Entenda o contrato antes de mexer

A view monta `window.ANALISAR` com tudo que veio do servidor — id do jogo, nomes dos times,
elencos, médias, setas. O arquivo JS lê dali e nunca mais fala com o Razor. Se precisa de
um dado novo no JavaScript, ele entra nesse objeto. **Não volte a escrever C# dentro do
JS.**

### O método Analisar, por partes

São 427 linhas em `JogosController`. Não leia de cima a baixo — leia por blocos:

1. Carrega o jogo e descobre a fase (inicial, final ou fase tática intermediária).
2. Busca a escalação do usuário. Se não existir, copia da escalação importada; se nem isso,
   monta uma base a partir da formação.
3. Na fase final, calcula quem entrou e quem saiu comparando com a escalação inicial.
4. Monta os dados dos balões de informação de cada jogador (gols, assistências, médias).

O passo 2 responde pela maioria dos chamados do tipo "meu campo apareceu vazio". Leia os
comentários dele com atenção — explicam casos que já deram problema de verdade.

### Exercício

Adicione uma informação nova ao balão do jogador — por exemplo, quantos cartões ele tem na
competição. Exige consultar no controller, colocar em `window.ANALISAR` e desenhar no JS: o
caminho completo da tela mais difícil do sistema.

**Concluído quando:** você adiciona informação nova à tela de análise sem quebrar
arrastar-e-soltar, e explica a diferença entre escalação inicial e final.

---

## Nível 08 — Serviços, integrações e o que roda sozinho

Boa parte dos dados não é digitada: vem de APIs externas de futebol. É a camada que mais
falha, porque depende de rede e de terceiros.

| Arquivo | Conteúdo |
|---|---|
| `Services/AtualizarJogadoresSemDataService.cs` (154 linhas) | Roda em segundo plano enquanto a aplicação está no ar. Comece por este. |
| `Services/RelatoriosService.cs` (945 linhas) | Toda a lógica dos relatórios, fora do controller. É o exemplo a seguir quando a regra de negócio cresce. |
| `Services/ApiFootballService.cs` (2.154 linhas) | A integração principal: jogos, escalações, estatísticas, elencos. O arquivo mais difícil do sistema — muita normalização de nome de jogador. |
| `Helpers/` (14 arquivos) | Cálculos reaproveitados: classificação, chaveamento, posições, bandeiras. Antes de escrever cálculo novo, procure aqui. |

### A regra que separa serviço de controller

Se a lógica é "como responder a esta tela", fica no controller. Se é "como o futebol
funciona" — quem se classifica, como se calcula aproveitamento — é serviço ou helper.

O `RelatoriosService` segue isso bem. O `JogadoresController`, com 2.293 linhas, é o
contraexemplo do sistema.

### A API para o aplicativo Android

`Controllers/Api/` é um mundo à parte: mesmas tabelas, mas autenticação por token JWT em vez
de cookie, e resposta sempre em JSON. Ao mexer numa regra de negócio, lembre que pode haver
um espelho dela ali.

**Concluído quando:** você investiga "por que este jogo não importou" percorrendo serviço,
logs e banco — sem chutar.

---

## Nível 09 — Infraestrutura, segurança e publicação

Volte ao `Program.cs`. Agora cada bloco liga uma peça que você já viu funcionando.

| Bloco | O que garante |
|---|---|
| `AddDbContext` | A conexão com o PostgreSQL. |
| `AddIdentity` | Usuários, senhas e bloqueio após 5 tentativas erradas. |
| `AuthorizeFilter` | Toda tela exige login por padrão. Para liberar uma, é preciso marcar explicitamente. |
| `AutoValidateAntiforgeryToken` | Todo POST exige token anti-CSRF, automaticamente. |
| `AssinaturaFilter` | Bloqueia usuário inadimplente. |
| `Content-Security-Policy` | Limita de onde a página pode carregar script, estilo e imagem. |
| `MapHealthChecks` | O endereço `/health`, usado pelo deploy para saber se a aplicação subiu de pé. |

### Regras que não se quebram

- Segredo nunca vai para `appsettings.json` nem para o Git — vai para user-secrets ou
  variável de ambiente.
- Tela nova nasce exigindo login. Se precisar liberar alguma, justifique.
- Não relaxe a política de segurança para "resolver" um recurso que não carrega — o
  problema é outro.

**Concluído quando:** você explica por que uma tela nova já nasce protegida, e o que
`/health` tem a ver com publicar.

---

## Casos reais deste sistema

Aconteceram de verdade. Cada um ensina um jeito de pensar que tutorial não ensina.

### 1. "As fotos somem e não voltam"

O serviço externo de imagens ficou instável e as imagens falharam. Mas a resposta de erro
era enviada com instrução de guardar por 24 horas — então o navegador guardou o erro e
seguiu mostrando imagem quebrada por um dia inteiro, mesmo depois de tudo normalizar.

**Lição:** resposta de erro nunca pode ser guardada em cache. Só sucesso pode.

### 2. "A tela demora e não sei por quê"

A tela de análise montava um dicionário com os gols de *todos* os jogadores da competição
para exibir os de 40 jogadores em campo. Ninguém percebeu porque, com pouco dado, era
rápido.

**Lição:** lentidão quase sempre é dado a mais, não código lento. Pergunte "quantas linhas
isso traz?" antes de "como otimizo?".

### 3. "Erro de rede que não era erro de rede"

O log enchia de falha de tempo esgotado ao buscar imagens. Parecia problema do serviço
externo. Era o servidor de nomes da máquina que não resolvia aquele endereço específico —
outros endereços do mesmo domínio funcionavam.

**Lição:** antes de mexer no código, confirme que o problema é do código.

### 4. Um erro 500 que não era para ser consertado

A tela `/Jogos/Details` devolvia erro 500: a view havia sido copiada da tela de Times e
esperava um tipo de dado que o controller não enviava. A reação natural seria consertar a
view.

Só que a tela não era mais usada — era o fluxo anterior à criação da `/Jogos/Analisar`, que
a substituiu. Consertar teria dado trabalho para manter viva uma tela que ninguém abre.
Ela foi removida, junto com a `EditEscalacao`, que fazia parte do mesmo fluxo antigo.

**Lição, e talvez a mais importante da lista:** antes de consertar, pergunte se aquilo ainda
deveria existir. Código morto que dá erro parece bug, e leva gente a "arrumar" o que devia
sair. Quem conhece o histórico do produto responde isso em dez segundos — pergunte.

---

## Regras da casa

- **Código em português.** Nomes de método, variável e comentário seguem o idioma do
  domínio: `BuscarEventos`, `escalacoesCasa`. Não misture inglês.
- **Comentário explica o porquê, não o quê.** Este projeto tem comentários muito bons —
  quase todos contam qual problema real motivou aquela linha. Mantenha o costume.
- **Sempre `async`.** Toda operação de banco usa `await` e termina em `Async`.
- **Nada de segredo no código.** Nem chave, nem senha, nem token.
- **CSS e JS de tela grande vão para arquivo.** Não volte a escrever bloco gigante dentro
  do `.cshtml`.
- **Compilar não é testar.** Suba e abra a tela.

### Antes de dizer "terminei"

1. `dotnet build` sem erros.
2. `dotnet test` — os 31 testes passando.
3. Subiu, abriu a tela afetada, ela funciona.
4. Testou também o caminho que dá errado (formulário vazio, id inexistente).
5. O banco de desenvolvimento ficou como estava — sem lixo de teste.

---

## O que você já pode resolver sozinho

| Tipo de chamado | Onde começar | Liberado a partir de |
|---|---|---|
| Texto errado na tela | `Views/` | nível 01 |
| Falta um filtro na listagem | `Controllers/` | nível 02 |
| Campo novo em cadastro | Model + migração + view | nível 03 |
| Tela lenta | `Include` e `Select` da consulta | nível 04 |
| Erro de referência nula | `Include` faltando | nível 05 |
| Botão não faz nada | Console do navegador, aba de rede | nível 06 |
| Campo aparece vazio na análise | `JogosController.Analisar` | nível 07 |
| Jogo não importou | `Services/ApiFootballService` | nível 08 |
| Migração com dado em produção | — | **peça revisão** |
| Mudança em segurança | — | **peça revisão** |
| Exclusão de dados em massa | — | **peça revisão** |

Os três últimos não são desconfiança de capacidade — são casos em que qualquer pessoa da
equipe, independente de experiência, pede revisão antes de executar. Erro ali não tem
desfazer.

No resto: quebre à vontade em desenvolvimento. É de graça, e é assim que se aprende.
