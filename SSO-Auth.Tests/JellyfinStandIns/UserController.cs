#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Mvc;

// Stand-ins with the same type name, routes, action names and parameter names as Jellyfin 12's
// Jellyfin.Api.Controllers.UserController, which is what PasswordLoginFilter matches on.
namespace Jellyfin.Api.Controllers;

public class AuthenticateUserByName
{
    public string Username { get; set; }

    public string Pw { get; set; }
}

public class QuickConnectDto
{
    public string Secret { get; set; }
}

[ApiController]
[Route("Users")]
public class UserController : ControllerBase
{
    [HttpGet("Public")]
    public ActionResult<string> GetPublicUsers() => "[]";

    [HttpPost("{userId}/Authenticate")]
    public ActionResult<string> AuthenticateUser(
        [FromRoute, Required] Guid userId,
        [FromQuery, Required] string pw,
        [FromServices] IUserManager users)
        => Content("signed-in-by-id:" + users.GetUserById(userId)?.Username);

    [HttpPost("AuthenticateByName")]
    public ActionResult<string> AuthenticateUserByName([FromBody, Required] AuthenticateUserByName request)
        => Content("signed-in:" + request.Username);

    [HttpPost("AuthenticateWithQuickConnect")]
    public ActionResult<string> AuthenticateWithQuickConnect([FromBody, Required] QuickConnectDto request)
        => Content("quick-connect");
}
