package com.comentarista.futebol

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.Surface
import androidx.compose.ui.Modifier
import com.comentarista.futebol.ui.navigation.ComentaristaNavHost
import com.comentarista.futebol.ui.theme.ComentaristaFutebolTheme
import dagger.hilt.android.AndroidEntryPoint

@AndroidEntryPoint
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            ComentaristaFutebolTheme {
                Surface(modifier = Modifier.fillMaxSize()) {
                    ComentaristaNavHost()
                }
            }
        }
    }
}
