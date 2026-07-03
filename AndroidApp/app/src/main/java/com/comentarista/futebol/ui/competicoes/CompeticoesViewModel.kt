package com.comentarista.futebol.ui.competicoes

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.comentarista.futebol.data.remote.dto.CompeticaoDto
import com.comentarista.futebol.data.repository.CompeticoesRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class CompeticoesUiState(
    val carregando: Boolean = true,
    val competicoes: List<CompeticaoDto> = emptyList(),
    val erro: String? = null
)

@HiltViewModel
class CompeticoesViewModel @Inject constructor(
    private val competicoesRepository: CompeticoesRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(CompeticoesUiState())
    val uiState: StateFlow<CompeticoesUiState> = _uiState.asStateFlow()

    init {
        carregarCompeticoes()
    }

    fun carregarCompeticoes() {
        viewModelScope.launch {
            _uiState.update { it.copy(carregando = true, erro = null) }
            val resultado = competicoesRepository.listarCompeticoes()
            resultado.fold(
                onSuccess = { competicoes -> _uiState.update { it.copy(carregando = false, competicoes = competicoes) } },
                onFailure = { _uiState.update { it.copy(carregando = false, erro = "Não foi possível carregar as competições.") } }
            )
        }
    }
}
