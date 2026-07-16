package com.comentarista.futebol.data.remote.dto

import kotlinx.serialization.Serializable

// Espelha ControleFutebolWeb/Models/Api/ClassificacaoDtos.cs
// (GET api/v1/competicoes/{id}/classificacao).

@Serializable
data class ClassificacaoDto(
    val competicaoId: Int,
    val competicaoNome: String,
    val temporada: Int? = null,
    val temporadasDisponiveis: List<Int> = emptyList(),
    val tabela: List<ClassificacaoLinhaDto> = emptyList(),
    val artilheiros: List<ArtilheiroDto> = emptyList()
)

@Serializable
data class ClassificacaoLinhaDto(
    val posicao: Int,
    val timeId: Int,
    val timeNome: String,
    val escudoUrl: String? = null,
    val pontos: Int,
    val jogos: Int,
    val vitorias: Int,
    val empates: Int,
    val derrotas: Int,
    val golsPro: Int,
    val golsContra: Int,
    val saldoGols: Int
)

@Serializable
data class ArtilheiroDto(
    val jogadorId: Int,
    val nome: String,
    val timeNome: String? = null,
    val fotoUrl: String? = null,
    val gols: Int
)
