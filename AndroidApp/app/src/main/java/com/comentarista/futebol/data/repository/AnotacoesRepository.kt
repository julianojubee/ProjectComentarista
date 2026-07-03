package com.comentarista.futebol.data.repository

import com.comentarista.futebol.data.remote.ApiService
import com.comentarista.futebol.data.remote.dto.AnotacaoTimeDto
import com.comentarista.futebol.data.remote.dto.AnotacaoTimeInput
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class AnotacoesRepository @Inject constructor(
    private val apiService: ApiService
) {
    suspend fun listar(timeId: Int): Result<List<AnotacaoTimeDto>> = runCatching {
        apiService.listarAnotacoes(timeId)
    }

    suspend fun criar(input: AnotacaoTimeInput): Result<AnotacaoTimeDto> = runCatching {
        apiService.criarAnotacao(input)
    }

    suspend fun atualizar(id: Int, input: AnotacaoTimeInput): Result<AnotacaoTimeDto> = runCatching {
        apiService.atualizarAnotacao(id, input)
    }

    suspend fun excluir(id: Int): Result<Unit> = runCatching {
        apiService.excluirAnotacao(id)
    }
}
