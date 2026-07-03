package com.comentarista.futebol.ui.jogadores

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.comentarista.futebol.data.remote.dto.JogadorDetalheDto
import com.comentarista.futebol.data.repository.JogadoresRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class JogadorDetailUiState(
    val carregando: Boolean = false,
    val jogador: JogadorDetalheDto? = null,
    val erro: String? = null
)

@HiltViewModel
class JogadorDetailViewModel @Inject constructor(
    private val jogadoresRepository: JogadoresRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(JogadorDetailUiState())
    val uiState: StateFlow<JogadorDetailUiState> = _uiState.asStateFlow()

    private var idCarregado: Int? = null

    fun carregar(id: Int) {
        if (idCarregado == id) return
        idCarregado = id

        viewModelScope.launch {
            _uiState.update { it.copy(carregando = true, erro = null) }
            val resultado = jogadoresRepository.detalheJogador(id)
            resultado.fold(
                onSuccess = { jogador -> _uiState.update { it.copy(carregando = false, jogador = jogador) } },
                onFailure = { _uiState.update { it.copy(carregando = false, erro = "Não foi possível carregar o jogador.") } }
            )
        }
    }
}
