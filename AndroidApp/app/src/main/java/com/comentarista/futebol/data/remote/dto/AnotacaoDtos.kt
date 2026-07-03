package com.comentarista.futebol.data.remote.dto

import kotlinx.serialization.Serializable

@Serializable
data class AnotacaoTimeDto(
    val id: Int,
    val timeId: Int,
    val timeNome: String? = null,
    val titulo: String,
    val conteudo: String,
    val categoria: String? = null,
    val dtInc: String,
    val dtAlt: String? = null
)

@Serializable
data class AnotacaoTimeInput(
    val timeId: Int,
    val titulo: String,
    val conteudo: String,
    val categoria: String? = null
)
