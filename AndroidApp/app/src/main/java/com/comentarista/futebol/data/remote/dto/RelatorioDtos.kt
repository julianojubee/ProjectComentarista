package com.comentarista.futebol.data.remote.dto

import kotlinx.serialization.Serializable

// Espelha ControleFutebolWeb/Models/Api/RelatorioDtos.cs (GET api/v1/relatorios/resumo).

@Serializable
data class RelatorioResumoDto(
    val temporada: Int? = null,
    val temporadasDisponiveis: List<Int> = emptyList(),
    val competicoes: List<CompeticaoRefDto> = emptyList(),
    val exibirSelecao: Boolean = false,

    val totalJogos: Int = 0,
    val totalGols: Int = 0,
    val totalGolsContra: Int = 0,
    val totalCartaoAmarelo: Int = 0,
    val totalCartaoVermelho: Int = 0,

    val rankingNotas: List<RankingNotaDto> = emptyList(),
    val rankingGoleiros: List<RankingNotaDto> = emptyList(),
    val rankingDefensores: List<RankingNotaDto> = emptyList(),
    val rankingMeias: List<RankingNotaDto> = emptyList(),
    val rankingAtacantes: List<RankingNotaDto> = emptyList(),

    val artilheiros: List<JogadorValorDto> = emptyList(),
    val assistencias: List<JogadorValorDto> = emptyList(),
    val maisPartidas: List<JogadorValorDto> = emptyList(),
    val maisCartoesAmarelos: List<JogadorValorDto> = emptyList(),
    val maisCartoesVermelhos: List<JogadorValorDto> = emptyList(),

    val timesAproveitamento: List<TimeEstatisticaDto> = emptyList(),
    val timesMaisPontos: List<TimeEstatisticaDto> = emptyList(),
    val timesGols: List<TimeEstatisticaDto> = emptyList(),
    val timesMenosGolsSofridos: List<TimeEstatisticaDto> = emptyList(),

    val mediasPorPosicao: List<MediaPosicaoDto> = emptyList()
)

@Serializable
data class RankingNotaDto(
    val jogadorId: Int,
    val nome: String,
    val nomeExibicao: String = "",
    val posicao: String = "",
    val timeNome: String? = null,
    val fotoUrl: String? = null,
    val notaFinal: Double,
    val notaLabel: String = "",
    val notaColor: String = "",
    val partidas: Int = 0,
    val vitorias: Int = 0,
    val derrotas: Int = 0,
    val empates: Int = 0
)

@Serializable
data class JogadorValorDto(
    val jogadorId: Int,
    val nome: String,
    val nomeExibicao: String = "",
    val posicao: String = "",
    val timeNome: String? = null,
    val fotoUrl: String? = null,
    val valor: Int,
    val detalhe: String = ""
)

@Serializable
data class TimeEstatisticaDto(
    val timeId: Int,
    val nome: String,
    val escudoUrl: String? = null,
    val jogos: Int = 0,
    val vitorias: Int = 0,
    val empates: Int = 0,
    val derrotas: Int = 0,
    val golsPro: Int = 0,
    val golsContra: Int = 0,
    val saldoGols: Int = 0,
    val pontos: Int = 0,
    val aproveitamento: Double = 0.0
)

@Serializable
data class MediaPosicaoDto(
    val posicao: String,
    val media: Double,
    val totalJogadores: Int
)
