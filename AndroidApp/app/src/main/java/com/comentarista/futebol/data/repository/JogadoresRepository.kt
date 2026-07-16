package com.comentarista.futebol.data.repository

import com.comentarista.futebol.data.remote.ApiService
import com.comentarista.futebol.data.remote.dto.JogadorDetalheDto
import com.comentarista.futebol.data.remote.dto.JogadorEstatisticasDto
import com.comentarista.futebol.data.remote.dto.JogadorResumoDto
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class JogadoresRepository @Inject constructor(
    private val apiService: ApiService
) {
    suspend fun listarJogadores(timeId: Int? = null, page: Int = 1): Result<List<JogadorResumoDto>> = runCatching {
        apiService.listarJogadores(timeId = timeId, page = page)
    }

    suspend fun detalheJogador(id: Int): Result<JogadorDetalheDto> = runCatching {
        apiService.detalheJogador(id)
    }

    suspend fun estatisticasJogador(id: Int, competicaoId: Int? = null): Result<JogadorEstatisticasDto> = runCatching {
        apiService.estatisticasJogador(id, competicaoId)
    }
}
