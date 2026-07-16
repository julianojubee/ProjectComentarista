package com.comentarista.futebol.ui.relatorios

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.comentarista.futebol.data.remote.dto.RelatorioResumoDto
import com.comentarista.futebol.data.repository.RelatoriosRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class RelatoriosUiState(
    val carregando: Boolean = true,
    val resumo: RelatorioResumoDto? = null,
    // Filtros ativos. temporada=null usa o padrão do servidor (a mais recente).
    val temporada: Int? = null,
    val competicaoId: Int? = null,
    val incluirNaoAnalisados: Boolean = false,
    val erro: String? = null
)

@HiltViewModel
class RelatoriosViewModel @Inject constructor(
    private val relatoriosRepository: RelatoriosRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(RelatoriosUiState())
    val uiState: StateFlow<RelatoriosUiState> = _uiState.asStateFlow()

    init {
        carregar()
    }

    fun onTemporadaSelecionada(temporada: Int?) {
        _uiState.update { it.copy(temporada = temporada) }
        carregar()
    }

    fun onCompeticaoSelecionada(competicaoId: Int?) {
        // Trocar de competição reseta a temporada: o servidor escolhe a mais
        // recente daquela competição (as temporadas disponíveis mudam junto).
        _uiState.update { it.copy(competicaoId = competicaoId, temporada = null) }
        carregar()
    }

    fun onIncluirNaoAnalisadosChange(incluir: Boolean) {
        _uiState.update { it.copy(incluirNaoAnalisados = incluir) }
        carregar()
    }

    fun carregar() {
        viewModelScope.launch {
            _uiState.update { it.copy(carregando = true, erro = null) }
            val estado = _uiState.value
            val resultado = relatoriosRepository.resumo(
                temporada = estado.temporada,
                competicaoId = estado.competicaoId,
                incluirNaoAnalisados = estado.incluirNaoAnalisados
            )
            resultado.fold(
                onSuccess = { resumo ->
                    _uiState.update {
                        // Fixa a temporada devolvida pelo servidor para o dropdown
                        // refletir o padrão (mais recente) já na primeira carga.
                        it.copy(carregando = false, resumo = resumo, temporada = resumo.temporada)
                    }
                },
                onFailure = {
                    _uiState.update { it.copy(carregando = false, erro = "Não foi possível carregar os relatórios.") }
                }
            )
        }
    }
}
