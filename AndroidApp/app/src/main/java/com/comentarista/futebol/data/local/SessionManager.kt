package com.comentarista.futebol.data.local

import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import com.comentarista.futebol.data.remote.dto.LoginResponse
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

data class SessaoUsuario(
    val token: String,
    val userName: String,
    val nome: String,
    val isAdmin: Boolean
)

// Guarda a sessão (token JWT + dados básicos do usuário) localmente via DataStore,
// para o app não pedir login toda vez que abrir.
@Singleton
class SessionManager @Inject constructor(
    private val dataStore: DataStore<Preferences>
) {
    private object Keys {
        val TOKEN = stringPreferencesKey("token")
        val USER_NAME = stringPreferencesKey("user_name")
        val NOME = stringPreferencesKey("nome")
        val IS_ADMIN = booleanPreferencesKey("is_admin")
    }

    val sessaoFlow: Flow<SessaoUsuario?> = dataStore.data.map { prefs ->
        val token = prefs[Keys.TOKEN] ?: return@map null
        SessaoUsuario(
            token = token,
            userName = prefs[Keys.USER_NAME].orEmpty(),
            nome = prefs[Keys.NOME].orEmpty(),
            isAdmin = prefs[Keys.IS_ADMIN] ?: false
        )
    }

    suspend fun tokenAtual(): String? = dataStore.data.first()[Keys.TOKEN]

    suspend fun salvarSessao(login: LoginResponse) {
        dataStore.edit { prefs ->
            prefs[Keys.TOKEN] = login.token
            prefs[Keys.USER_NAME] = login.userName
            prefs[Keys.NOME] = login.nome
            prefs[Keys.IS_ADMIN] = login.isAdmin
        }
    }

    suspend fun limparSessao() {
        dataStore.edit { it.clear() }
    }
}
