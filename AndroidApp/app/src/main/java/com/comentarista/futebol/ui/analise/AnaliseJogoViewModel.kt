package com.comentarista.futebol.ui.analise

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.comentarista.futebol.data.remote.dto.CriterioNotaDto
import com.comentarista.futebol.data.remote.dto.JogoDetalheDto
import com.comentarista.futebol.data.remote.dto.NotaDetalheDto
import com.comentarista.futebol.data.remote.dto.SalvarNotaApiRequest
import com.comentarista.futebol.data.repository.AnalisesRepository
import com.comentarista.futebol.data.repository.JogosRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject
import kotlin.math.max
import kotlin.math.round

// NotaBaseFixa/NotaMinima do CriteriosNotaHelper (backend) — ambas 4.0. Mesma
// fórmula usada para a nota "ao vivo" no cliente, antes de salvar.
private const val NOTA_BASE_FIXA = 4.0
private const val NOTA_MINIMA = 4.0

data class RascunhoNota(
    // AcaoId -> quantidade
    val quantidades: Map<String, Int> = emptyMap(),
    val notaManualTexto: String = "",
    val comentario: String = "",
    // true quando o rascunho ainda não tem diferença em relação ao que veio do servidor
    val salvo: Boolean = true
) {
    val notaManual: Double? get() = notaManualTexto.replace(',', '.').toDoubleOrNull()

    fun total(criterios: List<CriterioNotaDto>): Double {
        val pesoPorAcao = criterios.associateBy({ it.acaoId }, { it.peso })
        return quantidades.entries.sumOf { (acaoId, qtd) -> qtd * (pesoPorAcao[acaoId] ?: 0.0) }
    }

    fun notaFinal(criterios: List<CriterioNotaDto>): Double {
        val manual = notaManual
        if (manual != null) return arredondar(manual.coerceIn(0.0, 10.0))
        val bruta = NOTA_BASE_FIXA + total(criterios)
        return arredondar(max(NOTA_MINIMA, bruta.coerceAtMost(10.0)))
    }

    private fun arredondar(v: Double): Double = round(v * 100) / 100
}

data class AnaliseJogoUiState(
    val carregando: Boolean = true,
    val jogo: JogoDetalheDto? = null,
    val criterios: List<CriterioNotaDto> = emptyList(),
    val analisadoPorMim: Boolean = false,
    val observacoesGerais: String? = null,
    // JogadorId -> rascunho (jogadores sem rascunho ainda não têm entrada aqui)
    val rascunhos: Map<Int, RascunhoNota> = emptyMap(),
    val jogadorSelecionadoId: Int? = null,
    val salvando: Boolean = false,
    val erro: String? = null
)

@HiltViewModel
class AnaliseJogoViewModel @Inject constructor(
    private val jogosRepository: JogosRepository,
    private val analisesRepository: AnalisesRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(AnaliseJogoUiState())
    val uiState: StateFlow<AnaliseJogoUiState> = _uiState.asStateFlow()

    private var jogoIdCarregado: Int? = null

    fun carregar(jogoId: Int) {
        if (jogoIdCarregado == jogoId) return
        jogoIdCarregado = jogoId

        viewModelScope.launch {
            _uiState.update { AnaliseJogoUiState(carregando = true) }

            val jogoResult = jogosRepository.detalheJogo(jogoId)
            val criteriosResult = analisesRepository.criterios()
            val analiseResult = analisesRepository.analiseDoJogo(jogoId)

            if (jogoResult.isFailure) {
                _uiState.update { it.copy(carregando = false, erro = "Não foi possível carregar o jogo.") }
                return@launch
            }

            val criterios = criteriosResult.getOrDefault(emptyList())
            val analise = analiseResult.getOrNull()

            val rascunhos = analise?.notas.orEmpty().associate { nota ->
                nota.jogadorId to RascunhoNota(
                    quantidades = nota.detalhes.associate { it.acaoId to it.quantidade },
                    notaManualTexto = nota.notaManual?.toString() ?: "",
                    comentario = nota.comentario,
                    salvo = true
                )
            }

            _uiState.update {
                it.copy(
                    carregando = false,
                    jogo = jogoResult.getOrNull(),
                    criterios = criterios,
                    analisadoPorMim = analise?.analisadoPorMim ?: false,
                    observacoesGerais = analise?.observacoes,
                    rascunhos = rascunhos
                )
            }
        }
    }

    fun selecionarJogador(jogadorId: Int) {
        _uiState.update { it.copy(jogadorSelecionadoId = jogadorId) }
    }

    fun incrementar(jogadorId: Int, acaoId: String) = alterarQuantidade(jogadorId, acaoId, +1)

    fun decrementar(jogadorId: Int, acaoId: String) = alterarQuantidade(jogadorId, acaoId, -1)

    private fun alterarQuantidade(jogadorId: Int, acaoId: String, delta: Int) {
        atualizarRascunho(jogadorId) { rascunho ->
            val atual = rascunho.quantidades[acaoId] ?: 0
            val nova = (atual + delta).coerceAtLeast(0)
            rascunho.copy(
                quantidades = rascunho.quantidades + (acaoId to nova),
                salvo = false
            )
        }
    }

    fun setNotaManual(jogadorId: Int, texto: String) {
        atualizarRascunho(jogadorId) { it.copy(notaManualTexto = texto, salvo = false) }
    }

    fun setComentario(jogadorId: Int, texto: String) {
        atualizarRascunho(jogadorId) { it.copy(comentario = texto, salvo = false) }
    }

    fun preencherDasEstatisticas(jogoId: Int, jogadorId: Int) {
        viewModelScope.launch {
            analisesRepository.preenchimento(jogoId, jogadorId).onSuccess { preenchimento ->
                if (!preenchimento.encontrado) return@onSuccess
                atualizarRascunho(jogadorId) { rascunho ->
                    rascunho.copy(quantidades = preenchimento.quantidadesPorAcao, salvo = false)
                }
            }
        }
    }

    fun salvarJogador(jogoId: Int, jogadorId: Int) {
        val rascunho = _uiState.value.rascunhos[jogadorId] ?: return
        val criterios = _uiState.value.criterios

        viewModelScope.launch {
            _uiState.update { it.copy(salvando = true, erro = null) }

            val detalhes = rascunho.quantidades
                .filterValues { it > 0 }
                .mapNotNull { (acaoId, qtd) ->
                    val criterio = criterios.find { it.acaoId == acaoId } ?: return@mapNotNull null
                    NotaDetalheDto(
                        acaoId = acaoId,
                        acaoLabel = criterio.label,
                        quantidade = qtd,
                        peso = criterio.peso
                    )
                }

            val request = SalvarNotaApiRequest(
                jogadorId = jogadorId,
                total = rascunho.total(criterios),
                observacao = rascunho.comentario.ifBlank { null },
                notaManual = rascunho.notaManual,
                detalhes = detalhes
            )

            analisesRepository.salvarNota(jogoId, request).fold(
                onSuccess = {
                    atualizarRascunho(jogadorId) { it.copy(salvo = true) }
                    _uiState.update { it.copy(salvando = false) }
                },
                onFailure = {
                    _uiState.update { it.copy(salvando = false, erro = "Não foi possível salvar a nota.") }
                }
            )
        }
    }

    fun excluirNota(jogoId: Int, jogadorId: Int) {
        viewModelScope.launch {
            analisesRepository.excluirNota(jogoId, jogadorId).fold(
                onSuccess = {
                    _uiState.update { it.copy(rascunhos = it.rascunhos - jogadorId) }
                },
                onFailure = {
                    _uiState.update { it.copy(erro = "Não foi possível excluir a nota.") }
                }
            )
        }
    }

    fun alternarAnalisado(jogoId: Int, analisado: Boolean) {
        viewModelScope.launch {
            analisesRepository.atualizarStatus(jogoId, analisado, _uiState.value.observacoesGerais).fold(
                onSuccess = { resultado ->
                    _uiState.update { it.copy(analisadoPorMim = resultado.analisadoPorMim) }
                },
                onFailure = {
                    _uiState.update { it.copy(erro = "Não foi possível atualizar o status do jogo.") }
                }
            )
        }
    }

    fun setObservacoesGerais(jogoId: Int, texto: String) {
        _uiState.update { it.copy(observacoesGerais = texto) }
        viewModelScope.launch {
            analisesRepository.atualizarStatus(jogoId, _uiState.value.analisadoPorMim, texto)
        }
    }

    private inline fun atualizarRascunho(jogadorId: Int, transform: (RascunhoNota) -> RascunhoNota) {
        _uiState.update { estado ->
            val atual = estado.rascunhos[jogadorId] ?: RascunhoNota()
            estado.copy(rascunhos = estado.rascunhos + (jogadorId to transform(atual)))
        }
    }
}
