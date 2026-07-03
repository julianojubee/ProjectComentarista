package com.comentarista.futebol.ui.jogos

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.comentarista.futebol.data.remote.dto.JogoResumoDto
import com.comentarista.futebol.data.repository.JogosRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class JogosListUiState(
    val carregando: Boolean = true,
    val jogos: List<JogoResumoDto> = emptyList(),
    val erro: String? = null
)

@HiltViewModel
class JogosListViewModel @Inject constructor(
    private val jogosRepository: JogosRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(JogosListUiState())
    val uiState: StateFlow<JogosListUiState> = _uiState.asStateFlow()

    init {
        carregarJogos()
    }

    fun carregarJogos() {
        viewModelScope.launch {
            _uiState.update { it.copy(carregando = true, erro = null) }
            val resultado = jogosRepository.listarJogos()
            resultado.fold(
                onSuccess = { jogos -> _uiState.update { it.copy(carregando = false, jogos = jogos) } },
                onFailure = { _uiState.update { it.copy(carregando = false, erro = "Não foi possível carregar os jogos.") } }
            )
        }
    }
}
