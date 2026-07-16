package com.comentarista.futebol.data.repository

import com.comentarista.futebol.data.remote.ApiService
import com.comentarista.futebol.data.remote.dto.RelatorioResumoDto
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class RelatoriosRepository @Inject constructor(
    private val apiService: ApiService
) {
    suspend fun resumo(
        temporada: Int? = null,
        competicaoId: Int? = null,
        incluirNaoAnalisados: Boolean = false
    ): Result<RelatorioResumoDto> = runCatching {
        apiService.relatorioResumo(
            temporada = temporada,
            competicaoIds = competicaoId?.let { listOf(it) },
            incluirNaoAnalisados = incluirNaoAnalisados
        )
    }
}
