package com.comentarista.futebol.data.repository

import com.comentarista.futebol.data.remote.ApiService
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
}
