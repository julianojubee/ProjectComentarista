package com.comentarista.futebol.ui.relatorios

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.comentarista.futebol.data.remote.dto.JogadorValorDto
import com.comentarista.futebol.data.remote.dto.RankingNotaDto
import com.comentarista.futebol.data.remote.dto.RelatorioResumoDto
import com.comentarista.futebol.data.remote.dto.TimeEstatisticaDto
import com.comentarista.futebol.ui.common.NetworkImage

@Composable
fun RelatoriosScreen(viewModel: RelatoriosViewModel = hiltViewModel()) {
    val uiState by viewModel.uiState.collectAsState()

    Box(modifier = Modifier.fillMaxSize()) {
        when {
            uiState.carregando && uiState.resumo == null ->
                CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))

            uiState.erro != null -> Column(
                modifier = Modifier.align(Alignment.Center).padding(24.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Text(text = uiState.erro.orEmpty(), color = MaterialTheme.colorScheme.error)
                TextButton(onClick = viewModel::carregar) { Text("Tentar de novo") }
            }

            uiState.resumo != null -> RelatorioConteudo(
                resumo = uiState.resumo!!,
                temporadaSelecionada = uiState.temporada,
                competicaoSelecionada = uiState.competicaoId,
                incluirNaoAnalisados = uiState.incluirNaoAnalisados,
                atualizando = uiState.carregando,
                onTemporada = viewModel::onTemporadaSelecionada,
                onCompeticao = viewModel::onCompeticaoSelecionada,
                onIncluirNaoAnalisados = viewModel::onIncluirNaoAnalisadosChange
            )
        }
    }
}

@Composable
private fun RelatorioConteudo(
    resumo: RelatorioResumoDto,
    temporadaSelecionada: Int?,
    competicaoSelecionada: Int?,
    incluirNaoAnalisados: Boolean,
    atualizando: Boolean,
    onTemporada: (Int?) -> Unit,
    onCompeticao: (Int?) -> Unit,
    onIncluirNaoAnalisados: (Boolean) -> Unit
) {
    LazyColumn(
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item {
            CompeticoesFiltroRow(
                competicoes = resumo.competicoes,
                competicaoSelecionada = competicaoSelecionada,
                onCompeticao = onCompeticao
            )
        }

        item {
            FiltrosRow(
                temporadas = resumo.temporadasDisponiveis,
                temporadaSelecionada = temporadaSelecionada,
                incluirNaoAnalisados = incluirNaoAnalisados,
                atualizando = atualizando,
                onTemporada = onTemporada,
                onIncluirNaoAnalisados = onIncluirNaoAnalisados
            )
        }

        item { TotaisRow(resumo) }

        if (resumo.rankingNotas.isNotEmpty()) {
            item { SectionHeader("Melhores notas") }
            itemsIndexed(resumo.rankingNotas, key = { _, r -> "nota${r.jogadorId}" }) { i, r ->
                RankingNotaRow(posicaoRanking = i + 1, item = r)
            }
        }

        rankingJogadorValor("Artilheiros", "art", resumo.artilheiros)
        rankingJogadorValor("Assistências", "assis", resumo.assistencias)
        rankingJogadorValor("Mais partidas", "part", resumo.maisPartidas)

        rankingTimes("Times — aproveitamento", "aprov", resumo.timesAproveitamento) { "${it.aproveitamento}%" }
        rankingTimes("Times — mais pontos", "pontos", resumo.timesMaisPontos) { "${it.pontos} pts" }
        rankingTimes("Times — mais gols", "golspro", resumo.timesGols) { "${it.golsPro} gols" }

        if (resumo.mediasPorPosicao.isNotEmpty()) {
            item { SectionHeader("Nota média por posição") }
            itemsIndexed(resumo.mediasPorPosicao, key = { _, m -> "pos${m.posicao}" }) { _, m ->
                Card(modifier = Modifier.fillMaxWidth()) {
                    Row(
                        modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 10.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(m.posicao, modifier = Modifier.weight(1f))
                        Text(
                            "${m.media}",
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.primary
                        )
                        Text(
                            "  (${m.totalJogadores} jog.)",
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }
        }
    }
}

// Extensões do LazyListScope para repetir o padrão header + linhas sem aninhar listas.
private fun androidx.compose.foundation.lazy.LazyListScope.rankingJogadorValor(
    titulo: String,
    chave: String,
    itens: List<JogadorValorDto>
) {
    if (itens.isEmpty()) return
    item { SectionHeader(titulo) }
    itemsIndexed(itens, key = { _, j -> "$chave${j.jogadorId}" }) { i, j ->
        Card(modifier = Modifier.fillMaxWidth()) {
            Row(
                modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 10.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                PosicaoRanking(i + 1)
                NetworkImage(url = j.fotoUrl, size = 32.dp, circular = true)
                Spacer(modifier = Modifier.width(12.dp))
                Column(modifier = Modifier.weight(1f)) {
                    Text(j.nomeExibicao.ifBlank { j.nome }, style = MaterialTheme.typography.bodyLarge)
                    if (!j.timeNome.isNullOrBlank()) {
                        Text(
                            j.timeNome,
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
                Text("${j.valor}", fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.primary)
            }
        }
    }
}

private fun androidx.compose.foundation.lazy.LazyListScope.rankingTimes(
    titulo: String,
    chave: String,
    itens: List<TimeEstatisticaDto>,
    valor: (TimeEstatisticaDto) -> String
) {
    if (itens.isEmpty()) return
    item { SectionHeader(titulo) }
    itemsIndexed(itens, key = { _, t -> "$chave${t.timeId}" }) { i, t ->
        Card(modifier = Modifier.fillMaxWidth()) {
            Row(
                modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 10.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                PosicaoRanking(i + 1)
                NetworkImage(url = t.escudoUrl, size = 28.dp)
                Spacer(modifier = Modifier.width(12.dp))
                Column(modifier = Modifier.weight(1f)) {
                    Text(t.nome, style = MaterialTheme.typography.bodyLarge)
                    Text(
                        "${t.jogos} jogos · ${t.vitorias}V ${t.empates}E ${t.derrotas}D · saldo ${t.saldoGols}",
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                Text(valor(t), fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.primary)
            }
        }
    }
}

@Composable
private fun CompeticoesFiltroRow(
    competicoes: List<com.comentarista.futebol.data.remote.dto.CompeticaoRefDto>,
    competicaoSelecionada: Int?,
    onCompeticao: (Int?) -> Unit
) {
    if (competicoes.isEmpty()) return
    LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        item {
            FilterChip(
                selected = competicaoSelecionada == null,
                onClick = { onCompeticao(null) },
                label = { Text("Todas") }
            )
        }
        items(competicoes, key = { it.id }) { comp ->
            FilterChip(
                selected = competicaoSelecionada == comp.id,
                onClick = { onCompeticao(if (competicaoSelecionada == comp.id) null else comp.id) },
                label = { Text(comp.nome) }
            )
        }
    }
}

@Composable
private fun FiltrosRow(
    temporadas: List<Int>,
    temporadaSelecionada: Int?,
    incluirNaoAnalisados: Boolean,
    atualizando: Boolean,
    onTemporada: (Int?) -> Unit,
    onIncluirNaoAnalisados: (Boolean) -> Unit
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically
    ) {
        var menuAberto by remember { mutableStateOf(false) }
        Box {
            TextButton(onClick = { menuAberto = true }, enabled = temporadas.isNotEmpty()) {
                Text("Temporada: ${temporadaSelecionada ?: "—"}")
            }
            DropdownMenu(expanded = menuAberto, onDismissRequest = { menuAberto = false }) {
                temporadas.forEach { t ->
                    DropdownMenuItem(
                        text = { Text("$t") },
                        onClick = {
                            menuAberto = false
                            if (t != temporadaSelecionada) onTemporada(t)
                        }
                    )
                }
            }
        }

        Spacer(modifier = Modifier.weight(1f))

        Text("Incluir não analisados", style = MaterialTheme.typography.labelLarge)
        Spacer(modifier = Modifier.width(8.dp))
        Switch(checked = incluirNaoAnalisados, onCheckedChange = onIncluirNaoAnalisados)

        if (atualizando) {
            Spacer(modifier = Modifier.width(12.dp))
            CircularProgressIndicator(modifier = Modifier.width(20.dp))
        }
    }
}

@Composable
private fun TotaisRow(resumo: RelatorioResumoDto) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        TotalCard("Jogos", "${resumo.totalJogos}", Modifier.weight(1f))
        TotalCard("Gols", "${resumo.totalGols}", Modifier.weight(1f))
        TotalCard("Amarelos", "${resumo.totalCartaoAmarelo}", Modifier.weight(1f))
        TotalCard("Vermelhos", "${resumo.totalCartaoVermelho}", Modifier.weight(1f))
    }
}

@Composable
private fun TotalCard(rotulo: String, valor: String, modifier: Modifier = Modifier) {
    Card(modifier = modifier) {
        Column(
            modifier = Modifier.fillMaxWidth().padding(vertical = 12.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(valor, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
            Text(rotulo, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun SectionHeader(titulo: String) {
    Text(
        text = titulo,
        style = MaterialTheme.typography.titleMedium,
        fontWeight = FontWeight.Bold,
        modifier = Modifier.padding(top = 12.dp, bottom = 4.dp)
    )
}

@Composable
private fun PosicaoRanking(posicao: Int) {
    Text(
        text = "$posicao",
        style = MaterialTheme.typography.labelLarge,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier.widthIn(min = 24.dp)
    )
}

@Composable
private fun RankingNotaRow(posicaoRanking: Int, item: RankingNotaDto) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            PosicaoRanking(posicaoRanking)
            NetworkImage(url = item.fotoUrl, size = 36.dp, circular = true)
            Spacer(modifier = Modifier.width(12.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(item.nomeExibicao.ifBlank { item.nome }, style = MaterialTheme.typography.bodyLarge)
                Text(
                    listOfNotNull(item.posicao.ifBlank { null }, item.timeNome).joinToString(" · ") +
                        " · ${item.partidas}J ${item.vitorias}V ${item.empates}E ${item.derrotas}D",
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            NotaBadge(nota = item.notaFinal, corHex = item.notaColor, rotulo = item.notaLabel)
        }
    }
}

@Composable
private fun NotaBadge(nota: Double, corHex: String, rotulo: String) {
    // Cor vem do servidor como #rrggbb (mesma escala da web); cai para primary se inválida.
    val cor = remember(corHex) {
        runCatching { Color(android.graphics.Color.parseColor(corHex)) }
            .getOrDefault(Color.Unspecified)
    }.takeIf { it != Color.Unspecified } ?: MaterialTheme.colorScheme.primary

    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Surface(color = cor, shape = MaterialTheme.shapes.small) {
            Text(
                text = "$nota",
                color = Color.White,
                fontWeight = FontWeight.Bold,
                modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp)
            )
        }
        if (rotulo.isNotBlank()) {
            Text(
                rotulo,
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}
