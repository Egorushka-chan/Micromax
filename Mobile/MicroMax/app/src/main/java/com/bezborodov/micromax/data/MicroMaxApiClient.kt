package com.bezborodov.micromax.data

import org.json.JSONArray
import org.json.JSONObject
import java.io.OutputStreamWriter
import java.net.HttpURLConnection
import java.net.SocketTimeoutException
import java.net.URL

data class ProductDto(val id: Int, val sku: String, val name: String, val unit: String, val minQuantity: Double)
data class CellDto(val id: Int, val code: String, val name: String)
data class StockDto(val productName: String, val sku: String, val cellCode: String, val zoneCode: String, val quantity: Double, val unit: String)
data class OperationDto(val id: Int, val type: String, val productName: String, val sourceCell: String?, val targetCell: String?, val quantity: Double, val createdAt: String)
data class CreateProductRequest(val sku: String, val name: String, val unit: String, val minQuantity: Double)
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
data class AssistantChoiceDto(val id: String, val label: String, val kind: String)
data class AssistantCommandDefinitionDto(
    val type: String,
    val title: String,
    val description: String,
    val riskLevel: String,
    val examples: List<String>
)
data class AssistantCommandResultDto(val success: Boolean, val message: String, val details: List<String>)

class MicroMaxApiClient(
    private val baseUrl: String = "http://10.0.2.2:5101"
) {
    private data class RequestTimeouts(
        val connectTimeoutMs: Int = 5000,
        val readTimeoutMs: Int = 10000
    )

    private companion object {
        val DefaultTimeouts = RequestTimeouts()
        val AssistantTimeouts = RequestTimeouts(readTimeoutMs = 120000)
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
                    name = it.optString("name")
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
                    type = it.optString("type"),
                    productName = it.optString("productName"),
                    sourceCell = it.optNullableString("sourceCell"),
                    targetCell = it.optNullableString("targetCell"),
                    quantity = it.optDouble("quantity"),
                    createdAt = it.optString("createdAt")
                )
            }
        )
    }

    fun receive(productId: Int, targetCellId: Int, quantity: Double) {
        postJson("/api/operations/receive", JSONObject().apply {
            put("productId", productId)
            put("targetCellId", targetCellId)
            put("quantity", quantity)
            put("userId", JSONObject.NULL)
            put("comment", "РћРїРµСЂР°С†РёСЏ РёР· РјРѕР±РёР»СЊРЅРѕРіРѕ РїСЂРёР»РѕР¶РµРЅРёСЏ")
        })
    }

    fun writeOff(productId: Int, sourceCellId: Int, quantity: Double) {
        postJson("/api/operations/write-off", JSONObject().apply {
            put("productId", productId)
            put("sourceCellId", sourceCellId)
            put("quantity", quantity)
            put("userId", JSONObject.NULL)
            put("comment", "РћРїРµСЂР°С†РёСЏ РёР· РјРѕР±РёР»СЊРЅРѕРіРѕ РїСЂРёР»РѕР¶РµРЅРёСЏ")
        })
    }

    fun move(productId: Int, sourceCellId: Int, targetCellId: Int, quantity: Double) {
        postJson("/api/operations/move", JSONObject().apply {
            put("productId", productId)
            put("sourceCellId", sourceCellId)
            put("targetCellId", targetCellId)
            put("quantity", quantity)
            put("userId", JSONObject.NULL)
            put("comment", "РћРїРµСЂР°С†РёСЏ РёР· РјРѕР±РёР»СЊРЅРѕРіРѕ РїСЂРёР»РѕР¶РµРЅРёСЏ")
        })
    }

    fun createProduct(request: CreateProductRequest): ProductDto {
        val response = postJson("/api/products", JSONObject().apply {
            put("sku", request.sku)
            put("name", request.name)
            put("unit", request.unit)
            put("minQuantity", request.minQuantity)
        })

        return ProductDto(
            id = response.getInt("id"),
            sku = response.optString("sku"),
            name = response.optString("name"),
            unit = response.optString("unit"),
            minQuantity = response.optDouble("minQuantity")
        )
    }

    fun updateProductMinQuantity(product: ProductDto, minQuantity: Double): ProductDto {
        val response = putJson("/api/products/${product.id}", JSONObject().apply {
            put("id", product.id)
            put("sku", product.sku)
            put("name", product.name)
            put("unit", product.unit)
            put("minQuantity", minQuantity)
        })

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
            "/api/assistant/interpret",
            JSONObject().put("text", text),
            AssistantTimeouts
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

    private fun getArray(path: String): JSONArray {
        val connection = openConnection(path, "GET")
        try {
            return readResponse(connection).let(::JSONArray)
        } finally {
            connection.disconnect()
        }
    }

    private fun postJson(
        path: String,
        body: JSONObject,
        timeouts: RequestTimeouts = DefaultTimeouts
    ): JSONObject {
        val connection = openConnection(path, "POST", timeouts)
        try {
            connection.doOutput = true
            connection.setRequestProperty("Content-Type", "application/json")
            OutputStreamWriter(connection.outputStream).use { it.write(body.toString()) }
            return readResponse(connection).let(::JSONObject)
        } finally {
            connection.disconnect()
        }
    }

    private fun putJson(path: String, body: JSONObject): JSONObject {
        val connection = openConnection(path, "PUT")
        try {
            connection.doOutput = true
            connection.setRequestProperty("Content-Type", "application/json")
            OutputStreamWriter(connection.outputStream).use { it.write(body.toString()) }
            return readResponse(connection).let(::JSONObject)
        } finally {
            connection.disconnect()
        }
    }

    private fun openConnection(
        path: String,
        method: String,
        timeouts: RequestTimeouts = DefaultTimeouts
    ): HttpURLConnection {
        return (URL("$baseUrl$path").openConnection() as HttpURLConnection).apply {
            requestMethod = method
            connectTimeout = timeouts.connectTimeoutMs
            readTimeout = timeouts.readTimeoutMs
        }
    }

    private fun readResponse(connection: HttpURLConnection): String {
        try {
            val stream = if (connection.responseCode in 200..299) connection.inputStream else connection.errorStream
            val text = stream.bufferedReader().use { it.readText() }
            if (connection.responseCode !in 200..299) {
                val message = runCatching { JSONObject(text).optString("error") }.getOrDefault(text)
                error(message.ifBlank { "РћС€РёР±РєР° СЃРµСЂРІРµСЂР°: ${connection.responseCode}" })
            }
            return text
        } catch (_: SocketTimeoutException) {
            error("РР-РїРѕРјРѕС‰РЅРёРє РЅРµ СѓСЃРїРµР» РѕС‚РІРµС‚РёС‚СЊ Р·Р° РѕС‚РІРµРґС‘РЅРЅРѕРµ РІСЂРµРјСЏ.")
        }
    }
}

private fun <T> JSONArray.mapObjects(transform: (JSONObject) -> T): List<T> {
    return (0 until length()).map { index -> transform(getJSONObject(index)) }
}

private fun JSONArray.mapStrings(): List<String> {
    return (0 until length()).map { index -> optString(index) }
}

private fun JSONObject.optNullableString(name: String): String? {
    return if (isNull(name)) null else optString(name)
}
