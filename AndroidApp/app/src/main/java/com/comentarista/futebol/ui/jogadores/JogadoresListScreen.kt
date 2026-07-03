package com.comentarista.futebol.ui.jogadores

import androidx.compose.foundation.clickable
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
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.comentarista.futebol.R
import com.comentarista.futebol.data.remote.dto.JogadorResumoDto
import com.comentarista.futebol.ui.common.NetworkImage

@Composable
fun JogadoresListScreen(
    onJogadorClick: (Int) -> Unit,
    viewModel: JogadoresListViewModel = hiltViewModel()
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

            uiState.jogadores.isEmpty() -> Text(
                text = stringResource(R.string.jogadores_empty),
                modifier = Modifier.align(Alignment.Center).padding(24.dp)
            )

            else -> LazyColumn(contentPadding = PaddingValues(16.dp)) {
                items(uiState.jogadores, key = { it.id }) { jogador ->
                    JogadorCard(jogador, onClick = { onJogadorClick(jogador.id) })
                }
            }
        }
    }
}

@Composable
private fun JogadorCard(jogador: JogadorResumoDto, onClick: () -> Unit) {
    Card(modifier = Modifier.fillMaxWidth().padding(vertical = 6.dp).clickable(onClick = onClick)) {
        Row(modifier = Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
            NetworkImage(jogador.fotoUrl, size = 40.dp, circular = true, modifier = Modifier.padding(end = 12.dp))
            Column {
                Text(text = jogador.nomeExibicao, style = MaterialTheme.typography.bodyLarge)
                Text(
                    text = listOfNotNull(
                        jogador.posicao.ifBlank { null },
                        jogador.timeNome,
                        jogador.idade.takeIf { it > 0 }?.let { "$it anos" }
                    ).joinToString(" • "),
                    style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}
