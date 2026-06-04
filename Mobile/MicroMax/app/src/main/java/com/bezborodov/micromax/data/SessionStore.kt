package com.bezborodov.micromax.data

import android.content.Context
import android.content.SharedPreferences
import org.json.JSONArray
import org.json.JSONObject
import java.time.OffsetDateTime

class SessionStore(context: Context) {
    private val preferences: SharedPreferences =
        context.getSharedPreferences(PreferencesName, Context.MODE_PRIVATE)

    fun loadSession(): AuthSession? {
        val accessToken = preferences.getString(KeyAccessToken, null)
        val expiresAt = preferences.getString(KeyAccessTokenExpiresAt, null)
        val refreshToken = preferences.getString(KeyRefreshToken, null)
        val userJson = preferences.getString(KeyUserJson, null)
        if (accessToken.isNullOrBlank() || expiresAt.isNullOrBlank() || refreshToken.isNullOrBlank() || userJson.isNullOrBlank()) {
            return null
        }

        return runCatching {
            val user = parseUser(JSONObject(userJson))
            AuthSession(
                accessToken = accessToken,
                accessTokenExpiresAt = OffsetDateTime.parse(expiresAt),
                refreshToken = refreshToken,
                user = user,
                activeWarehouseIdForSettings = preferences.getInt(KeyActiveWarehouseId, MissingWarehouseId)
                    .takeIf { it != MissingWarehouseId }
            )
        }.getOrNull()
    }

    fun saveSession(session: AuthSession) {
        preferences.edit()
            .putString(KeyAccessToken, session.accessToken)
            .putString(KeyAccessTokenExpiresAt, session.accessTokenExpiresAt.toString())
            .putString(KeyRefreshToken, session.refreshToken)
            .putString(KeyUserJson, serializeUser(session.user).toString())
            .apply {
                if (session.activeWarehouseIdForSettings == null) {
                    remove(KeyActiveWarehouseId)
                } else {
                    putInt(KeyActiveWarehouseId, session.activeWarehouseIdForSettings)
                }
            }
            .apply()
    }

    fun clear() {
        preferences.edit().clear().apply()
    }

    private fun serializeUser(user: CurrentUser): JSONObject {
        return JSONObject().apply {
            put("id", user.id)
            put("email", user.email)
            put("displayName", user.displayName)
            put("isActive", user.isActive)
            put(
                "warehouses",
                JSONArray().apply {
                    user.warehouses.forEach { warehouse ->
                        put(
                            JSONObject().apply {
                                put("warehouseId", warehouse.warehouseId)
                                put("warehouseName", warehouse.warehouseName)
                                put("roleCode", warehouse.roleCode)
                                put("roleName", warehouse.roleName)
                            }
                        )
                    }
                }
            )
        }
    }

    private fun parseUser(jsonObject: JSONObject): CurrentUser {
        val warehousesJson = jsonObject.optJSONArray("warehouses")
        val warehouses = buildList {
            if (warehousesJson == null) {
                return@buildList
            }

            for (index in 0 until warehousesJson.length()) {
                val warehouse = warehousesJson.getJSONObject(index)
                add(
                    CurrentUserWarehouse(
                        warehouseId = warehouse.getInt("warehouseId"),
                        warehouseName = warehouse.optString("warehouseName"),
                        roleCode = warehouse.optString("roleCode"),
                        roleName = warehouse.optString("roleName")
                    )
                )
            }
        }

        return CurrentUser(
            id = jsonObject.getInt("id"),
            email = jsonObject.optString("email"),
            displayName = jsonObject.optString("displayName"),
            isActive = jsonObject.optBoolean("isActive"),
            warehouses = warehouses
        )
    }

    private companion object {
        const val PreferencesName = "micromax_session"
        const val KeyAccessToken = "access_token"
        const val KeyAccessTokenExpiresAt = "access_token_expires_at"
        const val KeyRefreshToken = "refresh_token"
        const val KeyUserJson = "user_json"
        const val KeyActiveWarehouseId = "active_warehouse_id"
        const val MissingWarehouseId = Int.MIN_VALUE
    }
}
