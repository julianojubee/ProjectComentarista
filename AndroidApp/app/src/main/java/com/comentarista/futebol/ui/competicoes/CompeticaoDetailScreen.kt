package com.comentarista.futebol.ui.competicoes

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
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.comentarista.futebol.data.remote.dto.ArtilheiroDto
import com.comentarista.futebol.data.remote.dto.ClassificacaoLinhaDto
import com.comentarista.futebol.data.remote.dto.JogoResumoDto
import com.comentarista.futebol.ui.common.NetworkImage

// Pane de detalhe da aba Competições: classificação + jogos da competição.
@Composable
fun CompeticaoDetailScreen(
    competicaoId: Int,
    competicaoNome: String?,
    viewModel: CompeticaoDetailViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    var abaSelecionada by remember { mutableIntStateOf(0) }

    LaunchedEffect(competicaoId) {
        viewModel.carregar(competicaoId)
    }

    Column(modifier = Modifier.fillMaxSize()) {
        Text(
            text = uiState.classificacao?.competicaoNome ?: competicaoNome ?: "Competição",
            style = MaterialTheme.typography.titleMedium,
            fontWeight = FontWeight.Bold,
            modifier = Modifier.padding(start = 16.dp, top = 12.dp, end = 16.dp)
        )

        TabRow(selectedTabIndex = abaSelecionada) {
            Tab(selected = abaSelecionada == 0, onClick = { abaSelecionada = 0 }, text = { Text("Classificação") })
            Tab(selected = abaSelecionada == 1, onClick = { abaSelecionada = 1 }, text = { Text("Jogos") })
        }

        Box(modifier = Modifier.fillMaxSize()) {
            when {
                uiState.carregando && uiState.classificacao == null ->
                    CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))

                uiState.erro != null -> Text(
                    text = uiState.erro.orEmpty(),
                    modifier = Modifier.align(Alignment.Center).padding(24.dp),
                    color = MaterialTheme.colorScheme.error
                )

                abaSelecionada == 0 -> ClassificacaoTab(uiState, viewModel::onTemporadaSelecionada)

                else -> JogosTab(uiState.jogos)
            }
        }
    }
}

@Composable
private fun ClassificacaoTab(uiState: CompeticaoDetailUiState, onTemporada: (Int) -> Unit) {
    val classificacao = uiState.classificacao ?: return

    LazyColumn(contentPadding = PaddingValues(16.dp)) {
        // Seletor de temporada
        if (classificacao.temporadasDisponiveis.size > 1) {
            item {
                LazyRow(
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    modifier = Modifier.padding(bottom = 8.dp)
                ) {
                    items(classificacao.temporadasDisponiveis, key = { it }) { t ->
                        FilterChip(
                            selected = uiState.temporada == t,
                            onClick = { onTemporada(t) },
                            label = { Text("$t") }
                        )
                    }
                }
            }
        }

        if (classificacao.tabela.isEmpty()) {
            item {
                Text(
                    text = "Sem jogos com placar nesta temporada.",
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(vertical = 24.dp)
                )
            }
        } else {
            item { CabecalhoTabela() }
            items(classificacao.tabela, key = { it.timeId }) { linha ->
                LinhaTabela(linha)
            }
        }

        if (classificacao.artilheiros.isNotEmpty()) {
            item {
                HorizontalDivider(modifier = Modifier.padding(vertical = 16.dp))
                Text(
                    text = "Artilheiros",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier.padding(bottom = 4.dp)
                )
            }
            items(classificacao.artilheiros, key = { it.jogadorId }) { a ->
                ArtilheiroLinha(a)
            }
        }
    }
}

@Composable
private fun CabecalhoTabela() {
    Row(
        modifier = Modifier.fillMaxWidth().padding(vertical = 6.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text("#", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.width(28.dp))
        Text("Time", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.weight(1f))
        NumeroColuna("P", cabecalho = true)
        NumeroColuna("J", cabecalho = true)
        NumeroColuna("V", cabecalho = true)
        NumeroColuna("E", cabecalho = true)
        NumeroColuna("D", cabecalho = true)
        NumeroColuna("SG", cabecalho = true, larga = true)
    }
}

@Composable
private fun LinhaTabela(linha: ClassificacaoLinhaDto) {
    Card(modifier = Modifier.fillMaxWidth().padding(vertical = 2.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 8.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = "${linha.posicao}",
                style = MaterialTheme.typography.labelLarge,
                fontWeight = FontWeight.Bold,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.width(28.dp)
            )
            Row(modifier = Modifier.weight(1f), verticalAlignment = Alignment.CenterVertically) {
                NetworkImage(linha.escudoUrl, size = 20.dp)
                Spacer(modifier = Modifier.width(6.dp))
                Text(text = linha.timeNome, style = MaterialTheme.typography.bodyMedium, maxLines = 1)
            }
            NumeroColuna("${linha.pontos}", destaque = true)
            NumeroColuna("${linha.jogos}")
            NumeroColuna("${linha.vitorias}")
            NumeroColuna("${linha.empates}")
            NumeroColuna("${linha.derrotas}")
            NumeroColuna(if (linha.saldoGols > 0) "+${linha.saldoGols}" else "${linha.saldoGols}", larga = true)
        }
    }
}

@Composable
private fun NumeroColuna(texto: String, cabecalho: Boolean = false, destaque: Boolean = false, larga: Boolean = false) {
    Text(
        text = texto,
        style = if (cabecalho) MaterialTheme.typography.labelSmall else MaterialTheme.typography.bodySmall,
        fontWeight = if (destaque) FontWeight.Bold else FontWeight.Normal,
        color = when {
            cabecalho -> MaterialTheme.colorScheme.onSurfaceVariant
            destaque -> MaterialTheme.colorScheme.primary
            else -> MaterialTheme.colorScheme.onSurface
        },
        textAlign = TextAlign.Center,
        modifier = Modifier.width(if (larga) 36.dp else 28.dp)
    )
}

@Composable
private fun ArtilheiroLinha(a: ArtilheiroDto) {
    Card(modifier = Modifier.fillMaxWidth().padding(vertical = 3.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            NetworkImage(a.fotoUrl, size = 32.dp, circular = true)
            Spacer(modifier = Modifier.width(10.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(text = a.nome, style = MaterialTheme.typography.bodyMedium)
                a.timeNome?.let {
                    Text(text = it, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
            Text(
                text = "${a.gols} gols",
                fontWeight = FontWeight.Bold,
                color = MaterialTheme.colorScheme.primary
            )
        }
    }
}

@Composable
private fun JogosTab(jogos: List<JogoResumoDto>) {
    if (jogos.isEmpty()) {
        Box(modifier = Modifier.fillMaxSize()) {
            Text(
                text = "Nenhum jogo nesta competição.",
                modifier = Modifier.align(Alignment.Center).padding(24.dp)
            )
        }
        return
    }
    LazyColumn(contentPadding = PaddingValues(16.dp)) {
        items(jogos, key = { it.id }) { jogo ->
            JogoDaCompeticaoCard(jogo)
        }
    }
}

@Composable
private fun JogoDaCompeticaoCard(jogo: JogoResumoDto) {
    Card(modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp)) {
        Column(modifier = Modifier.padding(horizontal = 16.dp, vertical = 10.dp)) {
            jogo.data?.take(10)?.let {
                Text(text = it, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            Row(
                modifier = Modifier.fillMaxWidth().padding(top = 2.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(modifier = Modifier.weight(1f), verticalAlignment = Alignment.CenterVertically) {
                    NetworkImage(jogo.timeCasaEscudoUrl, size = 22.dp)
                    Text(
                        text = jogo.timeCasaNome,
                        style = MaterialTheme.typography.bodyMedium,
                        modifier = Modifier.padding(start = 6.dp)
                    )
                }
                Text(
                    text = if (jogo.placarCasa != null && jogo.placarVisitante != null)
                        "${jogo.placarCasa} x ${jogo.placarVisitante}" else "x",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier.padding(horizontal = 10.dp)
                )
                Row(
                    modifier = Modifier.weight(1f),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.End
                ) {
                    Text(
                        text = jogo.timeVisitanteNome,
                        style = MaterialTheme.typography.bodyMedium,
                        textAlign = TextAlign.End,
                        modifier = Modifier.padding(end = 6.dp)
                    )
                    NetworkImage(jogo.timeVisitanteEscudoUrl, size = 22.dp)
                }
            }
        }
    }
}
