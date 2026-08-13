package com.comentarista.futebol.ui.analise

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.comentarista.futebol.data.remote.dto.ContextoNotaDto
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
import kotlin.math.round

// Peso inicial (nota base) do usuário, configurável em /CriteriosNota na web e
// entregue pelo endpoint de critérios como o item "peso_inicial". Mesma fórmula do
// CriteriosNotaHelper do backend: nota = base + ações, e sobre isso os mesmos ajustes
// da web, na mesma ordem — piso de merecimento, piso de participação curta e bônus de
// goleiro decisivo.
private const val ACAO_PESO_INICIAL = "peso_inicial"
private const val NOTA_BASE_PADRAO = 4.0
private const val PISO_JOGO_POSITIVO = 6.0
private const val PISO_JOGO_EQUILIBRADO = 5.0
private const val MINUTOS_PARTICIPACAO_CURTA = 45
private const val PISO_PARTICIPACAO_CURTA = 5.0
private const val BONUS_GOLEIRO_DECISIVO = 2.0
private const val TETO_GOLEIRO_DECISIVO = 7.0
private const val BONUS_GOL_DA_VITORIA = 1.0

fun notaBaseDe(criterios: List<CriterioNotaDto>): Double =
    criterios.find { it.acaoId == ACAO_PESO_INICIAL }?.peso?.takeIf { it in 0.0..10.0 }
        ?: NOTA_BASE_PADRAO

fun apenasAcoes(criterios: List<CriterioNotaDto>): List<CriterioNotaDto> =
    criterios.filter { it.acaoId != ACAO_PESO_INICIAL }

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

    // Até onde a nota sobe pelo merecimento, como o CriteriosNotaHelper.PisoDeMerecimento:
    // conta os TIPOS de ação marcados (cada um vale 1, sem olhar quantidade nem peso).
    fun pisoDeMerecimento(criterios: List<CriterioNotaDto>): Double {
        val pesoPorAcao = criterios.associateBy({ it.acaoId }, { it.peso })
        val marcadas = quantidades.filterValues { it > 0 }.keys.mapNotNull { pesoPorAcao[it] }
        val verdes = marcadas.count { it > 0 }
        val vermelhas = marcadas.count { it < 0 }

        if (verdes == 0 && vermelhas == 0)
            return if (total(criterios) > 0) PISO_JOGO_POSITIVO else 0.0
        return when {
            verdes > vermelhas -> PISO_JOGO_POSITIVO
            verdes == vermelhas -> PISO_JOGO_EQUILIBRADO
            else -> 0.0
        }
    }

    fun notaFinal(
        criterios: List<CriterioNotaDto>,
        notaBase: Double = NOTA_BASE_PADRAO,
        contexto: ContextoNotaDto? = null
    ): Double {
        val manual = notaManual
        if (manual != null) return arredondar(manual.coerceIn(0.0, 10.0))

        val acoes = total(criterios)
        var bruta = notaBase + acoes

        val piso = pisoDeMerecimento(criterios)
        if (bruta < piso) bruta = piso

        val minutos = contexto?.minutos ?: 0
        if (minutos in 1 until MINUTOS_PARTICIPACAO_CURTA && bruta < PISO_PARTICIPACAO_CURTA)
            bruta = PISO_PARTICIPACAO_CURTA

        if (contexto?.goleiroDecisivo == true && bruta < TETO_GOLEIRO_DECISIVO)
            bruta += BONUS_GOLEIRO_DECISIVO

        if (contexto?.golDaVitoria == true) bruta += BONUS_GOL_DA_VITORIA

        return arredondar(bruta.coerceIn(0.0, 10.0))
    }

    private fun arredondar(v: Double): Double = round(v * 100) / 100
}

data class AnaliseJogoUiState(
    val carregando: Boolean = true,
    val jogo: JogoDetalheDto? = null,
    val criterios: List<CriterioNotaDto> = emptyList(),
    // Peso inicial do usuário; o item "peso_inicial" não entra em `criterios`.
    val notaBase: Double = NOTA_BASE_PADRAO,
    val analisadoPorMim: Boolean = false,
    val observacoesGerais: String? = null,
    // JogadorId -> rascunho (jogadores sem rascunho ainda não têm entrada aqui)
    val rascunhos: Map<Int, RascunhoNota> = emptyMap(),
    // JogadorId -> minutos e goleiro decisivo naquela partida
    val contextos: Map<Int, ContextoNotaDto> = emptyMap(),
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

            val criteriosApi = criteriosResult.getOrDefault(emptyList())
            val notaBase = notaBaseDe(criteriosApi)
            val criterios = apenasAcoes(criteriosApi)
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
                    notaBase = notaBase,
                    analisadoPorMim = analise?.analisadoPorMim ?: false,
                    observacoesGerais = analise?.observacoes,
                    rascunhos = rascunhos,
                    contextos = analise?.contextos.orEmpty().associateBy { it.jogadorId }
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
