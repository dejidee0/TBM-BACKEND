using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/webhooks/paystack")]
public class PaystackWebhookController : ControllerBase
{
    private readonly PaystackWebhookService _service;

    public PaystackWebhookController(PaystackWebhookService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Handle()
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();

        var signature = Request.Headers["x-paystack-signature"].ToString();

        if (!_service.IsValidSignature(payload, signature))
            return Unauthorized("Invalid signature");

        await _service.HandleAsync(payload);

        return Ok();
    }
}
