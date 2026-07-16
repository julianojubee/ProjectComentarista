package com.comentarista.futebol.ui.common

import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp

// Mesma escala de cores das notas na web (RankingNotaItem.NotaColor)
fun corDaNota(nota: Double): Color = when {
    nota >= 8.5 -> Color(0xFFF59E0B)
    nota >= 7.5 -> Color(0xFF22C55E)
    nota >= 6.5 -> Color(0xFF3B82F6)
    nota >= 5.5 -> Color(0xFF6B7280)
    nota >= 4.5 -> Color(0xFFF97316)
    else -> Color(0xFFEF4444)
}

@Composable
fun NotaChip(nota: Double, modifier: Modifier = Modifier) {
    Surface(color = corDaNota(nota), shape = MaterialTheme.shapes.small, modifier = modifier) {
        Text(
            text = "$nota",
            color = Color.White,
            fontWeight = FontWeight.Bold,
            style = MaterialTheme.typography.labelMedium,
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 3.dp)
        )
    }
}
