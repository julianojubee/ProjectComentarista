package com.comentarista.futebol.data.repository

import com.comentarista.futebol.data.remote.ApiService
import com.comentarista.futebol.data.remote.dto.TimeDetalheDto
import com.comentarista.futebol.data.remote.dto.TimeResumoDto
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class TimesRepository @Inject constructor(
    private val apiService: ApiService
) {
    suspend fun listarTimes(nome: String? = null): Result<List<TimeResumoDto>> = runCatching {
        apiService.listarTimes(nome)
    }

    suspend fun detalheTime(id: Int): Result<TimeDetalheDto> = runCatching {
        apiService.detalheTime(id)
    }
}
