package com.comentarista.futebol.data.repository

import com.comentarista.futebol.data.remote.ApiService
import com.comentarista.futebol.data.remote.dto.JogoDetalheDto
import com.comentarista.futebol.data.remote.dto.JogoResumoDto
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class JogosRepository @Inject constructor(
    private val apiService: ApiService
) {
    // data (yyyy-MM-dd): jogos daquele dia no fuso do Brasil; null = listagem geral
    suspend fun listarJogos(
        data: String? = null,
        competicaoId: Int? = null,
        page: Int = 1
    ): Result<List<JogoResumoDto>> = runCatching {
        apiService.listarJogos(competicaoId = competicaoId, data = data, page = page)
    }

    suspend fun detalheJogo(id: Int): Result<JogoDetalheDto> = runCatching {
        apiService.detalheJogo(id)
    }
}
