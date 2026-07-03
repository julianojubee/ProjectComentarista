package com.comentarista.futebol.ui.times

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.comentarista.futebol.data.remote.dto.AnotacaoTimeDto
import com.comentarista.futebol.data.remote.dto.AnotacaoTimeInput
import com.comentarista.futebol.data.remote.dto.TimeDetalheDto
import com.comentarista.futebol.data.repository.AnotacoesRepository
import com.comentarista.futebol.data.repository.TimesRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class TimeDetailUiState(
    val carregando: Boolean = false,
    val time: TimeDetalheDto? = null,
    val anotacoes: List<AnotacaoTimeDto> = emptyList(),
    val erro: String? = null,
    val salvandoAnotacao: Boolean = false,
    val erroAnotacao: String? = null
)

@HiltViewModel
class TimeDetailViewModel @Inject constructor(
    private val timesRepository: TimesRepository,
    private val anotacoesRepository: AnotacoesRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(TimeDetailUiState())
    val uiState: StateFlow<TimeDetailUiState> = _uiState.asStateFlow()

    private var idCarregado: Int? = null

    fun carregar(id: Int) {
        if (idCarregado == id) return
        idCarregado = id

        viewModelScope.launch {
            _uiState.update { it.copy(carregando = true, erro = null) }
            val resultado = timesRepository.detalheTime(id)
            resultado.fold(
                onSuccess = { time -> _uiState.update { it.copy(carregando = false, time = time) } },
                onFailure = { _uiState.update { it.copy(carregando = false, erro = "Não foi possível carregar o time.") } }
            )
        }
        carregarAnotacoes(id)
    }

    private fun carregarAnotacoes(timeId: Int) {
        viewModelScope.launch {
            val resultado = anotacoesRepository.listar(timeId)
            resultado.onSuccess { anotacoes -> _uiState.update { it.copy(anotacoes = anotacoes) } }
        }
    }

    fun salvarAnotacao(id: Int?, titulo: String, conteudo: String, categoria: String?) {
        val timeId = idCarregado ?: return
        viewModelScope.launch {
            _uiState.update { it.copy(salvandoAnotacao = true, erroAnotacao = null) }
            val input = AnotacaoTimeInput(timeId = timeId, titulo = titulo, conteudo = conteudo, categoria = categoria)
            val resultado = if (id == null) anotacoesRepository.criar(input) else anotacoesRepository.atualizar(id, input)
            resultado.fold(
                onSuccess = {
                    _uiState.update { it.copy(salvandoAnotacao = false) }
                    carregarAnotacoes(timeId)
                },
                onFailure = { _uiState.update { it.copy(salvandoAnotacao = false, erroAnotacao = "Não foi possível salvar a anotação.") } }
            )
        }
    }

    fun excluirAnotacao(id: Int) {
        val timeId = idCarregado ?: return
        viewModelScope.launch {
            val resultado = anotacoesRepository.excluir(id)
            resultado.onSuccess { carregarAnotacoes(timeId) }
        }
    }
}
