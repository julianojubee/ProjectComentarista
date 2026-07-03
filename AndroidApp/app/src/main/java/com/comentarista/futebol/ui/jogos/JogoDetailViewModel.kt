package com.comentarista.futebol.ui.jogos

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.comentarista.futebol.data.remote.dto.JogoDetalheDto
import com.comentarista.futebol.data.repository.JogosRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class JogoDetailUiState(
    val carregando: Boolean = false,
    val jogo: JogoDetalheDto? = null,
    val erro: String? = null
)

@HiltViewModel
class JogoDetailViewModel @Inject constructor(
    private val jogosRepository: JogosRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(JogoDetailUiState())
    val uiState: StateFlow<JogoDetailUiState> = _uiState.asStateFlow()

    private var idCarregado: Int? = null

    fun carregar(id: Int) {
        if (idCarregado == id) return
        idCarregado = id

        viewModelScope.launch {
            _uiState.update { it.copy(carregando = true, erro = null) }
            val resultado = jogosRepository.detalheJogo(id)
            resultado.fold(
                onSuccess = { jogo -> _uiState.update { it.copy(carregando = false, jogo = jogo) } },
                onFailure = { _uiState.update { it.copy(carregando = false, erro = "Não foi possível carregar o jogo.") } }
            )
        }
    }
}
