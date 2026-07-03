package com.comentarista.futebol.ui.jogos

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
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.comentarista.futebol.data.remote.dto.JogoDetalheDto
import com.comentarista.futebol.ui.common.NetworkImage

@Composable
fun JogoDetailScreen(
    jogoId: Int,
    viewModel: JogoDetailViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()

    LaunchedEffect(jogoId) {
        viewModel.carregar(jogoId)
    }

    Box(modifier = Modifier.fillMaxSize().padding(24.dp)) {
        when {
            uiState.carregando -> CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
            uiState.erro != null -> Text(
                text = uiState.erro.orEmpty(),
                modifier = Modifier.align(Alignment.Center),
                color = MaterialTheme.colorScheme.error
            )
            uiState.jogo != null -> JogoDetailConteudo(uiState.jogo!!)
        }
    }
}

@Composable
private fun JogoDetailConteudo(jogo: JogoDetalheDto) {
    Column(modifier = Modifier.fillMaxWidth()) {
        jogo.competicaoNome?.let {
            Text(text = it, style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary)
        }

        Row2(jogo)

        HorizontalDivider(modifier = Modifier.padding(vertical = 16.dp))

        InfoLinha("Status", jogo.status)
        InfoLinha("Data", jogo.data)
        InfoLinha("Estádio", jogo.estadio)
        InfoLinha("Árbitro", jogo.arbitro)
        if (jogo.penaltisCasa != null || jogo.penaltisVisitante != null) {
            InfoLinha("Pênaltis", "${jogo.penaltisCasa ?: 0} x ${jogo.penaltisVisitante ?: 0}")
        }
    }
}

@Composable
private fun Row2(jogo: JogoDetalheDto) {
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
