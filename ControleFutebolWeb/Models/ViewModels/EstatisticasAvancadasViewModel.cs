namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>
    /// Tela /Jogadores/EstatisticasAvancadas/{id}: o que o FotMob publica sobre o
    /// jogador, buscado no clique e nunca gravado (ver FotMobPerfilService).
    ///
    /// Nada aqui vira registro no banco. É por isso que esta tela não pode criar
    /// divergência com o resto do sistema: não existe cópia nossa desses números para
    /// discordar deles depois.
    /// </summary>
    public class EstatisticasAvancadasViewModel
    {
        // Do NOSSO cadastro — para o cabeçalho da página e o link de volta.
        public int JogadorId { get; set; }
        public string JogadorNome { get; set; } = "";

        /// <summary>
        /// O jogador como o SISTEMA o conhece (foto, posição, idade, altura, clube,
        /// nacionalidade), para o cabeçalho ser o mesmo da página do jogador.
        ///
        /// É o nosso cadastro, e não a ficha de identificação da fonte externa — essa
        /// continua fora. A diferença é o que evita o problema de sempre: duas telas do
        /// próprio sistema mostrando o mesmo jogador com altura e país diferentes. Aqui
        /// as duas leem a mesma linha do banco, então elas não têm como discordar.
        ///
        /// Vem preenchido depois de FotMobCadastroService completar o que faltava, que
        /// é justamente o que faz este cabeçalho ter o que mostrar em quem chegou aqui
        /// sem altura nem foto.
        ///
        /// Null só se o jogador sumir entre uma consulta e outra; a view trata.
        /// </summary>
        public Jogador? Cadastro { get; set; }

        public long IdFotMob { get; set; }

        /// <summary>Preenchido quando a busca falhou; a view mostra o aviso e mais nada.</summary>
        public string? Erro { get; set; }

        /// <summary>
        /// Como o FotMob chama este jogador. Aparece uma vez, discreto, para o usuário
        /// confirmar que o vínculo aponta para a pessoa certa — o id foi descoberto por
        /// casamento automático, e conferir de olho custa nada.
        /// </summary>
        public string? NomeNoFotMob { get; set; }

        /// <summary>
        /// Posição principal segundo eles. Não substitui a nossa: a página do jogador já
        /// mostra a distribuição real por jogo, e ver as duas juntas é que informa.
        /// </summary>
        public string? PosicaoPrincipal { get; set; }

        public string? ValorDeMercado { get; set; }

        /// <summary>
        /// Lesão em curso, quando há. Vem do mesmo perfil já buscado para montar esta
        /// tela — não custa chamada nenhuma. Aparece aqui e no cabeçalho da página do
        /// jogador porque é a informação que explica um período inteiro de números
        /// parados, e quem abre uma das duas telas precisa dela do mesmo jeito.
        /// </summary>
        public SituacaoFisicaViewModel? Situacao { get; set; }

        /// <summary>
        /// Temporadas, cada uma com as competições que o jogador disputou nela. O
        /// agrupamento existe porque a lista crua é longa e repetitiva — um jogador com
        /// carreira comprida tem catorze linhas do tipo "2025/2026 · AFC Champions
        /// League", e escolher ano e competição em dois passos é bem mais legível do que
        /// varrer tudo de uma vez.
        /// </summary>
        public List<TemporadaAgrupada> Temporadas { get; set; } = new();

        /// <summary>Id (entryId) da competição em exibição.</summary>
        public string? CompeticaoSelecionada { get; set; }

        /// <summary>Nome da temporada em exibição — abre expandida na tela.</summary>
        public string? TemporadaSelecionada { get; set; }

        public List<GrupoMetricasFotMob> Grupos { get; set; } = new();
        public List<JogoFotMob> Jogos { get; set; } = new();
        public List<PassagemFotMob> Carreira { get; set; } = new();

        /// <summary>
        /// A carreira quebrada por temporada (jogos, gols e assistências de cada uma),
        /// com o detalhe por competição. Vem do mesmo bloco que monta <see cref="Carreira"/>,
        /// então o totalizador do topo consegue alternar entre "carreira toda" e uma
        /// temporada específica sem nenhuma busca nova.
        ///
        /// Não confundir com <see cref="Temporadas"/>: aquela é o seletor do bloco de
        /// desempenho, que lista entryIds para PEDIR estatística detalhada à fonte. Esta
        /// já traz os números somados e serve só para totalizar.
        /// </summary>
        public List<TemporadaCarreira> TemporadasCarreira { get; set; } = new();
        public List<TituloFotMob> Titulos { get; set; } = new();

        public bool TemDesempenho => Grupos.Count > 0;

        /// <summary>
        /// Gols e assistências somados da carreira inteira, para o topo da tela.
        ///
        /// Sai de <see cref="Carreira"/>, que já está nesta mesma página: a tela mostrava
        /// os números só dentro de uma competição de uma temporada por vez, e quem queria
        /// saber quanto o jogador fez no total tinha que somar as linhas de olho. Nenhuma
        /// busca nova — é a mesma resposta, lida de outro jeito.
        ///
        /// Clube e seleção entram separados porque somar os dois num número só apagaria a
        /// distinção que a própria tabela de carreira faz.
        /// </summary>
        public TotaisCarreira Totais => TotaisCarreira.De(Carreira);

        /// <summary>
        /// Opções do seletor do totalizador: "carreira toda" primeiro, depois uma por
        /// temporada, da mais recente para a mais antiga.
        ///
        /// Temporadas com o mesmo nome entram como uma só (quem trocou de clube no meio
        /// do ano tem duas linhas para o mesmo ano, e o total da temporada é a soma das
        /// duas) — os clubes vão no detalhe do cartão.
        /// </summary>
        public List<OpcaoTotalizador> OpcoesTotalizador
        {
            get
            {
                var opcoes = new List<OpcaoTotalizador>();

                var carreira = Totais;
                if (carreira.TemDados)
                {
                    opcoes.Add(new OpcaoTotalizador
                    {
                        Id = "carreira",
                        Rotulo = "Carreira",
                        Jogos = carreira.JogosGeral,
                        Gols = carreira.GolsGeral,
                        Assistencias = carreira.AssistenciasGeral,
                        Detalhe = "todas as passagens da carreira",
                        DetalheGols = carreira.TemSelecao
                            ? $"{carreira.Gols} por clubes · {carreira.GolsSelecao} pela seleção" : "",
                        DetalheAssistencias = carreira.TemSelecao
                            ? $"{carreira.Assistencias} por clubes · {carreira.AssistenciasSelecao} pela seleção" : "",
                    });
                }

                foreach (var grupo in TemporadasCarreira
                    .GroupBy(t => t.Nome)
                    .OrderByDescending(g => g.Key, StringComparer.Ordinal))
                {
                    var times = grupo.Select(t => t.Time)
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Distinct().ToList();

                    opcoes.Add(new OpcaoTotalizador
                    {
                        Id = "t:" + grupo.Key,
                        Rotulo = grupo.Key,
                        Jogos = grupo.Sum(t => t.Jogos),
                        Gols = grupo.Sum(t => t.Gols),
                        Assistencias = grupo.Sum(t => t.Assistencias),
                        Detalhe = times.Count > 0 ? string.Join(" · ", times) : "",
                        Competicoes = grupo.SelectMany(t => t.Competicoes)
                            .GroupBy(c => c.Nome)
                            .Select(g => new CompeticaoNaTemporada
                            {
                                Nome = g.Key,
                                Jogos = g.Sum(c => c.Jogos),
                                Gols = g.Sum(c => c.Gols),
                                Assistencias = g.Sum(c => c.Assistencias),
                            })
                            .OrderByDescending(c => c.Gols + c.Assistencias).ThenByDescending(c => c.Jogos)
                            .ToList(),
                    });
                }

                return opcoes;
            }
        }
    }

    /// <summary>Uma temporada da carreira, por passagem (clube ou seleção).</summary>
    public class TemporadaCarreira
    {
        /// <summary>"2026", "2024/2025" — como a fonte nomeia a temporada.</summary>
        public string Nome { get; set; } = "";
        public string Time { get; set; } = "";
        public bool Selecao { get; set; }
        public int Jogos { get; set; }
        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public List<CompeticaoNaTemporada> Competicoes { get; set; } = new();
    }

    public class CompeticaoNaTemporada
    {
        public string Nome { get; set; } = "";
        public int Jogos { get; set; }
        public int Gols { get; set; }
        public int Assistencias { get; set; }
    }

    /// <summary>Uma escolha do seletor do totalizador (a carreira toda ou uma temporada).</summary>
    public class OpcaoTotalizador
    {
        public string Id { get; set; } = "";
        public string Rotulo { get; set; } = "";
        public int Jogos { get; set; }
        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public int Participacoes => Gols + Assistencias;

        /// <summary>Linha fina do rodapé: os clubes da temporada, ou o que a carreira soma.</summary>
        public string Detalhe { get; set; } = "";

        /// <summary>Quebra clube/seleção sob o cartão — só faz sentido na carreira inteira.</summary>
        public string DetalheGols { get; set; } = "";
        public string DetalheAssistencias { get; set; } = "";

        /// <summary>Quebra por competição — só nas temporadas.</summary>
        public List<CompeticaoNaTemporada> Competicoes { get; set; } = new();
    }

    /// <summary>
    /// Soma das passagens da carreira (jogos, gols, assistências), com clube e seleção
    /// separados. Montado em <see cref="EstatisticasAvancadasViewModel.Totais"/>.
    /// </summary>
    public class TotaisCarreira
    {
        public int Jogos { get; private set; }
        public int Gols { get; private set; }
        public int Assistencias { get; private set; }

        public int JogosSelecao { get; private set; }
        public int GolsSelecao { get; private set; }
        public int AssistenciasSelecao { get; private set; }

        public int JogosGeral => Jogos + JogosSelecao;
        public int GolsGeral => Gols + GolsSelecao;
        public int AssistenciasGeral => Assistencias + AssistenciasSelecao;

        /// <summary>Participação direta em gol: gols + assistências.</summary>
        public int ParticipacoesGeral => GolsGeral + AssistenciasGeral;

        public bool TemSelecao => JogosSelecao > 0 || GolsSelecao > 0 || AssistenciasSelecao > 0;

        /// <summary>Sem nenhuma passagem com número não há o que totalizar; a tela omite o bloco.</summary>
        public bool TemDados => JogosGeral > 0 || GolsGeral > 0 || AssistenciasGeral > 0;

        public static TotaisCarreira De(IEnumerable<PassagemFotMob> carreira)
        {
            // Passagem curta costuma vir sem número nenhum (uma volta ao clube que durou
            // uma janela, por exemplo). Nesse caso a linha não soma nada, em vez de contar
            // como zero e fingir precisão que o dado não tem.
            static int Num(string? v) =>
                int.TryParse(v, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0;

            var t = new TotaisCarreira();
            foreach (var p in carreira)
            {
                if (p.Categoria == "Seleção")
                {
                    t.JogosSelecao += Num(p.Jogos);
                    t.GolsSelecao += Num(p.Gols);
                    t.AssistenciasSelecao += Num(p.Assistencias);
                }
                else
                {
                    t.Jogos += Num(p.Jogos);
                    t.Gols += Num(p.Gols);
                    t.Assistencias += Num(p.Assistencias);
                }
            }
            return t;
        }
    }

    /// <summary>Uma temporada e as competições disputadas nela.</summary>
    public class TemporadaAgrupada
    {
        /// <summary>"2025/2026" ou "2025", conforme a liga.</summary>
        public string Nome { get; set; } = "";
        public List<CompeticaoTemporada> Competicoes { get; set; } = new();
    }

    public class CompeticaoTemporada
    {
        /// <summary>
        /// O entryId ("1-1"), que é o que a fonte aceita para pedir as estatísticas —
        /// não o ano nem o id da liga.
        /// </summary>
        public string Id { get; set; } = "";
        public string Nome { get; set; } = "";

        /// <summary>
        /// False quando só existe o básico daquela competição. A tela marca essas para
        /// o usuário não clicar esperando o quadro completo e achar que quebrou.
        /// </summary>
        public bool TemDadosDetalhados { get; set; }
    }

    public class GrupoMetricasFotMob
    {
        public string Nome { get; set; } = "";
        public List<MetricaFotMob> Metricas { get; set; } = new();
    }

    public class MetricaFotMob
    {
        public string Nome { get; set; } = "";
        public string Valor { get; set; } = "";

        /// <summary>
        /// A métrica já é uma taxa (precisão de passe, duelos vencidos %). Nesse caso o
        /// valor sai com "%" e sem média por 90 — o FotMob repete a própria taxa no
        /// campo per90, e exibi-la como "84,8 por 90 minutos" seria absurdo.
        /// </summary>
        public bool EhPercentual { get; set; }

        /// <summary>
        /// Média por 90 minutos, para comparar quem jogou pouco com quem jogou muito.
        /// Null nas métricas percentuais.
        /// </summary>
        public double? Por90 { get; set; }

        /// <summary>
        /// Posição do jogador nessa métrica dentro da competição, de 0 a 100. É o dado
        /// mais valioso da tela e o único que não teríamos como calcular — depende de
        /// ter a liga inteira. Cálculo do FotMob, e a tela diz isso.
        /// </summary>
        public double? Percentil { get; set; }
    }

    /// <summary>
    /// Um jogo na base do FotMob. A lista é maior que o "Histórico por Jogo" da página
    /// do jogador porque inclui partidas que ninguém importou (seleção, outras
    /// competições) — a tela rotula a origem para que a diferença de contagem não passe
    /// por bug.
    ///
    /// Sem a nota do FotMob de propósito: a nota do site é a nota do site.
    /// </summary>
    public class JogoFotMob
    {
        public DateTime? Data { get; set; }
        public string? Competicao { get; set; }
        public string? Adversario { get; set; }

        /// <summary>Id do adversário, só para montar o escudo (/MediaProxy/Escudo/{id}).</summary>
        public long? AdversarioId { get; set; }
        public bool Mandante { get; set; }
        public string? Placar { get; set; }
        public int? Minutos { get; set; }
        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public int CartoesAmarelos { get; set; }
        public int CartoesVermelhos { get; set; }
    }

    public class PassagemFotMob
    {
        public string Categoria { get; set; } = "";
        public string Time { get; set; } = "";

        /// <summary>Id do clube/seleção, só para montar o escudo.</summary>
        public long? TimeId { get; set; }
        public string Periodo { get; set; } = "";
        public string? Jogos { get; set; }
        public string? Gols { get; set; }
        public string? Assistencias { get; set; }
        public bool Atual { get; set; }
    }

    public class TituloFotMob
    {
        public string Time { get; set; } = "";

        /// <summary>Id do clube/seleção, só para montar o escudo.</summary>
        public long? TimeId { get; set; }
        public string Competicao { get; set; } = "";
        public string Temporadas { get; set; } = "";
        public int Quantidade { get; set; }
    }
}
