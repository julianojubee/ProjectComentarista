package com.comentarista.futebol.data.remote.dto

import kotlinx.serialization.Serializable

@Serializable
data class JogadorResumoDto(
    val id: Int,
    val nome: String,
    val nomeExibicao: String,
    val posicao: String,
    val idade: Int,
    val numeroCamisa: Int? = null,
    val nacionalidadeNome: String? = null,
    val timeId: Int,
    val timeNome: String? = null,
    val fotoUrl: String? = null
)

@Serializable
data class JogadorDetalheDto(
    val id: Int,
    val nome: String,
    val nomeExibicao: String,
    val posicao: String,
    val idade: Int,
    val numeroCamisa: Int? = null,
    val nacionalidadeNome: String? = null,
    val timeId: Int,
    val timeNome: String? = null,
    val fotoUrl: String? = null,
    val dataNascimento: String? = null,
    val selecaoId: Int? = null,
    val selecaoNome: String? = null,
    val linkTransfermarket: String? = null,
    val observacoes: String? = null
)
