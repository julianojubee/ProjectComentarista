package com.comentarista.futebol.ui.login

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.comentarista.futebol.data.repository.AuthRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class LoginUiState(
    val username: String = "",
    val password: String = "",
    val verificandoSessao: Boolean = true,
    val carregando: Boolean = false,
    val erro: String? = null,
    val logado: Boolean = false
)

@HiltViewModel
class LoginViewModel @Inject constructor(
    private val authRepository: AuthRepository
) : ViewModel() {

    private val _uiState = MutableStateFlow(LoginUiState())
    val uiState: StateFlow<LoginUiState> = _uiState.asStateFlow()

    init {
        // Se já existe uma sessão salva (token de login anterior), pula a tela de
        // login e vai direto para a lista de jogos.
        viewModelScope.launch {
            val sessaoExistente = authRepository.sessaoFlow.first()
            _uiState.update { it.copy(verificandoSessao = false, logado = sessaoExistente != null) }
        }
    }

    fun onUsernameChange(value: String) {
        _uiState.update { it.copy(username = value, erro = null) }
    }

    fun onPasswordChange(value: String) {
        _uiState.update { it.copy(password = value, erro = null) }
    }

    fun onLoginClick() {
        val estado = _uiState.value
        if (estado.username.isBlank() || estado.password.isBlank()) {
            _uiState.update { it.copy(erro = "Preencha usuário e senha.") }
            return
        }

        viewModelScope.launch {
            _uiState.update { it.copy(carregando = true, erro = null) }
            val resultado = authRepository.login(estado.username, estado.password)
            resultado.fold(
                onSuccess = { _uiState.update { it.copy(carregando = false, logado = true) } },
                onFailure = { _uiState.update { it.copy(carregando = false, erro = "Usuário ou senha inválidos.") } }
            )
        }
    }
}
