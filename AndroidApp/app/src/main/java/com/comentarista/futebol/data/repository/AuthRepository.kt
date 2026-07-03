package com.comentarista.futebol.data.repository

import com.comentarista.futebol.data.local.SessaoUsuario
import com.comentarista.futebol.data.local.SessionManager
import com.comentarista.futebol.data.remote.ApiService
import com.comentarista.futebol.data.remote.dto.LoginRequest
import kotlinx.coroutines.flow.Flow
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class AuthRepository @Inject constructor(
    private val apiService: ApiService,
    private val sessionManager: SessionManager
) {
    val sessaoFlow: Flow<SessaoUsuario?> = sessionManager.sessaoFlow

    suspend fun login(username: String, password: String): Result<SessaoUsuario> = runCatching {
        val resposta = apiService.login(LoginRequest(username, password))
        sessionManager.salvarSessao(resposta)
        SessaoUsuario(
            token = resposta.token,
            userName = resposta.userName,
            nome = resposta.nome,
            isAdmin = resposta.isAdmin
        )
    }

    suspend fun logout() {
        sessionManager.limparSessao()
    }
}
