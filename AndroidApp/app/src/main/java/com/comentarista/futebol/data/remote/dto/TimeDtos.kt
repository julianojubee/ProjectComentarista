package com.comentarista.futebol.data.remote.dto

import kotlinx.serialization.Serializable

@Serializable
data class TimeResumoDto(
    val id: Int,
    val nome: String,
    val cidade: String? = null,
    val escudoUrl: String? = null,
    val ehSelecao: Boolean
)

@Serializable
data class TimeDetalheDto(
    val id: Int,
    val nome: String,
    val cidade: String? = null,
    val escudoUrl: String? = null,
    val ehSelecao: Boolean,
    val backgroundUrl: String? = null,
    val corPrincipal: String? = null,
    val corSecundaria: String? = null,
    val camisaUrl: String? = null,
    val camisaVisitanteUrl: String? = null,
    val linkTransfermarket: String? = null
)
