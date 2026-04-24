using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrelloClone.Api.Services;

namespace TrelloClone.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class PresenceController(IPresenceService presence) : ControllerBase
{
    [HttpGet]
    public IActionResult GetOnlineUsers() => Ok(presence.GetOnlineUsers());
}
