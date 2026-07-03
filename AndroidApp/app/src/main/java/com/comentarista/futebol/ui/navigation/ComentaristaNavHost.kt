package com.comentarista.futebol.ui.navigation

import androidx.compose.runtime.Composable
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.comentarista.futebol.ui.login.LoginScreen
import com.comentarista.futebol.ui.main.MainScaffold

private const val ROTA_LOGIN = "login"
private const val ROTA_MAIN = "main"

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
                }
            )
        }
    }
}
