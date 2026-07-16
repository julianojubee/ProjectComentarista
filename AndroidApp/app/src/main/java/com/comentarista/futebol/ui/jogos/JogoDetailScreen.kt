package com.comentarista.futebol.ui.jogos

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.RateReview
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
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
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.comentarista.futebol.data.remote.dto.EscalacaoJogadorDto
import com.comentarista.futebol.data.remote.dto.JogoDetalheDto
import com.comentarista.futebol.ui.common.NetworkImage

@Composable
fun JogoDetailScreen(
    jogoId: Int,
    onAnalisarClick: () -> Unit = {},
    viewModel: JogoDetailViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()

    LaunchedEffect(jogoId) {
        viewModel.carregar(jogoId)
    }

    Box(modifier = Modifier.fillMaxSize()) {
        when {
            uiState.carregando -> CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
            uiState.erro != null -> Text(
                text = uiState.erro.orEmpty(),
                modifier = Modifier.align(Alignment.Center),
                color = MaterialTheme.colorScheme.error
            )
            uiState.jogo != null -> JogoDetailConteudo(uiState.jogo!!, onAnalisarClick)
        }
    }
}

@Composable
private fun JogoDetailConteudo(jogo: JogoDetalheDto, onAnalisarClick: () -> Unit) {
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = androidx.compose.foundation.layout.PaddingValues(24.dp)
    ) {
        item {
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                jogo.competicaoNome?.let {
                    Text(text = it, style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary)
                }
                Button(onClick = onAnalisarClick) {
                    Icon(Icons.Filled.RateReview, contentDescription = null, modifier = Modifier.padding(end = 6.dp))
                    Text("Analisar jogo")
                }
            }
            PlacarHeader(jogo)
            HorizontalDivider(modifier = Modifier.padding(vertical = 16.dp))
            InfoLinha("Status", jogo.status)
            InfoLinha("Data", jogo.data)
            InfoLinha("Estádio", jogo.estadio)
            InfoLinha("Árbitro", jogo.arbitro)
            if (jogo.penaltisCasa != null || jogo.penaltisVisitante != null) {
                InfoLinha("Pênaltis", "${jogo.penaltisCasa ?: 0} x ${jogo.penaltisVisitante ?: 0}")
            }
        }

        // ── Gols ──────────────────────────────────────────────────────────
        if (jogo.gols.isNotEmpty()) {
            item { SecaoTitulo("Gols") }
            item {
                Column {
                    jogo.gols.forEach { g ->
                        Row(modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp)) {
                            Text("${g.minuto}'", color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.width(44.dp))
                            Text("⚽ ${g.jogadorNome}${if (g.contra) " (contra)" else ""}")
                        }
                    }
                }
            }
        }

        // ── Cartões ───────────────────────────────────────────────────────
        if (jogo.cartoes.isNotEmpty()) {
            item { SecaoTitulo("Cartões") }
            item {
                Column {
                    jogo.cartoes.forEach { c ->
                        Row(modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp)) {
                            Text("${c.minuto}'", color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.width(44.dp))
                            Text("${if (c.tipo == "Vermelho") "🟥" else "🟨"} ${c.jogadorNome}")
                        }
                    }
                }
            }
        }

        // ── Estatísticas da partida (casa x visitante) ────────────────────
        if (jogo.estatisticasTimes.isNotEmpty()) {
            item { SecaoTitulo("Estatísticas da partida") }
            item {
                Column {
                    jogo.estatisticasTimes.forEach { s ->
                        Row(
                            modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text(
                                text = s.valorCasa ?: "-",
                                fontWeight = FontWeight.Bold,
                                modifier = Modifier.width(60.dp)
                            )
                            Text(
                                text = nomeEstatistica(s.nome),
                                textAlign = TextAlign.Center,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                modifier = Modifier.weight(1f)
                            )
                            Text(
                                text = s.valorVisitante ?: "-",
                                fontWeight = FontWeight.Bold,
                                textAlign = TextAlign.End,
                                modifier = Modifier.width(60.dp)
                            )
                        }
                    }
                }
            }
        }

        // ── Escalações lado a lado ────────────────────────────────────────
        if (jogo.escalacaoCasa.isNotEmpty() || jogo.escalacaoVisitante.isNotEmpty()) {
            item { SecaoTitulo("Escalações") }
            item {
                Row(modifier = Modifier.fillMaxWidth()) {
                    EscalacaoColuna(
                        titulo = jogo.timeCasaNome,
                        jogadores = jogo.escalacaoCasa,
                        modifier = Modifier.weight(1f)
                    )
                    Spacer(modifier = Modifier.width(16.dp))
                    EscalacaoColuna(
                        titulo = jogo.timeVisitanteNome,
                        jogadores = jogo.escalacaoVisitante,
                        modifier = Modifier.weight(1f)
                    )
                }
            }
        }
    }
}

@Composable
private fun EscalacaoColuna(titulo: String, jogadores: List<EscalacaoJogadorDto>, modifier: Modifier = Modifier) {
    val titulares = jogadores.filter { it.titular }
    val reservas = jogadores.filter { !it.titular }

    Column(modifier = modifier) {
        Text(text = titulo, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)

        titulares.forEach { EscalacaoLinha(it) }

        if (reservas.isNotEmpty()) {
            Text(
                text = "Reservas",
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 8.dp, bottom = 2.dp)
            )
            reservas.forEach { EscalacaoLinha(it) }
        }
    }
}

@Composable
private fun EscalacaoLinha(jogador: EscalacaoJogadorDto) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(vertical = 3.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        NetworkImage(jogador.fotoUrl, size = 24.dp, circular = true)
        Spacer(modifier = Modifier.width(8.dp))
        Column {
            Text(text = jogador.nome, style = MaterialTheme.typography.bodyMedium)
            if (!jogador.posicao.isNullOrBlank()) {
                Text(
                    text = jogador.posicao,
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

@Composable
private fun SecaoTitulo(titulo: String) {
    Column {
        HorizontalDivider(modifier = Modifier.padding(vertical = 16.dp))
        Text(text = titulo, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
        Spacer(modifier = Modifier.padding(bottom = 4.dp))
    }
}

@Composable
private fun PlacarHeader(jogo: JogoDetalheDto) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(top = 12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        TimeComEscudo(
            nome = jogo.timeCasaNome,
            escudoUrl = jogo.timeCasaEscudoUrl,
            modifier = Modifier.weight(1f)
        )
        Text(
            text = placarTexto(jogo.placarCasa, jogo.placarVisitante),
            style = MaterialTheme.typography.titleLarge,
            modifier = Modifier.padding(horizontal = 16.dp)
        )
        TimeComEscudo(
            nome = jogo.timeVisitanteNome,
            escudoUrl = jogo.timeVisitanteEscudoUrl,
            alinhamentoDireita = true,
            modifier = Modifier.weight(1f)
        )
    }
    if (jogo.analisadoPorMim) {
        Surface(
            color = MaterialTheme.colorScheme.secondary,
            shape = MaterialTheme.shapes.small,
            modifier = Modifier.padding(top = 8.dp)
        ) {
            Text(
                text = "Analisado",
                color = Color.White,
                style = MaterialTheme.typography.labelMedium,
                modifier = Modifier.padding(horizontal = 8.dp, vertical = 2.dp)
            )
        }
    }
}

@Composable
private fun TimeComEscudo(
    nome: String,
    escudoUrl: String?,
    alinhamentoDireita: Boolean = false,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier,
        horizontalArrangement = if (alinhamentoDireita) Arrangement.End else Arrangement.Start,
        verticalAlignment = Alignment.CenterVertically
    ) {
        if (alinhamentoDireita) {
            Text(text = nome, style = MaterialTheme.typography.titleLarge, textAlign = TextAlign.End)
            NetworkImage(escudoUrl, size = 40.dp, modifier = Modifier.padding(start = 8.dp))
        } else {
            NetworkImage(escudoUrl, size = 40.dp, modifier = Modifier.padding(end = 8.dp))
            Text(text = nome, style = MaterialTheme.typography.titleLarge)
        }
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

private fun placarTexto(casa: Int?, visitante: Int?): String =
    if (casa != null && visitante != null) "$casa x $visitante" else "x"

// Tradução das chaves da api-football exibidas na web em inglês
private fun nomeEstatistica(chave: String): String = when (chave) {
    "Ball Possession" -> "Posse de bola"
    "Total Shots" -> "Finalizações"
    "Shots on Goal" -> "Finalizações no gol"
    "Shots off Goal" -> "Finalizações para fora"
    "Blocked Shots" -> "Finalizações bloqueadas"
    "Shots insidebox" -> "Finalizações na área"
    "Shots outsidebox" -> "De fora da área"
    "Corner Kicks" -> "Escanteios"
    "Offsides" -> "Impedimentos"
    "Fouls" -> "Faltas"
    "Yellow Cards" -> "Cartões amarelos"
    "Red Cards" -> "Cartões vermelhos"
    "Goalkeeper Saves" -> "Defesas do goleiro"
    "Total passes" -> "Passes"
    "Passes accurate" -> "Passes certos"
    "Passes %" -> "Precisão de passes"
    "expected_goals" -> "Gols esperados (xG)"
    else -> chave
}
