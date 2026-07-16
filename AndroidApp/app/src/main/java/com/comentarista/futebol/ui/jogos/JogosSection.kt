package com.comentarista.futebol.ui.jogos

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.Text
import androidx.compose.material3.adaptive.ExperimentalMaterial3AdaptiveApi
import androidx.compose.material3.adaptive.layout.AnimatedPane
import androidx.compose.material3.adaptive.layout.ListDetailPaneScaffold
import androidx.compose.material3.adaptive.layout.ListDetailPaneScaffoldRole
import androidx.compose.material3.adaptive.navigation.rememberListDetailPaneScaffoldNavigator
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier

// Lista + detalhe lado a lado quando a tela é grande (tablet); em celular, o
// próprio ListDetailPaneScaffold colapsa para uma tela por vez com transição.
@OptIn(ExperimentalMaterial3AdaptiveApi::class)
@Composable
fun JogosSection(onAbrirAnalise: (Int) -> Unit) {
    val navigator = rememberListDetailPaneScaffoldNavigator<Int>()

    BackHandler(navigator.canNavigateBack()) {
        navigator.navigateBack()
    }

    ListDetailPaneScaffold(
        directive = navigator.scaffoldDirective,
        value = navigator.scaffoldValue,
        listPane = {
            AnimatedPane {
                JogosListScreen(
                    onJogoClick = { id -> navigator.navigateTo(ListDetailPaneScaffoldRole.Detail, id) }
                )
            }
        },
        detailPane = {
            AnimatedPane {
                val jogoId = navigator.currentDestination?.content as? Int
                if (jogoId != null) {
                    JogoDetailScreen(jogoId = jogoId, onAnalisarClick = { onAbrirAnalise(jogoId) })
                } else {
                    Box(modifier = Modifier.fillMaxSize()) {
                        Text(
                            text = "Selecione um jogo",
                            modifier = Modifier.align(Alignment.Center)
                        )
                    }
                }
            }
        }
    )
}
