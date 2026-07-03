package com.comentarista.futebol.ui.times

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

@OptIn(ExperimentalMaterial3AdaptiveApi::class)
@Composable
fun TimesSection() {
    val navigator = rememberListDetailPaneScaffoldNavigator<Int>()

    BackHandler(navigator.canNavigateBack()) {
        navigator.navigateBack()
    }

    ListDetailPaneScaffold(
        directive = navigator.scaffoldDirective,
        value = navigator.scaffoldValue,
        listPane = {
            AnimatedPane {
                TimesListScreen(
                    onTimeClick = { id -> navigator.navigateTo(ListDetailPaneScaffoldRole.Detail, id) }
                )
            }
        },
        detailPane = {
            AnimatedPane {
                val timeId = navigator.currentDestination?.content as? Int
                if (timeId != null) {
                    TimeDetailScreen(timeId = timeId)
                } else {
                    Box(modifier = Modifier.fillMaxSize()) {
                        Text(text = "Selecione um time", modifier = Modifier.align(Alignment.Center))
                    }
                }
            }
        }
    )
}
