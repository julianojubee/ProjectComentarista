package com.comentarista.futebol.ui.jogadores

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
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.FilterChip
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.comentarista.futebol.data.remote.dto.JogadorDetalheDto
import com.comentarista.futebol.data.remote.dto.JogadorEstatisticasDto
import com.comentarista.futebol.data.remote.dto.JogadorJogoItemDto
import com.comentarista.futebol.ui.common.NetworkImage
import com.comentarista.futebol.ui.common.NotaChip

@Composable
fun JogadorDetailScreen(
    jogadorId: Int,
    viewModel: JogadorDetailViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()

    LaunchedEffect(jogadorId) {
        viewModel.carregar(jogadorId)
    }

    Box(modifier = Modifier.fillMaxSize()) {
        when {
            uiState.carregando -> CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
            uiState.erro != null -> Text(
                text = uiState.erro.orEmpty(),
                modifier = Modifier.align(Alignment.Center),
                color = MaterialTheme.colorScheme.error
            )
            uiState.jogador != null -> JogadorDetailConteudo(
                jogador = uiState.jogador!!,
                estatisticas = uiState.estatisticas,
                carregandoEstatisticas = uiState.carregandoEstatisticas,
                competicaoIdFiltro = uiState.competicaoIdFiltro,
                onFiltrarCompeticao = viewModel::filtrarPorCompeticao
            )
        }
    }
}

@Composable
private fun JogadorDetailConteudo(
    jogador: JogadorDetalheDto,
    estatisticas: JogadorEstatisticasDto?,
    carregandoEstatisticas: Boolean,
    competicaoIdFiltro: Int?,
    onFiltrarCompeticao: (Int?) -> Unit
) {
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(24.dp)
    ) {
        item {
            Row(verticalAlignment = Alignment.CenterVertically) {
                NetworkImage(jogador.fotoUrl, size = 64.dp, circular = true, modifier = Modifier.padding(end = 16.dp))
                Column {
                    Text(text = jogador.nomeExibicao, style = MaterialTheme.typography.titleLarge)
                    Text(
                        text = jogador.posicao,
                        style = MaterialTheme.typography.labelLarge,
                        color = MaterialTheme.colorScheme.primary
                    )
                }
            }

            HorizontalDivider(modifier = Modifier.padding(vertical = 16.dp))

            InfoLinha("Time", jogador.timeNome)
            InfoLinha("Seleção", jogador.selecaoNome)
            InfoLinha("Nacionalidade", jogador.nacionalidadeNome)
            if (jogador.idade > 0) InfoLinha("Idade", "${jogador.idade} anos")
            jogador.numeroCamisa?.let { InfoLinha("Camisa", "$it") }
            InfoLinha("Observações", jogador.observacoes)
        }

        // ── Estatísticas (mesma fonte da página /Jogadores/Estatisticas) ──
        item {
            HorizontalDivider(modifier = Modifier.padding(vertical = 16.dp))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(text = "Estatísticas", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                if (carregandoEstatisticas) {
                    Spacer(modifier = Modifier.width(12.dp))
                    CircularProgressIndicator(modifier = Modifier.width(18.dp))
                }
            }
        }

        if (estatisticas != null) {
            // Filtro por competição (quando o jogador tem mais de uma)
            if (estatisticas.competicoes.size > 1) {
                item {
                    LazyRow(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        modifier = Modifier.padding(vertical = 8.dp)
                    ) {
                        item {
                            FilterChip(
                                selected = competicaoIdFiltro == null,
                                onClick = { onFiltrarCompeticao(null) },
                                label = { Text("Todas") }
                            )
                        }
                        items(estatisticas.competicoes, key = { it.id }) { comp ->
                            FilterChip(
                                selected = competicaoIdFiltro == comp.id,
                                onClick = { onFiltrarCompeticao(comp.id) },
                                label = { Text(comp.nome) }
                            )
                        }
                    }
                }
            }

            item { TotaisJogador(estatisticas) }

            if (estatisticas.jogos.isNotEmpty()) {
                item {
                    Text(
                        text = "Histórico de jogos",
                        style = MaterialTheme.typography.labelLarge,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(top = 12.dp, bottom = 4.dp)
                    )
                }
                items(estatisticas.jogos, key = { it.jogoId }) { jogo ->
                    JogoDoJogadorLinha(jogo)
                }
            }
        }
    }
}

@Composable
private fun TotaisJogador(est: JogadorEstatisticasDto) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        TotalCard("Jogos", "${est.partidas}", Modifier.weight(1f))
        TotalCard("Gols", "${est.gols}", Modifier.weight(1f))
        TotalCard("Assist.", "${est.assistencias}", Modifier.weight(1f))
        TotalCard("V-E-D", "${est.vitorias}-${est.empates}-${est.derrotas}", Modifier.weight(1f))
        TotalCard("Nota", est.notaMedia?.let { "$it" } ?: "—", Modifier.weight(1f), destaque = true)
    }
}

@Composable
private fun TotalCard(rotulo: String, valor: String, modifier: Modifier = Modifier, destaque: Boolean = false) {
    Card(modifier = modifier) {
        Column(
            modifier = Modifier.fillMaxWidth().padding(vertical = 10.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(
                text = valor,
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Bold,
                color = if (destaque) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurface
            )
            Text(rotulo, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun JogoDoJogadorLinha(jogo: JogadorJogoItemDto) {
    Card(modifier = Modifier.fillMaxWidth().padding(vertical = 3.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            ResultadoBadge(jogo.resultado)
            Spacer(modifier = Modifier.width(10.dp))
            NetworkImage(jogo.adversarioEscudoUrl, size = 24.dp)
            Spacer(modifier = Modifier.width(8.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = "${if (jogo.isCasa) "vs" else "@"} ${jogo.adversarioNome}  ${jogo.golsPro}x${jogo.golsContra}",
                    style = MaterialTheme.typography.bodyMedium
                )
                val detalhes = buildList {
                    jogo.data?.take(10)?.let { add(it) }
                    jogo.posicao?.let { add(it) }
                    jogo.minutos?.let { add("${it}min") }
                    if (jogo.gols > 0) add("⚽${jogo.gols}")
                    if (jogo.assistencias > 0) add("🅰${jogo.assistencias}")
                    if (jogo.cartoesAmarelos > 0) add("🟨${jogo.cartoesAmarelos}")
                    if (jogo.cartoesVermelhos > 0) add("🟥${jogo.cartoesVermelhos}")
                }
                Text(
                    text = detalhes.joinToString(" · "),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            if (jogo.analisado && jogo.notaFinal != null) {
                NotaChip(jogo.notaFinal)
            } else {
                Text(
                    text = "—",
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(horizontal = 8.dp)
                )
            }
        }
    }
}

@Composable
private fun ResultadoBadge(resultado: String) {
    val cor = when (resultado) {
        "V" -> Color(0xFF22C55E)
        "D" -> Color(0xFFEF4444)
        "E" -> Color(0xFF6B7280)
        else -> MaterialTheme.colorScheme.surfaceVariant
    }
    Surface(color = cor, shape = MaterialTheme.shapes.small) {
        Text(
            text = resultado,
            color = Color.White,
            style = MaterialTheme.typography.labelMedium,
            fontWeight = FontWeight.Bold,
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 2.dp)
        )
    }
}

@Composable
private fun InfoLinha(rotulo: String, valor: String?) {
    if (valor.isNullOrBlank()) return
    Row(
        modifier = Modifier.fillMaxWidth().padding(vertical = 6.dp),
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        Text(text = rotulo, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(text = valor)
    }
}
