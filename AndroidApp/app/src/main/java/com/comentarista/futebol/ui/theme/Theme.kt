package com.comentarista.futebol.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable

private val LightColors = lightColorScheme(
    primary = GramadoVerde,
    secondary = Dourado,
    background = FundoClaro,
    surface = FundoClaro
)

private val DarkColors = darkColorScheme(
    primary = GramadoVerdeClaro,
    secondary = Dourado,
    background = FundoEscuro,
    surface = FundoEscuro
)

@Composable
fun ComentaristaFutebolTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit
) {
    val colorScheme = if (darkTheme) DarkColors else LightColors
    MaterialTheme(
        colorScheme = colorScheme,
        typography = Typography,
        content = content
    )
}
