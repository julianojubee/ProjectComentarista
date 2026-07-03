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
    suspend fun listarJogos(page: Int = 1): Result<List<JogoResumoDto>> = runCatching {
        apiService.listarJogos(page = page)
    }

    suspend fun detalheJogo(id: Int): Result<JogoDetalheDto> = runCatching {
        apiService.detalheJogo(id)
    }
}
