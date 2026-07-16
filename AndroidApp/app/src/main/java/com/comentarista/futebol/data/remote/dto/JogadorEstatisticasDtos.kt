package com.comentarista.futebol.data.remote.dto

import kotlinx.serialization.Serializable

// Espelha ControleFutebolWeb/Models/Api/JogadorEstatisticasDtos.cs
// (GET api/v1/jogadores/{id}/estatisticas).

@Serializable
data class JogadorEstatisticasDto(
    val jogadorId: Int,
    val nome: String,
    val competicaoIdFiltro: Int? = null,
    val competicoes: List<CompeticaoRefDto> = emptyList(),
    val partidas: Int = 0,
    val gols: Int = 0,
    val assistencias: Int = 0,
    val cartoesAmarelos: Int = 0,
    val cartoesVermelhos: Int = 0,
    val vitorias: Int = 0,
    val empates: Int = 0,
    val derrotas: Int = 0,
    val notaMedia: Double? = null,
    val jogos: List<JogadorJogoItemDto> = emptyList()
)

@Serializable
data class CompeticaoRefDto(
    val id: Int,
    val nome: String
)

@Serializable
data class JogadorJogoItemDto(
    val jogoId: Int,
    val data: String? = null,
    val competicaoNome: String? = null,
    val isCasa: Boolean = false,
    val adversarioNome: String = "",
    val adversarioEscudoUrl: String? = null,
    val golsPro: Int = 0,
    val golsContra: Int = 0,
    val resultado: String = "?",
    val posicao: String? = null,
    val minutos: Int? = null,
    val gols: Int = 0,
    val assistencias: Int = 0,
    val cartoesAmarelos: Int = 0,
    val cartoesVermelhos: Int = 0,
    val analisado: Boolean = false,
    val notaFinal: Double? = null,
    val origemManual: Boolean = false
)
