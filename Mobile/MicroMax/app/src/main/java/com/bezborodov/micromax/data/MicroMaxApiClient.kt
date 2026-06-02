package com.bezborodov.micromax.data

import org.json.JSONArray
import org.json.JSONObject
import java.io.OutputStreamWriter
import java.net.HttpURLConnection
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
    val commandType: String,
    val summary: String,
    val requiresConfirmation: Boolean
)

class MicroMaxApiClient(
    private val baseUrl: String = "http://10.0.2.2:5101"
) {
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
            put("comment", "Операция из мобильного приложения")
        })
    }

    fun writeOff(productId: Int, sourceCellId: Int, quantity: Double) {
        postJson("/api/operations/write-off", JSONObject().apply {
            put("productId", productId)
            put("sourceCellId", sourceCellId)
            put("quantity", quantity)
            put("userId", JSONObject.NULL)
            put("comment", "Операция из мобильного приложения")
        })
    }

    fun move(productId: Int, sourceCellId: Int, targetCellId: Int, quantity: Double) {
        postJson("/api/operations/move", JSONObject().apply {
            put("productId", productId)
            put("sourceCellId", sourceCellId)
            put("targetCellId", targetCellId)
            put("quantity", quantity)
            put("userId", JSONObject.NULL)
            put("comment", "Операция из мобильного приложения")
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
        val response = postJson("/api/assistant/interpret", JSONObject().put("text", text))
        return AssistantCommandDto(
            commandId = response.optString("commandId"),
            commandType = response.optString("commandType"),
            summary = response.optString("summary"),
            requiresConfirmation = response.optBoolean("requiresConfirmation")
        )
    }

    fun confirmAssistant(commandId: String) {
        postJson("/api/assistant/confirm", JSONObject().apply {
            put("commandId", commandId)
            put("confirmed", true)
        })
    }

    private fun getArray(path: String): JSONArray {
        val connection = openConnection(path, "GET")
        return readResponse(connection).let(::JSONArray)
    }

    private fun postJson(path: String, body: JSONObject): JSONObject {
        val connection = openConnection(path, "POST")
        connection.doOutput = true
        connection.setRequestProperty("Content-Type", "application/json")
        OutputStreamWriter(connection.outputStream).use { it.write(body.toString()) }
        return readResponse(connection).let(::JSONObject)
    }

    private fun putJson(path: String, body: JSONObject): JSONObject {
        val connection = openConnection(path, "PUT")
        connection.doOutput = true
        connection.setRequestProperty("Content-Type", "application/json")
        OutputStreamWriter(connection.outputStream).use { it.write(body.toString()) }
        return readResponse(connection).let(::JSONObject)
    }

    private fun openConnection(path: String, method: String): HttpURLConnection {
        return (URL("$baseUrl$path").openConnection() as HttpURLConnection).apply {
            requestMethod = method
            connectTimeout = 5000
            readTimeout = 10000
        }
    }

    private fun readResponse(connection: HttpURLConnection): String {
        val stream = if (connection.responseCode in 200..299) connection.inputStream else connection.errorStream
        val text = stream.bufferedReader().readText()
        if (connection.responseCode !in 200..299) {
            val message = runCatching { JSONObject(text).optString("error") }.getOrDefault(text)
            error(message.ifBlank { "Ошибка сервера: ${connection.responseCode}" })
        }
        return text
    }
}

private fun <T> JSONArray.mapObjects(transform: (JSONObject) -> T): List<T> {
    return (0 until length()).map { index -> transform(getJSONObject(index)) }
}

private fun JSONObject.optNullableString(name: String): String? {
    return if (isNull(name)) null else optString(name)
}
