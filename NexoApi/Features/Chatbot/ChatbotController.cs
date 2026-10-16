using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Chatbot.Dtos;

namespace NexoApi.Features.Chatbot;

[ApiController]
[Route("api/chatbot")]
[Authorize]
public class ChatbotController : ControllerBase
{
    private readonly IChatbotService _svc;
    public ChatbotController(IChatbotService svc) => _svc = svc;

    [HttpPost("mensaje")]
    public async Task<IActionResult> Mensaje([FromBody] ChatRequest req)
    {
        try
        {
            var resp = await _svc.ChatAsync(req);
            return Ok(resp);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
