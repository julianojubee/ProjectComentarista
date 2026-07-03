// Plugins declarados aqui e aplicados (sem versão) no app/build.gradle.kts.
// Se o Android Studio sugerir atualizar essas versões no primeiro sync, pode aceitar.
plugins {
    id("com.android.application") version "9.2.1" apply false
    id("org.jetbrains.kotlin.android") version "2.2.10" apply false
    id("org.jetbrains.kotlin.plugin.compose") version "2.2.10" apply false
    id("org.jetbrains.kotlin.plugin.serialization") version "2.2.10" apply false
    id("com.google.dagger.hilt.android") version "2.60" apply false
    id("com.google.devtools.ksp") version "2.2.10-2.0.2" apply false
}
