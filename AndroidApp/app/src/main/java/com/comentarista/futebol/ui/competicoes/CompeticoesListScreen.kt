package com.comentarista.futebol.ui.competicoes

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.PaddingValues
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
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.comentarista.futebol.data.remote.dto.CompeticaoDto

@Composable
fun CompeticoesListScreen(viewModel: CompeticoesViewModel = hiltViewModel()) {
    val uiState by viewModel.uiState.collectAsState()

    Box(modifier = Modifier.fillMaxSize()) {
        when {
            uiState.carregando -> CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))

            uiState.erro != null -> Text(
                text = uiState.erro.orEmpty(),
                modifier = Modifier.align(Alignment.Center).padding(24.dp),
                color = MaterialTheme.colorScheme.error
            )

            uiState.competicoes.isEmpty() -> Text(
                text = "Nenhuma competição encontrada.",
                modifier = Modifier.align(Alignment.Center).padding(24.dp)
            )

            else -> LazyColumn(contentPadding = PaddingValues(16.dp)) {
                items(uiState.competicoes, key = { it.id }) { competicao ->
                    CompeticaoCard(competicao)
                }
            }
        }
    }
}

@Composable
private fun CompeticaoCard(competicao: CompeticaoDto) {
    Card(modifier = Modifier.fillMaxWidth().padding(vertical = 6.dp)) {
        Text(text = competicao.nome, style = MaterialTheme.typography.bodyLarge, modifier = Modifier.padding(16.dp))
        Text(
            text = competicao.regiao,
            style = MaterialTheme.typography.labelLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(start = 16.dp, bottom = 16.dp)
        )
    }
}
