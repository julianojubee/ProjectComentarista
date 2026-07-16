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
    val status: String? = null,
    val analisadoPorMim: Boolean = false
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
    val analisadoPorMim: Boolean = false,
    val estadio: String? = null,
    val arbitro: String? = null,
    val penaltisCasa: Int? = null,
    val penaltisVisitante: Int? = null,
    val escalacaoCasa: List<EscalacaoJogadorDto> = emptyList(),
    val escalacaoVisitante: List<EscalacaoJogadorDto> = emptyList(),
    val gols: List<GolJogoDto> = emptyList(),
    val cartoes: List<CartaoJogoDto> = emptyList(),
    val estatisticasTimes: List<EstatisticaTimeJogoDto> = emptyList()
)

@Serializable
data class EscalacaoJogadorDto(
    val jogadorId: Int,
    val nome: String,
    val posicao: String? = null,
    val titular: Boolean = false,
    val fotoUrl: String? = null
)

@Serializable
data class GolJogoDto(
    val jogadorId: Int,
    val jogadorNome: String,
    val minuto: Int,
    val contra: Boolean = false
)

@Serializable
data class CartaoJogoDto(
    val jogadorId: Int,
    val jogadorNome: String,
    val minuto: Int,
    val tipo: String
)

@Serializable
data class EstatisticaTimeJogoDto(
    val nome: String,
    val valorCasa: String? = null,
    val valorVisitante: String? = null
)
