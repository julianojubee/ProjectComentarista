package com.comentarista.futebol.data.remote.dto

import kotlinx.serialization.Serializable

@Serializable
data class JogoResumoDto(
    val id: Int,
    val data: String? = null,
    val competicaoId: Int,
    val competicaoNome: String? = null,
    val timeCasaId: Int,
    val timeCasaNome: String,
    val timeCasaEscudoUrl: String? = null,
    val timeVisitanteId: Int,
    val timeVisitanteNome: String,
    val timeVisitanteEscudoUrl: String? = null,
    val placarCasa: Int? = null,
    val placarVisitante: Int? = null,
    val status: String? = null
)

@Serializable
data class JogoDetalheDto(
    val id: Int,
    val data: String? = null,
    val competicaoId: Int,
    val competicaoNome: String? = null,
    val timeCasaId: Int,
    val timeCasaNome: String,
    val timeCasaEscudoUrl: String? = null,
    val timeVisitanteId: Int,
    val timeVisitanteNome: String,
    val timeVisitanteEscudoUrl: String? = null,
    val placarCasa: Int? = null,
    val placarVisitante: Int? = null,
    val status: String? = null,
    val estadio: String? = null,
    val arbitro: String? = null,
    val penaltisCasa: Int? = null,
    val penaltisVisitante: Int? = null
)
