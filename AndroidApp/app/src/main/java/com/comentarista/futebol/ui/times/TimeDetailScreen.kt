package com.comentarista.futebol.ui.times

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.comentarista.futebol.data.remote.dto.AnotacaoTimeDto
import com.comentarista.futebol.ui.common.NetworkImage

@Composable
fun TimeDetailScreen(
    timeId: Int,
    viewModel: TimeDetailViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()

    var dialogoAberto by remember { mutableStateOf(false) }
    var anotacaoEmEdicao by remember { mutableStateOf<AnotacaoTimeDto?>(null) }
    var anotacaoParaExcluir by remember { mutableStateOf<AnotacaoTimeDto?>(null) }

    LaunchedEffect(timeId) {
        viewModel.carregar(timeId)
    }

    Scaffold(
        floatingActionButton = {
            FloatingActionButton(onClick = {
                anotacaoEmEdicao = null
                dialogoAberto = true
            }) {
                Icon(Icons.Filled.Add, contentDescription = "Nova anotação")
            }
        }
    ) { padding ->
        Box(modifier = Modifier.fillMaxSize().padding(padding)) {
            when {
                uiState.carregando -> CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
                uiState.erro != null -> Text(
                    text = uiState.erro.orEmpty(),
                    modifier = Modifier.align(Alignment.Center).padding(24.dp),
                    color = MaterialTheme.colorScheme.error
                )
                uiState.time != null -> LazyColumn(modifier = Modifier.padding(16.dp)) {
                    item {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            NetworkImage(uiState.time!!.escudoUrl, size = 48.dp, modifier = Modifier.padding(end = 12.dp))
                            Column {
                                Text(text = uiState.time!!.nome, style = MaterialTheme.typography.titleLarge)
                                uiState.time!!.cidade?.let {
                                    Text(text = it, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                }
                            }
                        }
                        HorizontalDivider(modifier = Modifier.padding(vertical = 16.dp))
                        Text(text = "Anotações", style = MaterialTheme.typography.titleLarge)
                    }

                    if (uiState.anotacoes.isEmpty()) {
                        item {
                            Text(
                                text = "Nenhuma anotação ainda. Toque em + pra criar a primeira.",
                                modifier = Modifier.padding(vertical = 16.dp),
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }

                    items(uiState.anotacoes, key = { it.id }) { anotacao ->
                        AnotacaoCard(
                            anotacao = anotacao,
                            onEditar = {
                                anotacaoEmEdicao = anotacao
                                dialogoAberto = true
                            },
                            onExcluir = { anotacaoParaExcluir = anotacao }
                        )
                    }
                }
            }
        }
    }

    if (dialogoAberto) {
        AnotacaoFormDialog(
            anotacaoExistente = anotacaoEmEdicao,
            onDismiss = { dialogoAberto = false },
            onSalvar = { titulo, conteudo, categoria ->
                viewModel.salvarAnotacao(anotacaoEmEdicao?.id, titulo, conteudo, categoria)
                dialogoAberto = false
            }
        )
    }

    anotacaoParaExcluir?.let { anotacao ->
        AlertDialog(
            onDismissRequest = { anotacaoParaExcluir = null },
            title = { Text("Excluir anotação?") },
            text = { Text(anotacao.titulo) },
            confirmButton = {
                TextButton(onClick = {
                    viewModel.excluirAnotacao(anotacao.id)
                    anotacaoParaExcluir = null
                }) { Text("Excluir") }
            },
            dismissButton = {
                TextButton(onClick = { anotacaoParaExcluir = null }) { Text("Cancelar") }
            }
        )
    }
}

@Composable
private fun AnotacaoCard(anotacao: AnotacaoTimeDto, onEditar: () -> Unit, onExcluir: () -> Unit) {
    Card(modifier = Modifier.fillMaxWidth().padding(vertical = 6.dp)) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(text = anotacao.titulo, style = MaterialTheme.typography.bodyLarge)
                    anotacao.categoria?.let {
                        Text(text = it, style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary)
                    }
                }
                IconButton(onClick = onEditar) {
                    Icon(Icons.Filled.Edit, contentDescription = "Editar", modifier = Modifier.size(20.dp))
                }
                IconButton(onClick = onExcluir) {
                    Icon(Icons.Filled.Delete, contentDescription = "Excluir", modifier = Modifier.size(20.dp))
                }
            }
            Text(text = anotacao.conteudo, modifier = Modifier.padding(top = 4.dp))
        }
    }
}
