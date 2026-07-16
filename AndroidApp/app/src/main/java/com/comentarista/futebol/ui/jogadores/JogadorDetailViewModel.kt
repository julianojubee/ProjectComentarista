package com.comentarista.futebol.ui.jogadores

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.comentarista.futebol.data.remote.dto.JogadorDetalheDto
import com.comentarista.futebol.data.remote.dto.JogadorEstatisticasDto
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
    // Estatísticas carregam em paralelo ao cadastro; a tela mostra o cadastro
    // assim que chega e a seção de estatísticas quando esta terminar.
    val carregandoEstatisticas: Boolean = false,
    val estatisticas: JogadorEstatisticasDto? = null,
    val competicaoIdFiltro: Int? = null,
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
            _uiState.update { it.copy(carregando = true, erro = null, estatisticas = null, competicaoIdFiltro = null) }
            val resultado = jogadoresRepository.detalheJogador(id)
            resultado.fold(
                onSuccess = { jogador -> _uiState.update { it.copy(carregando = false, jogador = jogador) } },
                onFailure = { _uiState.update { it.copy(carregando = false, erro = "Não foi possível carregar o jogador.") } }
            )
        }
        carregarEstatisticas(id, competicaoId = null)
    }

    fun filtrarPorCompeticao(competicaoId: Int?) {
        val id = idCarregado ?: return
        _uiState.update { it.copy(competicaoIdFiltro = competicaoId) }
        carregarEstatisticas(id, competicaoId)
    }

    private fun carregarEstatisticas(id: Int, competicaoId: Int?) {
        viewModelScope.launch {
            _uiState.update { it.copy(carregandoEstatisticas = true) }
            val resultado = jogadoresRepository.estatisticasJogador(id, competicaoId)
            resultado.fold(
                // Falha nas estatísticas não derruba a tela: o cadastro continua
                // visível e a seção simplesmente não aparece.
                onSuccess = { est -> _uiState.update { it.copy(carregandoEstatisticas = false, estatisticas = est) } },
                onFailure = { _uiState.update { it.copy(carregandoEstatisticas = false) } }
            )
        }
    }
}
