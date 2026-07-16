package com.comentarista.futebol.ui.jogos

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowLeft
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.comentarista.futebol.R
import com.comentarista.futebol.data.remote.dto.JogoResumoDto
import com.comentarista.futebol.ui.common.NetworkImage
import java.time.format.DateTimeFormatter

// Pane de lista (usado dentro de JogosSection.kt). Sem Scaffold/TopBar próprios —
// isso já vem do MainScaffold que envolve todas as seções.
@Composable
fun JogosListScreen(
    onJogoClick: (Int) -> Unit,
    viewModel: JogosListViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()

    Column(modifier = Modifier.fillMaxSize()) {
        FiltroDiaRow(
            uiState = uiState,
            onTodos = viewModel::mostrarTodos,
            onHoje = viewModel::mostrarHoje,
            onMudarDia = viewModel::mudarDia
        )

        Box(modifier = Modifier.fillMaxSize()) {
            when {
                uiState.carregando -> CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))

                uiState.erro != null -> Text(
                    text = uiState.erro.orEmpty(),
                    modifier = Modifier.align(Alignment.Center).padding(24.dp),
                    color = MaterialTheme.colorScheme.error
                )

                uiState.jogos.isEmpty() -> Text(
                    text = if (uiState.dia != null) "Nenhum jogo neste dia." else stringResource(R.string.jogos_empty),
                    modifier = Modifier.align(Alignment.Center).padding(24.dp)
                )

                else -> LazyColumn(contentPadding = PaddingValues(start = 16.dp, end = 16.dp, bottom = 16.dp)) {
                    items(uiState.jogos, key = { it.id }) { jogo ->
                        JogoCard(jogo, onClick = { onJogoClick(jogo.id) })
                    }
                }
            }
        }
    }
}

@Composable
private fun FiltroDiaRow(
    uiState: JogosListUiState,
    onTodos: () -> Unit,
    onHoje: () -> Unit,
    onMudarDia: (Long) -> Unit
) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        FilterChip(
            selected = uiState.dia != null,
            onClick = onHoje,
            label = { Text("Hoje") }
        )
        Spacer(modifier = Modifier.padding(start = 8.dp))
        FilterChip(
            selected = uiState.dia == null,
            onClick = onTodos,
            label = { Text("Todos") }
        )

        Spacer(modifier = Modifier.weight(1f))

        if (uiState.dia != null) {
            IconButton(onClick = { onMudarDia(-1) }) {
                Icon(Icons.AutoMirrored.Filled.KeyboardArrowLeft, contentDescription = "Dia anterior")
            }
            Text(
                text = if (uiState.ehHoje) "Hoje"
                       else uiState.dia.format(DateTimeFormatter.ofPattern("dd/MM")),
                style = MaterialTheme.typography.labelLarge,
                fontWeight = FontWeight.Bold
            )
            IconButton(onClick = { onMudarDia(1) }) {
                Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, contentDescription = "Dia seguinte")
            }
        }
    }
}

@Composable
private fun JogoCard(jogo: JogoResumoDto, onClick: () -> Unit) {
    Card(modifier = Modifier.fillMaxWidth().padding(vertical = 6.dp).clickable(onClick = onClick)) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                jogo.competicaoNome?.let {
                    Text(text = it, style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary)
                }
                Spacer(modifier = Modifier.weight(1f))
                if (jogo.analisadoPorMim) {
                    // Mesma informação do painel /Jogos/Hoje da web
                    Icon(
                        Icons.Filled.CheckCircle,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.secondary,
                        modifier = Modifier.padding(end = 4.dp)
                    )
                    Text(
                        text = "Analisado",
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.secondary
                    )
                }
            }
            Row(
                modifier = Modifier.fillMaxWidth().padding(top = 4.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(modifier = Modifier.weight(1f), verticalAlignment = Alignment.CenterVertically) {
                    NetworkImage(jogo.timeCasaEscudoUrl)
                    Text(
                        text = jogo.timeCasaNome,
                        style = MaterialTheme.typography.bodyLarge,
                        modifier = Modifier.padding(start = 8.dp)
                    )
                }
                Text(
                    text = placarTexto(jogo),
                    style = MaterialTheme.typography.titleLarge,
                    modifier = Modifier.padding(horizontal = 12.dp)
                )
                Row(
                    modifier = Modifier.weight(1f),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.End
                ) {
                    Text(
                        text = jogo.timeVisitanteNome,
                        style = MaterialTheme.typography.bodyLarge,
                        textAlign = TextAlign.End,
                        modifier = Modifier.padding(end = 8.dp)
                    )
                    NetworkImage(jogo.timeVisitanteEscudoUrl)
                }
            }
        }
    }
}

private fun placarTexto(jogo: JogoResumoDto): String {
    val casa = jogo.placarCasa
    val visitante = jogo.placarVisitante
    return if (casa != null && visitante != null) "$casa x $visitante" else "x"
}
