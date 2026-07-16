package com.comentarista.futebol.ui.navigation

import androidx.compose.runtime.Composable
import androidx.navigation.NavHostController
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import com.comentarista.futebol.ui.analise.AnaliseJogoScreen
import com.comentarista.futebol.ui.login.LoginScreen
import com.comentarista.futebol.ui.main.MainScaffold

private const val ROTA_LOGIN = "login"
private const val ROTA_MAIN = "main"
private const val ROTA_ANALISE = "analise/{jogoId}"

@Composable
fun ComentaristaNavHost(navController: NavHostController = rememberNavController()) {
    NavHost(navController = navController, startDestination = ROTA_LOGIN) {
        composable(ROTA_LOGIN) {
            LoginScreen(
                onLoginSucesso = {
                    navController.navigate(ROTA_MAIN) {
                        popUpTo(ROTA_LOGIN) { inclusive = true }
                    }
                }
            )
        }
        composable(ROTA_MAIN) {
            MainScaffold(
                onDeslogado = {
                    navController.navigate(ROTA_LOGIN) {
                        popUpTo(ROTA_MAIN) { inclusive = true }
                    }
                },
                onAbrirAnalise = { jogoId ->
                    navController.navigate("analise/$jogoId")
                }
            )
        }
        composable(
            route = ROTA_ANALISE,
            arguments = listOf(navArgument("jogoId") { type = NavType.IntType })
        ) { backStackEntry ->
            val jogoId = backStackEntry.arguments?.getInt("jogoId") ?: return@composable
            AnaliseJogoScreen(
                jogoId = jogoId,
                onVoltar = { navController.popBackStack() }
            )
        }
    }
}
