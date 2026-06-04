package com.bezborodov.micromax.ui.auth

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.bezborodov.micromax.data.RoleAdmin
import com.bezborodov.micromax.data.SessionRepository
import com.bezborodov.micromax.data.UnauthorizedException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class SessionViewModel(
    private val sessionRepository: SessionRepository
) : ViewModel() {
    var uiState by mutableStateOf(SessionUiState())
        private set

    init {
        restoreSession()
    }

    fun restoreSession() {
        viewModelScope.launch {
            uiState = uiState.copy(isRestoringSession = true, message = null)
            val session = runCatching {
                withContext(Dispatchers.IO) { sessionRepository.restoreSession() }
            }.getOrNull()

            uiState = SessionUiState(
                isRestoringSession = false,
                currentSession = session
            )
        }
    }

    fun login(email: String, password: String) {
        if (email.isBlank() || password.isBlank()) {
            uiState = uiState.copy(message = "Введите email и пароль.")
            return
        }

        viewModelScope.launch {
            uiState = uiState.copy(isSubmitting = true, message = null)
            val result = runCatching {
                withContext(Dispatchers.IO) { sessionRepository.login(email, password) }
            }

            uiState = result.fold(
                onSuccess = { session ->
                    SessionUiState(
                        isRestoringSession = false,
                        currentSession = session
                    )
                },
                onFailure = {
                    uiState.copy(
                        isRestoringSession = false,
                        isSubmitting = false,
                        message = it.message ?: "Не удалось выполнить вход."
                    )
                }
            )
        }
    }

    fun register(email: String, password: String, displayName: String) {
        if (email.isBlank() || password.isBlank() || displayName.isBlank()) {
            uiState = uiState.copy(message = "Заполните имя, email и пароль.")
            return
        }

        viewModelScope.launch {
            uiState = uiState.copy(isSubmitting = true, message = null)
            val result = runCatching {
                withContext(Dispatchers.IO) { sessionRepository.register(email, password, displayName) }
            }

            uiState = result.fold(
                onSuccess = { session ->
                    SessionUiState(
                        isRestoringSession = false,
                        currentSession = session
                    )
                },
                onFailure = {
                    uiState.copy(
                        isRestoringSession = false,
                        isSubmitting = false,
                        message = it.message ?: "Не удалось зарегистрировать пользователя."
                    )
                }
            )
        }
    }

    fun logout() {
        viewModelScope.launch {
            uiState = uiState.copy(isSubmitting = true, message = null)
            withContext(Dispatchers.IO) { sessionRepository.logout() }
            uiState = SessionUiState(isRestoringSession = false)
        }
    }

    fun handleUnauthorized() {
        sessionRepository.clearSession()
        uiState = SessionUiState(
            isRestoringSession = false,
            message = "Сессия завершена. Войдите снова."
        )
    }

    fun createFirstWarehouse(name: String, address: String?) {
        if (name.isBlank()) {
            uiState = uiState.copy(message = "Укажите название склада.")
            return
        }

        viewModelScope.launch {
            uiState = uiState.copy(isCreatingWarehouse = true, message = null)
            val result = runCatching {
                withContext(Dispatchers.IO) { sessionRepository.createFirstWarehouse(name, address) }
            }

            uiState = result.fold(
                onSuccess = { session ->
                    SessionUiState(
                        isRestoringSession = false,
                        currentSession = session
                    )
                },
                onFailure = {
                    uiState.copy(
                        isCreatingWarehouse = false,
                        message = it.message ?: "Не удалось создать склад."
                    )
                }
            )
        }
    }

    fun selectActiveWarehouse(warehouseId: Int) {
        val currentWarehouseId = uiState.selectedWarehouseId
        if (warehouseId == currentWarehouseId) {
            return
        }

        viewModelScope.launch {
            val result = runCatching {
                withContext(Dispatchers.IO) { sessionRepository.selectActiveWarehouse(warehouseId) }
            }

            uiState = result.fold(
                onSuccess = { session ->
                    uiState.copy(
                        currentSession = session,
                        warehouseUsers = emptyList(),
                        loadedWarehouseUsersWarehouseId = null,
                        message = null
                    )
                },
                onFailure = {
                    if (it is UnauthorizedException) {
                        handleUnauthorized()
                        uiState
                    } else {
                        uiState.copy(message = it.message ?: "Не удалось переключить склад.")
                    }
                }
            )
        }
    }

    fun loadUsersForSelectedWarehouse(force: Boolean = false) {
        val warehouseId = uiState.selectedWarehouseId ?: return
        if (!uiState.canManageSelectedWarehouseUsers) {
            uiState = uiState.copy(
                warehouseUsers = emptyList(),
                loadedWarehouseUsersWarehouseId = null,
                isWarehouseUsersLoading = false
            )
            return
        }
        if (uiState.isWarehouseUsersLoading || uiState.isWarehouseUserSubmitting) {
            return
        }
        if (!force && uiState.loadedWarehouseUsersWarehouseId == warehouseId) {
            return
        }

        viewModelScope.launch {
            uiState = uiState.copy(isWarehouseUsersLoading = true, message = null)
            val result = runCatching {
                withContext(Dispatchers.IO) { sessionRepository.loadWarehouseUsers(warehouseId) }
            }

            uiState = result.fold(
                onSuccess = { users ->
                    uiState.copy(
                        isWarehouseUsersLoading = false,
                        warehouseUsers = users,
                        loadedWarehouseUsersWarehouseId = warehouseId
                    )
                },
                onFailure = {
                    if (it is UnauthorizedException) {
                        handleUnauthorized()
                        uiState
                    } else {
                        uiState.copy(
                            isWarehouseUsersLoading = false,
                            message = it.message ?: "Не удалось загрузить список пользователей склада."
                        )
                    }
                }
            )
        }
    }

    fun addWarehouseUser(email: String, roleCode: String) {
        val warehouseId = uiState.selectedWarehouseId ?: return
        if (email.isBlank()) {
            uiState = uiState.copy(message = "Укажите email пользователя.")
            return
        }

        mutateWarehouseUsers(
            successMessage = "Пользователь добавлен в склад."
        ) {
            sessionRepository.addWarehouseUser(warehouseId, email, roleCode)
            reloadSessionAndUsers()
        }
    }

    fun updateWarehouseUserRole(userId: Int, roleCode: String) {
        val warehouseId = uiState.selectedWarehouseId ?: return
        mutateWarehouseUsers(
            successMessage = "Роль пользователя обновлена."
        ) {
            sessionRepository.updateWarehouseUserRole(warehouseId, userId, roleCode)
            reloadSessionAndUsers()
        }
    }

    fun removeWarehouseUser(userId: Int) {
        val warehouseId = uiState.selectedWarehouseId ?: return
        mutateWarehouseUsers(
            successMessage = "Пользователь удалён из склада."
        ) {
            sessionRepository.removeWarehouseUser(warehouseId, userId)
            reloadSessionAndUsers()
        }
    }

    fun clearMessage() {
        if (uiState.message == null) {
            return
        }
        uiState = uiState.copy(message = null)
    }

    private fun mutateWarehouseUsers(
        successMessage: String,
        action: suspend SessionRepository.() -> SessionUiState
    ) {
        if (uiState.isWarehouseUserSubmitting) {
            return
        }

        viewModelScope.launch {
            val previousState = uiState
            uiState = uiState.copy(isWarehouseUserSubmitting = true, message = null)
            val result = runCatching {
                withContext(Dispatchers.IO) {
                    sessionRepository.action()
                }
            }

            uiState = result.fold(
                onSuccess = { updatedState ->
                    updatedState.copy(
                        isWarehouseUserSubmitting = false,
                        message = successMessage
                    )
                },
                onFailure = {
                    if (it is UnauthorizedException) {
                        handleUnauthorized()
                        uiState
                    } else {
                        previousState.copy(
                            isWarehouseUserSubmitting = false,
                            message = it.message ?: "Не удалось выполнить действие с пользователями склада."
                        )
                    }
                }
            )
        }
    }

    private fun SessionRepository.reloadSessionAndUsers(): SessionUiState {
        val session = loadCurrentUser()
        val selectedWarehouseId = session.activeWarehouseIdForSettings
        val selectedWarehouse = session.user.warehouses.firstOrNull { it.warehouseId == selectedWarehouseId }
        val users = if (selectedWarehouse?.roleCode == RoleAdmin && selectedWarehouseId != null) {
            loadWarehouseUsers(selectedWarehouseId)
        } else {
            emptyList()
        }

        val state = SessionUiState(
            isRestoringSession = false,
            currentSession = session,
            warehouseUsers = users,
            loadedWarehouseUsersWarehouseId = if (users.isEmpty()) null else selectedWarehouseId
        )
        return state
    }
}

class SessionViewModelFactory(
    private val sessionRepository: SessionRepository
) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T {
        if (modelClass.isAssignableFrom(SessionViewModel::class.java)) {
            return SessionViewModel(sessionRepository) as T
        }
        throw IllegalArgumentException("Unknown ViewModel class: ${modelClass.name}")
    }
}
