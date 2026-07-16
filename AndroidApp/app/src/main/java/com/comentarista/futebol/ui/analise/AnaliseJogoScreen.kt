package com.comentarista.futebol.ui.analise

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Autorenew
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.comentarista.futebol.data.remote.dto.CriterioNotaDto
import com.comentarista.futebol.data.remote.dto.EscalacaoJogadorDto
import com.comentarista.futebol.data.remote.dto.JogoDetalheDto
import com.comentarista.futebol.ui.common.NetworkImage
import com.comentarista.futebol.ui.common.NotaChip

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AnaliseJogoScreen(
    jogoId: Int,
    onVoltar: () -> Unit,
    viewModel: AnaliseJogoViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()

    LaunchedEffect(jogoId) {
        viewModel.carregar(jogoId)
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    val jogo = uiState.jogo
                    Text(
                        text = if (jogo != null) "${jogo.timeCasaNome} x ${jogo.timeVisitanteNome}" else "Análise",
                        maxLines = 1
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onVoltar) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar")
                    }
                },
                actions = {
                    Text("Analisado", modifier = Modifier.padding(end = 4.dp))
                    Switch(
                        checked = uiState.analisadoPorMim,
                        onCheckedChange = { viewModel.alternarAnalisado(jogoId, it) }
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                }
            )
        }
    ) { padding ->
        Box(modifier = Modifier.fillMaxSize().padding(padding)) {
            when {
                uiState.carregando -> CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))

                uiState.erro != null && uiState.jogo == null -> Text(
                    text = uiState.erro.orEmpty(),
                    modifier = Modifier.align(Alignment.Center).padding(24.dp),
                    color = MaterialTheme.colorScheme.error
                )

                uiState.jogo != null -> AnaliseConteudo(jogoId, uiState, viewModel)
            }
        }
    }
}

@Composable
private fun AnaliseConteudo(jogoId: Int, uiState: AnaliseJogoUiState, viewModel: AnaliseJogoViewModel) {
    val jogo = uiState.jogo!!
    // Tablet-first: painéis lado a lado quando a janela é larga (>= 600dp), senão empilha.
    val larguraJanela = LocalConfiguration.current.screenWidthDp
    val ladoALado = larguraJanela >= 600

    if (ladoALado) {
        Row(modifier = Modifier.fillMaxSize()) {
            PainelEscalados(
                jogo = jogo,
                uiState = uiState,
                onSelecionar = viewModel::selecionarJogador,
                modifier = Modifier.weight(0.4f).fillMaxHeight()
            )
            HorizontalDivider(modifier = Modifier.fillMaxHeight().width(1.dp))
            PainelAvaliacao(
                jogoId = jogoId,
                uiState = uiState,
                viewModel = viewModel,
                modifier = Modifier.weight(0.6f).fillMaxHeight()
            )
        }
    } else {
        Column(modifier = Modifier.fillMaxSize()) {
            PainelEscalados(
                jogo = jogo,
                uiState = uiState,
                onSelecionar = viewModel::selecionarJogador,
                modifier = Modifier.weight(0.45f).fillMaxWidth()
            )
            HorizontalDivider()
            PainelAvaliacao(
                jogoId = jogoId,
                uiState = uiState,
                viewModel = viewModel,
                modifier = Modifier.weight(0.55f).fillMaxWidth()
            )
        }
    }
}

@Composable
private fun PainelEscalados(
    jogo: JogoDetalheDto,
    uiState: AnaliseJogoUiState,
    onSelecionar: (Int) -> Unit,
    modifier: Modifier = Modifier
) {
    LazyColumn(modifier = modifier, contentPadding = PaddingValues(12.dp)) {
        item {
            Row(
                modifier = Modifier.fillMaxWidth().padding(bottom = 8.dp),
                horizontalArrangement = Arrangement.Center
            ) {
                Text(
                    text = "${jogo.placarCasa ?: "-"} x ${jogo.placarVisitante ?: "-"}",
                    style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.Bold
                )
            }
        }

        item { TimeSecaoTitulo(jogo.timeCasaNome) }
        items(escaladosOrdenados(jogo.escalacaoCasa), key = { "casa${it.jogadorId}" }) { jogador ->
            JogadorEscaladoLinha(
                jogador = jogador,
                selecionado = uiState.jogadorSelecionadoId == jogador.jogadorId,
                rascunho = uiState.rascunhos[jogador.jogadorId],
                criterios = uiState.criterios,
                onClick = { onSelecionar(jogador.jogadorId) }
            )
        }

        item { TimeSecaoTitulo(jogo.timeVisitanteNome) }
        items(escaladosOrdenados(jogo.escalacaoVisitante), key = { "vis${it.jogadorId}" }) { jogador ->
            JogadorEscaladoLinha(
                jogador = jogador,
                selecionado = uiState.jogadorSelecionadoId == jogador.jogadorId,
                rascunho = uiState.rascunhos[jogador.jogadorId],
                criterios = uiState.criterios,
                onClick = { onSelecionar(jogador.jogadorId) }
            )
        }
    }
}

private fun escaladosOrdenados(lista: List<EscalacaoJogadorDto>) =
    lista.sortedWith(compareByDescending<EscalacaoJogadorDto> { it.titular }.thenBy { it.nome })

@Composable
private fun TimeSecaoTitulo(nome: String) {
    Text(
        text = nome,
        style = MaterialTheme.typography.titleSmall,
        fontWeight = FontWeight.Bold,
        color = MaterialTheme.colorScheme.primary,
        modifier = Modifier.padding(top = 12.dp, bottom = 4.dp)
    )
}

@Composable
private fun JogadorEscaladoLinha(
    jogador: EscalacaoJogadorDto,
    selecionado: Boolean,
    rascunho: RascunhoNota?,
    criterios: List<CriterioNotaDto>,
    onClick: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth().padding(vertical = 2.dp),
        colors = if (selecionado)
            CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer)
        else CardDefaults.cardColors(),
        onClick = onClick
    ) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 10.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            NetworkImage(jogador.fotoUrl, size = 28.dp, circular = true)
            Spacer(modifier = Modifier.width(8.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(text = jogador.nome, style = MaterialTheme.typography.bodyMedium, maxLines = 1)
                jogador.posicao?.let {
                    Text(text = it, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
            if (rascunho != null) {
                if (!rascunho.salvo) {
                    Text(
                        text = "●",
                        color = MaterialTheme.colorScheme.error,
                        modifier = Modifier.padding(end = 6.dp)
                    )
                }
                NotaChip(rascunho.notaFinal(criterios))
            }
        }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun PainelAvaliacao(
    jogoId: Int,
    uiState: AnaliseJogoUiState,
    viewModel: AnaliseJogoViewModel,
    modifier: Modifier = Modifier
) {
    val jogadorId = uiState.jogadorSelecionadoId

    if (jogadorId == null) {
        Box(modifier = modifier) {
            Text(
                text = "Selecione um jogador na escalação",
                modifier = Modifier.align(Alignment.Center).padding(24.dp),
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        return
    }

    val jogo = uiState.jogo!!
    val jogador = (jogo.escalacaoCasa + jogo.escalacaoVisitante).find { it.jogadorId == jogadorId }
    val rascunho = uiState.rascunhos[jogadorId] ?: RascunhoNota()
    val criterios = uiState.criterios

    LazyColumn(modifier = modifier, contentPadding = PaddingValues(16.dp)) {
        item {
            Row(verticalAlignment = Alignment.CenterVertically) {
                NetworkImage(jogador?.fotoUrl, size = 48.dp, circular = true)
                Spacer(modifier = Modifier.width(12.dp))
                Column(modifier = Modifier.weight(1f)) {
                    Text(text = jogador?.nome ?: "", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                    jogador?.posicao?.let {
                        Text(text = it, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.primary)
                    }
                }
                NotaChip(rascunho.notaFinal(criterios))
            }

            Row(modifier = Modifier.fillMaxWidth().padding(top = 12.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedButton(onClick = { viewModel.preencherDasEstatisticas(jogoId, jogadorId) }) {
                    Icon(Icons.Filled.Autorenew, contentDescription = null, modifier = Modifier.padding(end = 4.dp))
                    Text("Pré-preencher")
                }
            }

            Text(
                text = "Ações",
                style = MaterialTheme.typography.titleSmall,
                fontWeight = FontWeight.Bold,
                modifier = Modifier.padding(top = 16.dp, bottom = 8.dp)
            )
        }

        item {
            // FlowRow (não-lazy): a lista de critérios é pequena (~20), então não
            // precisa de grid preguiçoso — e evitar aninhar um scroll lazy dentro
            // de outro (LazyVerticalGrid dentro de item{} de LazyColumn trava com
            // altura infinita).
            FlowRow(
                modifier = Modifier.fillMaxWidth().padding(bottom = 8.dp),
                horizontalArrangement = Arrangement.spacedBy(4.dp),
                verticalArrangement = Arrangement.spacedBy(4.dp)
            ) {
                criterios.forEach { criterio ->
                    CriterioCard(
                        criterio = criterio,
                        quantidade = rascunho.quantidades[criterio.acaoId] ?: 0,
                        onIncrementar = { viewModel.incrementar(jogadorId, criterio.acaoId) },
                        onDecrementar = { viewModel.decrementar(jogadorId, criterio.acaoId) },
                        modifier = Modifier.widthIn(min = 160.dp)
                    )
                }
            }
        }

        item {
            OutlinedTextField(
                value = rascunho.notaManualTexto,
                onValueChange = { viewModel.setNotaManual(jogadorId, it) },
                label = { Text("Nota manual (opcional, 0-10)") },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp)
            )

            OutlinedTextField(
                value = rascunho.comentario,
                onValueChange = { viewModel.setComentario(jogadorId, it) },
                label = { Text("Comentário") },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp)
            )

            Row(
                modifier = Modifier.fillMaxWidth().padding(top = 16.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Button(
                    onClick = { viewModel.salvarJogador(jogoId, jogadorId) },
                    enabled = !uiState.salvando,
                    modifier = Modifier.weight(1f)
                ) {
                    Text("Salvar jogador")
                }
                TextButton(onClick = { viewModel.excluirNota(jogoId, jogadorId) }) {
                    Icon(Icons.Filled.Delete, contentDescription = null)
                    Text("Excluir")
                }
            }
        }
    }
}

@Composable
private fun CriterioCard(
    criterio: CriterioNotaDto,
    quantidade: Int,
    onIncrementar: () -> Unit,
    onDecrementar: () -> Unit,
    modifier: Modifier = Modifier
) {
    Card(modifier = modifier.padding(4.dp)) {
        Column(modifier = Modifier.padding(10.dp)) {
            Text(text = criterio.label, style = MaterialTheme.typography.labelMedium, maxLines = 2)
            Text(
                text = formatarPeso(criterio.peso),
                style = MaterialTheme.typography.labelSmall,
                color = if (criterio.peso >= 0) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.error
            )
            Row(
                modifier = Modifier.fillMaxWidth().padding(top = 6.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                // Alvos de toque grandes: análise é feita em pé, durante a partida.
                IconButton(onClick = onDecrementar, modifier = Modifier.width(48.dp)) {
                    Icon(Icons.Filled.Remove, contentDescription = "Diminuir")
                }
                Text(text = "$quantidade", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                IconButton(onClick = onIncrementar, modifier = Modifier.width(48.dp)) {
                    Icon(Icons.Filled.Add, contentDescription = "Aumentar")
                }
            }
        }
    }
}

private fun formatarPeso(peso: Double): String = if (peso >= 0) "+$peso" else "$peso"
