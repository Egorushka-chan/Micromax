package com.bezborodov.micromax.data

open class ApiException(message: String) : IllegalStateException(message)

class UnauthorizedException(
    message: String = "Сессия истекла. Войдите снова."
) : ApiException(message)
