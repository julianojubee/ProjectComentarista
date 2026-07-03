package com.comentarista.futebol.ui.jogos

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.comentarista.futebol.R
import com.comentarista.futebol.data.remote.dto.JogoResumoDto
import com.comentarista.futebol.ui.common.NetworkImage

// Pane de lista (usado dentro de JogosSection.kt). Sem Scaffold/TopBar próprios —
// isso já vem do MainScaffold que envolve todas as seções.
@Composable
fun JogosListScreen(
    onJogoClick: (Int) -> Unit,
    viewModel: JogosListViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()

    Box(modifier = Modifier.fillMaxSize()) {
        when {
            uiState.carregando -> CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))

            uiState.erro != null -> Text(
                text = uiState.erro.orEmpty(),
                modifier = Modifier.align(Alignment.Center).padding(24.dp),
                color = MaterialTheme.colorScheme.error
            )

            uiState.jogos.isEmpty() -> Text(
                text = stringResource(R.string.jogos_empty),
                modifier = Modifier.align(Alignment.Center).padding(24.dp)
            )

            else -> LazyColumn(contentPadding = PaddingValues(16.dp)) {
                items(uiState.jogos, key = { it.id }) { jogo ->
                    JogoCard(jogo, onClick = { onJogoClick(jogo.id) })
                }
            }
        }
    }
}

@Composable
private fun JogoCard(jogo: JogoResumoDto, onClick: () -> Unit) {
    Card(modifier = Modifier.fillMaxWidth().padding(vertical = 6.dp).clickable(onClick = onClick)) {
        Column(modifier = Modifier.padding(16.dp)) {
            jogo.competicaoNome?.let {
                Text(text = it, style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary)
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
