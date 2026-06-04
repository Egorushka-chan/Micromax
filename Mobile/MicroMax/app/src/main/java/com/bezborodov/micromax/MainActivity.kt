package com.bezborodov.micromax

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.tooling.preview.Preview
import com.bezborodov.micromax.data.MicroMaxApiClient
import com.bezborodov.micromax.data.SessionRepository
import com.bezborodov.micromax.data.SessionStore
import com.bezborodov.micromax.ui.auth.AppRoot
import com.bezborodov.micromax.ui.auth.AuthScreen
import com.bezborodov.micromax.ui.auth.SessionUiState
import com.bezborodov.micromax.ui.theme.MicroMaxTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            MicroMaxTheme {
                val context = LocalContext.current.applicationContext
                val apiClient = remember { MicroMaxApiClient() }
                val sessionStore = remember { SessionStore(context) }
                val sessionRepository = remember {
                    SessionRepository(sessionStore, apiClient).also { repository ->
                        apiClient.sessionAuthDelegate = repository
                    }
                }

                AppRoot(
                    apiClient = apiClient,
                    sessionRepository = sessionRepository
                )
            }
        }
    }
}

@Preview(showBackground = true)
@Composable
fun GreetingPreview() {
    MicroMaxTheme {
        AuthScreen(
            state = SessionUiState(isRestoringSession = false),
            onLogin = { _, _ -> },
            onRegister = { _, _, _ -> },
            onClearMessage = {}
        )
    }
}
