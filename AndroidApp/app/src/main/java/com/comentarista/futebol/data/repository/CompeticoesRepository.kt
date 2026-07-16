package com.comentarista.futebol.data.repository

import com.comentarista.futebol.data.remote.ApiService
import com.comentarista.futebol.data.remote.dto.ClassificacaoDto
import com.comentarista.futebol.data.remote.dto.CompeticaoDto
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class CompeticoesRepository @Inject constructor(
    private val apiService: ApiService
) {
    suspend fun listarCompeticoes(): Result<List<CompeticaoDto>> = runCatching {
        apiService.listarCompeticoes()
    }

    suspend fun classificacao(id: Int, temporada: Int? = null): Result<ClassificacaoDto> = runCatching {
        apiService.classificacaoCompeticao(id, temporada)
    }
}
