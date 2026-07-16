package com.comentarista.futebol.data.repository

import com.comentarista.futebol.data.remote.ApiService
import com.comentarista.futebol.data.remote.dto.AnaliseJogoDto
import com.comentarista.futebol.data.remote.dto.CriterioNotaDto
import com.comentarista.futebol.data.remote.dto.NotaJogadorDto
import com.comentarista.futebol.data.remote.dto.PreenchimentoDto
import com.comentarista.futebol.data.remote.dto.SalvarNotaApiRequest
import com.comentarista.futebol.data.remote.dto.StatusAnaliseRequest
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class AnalisesRepository @Inject constructor(
    private val apiService: ApiService
) {
    suspend fun criterios(): Result<List<CriterioNotaDto>> = runCatching {
        apiService.criteriosAnalise()
    }

    suspend fun analiseDoJogo(jogoId: Int): Result<AnaliseJogoDto> = runCatching {
        apiService.analiseDoJogo(jogoId)
    }

    suspend fun salvarNota(jogoId: Int, request: SalvarNotaApiRequest): Result<NotaJogadorDto> = runCatching {
        apiService.salvarNota(jogoId, request)
    }

    suspend fun excluirNota(jogoId: Int, jogadorId: Int): Result<Unit> = runCatching {
        apiService.excluirNota(jogoId, jogadorId)
    }

    suspend fun atualizarStatus(jogoId: Int, analisado: Boolean, observacoes: String? = null): Result<AnaliseJogoDto> = runCatching {
        apiService.atualizarStatusAnalise(jogoId, StatusAnaliseRequest(analisado = analisado, observacoes = observacoes))
    }

    suspend fun preenchimento(jogoId: Int, jogadorId: Int): Result<PreenchimentoDto> = runCatching {
        apiService.preenchimentoJogador(jogoId, jogadorId)
    }
}
