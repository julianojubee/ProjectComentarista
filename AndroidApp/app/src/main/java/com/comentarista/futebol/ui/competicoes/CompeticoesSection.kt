package com.comentarista.futebol.ui.competicoes

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
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier

// Lista + jogos da competição lado a lado em tablet (mesmo padrão das outras seções).
@OptIn(ExperimentalMaterial3AdaptiveApi::class)
@Composable
fun CompeticoesSection() {
    val navigator = rememberListDetailPaneScaffoldNavigator<Int>()
    // Nome da competição selecionada (só para o título do pane de detalhe)
    var nomeSelecionado by remember { mutableStateOf<String?>(null) }

    BackHandler(navigator.canNavigateBack()) {
        navigator.navigateBack()
    }

    ListDetailPaneScaffold(
        directive = navigator.scaffoldDirective,
        value = navigator.scaffoldValue,
        listPane = {
            AnimatedPane {
                CompeticoesListScreen(
                    onCompeticaoClick = { id, nome ->
                        nomeSelecionado = nome
                        navigator.navigateTo(ListDetailPaneScaffoldRole.Detail, id)
                    }
                )
            }
        },
        detailPane = {
            AnimatedPane {
                val competicaoId = navigator.currentDestination?.content as? Int
                if (competicaoId != null) {
                    CompeticaoDetailScreen(
                        competicaoId = competicaoId,
                        competicaoNome = nomeSelecionado
                    )
                } else {
                    Box(modifier = Modifier.fillMaxSize()) {
                        Text(
                            text = "Selecione uma competição",
                            modifier = Modifier.align(Alignment.Center)
                        )
                    }
                }
            }
        }
    )
}
