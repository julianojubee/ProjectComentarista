package com.comentarista.futebol.ui.main

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.BarChart
import androidx.compose.material.icons.filled.EmojiEvents
import androidx.compose.material.icons.filled.Groups
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.SportsSoccer
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.adaptive.navigationsuite.NavigationSuiteScaffold
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.hilt.navigation.compose.hiltViewModel
import com.comentarista.futebol.ui.competicoes.CompeticoesSection
import com.comentarista.futebol.ui.jogadores.JogadoresSection
import com.comentarista.futebol.ui.jogos.JogosSection
import com.comentarista.futebol.ui.relatorios.RelatoriosScreen
import com.comentarista.futebol.ui.times.TimesSection

private enum class Destino(val rotulo: String) {
    JOGOS("Jogos"),
    JOGADORES("Jogadores"),
    TIMES("Times"),
    COMPETICOES("Competições"),
    RELATORIOS("Relatórios")
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MainScaffold(
    onDeslogado: () -> Unit,
    onAbrirAnalise: (Int) -> Unit,
    viewModel: MainViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    var destino by remember { mutableStateOf(Destino.JOGOS) }

    LaunchedEffect(uiState.deslogado) {
        if (uiState.deslogado) onDeslogado()
    }

    NavigationSuiteScaffold(
        navigationSuiteItems = {
            item(
                selected = destino == Destino.JOGOS,
                onClick = { destino = Destino.JOGOS },
                icon = { Icon(Icons.Filled.SportsSoccer, contentDescription = null) },
                label = { Text(Destino.JOGOS.rotulo) }
            )
            item(
                selected = destino == Destino.JOGADORES,
                onClick = { destino = Destino.JOGADORES },
                icon = { Icon(Icons.Filled.Person, contentDescription = null) },
                label = { Text(Destino.JOGADORES.rotulo) }
            )
            item(
                selected = destino == Destino.TIMES,
                onClick = { destino = Destino.TIMES },
                icon = { Icon(Icons.Filled.Groups, contentDescription = null) },
                label = { Text(Destino.TIMES.rotulo) }
            )
            item(
                selected = destino == Destino.COMPETICOES,
                onClick = { destino = Destino.COMPETICOES },
                icon = { Icon(Icons.Filled.EmojiEvents, contentDescription = null) },
                label = { Text(Destino.COMPETICOES.rotulo) }
            )
            item(
                selected = destino == Destino.RELATORIOS,
                onClick = { destino = Destino.RELATORIOS },
                icon = { Icon(Icons.Filled.BarChart, contentDescription = null) },
                label = { Text(Destino.RELATORIOS.rotulo) }
            )
        }
    ) {
        Scaffold(
            topBar = {
                TopAppBar(
                    title = { Text(destino.rotulo) },
                    actions = {
                        TextButton(onClick = viewModel::onLogoutClick) { Text("Sair") }
                    }
                )
            }
        ) { padding ->
            Box(modifier = Modifier.fillMaxSize().padding(padding)) {
                when (destino) {
                    Destino.JOGOS -> JogosSection(onAbrirAnalise = onAbrirAnalise)
                    Destino.JOGADORES -> JogadoresSection()
                    Destino.TIMES -> TimesSection()
                    Destino.COMPETICOES -> CompeticoesSection()
                    Destino.RELATORIOS -> RelatoriosScreen()
                }
            }
        }
    }
}
