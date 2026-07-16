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
import java.time.LocalDate
import java.time.ZoneId
import javax.inject.Inject

data class JogosListUiState(
    val carregando: Boolean = true,
    val jogos: List<JogoResumoDto> = emptyList(),
    // Dia filtrado (fuso do Brasil, como /Jogos/Hoje na web); null = listagem geral
    val dia: LocalDate? = null,
    val erro: String? = null
) {
    val ehHoje: Boolean get() = dia == LocalDate.now(FUSO_BRASIL)

    companion object {
        val FUSO_BRASIL: ZoneId = ZoneId.of("America/Sao_Paulo")
    }
}

@HiltViewModel
class JogosListViewModel @Inject constructor(
    private val jogosRepository: JogosRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(JogosListUiState())
    val uiState: StateFlow<JogosListUiState> = _uiState.asStateFlow()

    init {
        // Abre direto nos jogos de hoje — mesmo papel do painel /Jogos/Hoje da web
        mostrarHoje()
    }

    fun mostrarTodos() {
        _uiState.update { it.copy(dia = null) }
        carregarJogos()
    }

    fun mostrarHoje() {
        _uiState.update { it.copy(dia = LocalDate.now(JogosListUiState.FUSO_BRASIL)) }
        carregarJogos()
    }

    fun mudarDia(dias: Long) {
        val atual = _uiState.value.dia ?: return
        _uiState.update { it.copy(dia = atual.plusDays(dias)) }
        carregarJogos()
    }

    fun carregarJogos() {
        viewModelScope.launch {
            _uiState.update { it.copy(carregando = true, erro = null) }
            val dia = _uiState.value.dia
            val resultado = jogosRepository.listarJogos(data = dia?.toString())
            resultado.fold(
                onSuccess = { jogos -> _uiState.update { it.copy(carregando = false, jogos = jogos) } },
                onFailure = { _uiState.update { it.copy(carregando = false, erro = "Não foi possível carregar os jogos.") } }
            )
        }
    }
}
