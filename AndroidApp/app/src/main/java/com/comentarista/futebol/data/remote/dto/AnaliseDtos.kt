package com.comentarista.futebol.data.remote.dto

import kotlinx.serialization.Serializable

// Espelha ControleFutebolWeb/Models/Api/AnaliseDtos.cs (api/v1/analises/*).

@Serializable
data class CriterioNotaDto(
    val acaoId: String,
    val label: String,
    val peso: Double,
    val ordem: Int = 0
)

@Serializable
data class AnaliseJogoDto(
    val jogoId: Int,
    val analisadoPorMim: Boolean = false,
    val observacoes: String? = null,
    val notas: List<NotaJogadorDto> = emptyList()
)

@Serializable
data class NotaJogadorDto(
    val jogadorId: Int,
    val total: Double = 0.0,
    val notaManual: Double? = null,
    val comentario: String = "",
    val notaFinal: Double = 0.0,
    val detalhes: List<NotaDetalheDto> = emptyList()
)

@Serializable
data class NotaDetalheDto(
    val acaoId: String,
    val acaoLabel: String,
    val quantidade: Int,
    val peso: Double
)

@Serializable
data class SalvarNotaApiRequest(
    val jogadorId: Int,
    val total: Double,
    val observacao: String? = null,
    val notaManual: Double? = null,
    val detalhes: List<NotaDetalheDto> = emptyList()
)

@Serializable
data class StatusAnaliseRequest(
    val analisado: Boolean,
    val observacoes: String? = null
)

@Serializable
data class PreenchimentoDto(
    val encontrado: Boolean = false,
    val minutos: Int? = null,
    val rating: Double? = null,
    val quantidadesPorAcao: Map<String, Int> = emptyMap()
)
