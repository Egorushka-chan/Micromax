using Microsoft.AspNetCore.Mvc;

namespace MicroMax.Server.Controllers;

/// <summary>
/// Базовый API-контроллер с общими HTTP-хелперами.
/// </summary>
[ApiController]
public abstract class MicroMaxControllerBase : ControllerBase
{
    protected CreatedResult CreatedResource(string location, object value) => Created(location, value);
}
