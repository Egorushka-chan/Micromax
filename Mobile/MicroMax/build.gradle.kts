// Top-level build file where you can add configuration options common to all sub-projects/modules.
plugins {
    alias(libs.plugins.android.application) apply false
    alias(libs.plugins.kotlin.compose) apply false
}

allprojects {
    val safeName = path.trim(':').replace(':', '_').ifBlank { "root" }
    layout.buildDirectory.set(file("${System.getProperty("user.home")}/.gradle/micromax-build/$safeName"))
}
