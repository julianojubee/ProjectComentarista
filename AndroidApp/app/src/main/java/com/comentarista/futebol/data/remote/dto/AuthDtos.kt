package com.comentarista.futebol.data.remote.dto

import kotlinx.serialization.Serializable

@Serializable
data class LoginRequest(
    val username: String,
    val password: String
)

@Serializable
data class LoginResponse(
    val token: String,
    val expiresAtUtc: String,
    val userName: String,
    val nome: String,
    val isAdmin: Boolean
)
