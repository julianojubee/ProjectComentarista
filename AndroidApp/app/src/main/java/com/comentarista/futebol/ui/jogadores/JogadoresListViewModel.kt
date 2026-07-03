package com.comentarista.futebol.ui.jogadores

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.comentarista.futebol.data.remote.dto.JogadorResumoDto
import com.comentarista.futebol.data.repository.JogadoresRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class JogadoresListUiState(
    val carregando: Boolean = true,
    val jogadores: List<JogadorResumoDto> = emptyList(),
    val erro: String? = null
)

@HiltViewModel
class JogadoresListViewModel @Inject constructor(
    private val jogadoresRepository: JogadoresRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(JogadoresListUiState())
    val uiState: StateFlow<JogadoresListUiState> = _uiState.asStateFlow()

    init {
        carregarJogadores()
    }

    fun carregarJogadores() {
        viewModelScope.launch {
            _uiState.update { it.copy(carregando = true, erro = null) }
            val resultado = jogadoresRepository.listarJogadores()
            resultado.fold(
                onSuccess = { jogadores -> _uiState.update { it.copy(carregando = false, jogadores = jogadores) } },
                onFailure = { _uiState.update { it.copy(carregando = false, erro = "Não foi possível carregar os jogadores.") } }
            )
        }
    }
}
