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

data class CreateWarehouseFromTemplateRequest(
    val name: String,
    val address: String?,
    val templateCode: String
)

data class WarehouseSetupResultDto(
    val warehouseId: Int,
    val warehouseName: String,
    val roleCode: String,
    val roleName: String,
    val zonesCreated: Int,
    val cellsCreated: Int
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
    val productId: Int?,
    val sourceCellId: Int?,
    val targetCellId: Int?,
    val quantity: Double?,
    val minQuantity: Double?,
    val summary: String,
    val requiresConfirmation: Boolean,
    val clarificationQuestion: String?,
    val clarificationTarget: String?,
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
    private val baseUrl: String = "http://95.104.193.73:80",
    private val baseUrlLocal: String = "http://10.0.2.2:5101"
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

    fun createWarehouseFromTemplate(request: CreateWarehouseFromTemplateRequest): WarehouseSetupResultDto {
        val response = postJson(
            path = "/api/warehouse-setup",
            body = JSONObject().apply {
                put("name", request.name)
                put("address", request.address ?: JSONObject.NULL)
                put("templateCode", request.templateCode)
            }
        )
        return response.toWarehouseSetupResultDto()
    }

    fun getWarehouseSetupTemplates(): List<WarehouseSetupTemplate> {
        return getArray("/api/warehouse-setup/templates").mapObjects { template ->
            WarehouseSetupTemplate(
                code = template.optString("code"),
                name = template.optString("name"),
                description = template.optString("description"),
                zonesCount = template.optInt("zonesCount"),
                cellsCount = template.optInt("cellsCount")
            )
        }
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
                it.toProductDto()
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
                it.toStockDto()
            },
            operations = getArray("/api/operations").mapObjects {
                it.toOperationDto()
            }
        )
    }

    fun loadSnapshot(warehouseId: Int): WarehouseSnapshot {
        return getObject("/api/warehouses/$warehouseId/snapshot").toWarehouseSnapshot()
    }

    fun receive(productId: Int, targetCellId: Int, quantity: Double, comment: String? = null) {
        postJson("/api/operations/receive", JSONObject().apply {
            put("productId", productId)
            put("targetCellId", targetCellId)
            put("quantity", quantity)
            put("comment", buildComment(comment))
        })
    }

    fun receive(warehouseId: Int, productId: Int, targetCellId: Int, quantity: Double, comment: String? = null) {
        postJson("/api/warehouses/$warehouseId/operations/receive", JSONObject().apply {
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

    fun writeOff(warehouseId: Int, productId: Int, sourceCellId: Int, quantity: Double, comment: String? = null) {
        postJson("/api/warehouses/$warehouseId/operations/write-off", JSONObject().apply {
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

    fun move(
        warehouseId: Int,
        productId: Int,
        sourceCellId: Int,
        targetCellId: Int,
        quantity: Double,
        comment: String? = null
    ) {
        postJson("/api/warehouses/$warehouseId/operations/move", JSONObject().apply {
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

    fun adjust(warehouseId: Int, productId: Int, targetCellId: Int, targetQuantity: Double, comment: String? = null) {
        postJson("/api/warehouses/$warehouseId/operations/adjust", JSONObject().apply {
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

        return response.toProductDto()
    }

    fun createProduct(warehouseId: Int, request: CreateProductRequest): ProductDto {
        val response = postJson("/api/warehouses/$warehouseId/products", JSONObject().apply {
            put("sku", request.sku)
            put("name", request.name)
            put("unit", request.unit)
            put("minQuantity", request.minQuantity)
            put(
                "initialBarcode",
                request.initialBarcode?.toJsonObject() ?: JSONObject.NULL
            )
        })

        return response.toProductDto()
    }

    fun resolveBarcode(value: String): BarcodeResolveDto {
        val response = getObject("/api/barcodes/resolve?value=${encodeQueryValue(value)}")
        return response.toBarcodeResolveDto()
    }

    fun resolveBarcode(warehouseId: Int, value: String): BarcodeResolveDto {
        val response = getObject("/api/warehouses/$warehouseId/barcodes/resolve?value=${encodeQueryValue(value)}")
        return response.toBarcodeResolveDto()
    }

    fun getProductBarcodes(productId: Int): List<BarcodeDto> {
        return getArray("/api/products/$productId/barcodes").mapObjects { barcode ->
            barcode.toBarcodeDto()
        }
    }

    fun getProductBarcodes(warehouseId: Int, productId: Int): List<BarcodeDto> {
        return getArray("/api/warehouses/$warehouseId/products/$productId/barcodes").mapObjects { barcode ->
            barcode.toBarcodeDto()
        }
    }

    fun addProductBarcode(productId: Int, request: BarcodeDraftDto): BarcodeDto {
        val response = postJson("/api/products/$productId/barcodes", request.toJsonObject())
        return response.toBarcodeDto()
    }

    fun addProductBarcode(warehouseId: Int, productId: Int, request: BarcodeDraftDto): BarcodeDto {
        val response = postJson("/api/warehouses/$warehouseId/products/$productId/barcodes", request.toJsonObject())
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

    fun deactivateBarcode(warehouseId: Int, barcodeId: Int) {
        sendWithoutJsonResponse(
            path = "/api/warehouses/$warehouseId/barcodes/$barcodeId",
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

        return response.toProductDto()
    }

    fun updateProductMinQuantity(warehouseId: Int, product: ProductDto, minQuantity: Double): ProductDto {
        val response = requestJsonObject(
            path = "/api/warehouses/$warehouseId/products/${product.id}",
            method = "PUT",
            body = JSONObject().apply {
                put("id", product.id)
                put("sku", product.sku)
                put("name", product.name)
                put("unit", product.unit)
                put("minQuantity", minQuantity)
            }
        )

        return response.toProductDto()
    }

    fun interpretAssistant(text: String): AssistantCommandDto {
        val response = postJson(
            path = "/api/assistant/interpret",
            body = JSONObject().put("text", text),
            timeouts = AssistantTimeouts
        )
        return response.toAssistantCommandDto()
    }

    fun interpretAssistant(warehouseId: Int, text: String): AssistantCommandDto {
        val response = postJson(
            path = "/api/warehouses/$warehouseId/assistant/interpret",
            body = JSONObject().put("text", text),
            timeouts = AssistantTimeouts
        )
        return response.toAssistantCommandDto()
    }

    fun confirmAssistant(commandId: String, confirmed: Boolean = true): AssistantCommandResultDto {
        val response = postJson("/api/assistant/confirm", JSONObject().apply {
            put("commandId", commandId)
            put("confirmed", confirmed)
        })
        return response.toAssistantCommandResultDto()
    }

    fun confirmAssistant(warehouseId: Int, commandId: String, confirmed: Boolean = true): AssistantCommandResultDto {
        val response = postJson("/api/warehouses/$warehouseId/assistant/confirm", JSONObject().apply {
            put("commandId", commandId)
            put("confirmed", confirmed)
        })
        return response.toAssistantCommandResultDto()
    }

    fun clarifyAssistant(commandId: String, choiceId: String): AssistantCommandDto {
        val response = postJson(
            path = "/api/assistant/clarify",
            body = JSONObject().apply {
                put("commandId", commandId)
                put("choiceId", choiceId)
            },
            timeouts = AssistantTimeouts
        )
        return response.toAssistantCommandDto()
    }

    fun clarifyAssistant(warehouseId: Int, commandId: String, choiceId: String): AssistantCommandDto {
        val response = postJson(
            path = "/api/warehouses/$warehouseId/assistant/clarify",
            body = JSONObject().apply {
                put("commandId", commandId)
                put("choiceId", choiceId)
            },
            timeouts = AssistantTimeouts
        )
        return response.toAssistantCommandDto()
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

private fun JSONObject.toProductDto(): ProductDto {
    return ProductDto(
        id = getInt("id"),
        sku = optString("sku"),
        name = optString("name"),
        unit = optString("unit"),
        minQuantity = optDouble("minQuantity")
    )
}

private fun JSONObject.toStockDto(): StockDto {
    return StockDto(
        productName = optString("productName"),
        sku = optString("sku"),
        cellCode = optString("cellCode"),
        zoneCode = optString("zoneCode"),
        quantity = optDouble("quantity"),
        unit = optString("unit")
    )
}

private fun JSONObject.toOperationDto(): OperationDto {
    return OperationDto(
        id = getInt("id"),
        warehouseId = optInt("warehouseId"),
        type = optString("type"),
        productName = optString("productName"),
        sourceCell = optNullableString("sourceCell"),
        targetCell = optNullableString("targetCell"),
        appUserId = optNullableInt("appUserId"),
        performedBy = optNullableString("performedBy"),
        quantity = optDouble("quantity"),
        comment = optNullableString("comment"),
        createdAt = optString("createdAt")
    )
}

private fun JSONObject.toWarehouseSnapshot(): WarehouseSnapshot {
    return WarehouseSnapshot(
        products = optJSONArray("products")?.mapObjects { it.toProductDto() }.orEmpty(),
        cells = optJSONArray("cells")?.mapObjects {
            CellDto(
                id = it.getInt("id"),
                code = it.optString("code"),
                name = it.optString("name"),
                warehouseId = it.optInt("warehouseId"),
                zoneCode = it.optString("zoneCode"),
                warehouseName = it.optString("warehouseName")
            )
        }.orEmpty(),
        stocks = optJSONArray("stocks")?.mapObjects { it.toStockDto() }.orEmpty(),
        operations = optJSONArray("operations")?.mapObjects { it.toOperationDto() }.orEmpty()
    )
}

private fun JSONObject.toBarcodeResolveDto(): BarcodeResolveDto {
    return BarcodeResolveDto(
        found = optBoolean("found"),
        value = optString("value"),
        entityType = optNullableString("entityType"),
        entityId = optNullableInt("entityId"),
        title = optNullableString("title"),
        subtitle = optNullableString("subtitle")
    )
}

private fun JSONObject.toAssistantCommandDto(): AssistantCommandDto {
    return AssistantCommandDto(
        commandId = optString("commandId"),
        mode = optString("mode"),
        provider = optString("provider"),
        commandType = optString("commandType"),
        riskLevel = optString("riskLevel"),
        productId = optNullableInt("productId"),
        sourceCellId = optNullableInt("sourceCellId"),
        targetCellId = optNullableInt("targetCellId"),
        quantity = optNullableDouble("quantity"),
        minQuantity = optNullableDouble("minQuantity"),
        summary = optString("summary"),
        requiresConfirmation = optBoolean("requiresConfirmation"),
        clarificationQuestion = optNullableString("clarificationQuestion"),
        clarificationTarget = optNullableString("clarificationTarget"),
        choices = optJSONArray("choices")?.mapObjects {
            AssistantChoiceDto(
                id = it.optString("id"),
                label = it.optString("label"),
                kind = it.optString("kind")
            )
        }.orEmpty()
    )
}

private fun JSONObject.toAssistantCommandResultDto(): AssistantCommandResultDto {
    return AssistantCommandResultDto(
        success = optBoolean("success"),
        message = optString("message"),
        details = optJSONArray("details")?.mapStrings().orEmpty()
    )
}

private fun JSONObject.toWarehouseSetupResultDto(): WarehouseSetupResultDto {
    return WarehouseSetupResultDto(
        warehouseId = getInt("warehouseId"),
        warehouseName = optString("warehouseName"),
        roleCode = optString("roleCode"),
        roleName = optString("roleName"),
        zonesCreated = optInt("zonesCreated"),
        cellsCreated = optInt("cellsCreated")
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

private fun JSONObject.optNullableDouble(name: String): Double? {
    return if (isNull(name)) null else optDouble(name)
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
