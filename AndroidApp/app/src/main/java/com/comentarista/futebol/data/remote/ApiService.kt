package com.comentarista.futebol.data.remote

import com.comentarista.futebol.data.remote.dto.AnotacaoTimeDto
import com.comentarista.futebol.data.remote.dto.AnotacaoTimeInput
import com.comentarista.futebol.data.remote.dto.CompeticaoDto
import com.comentarista.futebol.data.remote.dto.JogadorDetalheDto
import com.comentarista.futebol.data.remote.dto.JogadorResumoDto
import com.comentarista.futebol.data.remote.dto.JogoDetalheDto
import com.comentarista.futebol.data.remote.dto.JogoResumoDto
import com.comentarista.futebol.data.remote.dto.LoginRequest
import com.comentarista.futebol.data.remote.dto.LoginResponse
import com.comentarista.futebol.data.remote.dto.TimeDetalheDto
import com.comentarista.futebol.data.remote.dto.TimeResumoDto
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.PUT
import retrofit2.http.POST
import retrofit2.http.Path
import retrofit2.http.Query

// Espelha as rotas de ControleFutebolWeb/Controllers/Api/*.
interface ApiService {

    @POST("api/v1/auth/login")
    suspend fun login(@Body request: LoginRequest): LoginResponse

    @GET("api/v1/jogos")
    suspend fun listarJogos(
        @Query("competicaoId") competicaoId: Int? = null,
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 50
    ): List<JogoResumoDto>

    @GET("api/v1/jogos/{id}")
    suspend fun detalheJogo(@Path("id") id: Int): JogoDetalheDto

    @GET("api/v1/jogadores")
    suspend fun listarJogadores(
        @Query("timeId") timeId: Int? = null,
        @Query("posicao") posicao: String? = null,
        @Query("nome") nome: String? = null,
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 50
    ): List<JogadorResumoDto>

    @GET("api/v1/jogadores/{id}")
    suspend fun detalheJogador(@Path("id") id: Int): JogadorDetalheDto

    @GET("api/v1/times")
    suspend fun listarTimes(@Query("nome") nome: String? = null): List<TimeResumoDto>

    @GET("api/v1/times/{id}")
    suspend fun detalheTime(@Path("id") id: Int): TimeDetalheDto

    @GET("api/v1/competicoes")
    suspend fun listarCompeticoes(): List<CompeticaoDto>

    @GET("api/v1/anotacoes")
    suspend fun listarAnotacoes(
        @Query("timeId") timeId: Int,
        @Query("q") q: String? = null
    ): List<AnotacaoTimeDto>

    @POST("api/v1/anotacoes")
    suspend fun criarAnotacao(@Body input: AnotacaoTimeInput): AnotacaoTimeDto

    @PUT("api/v1/anotacoes/{id}")
    suspend fun atualizarAnotacao(@Path("id") id: Int, @Body input: AnotacaoTimeInput): AnotacaoTimeDto

    @DELETE("api/v1/anotacoes/{id}")
    suspend fun excluirAnotacao(@Path("id") id: Int)
}
