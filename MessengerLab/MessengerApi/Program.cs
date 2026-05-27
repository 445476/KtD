using MessengerApi.Services;
using MessengerApi.Storage;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Реєстрація залежностей (Services та Storage)
builder.Services.AddSingleton<JsonDatabase>();
builder.Services.AddScoped<MessageService>();

var app = builder.Build();

// --- API Routes ---

app.MapPost("/users", ([FromBody] CreateUserDto req, MessageService service) =>
{
    try { return Results.Created("", service.CreateUser(req.Name)); }
    catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/conversations", ([FromBody] CreateConvDto req, MessageService service) =>
{
    return Results.Created("", service.CreateConversation(req.Type));
});

app.MapPost("/messages", ([FromBody] SendMessageDto req, MessageService service) =>
{
    try { return Results.Created("", service.SendMessage(req.ConversationId, req.SenderId, req.Text)); }
    catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapGet("/conversations/{id}/messages", (string id, MessageService service) =>
{
    return Results.Ok(service.GetMessages(id));
});

app.Run();

// DTOs для мапінгу вхідних JSON запитів
record CreateUserDto(string Name);
record CreateConvDto(string Type);
record SendMessageDto(string ConversationId, string SenderId, string Text);

// Додаємо цей рядок для інтеграційних тестів
public partial class Program { }