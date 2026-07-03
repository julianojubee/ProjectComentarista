package com.comentarista.futebol.ui.times

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
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
import com.comentarista.futebol.data.remote.dto.TimeResumoDto
import com.comentarista.futebol.ui.common.NetworkImage

@Composable
fun TimesListScreen(
    onTimeClick: (Int) -> Unit,
    viewModel: TimesListViewModel = hiltViewModel()
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

            uiState.times.isEmpty() -> Text(
                text = stringResource(R.string.times_empty),
                modifier = Modifier.align(Alignment.Center).padding(24.dp)
            )

            else -> LazyColumn(contentPadding = PaddingValues(16.dp)) {
                items(uiState.times, key = { it.id }) { time ->
                    TimeCard(time, onClick = { onTimeClick(time.id) })
                }
            }
        }
    }
}

@Composable
private fun TimeCard(time: TimeResumoDto, onClick: () -> Unit) {
    Card(modifier = Modifier.fillMaxWidth().padding(vertical = 6.dp).clickable(onClick = onClick)) {
        Row(modifier = Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
            NetworkImage(time.escudoUrl, size = 32.dp, modifier = Modifier.padding(end = 12.dp))
            Text(text = time.nome, style = MaterialTheme.typography.bodyLarge)
        }
    }
}
