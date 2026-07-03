package com.comentarista.futebol.ui.times

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.comentarista.futebol.data.remote.dto.AnotacaoTimeDto

// Diálogo reusado tanto pra criar (anotacaoExistente == null) quanto pra editar.
@Composable
fun AnotacaoFormDialog(
    anotacaoExistente: AnotacaoTimeDto?,
    onDismiss: () -> Unit,
    onSalvar: (titulo: String, conteudo: String, categoria: String?) -> Unit
) {
    var titulo by remember { mutableStateOf(anotacaoExistente?.titulo.orEmpty()) }
    var conteudo by remember { mutableStateOf(anotacaoExistente?.conteudo.orEmpty()) }
    var categoria by remember { mutableStateOf(anotacaoExistente?.categoria.orEmpty()) }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(if (anotacaoExistente == null) "Nova anotação" else "Editar anotação") },
        text = {
            Column {
                OutlinedTextField(
                    value = titulo,
                    onValueChange = { titulo = it },
                    label = { Text("Título") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
                OutlinedTextField(
                    value = categoria,
                    onValueChange = { categoria = it },
                    label = { Text("Categoria (opcional)") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth().padding(top = 8.dp)
                )
                OutlinedTextField(
                    value = conteudo,
                    onValueChange = { conteudo = it },
                    label = { Text("Conteúdo") },
                    minLines = 3,
                    modifier = Modifier.fillMaxWidth().padding(top = 8.dp)
                )
            }
        },
        confirmButton = {
            TextButton(
                onClick = { onSalvar(titulo, conteudo, categoria.ifBlank { null }) },
                enabled = titulo.isNotBlank() && conteudo.isNotBlank()
            ) {
                Text("Salvar")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Cancelar") }
        }
    )
}
