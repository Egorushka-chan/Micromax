package com.bezborodov.micromax.data

import org.json.JSONArray
import org.json.JSONObject
import java.io.OutputStreamWriter
import java.net.HttpURLConnection
import java.net.ProtocolException
import java.net.SocketTimeoutException
import java.net.URL
import java.time.OffsetDateTime

data class ProductDto(
    val id: Int,
    val sku: String,
    val name: String,
    val unit: String,
    val minQuantity: Double
)

data class CellDto(
    val id: Int,
    val code: String,
    val name: String,
    val warehouseId: Int,
    val zoneCode: String,
    val warehouseName: String
)

data class StockDto(
    val productName: String,
    val sku: String,
    val cellCode: String,
    val zoneCode: String,
    val quantity: Double,
    val unit: String
)

data class OperationDto(
    val id: Int,
    val warehouseId: Int,
    val type: String,
    val productName: String,
    val sourceCell: String?,
    val targetCell: String?,
    val appUserId: Int?,
    val performedBy: String?,
    val quantity: Double,
    val comment: String?,
    val createdAt: String
)

data class WarehouseDto(
    val id: Int,
    val name: String,
    val address: String?
)

data class CreateWarehouseRequest(
    val name: String,
    val address: String?
)

data class CreateProductRequest(
    val sku: String,
    val name: String,
    val unit: String,
    val minQuantity: Double,
    val initialBarcode: BarcodeDraftDto? = null
)

data class BarcodeDraftDto(
    val value: String,
    val symbology: String? = null,
    val isPrimary: Boolean? = null
)

data class BarcodeDto(
    val id: Int,
    val value: String,
    val symbology: String,
    val isPrimary: Boolean,
    val isActive: Boolean,
    val createdAt: String,
    val createdByUserId: Int
)

data class BarcodeResolveDto(
    val found: Boolean,
    val value: String,
    val entityType: String?,
    val entityId: Int?,
    val title: String?,
    val subtitle: String?
)

data class WarehouseSnapshot(
    val products: List<ProductDto> = emptyList(),
    val cells: List<CellDto> = emptyList(),
    val stocks: List<StockDto> = emptyList(),
    val operations: List<OperationDto> = emptyList()
)

data class AssistantCommandDto(
    val commandId: String,
    val mode: String,
    val provider: String,
    val commandType: String,
    val riskLevel: String,
    val summary: String,
    val requiresConfirmation: Boolean,
    val clarificationQuestion: String?,
    val choices: List<AssistantChoiceDto>
)

data class AssistantChoiceDto(
    val id: String,
    val label: String,
    val kind: String
)

data class AssistantCommandDefinitionDto(
    val type: String,
    val title: String,
    val description: String,
    val riskLevel: String,
    val examples: List<String>
)

data class AssistantCommandResultDto(
    val success: Boolean,
    val message: String,
    val details: List<String>
)

class MicroMaxApiClient(
    private val baseUrl: String = "http://10.0.2.2:5101"
) {
    var sessionAuthDelegate: SessionAuthDelegate? = null

    private data class RequestTimeouts(
        val connectTimeoutMs: Int = 5_000,
        val readTimeoutMs: Int = 10_000,
        val timeoutMessage: String = "Сервер не ответил за отведённое время."
    )

    private data class RawResponse(
        val statusCode: Int,
        val body: String
    )

    private companion object {
        val DefaultTimeouts = RequestTimeouts()
        val AssistantTimeouts = RequestTimeouts(
            readTimeoutMs = 210_000,
            timeoutMessage = "ИИ-помощник не успел ответить за отведённое время."
        )
        const val MobileComment = "Операция из мобильного приложения"
    }

    fun register(email: String, password: String, displayName: String): AuthTokens {
        val response = postJson(
            path = "/api/auth/register",
            body = JSONObject().apply {
                put("email", email.trim())
                put("password", password)
                put("displayName", displayName.trim())
            },
            authenticated = false
        )
        return response.toAuthTokens()
    }

    fun login(email: String, password: String): AuthTokens {
        val response = postJson(
            path = "/api/auth/login",
            body = JSONObject().apply {
                put("email", email.trim())
                put("password", password)
            },
            authenticated = false
        )
        return response.toAuthTokens()
    }

    fun refresh(refreshToken: String): AuthTokens {
        val response = postJson(
            path = "/api/auth/refresh",
            body = JSONObject().apply { put("refreshToken", refreshToken) },
            authenticated = false
        )
        return response.toAuthTokens()
    }

    fun logout(refreshToken: String) {
        sendWithoutJsonResponse(
            path = "/api/auth/logout",
            method = "POST",
            body = JSONObject().apply { put("refreshToken", refreshToken) },
            authenticated = false
        )
    }

    fun getCurrentUser(accessTokenOverride: String? = null): CurrentUser {
        return getObject(
            path = "/api/users/me",
            accessTokenOverride = accessTokenOverride
        ).toCurrentUser()
    }

    fun getWarehouses(): List<WarehouseDto> {
        return getArray("/api/warehouses").mapObjects { warehouse ->
            WarehouseDto(
                id = warehouse.getInt("id"),
                name = warehouse.optString("name"),
                address = warehouse.optNullableString("address")
            )
        }
    }

    fun createWarehouse(request: CreateWarehouseRequest): WarehouseDto {
        val response = postJson(
            path = "/api/warehouses",
            body = JSONObject().apply {
                put("name", request.name)
                put("address", request.address ?: JSONObject.NULL)
            }
        )
        return WarehouseDto(
            id = response.getInt("id"),
            name = response.optString("name"),
            address = response.optNullableString("address")
        )
    }

    fun getWarehouseUsers(warehouseId: Int): List<WarehouseUser> {
        return getArray("/api/warehouses/$warehouseId/users").mapObjects { user ->
            user.toWarehouseUser()
        }
    }

    fun addWarehouseUser(warehouseId: Int, email: String, roleCode: String): WarehouseUser {
        val response = postJson(
            path = "/api/warehouses/$warehouseId/users",
            body = JSONObject().apply {
                put("email", email)
                put("roleCode", roleCode)
            }
        )
        return response.toWarehouseUser()
    }

    fun updateWarehouseUserRole(warehouseId: Int, userId: Int, roleCode: String): WarehouseUser {
        val response = requestJsonObject(
            path = "/api/warehouses/$warehouseId/users/$userId/role",
            method = "PATCH",
            body = JSONObject().apply { put("roleCode", roleCode) }
        )
        return response.toWarehouseUser()
    }

    fun removeWarehouseUser(warehouseId: Int, userId: Int) {
        sendWithoutJsonResponse(
            path = "/api/warehouses/$warehouseId/users/$userId",
            method = "DELETE"
        )
    }

    fun loadSnapshot(): WarehouseSnapshot {
        return WarehouseSnapshot(
            products = getArray("/api/products").mapObjects {
                ProductDto(
                    id = it.getInt("id"),
                    sku = it.optString("sku"),
                    name = it.optString("name"),
                    unit = it.optString("unit"),
                    minQuantity = it.optDouble("minQuantity")
                )
            },
            cells = getArray("/api/cells").mapObjects {
                CellDto(
                    id = it.getInt("id"),
                    code = it.optString("code"),
                    name = it.optString("name"),
                    warehouseId = it.optInt("warehouseId"),
                    zoneCode = it.optString("zoneCode"),
                    warehouseName = it.optString("warehouseName")
                )
            },
            stocks = getArray("/api/stocks").mapObjects {
                StockDto(
                    productName = it.optString("productName"),
                    sku = it.optString("sku"),
                    cellCode = it.optString("cellCode"),
                    zoneCode = it.optString("zoneCode"),
                    quantity = it.optDouble("quantity"),
                    unit = it.optString("unit")
                )
            },
            operations = getArray("/api/operations").mapObjects {
                OperationDto(
                    id = it.getInt("id"),
                    warehouseId = it.optInt("warehouseId"),
                    type = it.optString("type"),
                    productName = it.optString("productName"),
                    sourceCell = it.optNullableString("sourceCell"),
                    targetCell = it.optNullableString("targetCell"),
                    appUserId = it.optNullableInt("appUserId"),
                    performedBy = it.optNullableString("performedBy"),
                    quantity = it.optDouble("quantity"),
                    comment = it.optNullableString("comment"),
                    createdAt = it.optString("createdAt")
                )
            }
        )
    }

    fun receive(productId: Int, targetCellId: Int, quantity: Double, comment: String? = null) {
        postJson("/api/operations/receive", JSONObject().apply {
            put("productId", productId)
            put("targetCellId", targetCellId)
            put("quantity", quantity)
            put("comment", buildComment(comment))
        })
    }

    fun writeOff(productId: Int, sourceCellId: Int, quantity: Double, comment: String? = null) {
        postJson("/api/operations/write-off", JSONObject().apply {
            put("productId", productId)
            put("sourceCellId", sourceCellId)
            put("quantity", quantity)
            put("comment", buildComment(comment))
        })
    }

    fun move(productId: Int, sourceCellId: Int, targetCellId: Int, quantity: Double, comment: String? = null) {
        postJson("/api/operations/move", JSONObject().apply {
            put("productId", productId)
            put("sourceCellId", sourceCellId)
            put("targetCellId", targetCellId)
            put("quantity", quantity)
            put("comment", buildComment(comment))
        })
    }

    fun adjust(productId: Int, targetCellId: Int, targetQuantity: Double, comment: String? = null) {
        postJson("/api/operations/adjust", JSONObject().apply {
            put("productId", productId)
            put("targetCellId", targetCellId)
            put("targetQuantity", targetQuantity)
            put("comment", buildComment(comment))
        })
    }

    fun createProduct(request: CreateProductRequest): ProductDto {
        val response = postJson("/api/products", JSONObject().apply {
            put("sku", request.sku)
            put("name", request.name)
            put("unit", request.unit)
            put("minQuantity", request.minQuantity)
            put(
                "initialBarcode",
                request.initialBarcode?.toJsonObject() ?: JSONObject.NULL
            )
        })

        return ProductDto(
            id = response.getInt("id"),
            sku = response.optString("sku"),
            name = response.optString("name"),
            unit = response.optString("unit"),
            minQuantity = response.optDouble("minQuantity")
        )
    }

    fun resolveBarcode(value: String): BarcodeResolveDto {
        val response = getObject("/api/barcodes/resolve?value=${encodeQueryValue(value)}")
        return BarcodeResolveDto(
            found = response.optBoolean("found"),
            value = response.optString("value"),
            entityType = response.optNullableString("entityType"),
            entityId = response.optNullableInt("entityId"),
            title = response.optNullableString("title"),
            subtitle = response.optNullableString("subtitle")
        )
    }

    fun getProductBarcodes(productId: Int): List<BarcodeDto> {
        return getArray("/api/products/$productId/barcodes").mapObjects { barcode ->
            barcode.toBarcodeDto()
        }
    }

    fun addProductBarcode(productId: Int, request: BarcodeDraftDto): BarcodeDto {
        val response = postJson("/api/products/$productId/barcodes", request.toJsonObject())
        return response.toBarcodeDto()
    }

    fun getCellBarcodes(cellId: Int): List<BarcodeDto> {
        return getArray("/api/cells/$cellId/barcodes").mapObjects { barcode ->
            barcode.toBarcodeDto()
        }
    }

    fun addCellBarcode(cellId: Int, request: BarcodeDraftDto): BarcodeDto {
        val response = postJson("/api/cells/$cellId/barcodes", request.toJsonObject())
        return response.toBarcodeDto()
    }

    fun deactivateBarcode(barcodeId: Int) {
        sendWithoutJsonResponse(
            path = "/api/barcodes/$barcodeId",
            method = "DELETE"
        )
    }

    fun updateProductMinQuantity(product: ProductDto, minQuantity: Double): ProductDto {
        val response = requestJsonObject(
            path = "/api/products/${product.id}",
            method = "PUT",
            body = JSONObject().apply {
                put("id", product.id)
                put("sku", product.sku)
                put("name", product.name)
                put("unit", product.unit)
                put("minQuantity", minQuantity)
            }
        )

        return ProductDto(
            id = response.getInt("id"),
            sku = response.optString("sku"),
            name = response.optString("name"),
            unit = response.optString("unit"),
            minQuantity = response.optDouble("minQuantity")
        )
    }

    fun interpretAssistant(text: String): AssistantCommandDto {
        val response = postJson(
            path = "/api/assistant/interpret",
            body = JSONObject().put("text", text),
            timeouts = AssistantTimeouts
        )
        return AssistantCommandDto(
            commandId = response.optString("commandId"),
            mode = response.optString("mode"),
            provider = response.optString("provider"),
            commandType = response.optString("commandType"),
            riskLevel = response.optString("riskLevel"),
            summary = response.optString("summary"),
            requiresConfirmation = response.optBoolean("requiresConfirmation"),
            clarificationQuestion = response.optNullableString("clarificationQuestion"),
            choices = response.optJSONArray("choices")?.mapObjects {
                AssistantChoiceDto(
                    id = it.optString("id"),
                    label = it.optString("label"),
                    kind = it.optString("kind")
                )
            }.orEmpty()
        )
    }

    fun confirmAssistant(commandId: String): AssistantCommandResultDto {
        val response = postJson("/api/assistant/confirm", JSONObject().apply {
            put("commandId", commandId)
            put("confirmed", true)
        })
        return AssistantCommandResultDto(
            success = response.optBoolean("success"),
            message = response.optString("message"),
            details = response.optJSONArray("details")?.mapStrings().orEmpty()
        )
    }

    fun loadAssistantCommands(): List<AssistantCommandDefinitionDto> {
        return getArray("/api/assistant/commands").mapObjects {
            AssistantCommandDefinitionDto(
                type = it.optString("type"),
                title = it.optString("title"),
                description = it.optString("description"),
                riskLevel = it.optString("riskLevel"),
                examples = it.optJSONArray("examples")?.mapStrings().orEmpty()
            )
        }
    }

    private fun buildComment(comment: String?): String {
        val userComment = comment?.trim().orEmpty()
        return if (userComment.isEmpty()) MobileComment else userComment
    }

    private fun encodeQueryValue(value: String): String {
        return java.net.URLEncoder.encode(value, Charsets.UTF_8)
    }

    private fun getArray(
        path: String,
        accessTokenOverride: String? = null
    ): JSONArray {
        return requestJsonArray(
            path = path,
            method = "GET",
            accessTokenOverride = accessTokenOverride
        )
    }

    private fun getObject(
        path: String,
        accessTokenOverride: String? = null
    ): JSONObject {
        return requestJsonObject(
            path = path,
            method = "GET",
            accessTokenOverride = accessTokenOverride
        )
    }

    private fun postJson(
        path: String,
        body: JSONObject,
        timeouts: RequestTimeouts = DefaultTimeouts,
        authenticated: Boolean = true,
        accessTokenOverride: String? = null
    ): JSONObject {
        return requestJsonObject(
            path = path,
            method = "POST",
            body = body,
            timeouts = timeouts,
            authenticated = authenticated,
            accessTokenOverride = accessTokenOverride
        )
    }

    private fun requestJsonObject(
        path: String,
        method: String,
        body: JSONObject? = null,
        timeouts: RequestTimeouts = DefaultTimeouts,
        authenticated: Boolean = true,
        accessTokenOverride: String? = null
    ): JSONObject {
        val response = executeRequest(
            path = path,
            method = method,
            body = body,
            timeouts = timeouts,
            authenticated = authenticated,
            accessTokenOverride = accessTokenOverride
        )
        return response.body.let(::JSONObject)
    }

    private fun requestJsonArray(
        path: String,
        method: String,
        body: JSONObject? = null,
        timeouts: RequestTimeouts = DefaultTimeouts,
        authenticated: Boolean = true,
        accessTokenOverride: String? = null
    ): JSONArray {
        val response = executeRequest(
            path = path,
            method = method,
            body = body,
            timeouts = timeouts,
            authenticated = authenticated,
            accessTokenOverride = accessTokenOverride
        )
        return response.body.let(::JSONArray)
    }

    private fun sendWithoutJsonResponse(
        path: String,
        method: String,
        body: JSONObject? = null,
        timeouts: RequestTimeouts = DefaultTimeouts,
        authenticated: Boolean = true
    ) {
        executeRequest(
            path = path,
            method = method,
            body = body,
            timeouts = timeouts,
            authenticated = authenticated
        )
    }

    private fun executeRequest(
        path: String,
        method: String,
        body: JSONObject? = null,
        timeouts: RequestTimeouts = DefaultTimeouts,
        authenticated: Boolean = true,
        accessTokenOverride: String? = null
    ): RawResponse {
        var canRetryAfterRefresh = authenticated && accessTokenOverride == null

        while (true) {
            val accessToken = when {
                !authenticated -> null
                accessTokenOverride != null -> accessTokenOverride
                else -> sessionAuthDelegate?.refreshSessionIfNeeded(force = false)?.accessToken
                    ?: throw UnauthorizedException("Сессия отсутствует. Войдите снова.")
            }

            val connection = try {
                openConnection(path, method, timeouts)
            } catch (_: SocketTimeoutException) {
                throw ApiException(timeouts.timeoutMessage)
            }

            try {
                connection.setRequestProperty("Accept", "application/json")
                if (!accessToken.isNullOrBlank()) {
                    connection.setRequestProperty("Authorization", "Bearer $accessToken")
                }

                if (body != null) {
                    connection.doOutput = true
                    connection.setRequestProperty("Content-Type", "application/json; charset=utf-8")
                    OutputStreamWriter(connection.outputStream, Charsets.UTF_8).use { writer ->
                        writer.write(body.toString())
                    }
                }

                val response = readRawResponse(connection, timeouts)
                if (response.statusCode == HttpURLConnection.HTTP_UNAUTHORIZED && authenticated) {
                    if (canRetryAfterRefresh && sessionAuthDelegate?.refreshSessionIfNeeded(force = true) != null) {
                        canRetryAfterRefresh = false
                        continue
                    }

                    sessionAuthDelegate?.clearSession()
                    throw UnauthorizedException()
                }

                if (response.statusCode !in 200..299) {
                    throw ApiException(parseErrorMessage(response))
                }

                return response
            } finally {
                connection.disconnect()
            }
        }
    }

    private fun openConnection(
        path: String,
        method: String,
        timeouts: RequestTimeouts
    ): HttpURLConnection {
        return (URL("$baseUrl$path").openConnection() as HttpURLConnection).apply {
            setRequestMethodCompat(method)
            connectTimeout = timeouts.connectTimeoutMs
            readTimeout = timeouts.readTimeoutMs
        }
    }

    private fun readRawResponse(
        connection: HttpURLConnection,
        timeouts: RequestTimeouts
    ): RawResponse {
        return try {
            val statusCode = connection.responseCode
            val stream = if (statusCode in 200..299) {
                connection.inputStream
            } else {
                connection.errorStream
            }
            RawResponse(
                statusCode = statusCode,
                body = stream?.bufferedReader(Charsets.UTF_8)?.use { it.readText() }.orEmpty()
            )
        } catch (_: SocketTimeoutException) {
            throw ApiException(timeouts.timeoutMessage)
        }
    }

    private fun parseErrorMessage(response: RawResponse): String {
        val fallback = if (response.statusCode == HttpURLConnection.HTTP_UNAUTHORIZED) {
            "Сессия истекла. Войдите снова."
        } else {
            "Ошибка сервера: ${response.statusCode}"
        }
        if (response.body.isBlank()) {
            return fallback
        }

        return runCatching {
            val json = JSONObject(response.body)
            json.optString("detail")
                .ifBlank { json.optString("title") }
                .ifBlank { json.optString("error") }
                .ifBlank { json.optString("message") }
                .ifBlank { fallback }
        }.getOrDefault(response.body.ifBlank { fallback })
    }
}

private fun BarcodeDraftDto.toJsonObject(): JSONObject {
    return JSONObject().apply {
        put("value", value)
        put("symbology", symbology ?: JSONObject.NULL)
        put("isPrimary", isPrimary ?: JSONObject.NULL)
    }
}

private fun JSONObject.toBarcodeDto(): BarcodeDto {
    return BarcodeDto(
        id = getInt("id"),
        value = optString("value"),
        symbology = optString("symbology"),
        isPrimary = optBoolean("isPrimary"),
        isActive = optBoolean("isActive"),
        createdAt = optString("createdAt"),
        createdByUserId = optInt("createdByUserId")
    )
}

private fun JSONArray.mapStrings(): List<String> {
    return (0 until length()).map { index -> optString(index) }
}

private fun <T> JSONArray.mapObjects(transform: (JSONObject) -> T): List<T> {
    return (0 until length()).map { index -> transform(getJSONObject(index)) }
}

private fun JSONObject.optNullableInt(name: String): Int? {
    return if (isNull(name)) null else optInt(name)
}

private fun JSONObject.optNullableString(name: String): String? {
    return if (isNull(name)) null else optString(name)
}

private fun JSONObject.toAuthTokens(): AuthTokens {
    return AuthTokens(
        accessToken = optString("accessToken"),
        accessTokenExpiresAt = OffsetDateTime.parse(optString("accessTokenExpiresAt")),
        refreshToken = optString("refreshToken"),
        user = optJSONObject("user")?.toCurrentUser()
            ?: CurrentUser(
                id = 0,
                email = "",
                displayName = "",
                isActive = true,
                warehouses = emptyList()
            )
    )
}

private fun JSONObject.toCurrentUser(): CurrentUser {
    return CurrentUser(
        id = getInt("id"),
        email = optString("email"),
        displayName = optString("displayName"),
        isActive = optBoolean("isActive"),
        warehouses = optJSONArray("warehouses")?.mapObjects { warehouse ->
            CurrentUserWarehouse(
                warehouseId = warehouse.getInt("warehouseId"),
                warehouseName = warehouse.optString("warehouseName"),
                roleCode = warehouse.optString("roleCode"),
                roleName = warehouse.optString("roleName")
            )
        }.orEmpty()
    )
}

private fun JSONObject.toWarehouseUser(): WarehouseUser {
    return WarehouseUser(
        userId = getInt("userId"),
        email = optString("email"),
        displayName = optString("displayName"),
        isActive = optBoolean("isActive"),
        roleCode = optString("roleCode"),
        roleName = optString("roleName"),
        createdAt = optString("createdAt")
    )
}

private fun HttpURLConnection.setRequestMethodCompat(method: String) {
    try {
        requestMethod = method
    } catch (exception: ProtocolException) {
        if (method != "PATCH") {
            throw exception
        }

        val delegate = runCatching {
            javaClass.getDeclaredField("delegate").apply { isAccessible = true }.get(this)
        }.getOrNull()
        if (delegate is HttpURLConnection) {
            delegate.setRequestMethodCompat(method)
            return
        }

        var currentClass: Class<*>? = javaClass
        while (currentClass != null) {
            val methodField = runCatching {
                currentClass.getDeclaredField("method").apply { isAccessible = true }
            }.getOrNull()

            if (methodField != null) {
                methodField.set(this, method)
                return
            }
            currentClass = currentClass.superclass
        }

        throw exception
    }
}
