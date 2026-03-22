using AgentApp.Backend.Models;
using AgentApp.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentApp.Backend.Controllers;

[ApiController]
[Route("api/conversations")]
public class ConversationsController(ConversationStore store) : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() => Ok(store.GetAll());

    [HttpPost]
    public IActionResult Create()
    {
        var c = store.Create();
        return Ok(c.ToSummary());
    }

    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        var c = store.Get(id);
        if (c is null) return NotFound();
        return Ok(new
        {
            c.Id,
            c.Title,
            c.CreatedAt,
            Messages = c.Messages
        });
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        if (!store.Delete(id)) return NotFound();
        return NoContent();
    }
}
