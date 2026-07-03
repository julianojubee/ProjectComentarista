package com.comentarista.futebol.ui.jogadores

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.comentarista.futebol.data.remote.dto.JogadorDetalheDto
import com.comentarista.futebol.ui.common.NetworkImage

@Composable
fun JogadorDetailScreen(
    jogadorId: Int,
    viewModel: JogadorDetailViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()

    LaunchedEffect(jogadorId) {
        viewModel.carregar(jogadorId)
    }

    Box(modifier = Modifier.fillMaxSize().padding(24.dp)) {
        when {
            uiState.carregando -> CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
            uiState.erro != null -> Text(
                text = uiState.erro.orEmpty(),
                modifier = Modifier.align(Alignment.Center),
                color = MaterialTheme.colorScheme.error
            )
            uiState.jogador != null -> JogadorDetailConteudo(uiState.jogador!!)
        }
    }
}

@Composable
private fun JogadorDetailConteudo(jogador: JogadorDetalheDto) {
    Column(modifier = Modifier.fillMaxWidth()) {
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
