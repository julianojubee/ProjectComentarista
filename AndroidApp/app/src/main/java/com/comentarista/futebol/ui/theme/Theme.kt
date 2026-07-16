package com.comentarista.futebol.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

// Mapeamento da paleta do site para o color scheme do Material 3:
// azul (--link) = ação primária, dourado (--gold) = secundária/marca,
// surfaces/bordas/textos idênticos aos das variáveis CSS.
private val LightColors = lightColorScheme(
    primary = ClaroAzul,
    onPrimary = Color.White,
    primaryContainer = ClaroAzulSurface,
    onPrimaryContainer = ClaroAzulEscuro,
    secondary = Dourado,
    onSecondary = Color.White,
    secondaryContainer = ClaroSurface3,
    onSecondaryContainer = ClaroTexto,
    background = ClaroBg,
    onBackground = ClaroTexto,
    surface = ClaroBg,
    onSurface = ClaroTexto,
    surfaceVariant = ClaroSurface3,
    onSurfaceVariant = ClaroTextoMuted,
    surfaceContainerLowest = ClaroBg,
    surfaceContainerLow = ClaroSurface2,   // cards (Card usa surfaceContainerLow)
    surfaceContainer = ClaroSurface2,      // bottom bar / nav rail
    surfaceContainerHigh = ClaroSurface3,
    surfaceContainerHighest = ClaroSurface3,
    outline = ClaroBordaForte,
    outlineVariant = ClaroBorda,
)

private val DarkColors = darkColorScheme(
    primary = EscuroAzul,
    onPrimary = Color(0xFF0B1220),
    primaryContainer = EscuroAzulSurface,
    onPrimaryContainer = EscuroAzulClaro,
    secondary = Dourado,
    onSecondary = Color(0xFF1A1300),
    secondaryContainer = EscuroSurface3,
    onSecondaryContainer = EscuroTexto,
    background = EscuroBg,
    onBackground = EscuroTexto,
    surface = EscuroBg,
    onSurface = EscuroTexto,
    surfaceVariant = EscuroSurface3,
    onSurfaceVariant = EscuroTextoMuted,
    surfaceContainerLowest = EscuroBg,
    surfaceContainerLow = EscuroSurface,   // cards
    surfaceContainer = EscuroSurface2,     // bottom bar / nav rail
    surfaceContainerHigh = EscuroSurface3,
    surfaceContainerHighest = EscuroSurface3,
    outline = EscuroBordaForte,
    outlineVariant = EscuroBorda,
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
