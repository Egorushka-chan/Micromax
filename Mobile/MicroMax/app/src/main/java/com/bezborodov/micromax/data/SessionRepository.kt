package com.bezborodov.micromax.data

import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import java.time.OffsetDateTime

interface SessionAuthDelegate {
    fun getSession(): AuthSession?
    fun refreshSessionIfNeeded(force: Boolean = false): AuthSession?
    fun clearSession()
}

class SessionRepository(
    private val sessionStore: SessionStore,
    private val apiClient: MicroMaxApiClient
) : SessionAuthDelegate {
    private val refreshMutex = Mutex()

    fun restoreSession(): AuthSession? {
        if (sessionStore.loadSession() == null) {
            return null
        }

        return runCatching { loadCurrentUser() }
            .getOrElse {
                clearSession()
                null
            }
    }

    fun login(email: String, password: String): AuthSession {
        val tokens = apiClient.login(email, password)
        return loadCurrentUserForTokens(tokens, sessionStore.loadSession()?.selectedWarehouse?.warehouseId)
    }

    fun register(email: String, password: String, displayName: String): AuthSession {
        val tokens = apiClient.register(email, password, displayName)
        return loadCurrentUserForTokens(tokens, sessionStore.loadSession()?.selectedWarehouse?.warehouseId)
    }

    fun logout() {
        val refreshToken = sessionStore.loadSession()?.refreshToken
        if (!refreshToken.isNullOrBlank()) {
            runCatching { apiClient.logout(refreshToken) }
        }
        clearSession()
    }

    fun loadCurrentUser(): AuthSession {
        val currentSession = sessionStore.loadSession()
            ?: throw UnauthorizedException("Сессия отсутствует. Войдите снова.")
        val user = apiClient.getCurrentUser()
        val updatedSession = normalizeSession(
            currentSession.copy(user = user)
        )
        sessionStore.saveSession(updatedSession)
        return updatedSession
    }

    fun createWarehouse(name: String, address: String?): AuthSession {
        val warehouse = apiClient.createWarehouse(CreateWarehouseRequest(name.trim(), address?.trim()?.ifBlank { null }))
        return loadCurrentUserForNewWarehouse(warehouse.id)
    }

    fun createWarehouseFromTemplate(name: String, address: String?, templateCode: String): AuthSession {
        val warehouse = apiClient.createWarehouseFromTemplate(
            CreateWarehouseFromTemplateRequest(
                name = name.trim(),
                address = address?.trim()?.ifBlank { null },
                templateCode = templateCode
            )
        )
        return loadCurrentUserForNewWarehouse(warehouse.warehouseId)
    }

    fun selectActiveWarehouse(warehouseId: Int): AuthSession {
        val session = sessionStore.loadSession()
            ?: throw UnauthorizedException("Сессия отсутствует. Войдите снова.")
        val selectedWarehouse = session.user.warehouses.firstOrNull { it.warehouseId == warehouseId }
            ?: throw ApiException("Выбранный склад больше недоступен.")
        val updatedSession = normalizeSession(session.copy(selectedWarehouse = selectedWarehouse))
        sessionStore.saveSession(updatedSession)
        return updatedSession
    }

    fun loadWarehouseUsers(warehouseId: Int): List<WarehouseUser> {
        return apiClient.getWarehouseUsers(warehouseId)
    }

    fun addWarehouseUser(warehouseId: Int, email: String, roleCode: String): WarehouseUser {
        return apiClient.addWarehouseUser(warehouseId, email.trim(), roleCode)
    }

    fun updateWarehouseUserRole(warehouseId: Int, userId: Int, roleCode: String): WarehouseUser {
        return apiClient.updateWarehouseUserRole(warehouseId, userId, roleCode)
    }

    fun removeWarehouseUser(warehouseId: Int, userId: Int) {
        apiClient.removeWarehouseUser(warehouseId, userId)
    }

    fun loadWarehouseSetupTemplates(): List<WarehouseSetupTemplate> {
        return apiClient.getWarehouseSetupTemplates()
    }

    override fun getSession(): AuthSession? = sessionStore.loadSession()

    override fun refreshSessionIfNeeded(force: Boolean): AuthSession? = runBlocking {
        refreshMutex.withLock {
            val currentSession = sessionStore.loadSession() ?: return@withLock null
            val expiresSoon = currentSession.accessTokenExpiresAt <= OffsetDateTime.now().plusSeconds(30)
            if (!force && !expiresSoon) {
                return@withLock currentSession
            }

            val refreshedTokens = runCatching {
                apiClient.refresh(currentSession.refreshToken)
            }.getOrElse {
                clearSession()
                return@withLock null
            }

            return@withLock loadCurrentUserForTokens(
                tokens = refreshedTokens,
                previousSelectedWarehouseId = currentSession.selectedWarehouse?.warehouseId
            )
        }
    }

    override fun clearSession() {
        sessionStore.clear()
    }

    private fun loadCurrentUserForTokens(
        tokens: AuthTokens,
        previousSelectedWarehouseId: Int?
    ): AuthSession {
        val user = apiClient.getCurrentUser(accessTokenOverride = tokens.accessToken)
        val session = normalizeSession(
            AuthSession(
                accessToken = tokens.accessToken,
                accessTokenExpiresAt = tokens.accessTokenExpiresAt,
                refreshToken = tokens.refreshToken,
                user = user,
                selectedWarehouse = user.warehouses.firstOrNull { it.warehouseId == previousSelectedWarehouseId }
            )
        )
        sessionStore.saveSession(session)
        return session
    }

    private fun loadCurrentUserForNewWarehouse(warehouseId: Int): AuthSession {
        val session = loadCurrentUser()
        val selectedWarehouse = session.user.warehouses.firstOrNull { it.warehouseId == warehouseId }
            ?: return session
        val updatedSession = normalizeSession(session.copy(selectedWarehouse = selectedWarehouse))
        sessionStore.saveSession(updatedSession)
        return updatedSession
    }

    private fun normalizeSession(session: AuthSession): AuthSession {
        val selectedWarehouseId = session.selectedWarehouse?.warehouseId
        val selectedWarehouse = selectedWarehouseId
            ?.let { warehouseId -> session.user.warehouses.firstOrNull { it.warehouseId == warehouseId } }
            ?: session.user.warehouses.singleOrNull()

        return session.copy(selectedWarehouse = selectedWarehouse)
    }
}
