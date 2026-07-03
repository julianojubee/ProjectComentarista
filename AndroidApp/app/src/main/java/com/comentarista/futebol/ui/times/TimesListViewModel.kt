package com.comentarista.futebol.ui.times

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.comentarista.futebol.data.remote.dto.TimeResumoDto
import com.comentarista.futebol.data.repository.TimesRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class TimesListUiState(
    val carregando: Boolean = true,
    val times: List<TimeResumoDto> = emptyList(),
    val erro: String? = null
)

@HiltViewModel
class TimesListViewModel @Inject constructor(
    private val timesRepository: TimesRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(TimesListUiState())
    val uiState: StateFlow<TimesListUiState> = _uiState.asStateFlow()

    init {
        carregarTimes()
    }

    fun carregarTimes() {
        viewModelScope.launch {
            _uiState.update { it.copy(carregando = true, erro = null) }
            val resultado = timesRepository.listarTimes()
            resultado.fold(
                onSuccess = { times -> _uiState.update { it.copy(carregando = false, times = times) } },
                onFailure = { _uiState.update { it.copy(carregando = false, erro = "Não foi possível carregar os times.") } }
            )
        }
    }
}
