package com.comentarista.futebol.ui.competicoes

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.comentarista.futebol.data.remote.dto.ClassificacaoDto
import com.comentarista.futebol.data.remote.dto.JogoResumoDto
import com.comentarista.futebol.data.repository.CompeticoesRepository
import com.comentarista.futebol.data.repository.JogosRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class CompeticaoDetailUiState(
    val carregando: Boolean = false,
    val classificacao: ClassificacaoDto? = null,
    val temporada: Int? = null,
    val jogos: List<JogoResumoDto> = emptyList(),
    val erro: String? = null
)

@HiltViewModel
class CompeticaoDetailViewModel @Inject constructor(
    private val jogosRepository: JogosRepository,
    private val competicoesRepository: CompeticoesRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(CompeticaoDetailUiState())
    val uiState: StateFlow<CompeticaoDetailUiState> = _uiState.asStateFlow()

    private var idCarregado: Int? = null

    fun carregar(competicaoId: Int) {
        if (idCarregado == competicaoId) return
        idCarregado = competicaoId
        _uiState.update { CompeticaoDetailUiState(carregando = true) }

        viewModelScope.launch {
            // Classificação e jogos em paralelo (cada um atualiza sua fatia do estado)
            launch {
                competicoesRepository.classificacao(competicaoId).fold(
                    onSuccess = { c -> _uiState.update { it.copy(carregando = false, classificacao = c, temporada = c.temporada) } },
                    onFailure = { _uiState.update { it.copy(carregando = false, erro = "Não foi possível carregar a competição.") } }
                )
            }
            launch {
                jogosRepository.listarJogos(competicaoId = competicaoId).fold(
                    onSuccess = { jogos -> _uiState.update { it.copy(jogos = jogos) } },
                    onFailure = { /* aba de jogos fica vazia; classificação segue */ }
                )
            }
        }
    }

    fun onTemporadaSelecionada(temporada: Int) {
        val id = idCarregado ?: return
        if (temporada == _uiState.value.temporada) return
        viewModelScope.launch {
            _uiState.update { it.copy(carregando = true, temporada = temporada) }
            competicoesRepository.classificacao(id, temporada).fold(
                onSuccess = { c -> _uiState.update { it.copy(carregando = false, classificacao = c) } },
                onFailure = { _uiState.update { it.copy(carregando = false) } }
            )
        }
    }
}
