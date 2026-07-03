package com.comentarista.futebol.ui.common

import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import coil.compose.AsyncImage

// Escudo de time, foto de jogador etc. — não renderiza nada se a URL vier vazia
// (em vez de mostrar um placeholder quebrado).
@Composable
fun NetworkImage(
    url: String?,
    size: Dp = 28.dp,
    circular: Boolean = false,
    modifier: Modifier = Modifier
) {
    if (url.isNullOrBlank()) return
    AsyncImage(
        model = url,
        contentDescription = null,
        modifier = modifier
            .size(size)
            .let { if (circular) it.clip(CircleShape) else it }
    )
}
