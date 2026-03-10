using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.Contact;
using TBM.Application.Interfaces;

namespace TBM.API.Controllers.V1;

[ApiController]
[Route("api/v1/contact")]
[EnableRateLimiting("DynamicPolicy")]
public class ContactController : ControllerBase
{
    private readonly IContactService _contactService;

    public ContactController(IContactService contactService)
    {
        _contactService = contactService;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContactMessageDto dto)
    {
        var result = await _contactService.SubmitAsync(dto);
        if (!result.Success || result.Data == null)
        {
            return BadRequest(result);
        }

        return Ok(new
        {
            accepted = result.Data.Accepted,
            referenceId = result.Data.ReferenceId
        });
    }
}
