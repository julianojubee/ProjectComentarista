# Planejamento — Blog público em `/blog`

Objetivo: uma área de blog no ControleFutebolWeb com **leitura 100% pública** (sem login)
e **escrita/edição restrita** a usuários autorizados, seguindo as práticas usuais de um
blog editorial (SEO, rascunho/publicação, sanitização de conteúdo, feed, sitemap).

> **Status: Fase 1 implementada** (migration `20260727174845_AddBlog`) **+ upload de imagens**.
> No ar: `/blog` (listagem paginada), `/blog/{slug}` (post), `/blog/admin` (painel do
> autor com editor Markdown, preview, upload de imagem no corpo e na capa,
> publicar/despublicar/excluir), flag `EhAutorBlog` com toggle na tela de usuários,
> link "Blog" no menu do site logado.
> Pendentes: categorias/tags na UI, agendamento, revisões, SEO completo, RSS,
> sitemap, busca — ver seção 10.

---

## 1. Restrições do projeto que condicionam o desenho

Levantadas em `Program.cs` e nos filtros globais — são o ponto mais delicado, porque hoje
**tudo no site é privado por padrão**:

| Item | Onde | Impacto no blog |
|---|---|---|
| `AuthorizeFilter` global | `Program.cs:111` | Todo controller exige login. O blog público precisa de `[AllowAnonymous]` **explícito** no controller de leitura. |
| `AssinaturaFilter` global | `Program.cs:117` / `Filters/AssinaturaFilter.cs` | Anônimo passa direto (sem `userId`), mas **usuário logado e inadimplente seria redirecionado para `/Account/Bloqueado` ao abrir `/blog`**. Precisa isentar o controller público (mesma técnica do `EhControllerAccount`). |
| `AtividadeUsuarioFilter` | `Program.cs:115` | Sem impacto (só age com usuário autenticado). |
| `AutoValidateAntiforgeryToken` global | `Program.cs:113` | Todos os POSTs do editor já vêm protegidos; formulários Razor com tag helper funcionam sem ajuste. |
| CSP restritiva | `Program.cs:244-256` | `script-src 'self' 'unsafe-inline'` → **editor não pode vir de CDN**, tem que ser servido de `wwwroot/lib`. `img-src 'self' data: https:` já permite imagens externas. |
| Cookie `__Host-` + `SecurePolicy.Always` | `Program.cs:99-101` | Sem impacto na leitura anônima. |
| `_Layout.cshtml` | `Views/Shared/_Layout.cshtml:1-38` | Injeta `UserManager`/`FutebolContext` e monta menu por usuário. Funciona com anônimo (guardado por `if (_userId != null)`), mas o menu é todo de área logada → **layout público separado**. |
| Datas | `FutebolContext.AjustarDatasParaUtc` | `DateTime` em coluna `timestamptz` já é convertido para UTC automaticamente. Guardar tudo em UTC e formatar para UTC-3 na view. |
| Banco | PostgreSQL + EF Core migrations | Modelo novo entra como migration normal (`dotnet ef migrations add`). `UseSerialColumns()` já configurado. |

---

## 2. Modelo de dados

Cinco tabelas novas. Nomes em português, seguindo o padrão dos models existentes
(`ObservacaoJogoTag`, `AnotacaoTime`).

### `BlogPost`
```
Id                int (PK)
Titulo            string(200)      obrigatório
Slug              string(200)      obrigatório, ÚNICO (índice único)
Resumo            string(300)      usado no card da listagem e na meta description
ConteudoMarkdown  text             fonte da verdade do texto
ConteudoHtml      text             HTML renderizado + sanitizado (cache de leitura)
ImagemCapaUrl     string(500)?     capa / og:image
ImagemCapaAlt     string(200)?     acessibilidade
Status            enum             Rascunho | Agendado | Publicado | Arquivado
PublicadoEm       timestamptz?     data de publicação (pode ser futura = agendado)
AtualizadoEm      timestamptz?     exibido como "atualizado em"
CriadoEm          timestamptz
AutorId           string (FK ApplicationUser)
CategoriaId       int? (FK BlogCategoria)
TempoLeituraMin   int              calculado no save (palavras / 200)
Visualizacoes     int              contador simples
MetaTitulo        string(70)?      override de SEO
MetaDescricao     string(160)?     override de SEO
ExcluidoEm        timestamptz?     soft delete
```

### `BlogCategoria`
`Id, Nome, Slug (único), Descricao?, Ordem` — ex.: *Análises*, *Rodada*, *Bastidores*.

### `BlogTag`
`Id, Nome, Slug (único)`.

### `BlogPostTag`
Tabela de junção `PostId + TagId` (chave composta).

### `BlogPostRevisao`
`Id, PostId, ConteudoMarkdown, Titulo, SalvoEm, SalvoPorId` — histórico de edições.
Barato de implementar e evita perder texto; grave uma revisão a cada salvamento.

### Índices
- `BlogPost.Slug` único.
- `BlogPost (Status, PublicadoEm DESC)` — é a query da listagem pública.
- `BlogCategoria.Slug` e `BlogTag.Slug` únicos.
- Full-text opcional (fase 2): coluna `tsvector` gerada no Postgres para a busca.

### Vínculo com o domínio do site (diferencial)
Campos opcionais `JogoId?`, `TimeId?`, `JogadorId?` no post permitem publicar um texto
"amarrado" a um jogo/time/jogador e depois exibir os posts relacionados dentro das telas
já existentes. Recomendo deixar as colunas prontas desde a primeira migration mesmo que a
exibição venha depois — evita nova migration.

---

## 3. Rotas

### Público (anônimo)
| Rota | Função |
|---|---|
| `GET /blog` | Listagem paginada (12 por página), ordenada por `PublicadoEm DESC` |
| `GET /blog/pagina/{n}` | Paginação com URL indexável |
| `GET /blog/{slug}` | Post individual |
| `GET /blog/categoria/{slug}` | Listagem filtrada |
| `GET /blog/tag/{slug}` | Listagem filtrada |
| `GET /blog/busca?q=` | Busca simples (título + resumo + conteúdo) |
| `GET /blog/feed.rss` | Feed RSS 2.0 |
| `GET /blog/sitemap.xml` | Sitemap só do blog |
| `GET /blog/preview/{slug}?token=` | Pré-visualização de rascunho via token assinado |

### Área de escrita (autenticada)
| Rota | Função |
|---|---|
| `GET /blog/admin` | Lista de posts do autor (admin vê todos) + filtros por status |
| `GET/POST /blog/admin/novo` | Criar |
| `GET/POST /blog/admin/editar/{id}` | Editar |
| `POST /blog/admin/publicar/{id}` | Publicar / agendar |
| `POST /blog/admin/despublicar/{id}` | Voltar para rascunho |
| `POST /blog/admin/excluir/{id}` | Soft delete |
| `POST /blog/admin/imagem` | Upload de imagem (retorna URL) |
| `POST /blog/admin/autosave/{id}` | Salvamento automático (a cada 30s) |
| `GET /blog/admin/revisoes/{id}` | Histórico e restauração |

Separação em **dois controllers** (`BlogController` público e `BlogAdminController`) — não
misturar ações públicas e privadas no mesmo arquivo é o que reduz o risco de vazar rascunho.

---

## 4. Autorização

**Decisão: flag booleana `EhAutorBlog` em `ApplicationUser`** (não role Identity).
Segue o padrão já estabelecido de `IsAdmin` — o projeto tem as tabelas de roles do
Identity, mas nunca as usou; introduzi-las só para o blog criaria dois mecanismos de
autorização convivendo. Para "pode/não pode escrever", o bool é mais simples e
consistente.

1. Coluna **`EhAutorBlog`** (bool, default `false`) em `ApplicationUser` — migration simples.
2. Nova política **`"BlogEscrever"`**: aprova se `IsAdmin == true` **ou** `EhAutorBlog == true`. Handler no mesmo molde do `AdminHandler` (consulta o banco a cada checagem — desejável: desmarcar a flag revoga o acesso na hora, sem esperar relogin).
3. Checkbox "Autor do blog" na tela de usuários do `/Admin`, ao lado do controle de admin já existente.
4. Regra de escopo no `BlogAdminController`: autor edita **só os próprios posts**; admin edita todos.
5. Se um dia surgirem papéis combináveis (revisor, moderador de comentários…), aí sim migrar para roles — a flag é trivialmente convertível.

**Checklist de segurança da leitura pública:**
- `[AllowAnonymous]` no `BlogController` inteiro.
- Isentar o `BlogController` do `AssinaturaFilter` (senão usuário inadimplente logado não lê o blog).
- Toda query pública filtra `Status == Publicado && PublicadoEm <= agora && ExcluidoEm == null` — **em um único método privado reutilizado**, nunca repetido por ação.
- `/blog/{slug}` de post não publicado devolve **404**, não 403 (não revela existência).

---

## 5. Editor e conteúdo

### Formato: Markdown como fonte da verdade
Recomendo **Markdown (Markdig)** em vez de HTML rico direto no banco:
- superfície de XSS muito menor;
- diff/revisão legível;
- portável se um dia migrar de stack.

Fluxo: autor escreve Markdown → no `Save`, o servidor renderiza com **Markdig** → passa o
HTML por **Ganss.Xss (HtmlSanitizer)** com allowlist de tags → grava em `ConteudoHtml`.
A view pública só imprime `@Html.Raw(post.ConteudoHtml)`, sem processar nada em tempo de request.

Pacotes novos: `Markdig` e `Ganss.Xss`.

### Editor no navegador
**EasyMDE** ou **Toast UI Editor**, baixados para `wwwroot/lib/` — a CSP proíbe CDN.
Se preferir zero dependência: `<textarea>` + toolbar própria + preview server-side via
endpoint `/blog/admin/preview-md`. Funciona, custa mais trabalho de UX.

### Upload de imagens — **implementado**
`POST /blog/admin/imagem` (política `BlogEscrever` + antiforgery), usado pelo botão
"Inserir imagem" do corpo e pelo "Enviar arquivo" da capa. Devolve `{ url }`.

- Destino: `wwwroot/uploads/blog/{yyyy}/{MM}/{guid}.{ext}` — particionado por ano/mês.
- **O formato vem dos magic bytes do conteúdo, não da extensão do nome enviado.**
  Aceita JPG, PNG, GIF, WebP. Verificado: `.exe` renomeado para `.jpg`, HTML renomeado
  para `.png` e SVG são todos recusados.
- **SVG é recusado de propósito**: é XML, pode conter `<script>`, e seria servido do
  próprio domínio (mesma origem do cookie de sessão).
- Nome sempre regerado (GUID) — o nome enviado é descartado, o que elimina path
  traversal e colisão de arquivos.
- Limite de 5 MB no helper + `RequestSizeLimit` no action.
- `wwwroot/uploads/` está no `.gitignore`.
- **Deploy auditado**: `deploy.ps1` usa `scp -r publish/* → destino`, que sobrepõe sem
  apagar — a pasta de uploads no servidor sobrevive aos deploys. Nenhuma mudança
  necessária no script.
- Redimensionamento (largura máx. + thumbnail) ficou de fora: exigiria `ImageSharp`,
  cuja licença cobra para uso comercial acima de certo faturamento. Hoje o controle é
  só o limite de 5 MB.

O campo da capa aceita `https://…` **ou** caminho relativo do próprio site
(`/uploads/blog/…`) — validado por `ImagemUrlValidaAttribute`. `http://` é recusado no
formulário porque a CSP (`img-src … https:`) bloquearia a imagem no navegador, e falha
silenciosa de imagem é pior que erro de validação.

### Slug
Gerado do título: minúsculas, sem acento, hífens, sem stop words no fim.
Colisão → sufixo `-2`, `-3`. **Slug não muda depois de publicado**; se o autor mudar o
título, manter o slug antigo e (fase 2) gravar redirect 301 do antigo para o novo.

---

## 6. SEO e distribuição

Sem isso, um blog não cumpre a função. Itens da v1:

- `<title>` e `<meta name="description">` por post (usar `MetaTitulo`/`MetaDescricao`, com fallback para `Titulo`/`Resumo`).
- `<link rel="canonical">` absoluto em todas as páginas do blog.
- **Open Graph** + **Twitter Card** (`og:title`, `og:description`, `og:image`, `og:type=article`, `article:published_time`) — é o que faz o link ficar bonito no WhatsApp/X.
- **JSON-LD** `BlogPosting` (headline, datePublished, dateModified, author, image) + `BreadcrumbList`.
- `sitemap.xml` do blog + `robots.txt` na raiz apontando para ele (verificar se já existe robots.txt — não existe hoje).
- Feed **RSS** em `/blog/feed.rss` com `<link rel="alternate">` no `<head>`.
- HTML semântico: `<article>`, `<time datetime>`, `<h1>` único por post, hierarquia de headings correta.
- `alt` obrigatório nas imagens (o campo `ImagemCapaAlt` existe para isso).
- URLs limpas e estáveis (`/blog/analise-flamengo-palmeiras`), nunca `?id=42`.

**Importante:** o `_Layout.cshtml` atual não tem nenhum bloco de meta tags flexível. O layout
público precisa de `@RenderSectionAsync("Meta", required: false)` para as views injetarem OG/JSON-LD.

---

## 7. Layout e UI

Criar `Views/Shared/_LayoutBlog.cshtml`:
- Navbar enxuta: logo, categorias, busca, e botão "Entrar" (ou "Escrever" se autenticado com permissão).
- Reaproveita `site.css`, Bootstrap e o toggle de tema já existentes (`data-theme` / `data-bs-theme`).
- **Não** injeta `UserManager`/`FutebolContext` como o layout atual — página pública tem que
  renderizar sem tocar no banco por usuário.
- CSS próprio `wwwroot/css/blog.css` para a tipografia do artigo (largura de leitura ~70ch,
  line-height ~1.7, tamanho ≥ 18px).

Telas:
1. **Listagem** — card com capa, categoria, título, resumo, autor, data, tempo de leitura. Post em destaque no topo.
2. **Post** — capa, título, meta (autor/data/tempo), corpo, tags, "posts relacionados" (mesma categoria/tags), botões de compartilhar.
3. **Editor** — duas colunas: Markdown + preview; barra lateral com status, categoria, tags, capa, SEO, agendamento. Indicador de "salvo automaticamente".
4. **Lista admin** — tabela com status, data, autor, visualizações, ações.

---

## 8. Performance e cache

- `AsNoTracking()` em toda leitura pública (padrão já usado no projeto).
- Paginação com `Skip/Take` (volume não justifica keyset agora).
- **Output cache** nas rotas públicas (ASP.NET Core `AddOutputCache`, 5 min), com tag por
  post e `EvictByTag` ao publicar/editar. Reduz o custo do blog a ~zero em picos de tráfego.
- Listagem projeta só as colunas do card — nunca carregar `ConteudoHtml` na listagem.
- `ETag`/`Last-Modified` no post individual.
- Contador de visualizações: incremento assíncrono e agrupado, nunca um `UPDATE` síncrono
  dentro do request (ou usar tabela de eventos + job).

---

## 9. Testes

O projeto já tem `ControleFutebolWeb.Tests`. Cobrir:
- Geração de slug (acentos, duplicatas, títulos longos, caracteres especiais).
- Sanitização: `<script>`, `onerror=`, `javascript:` em link — todos removidos.
- Filtro de publicação: rascunho, agendado com data futura e soft-deleted **não** aparecem no público.
- Autorização: usuário sem role não acessa `/blog/admin`; autor não edita post de outro.
- Renderização Markdown → HTML esperado.
- Upload: rejeita arquivo com extensão de imagem mas conteúdo executável.

---

## 10. Fases de entrega

**Fase 1 — Fundação (MVP publicável)**
Models + migration (incl. flag `EhAutorBlog`), `BlogController` público (listagem + post),
`BlogAdminController` com CRUD e Markdown, política `BlogEscrever` + checkbox no `/Admin`,
layout público, slug, sanitização, isenção do `AssinaturaFilter`.
→ *já dá para publicar o primeiro texto.*

**Fase 2 — Qualidade editorial**
Categorias e tags, capa e upload de imagens, rascunho/agendamento/preview por token,
autosave, revisões (`BlogPostRevisao` pode ficar fora da migration inicial), tempo de
leitura, posts relacionados.

**Fase 3 — Alcance**
SEO completo (OG, JSON-LD, sitemap, robots), RSS, busca, output cache (só se houver
tráfego que justifique), contador de views, link do blog no menu do site logado.

**Fase 4 — Opcionais**
Comentários (exigem moderação, honeypot e rate limit — só se realmente quiser), newsletter,
métricas por post, posts vinculados a jogo/time/jogador nas telas existentes,
endpoint no app Android.

---

## 11. Decisões que dependem de você

1. ~~**Quem escreve?**~~ **Decidido**: flag `EhAutorBlog` no usuário, marcada pelo admin (ver seção 4).
2. **Markdown ou editor rich text WYSIWYG?** (recomendo Markdown; WYSIWYG é possível, custa sanitização mais rígida)
3. **Comentários do público?** (recomendo deixar fora da v1 — spam e moderação dão mais trabalho que o blog inteiro)
4. **Onde ficam as imagens?** disco do servidor (`wwwroot/uploads`) ou só URLs externas coladas? Disco exige ajuste no `deploy.ps1` para não apagar os arquivos.
5. **`/blog` entra no menu principal do site logado** e/ou vira porta de entrada pública do domínio?

---

## 12. Riscos conhecidos

| Risco | Mitigação |
|---|---|
| Rascunho vazar publicamente | Filtro de publicação centralizado em um único método + teste automatizado |
| XSS pelo conteúdo do post | Markdig + Ganss.Xss no servidor; nunca sanitizar só no cliente |
| Deploy apagar `wwwroot/uploads` | Auditar `deploy.ps1` antes da fase 2 |
| CSP quebrar o editor | Vendorizar o editor em `wwwroot/lib`, sem CDN |
| `AssinaturaFilter` bloquear leitor logado inadimplente | Isentar `BlogController` explicitamente |
| Slug mudando e quebrando links | Slug imutável após publicação + redirect 301 |
