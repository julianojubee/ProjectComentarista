package com.comentarista.futebol.data.remote.dto

import kotlinx.serialization.Serializable

@Serializable
data class CompeticaoDto(
    val id: Int,
    val nome: String,
    val regiao: String,
    val tipo: String,
    val ehSelecaoNacional: Boolean,
    val topTier: Boolean,
    val logoUrl: String? = null
)
