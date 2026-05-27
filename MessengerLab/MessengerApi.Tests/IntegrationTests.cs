using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MessengerApi.Models;
using Xunit;

namespace MessengerApi.Tests;

public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client;
    private readonly string _dbPath = "database.json";

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        // Очищаємо БД перед тестом
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task CompleteMessengerFlow_WorksCorrectly()
    {
        // 1. Створення Користувача
        var userRes = await _client.PostAsJsonAsync("/users", new { Name = "Alice" });
        userRes.EnsureSuccessStatusCode();
        var user = await userRes.Content.ReadFromJsonAsync<User>();
        Assert.NotNull(user);

        // 2. Створення Бесіди
        var convRes = await _client.PostAsJsonAsync("/conversations", new { Type = "direct" });
        convRes.EnsureSuccessStatusCode();
        var conv = await convRes.Content.ReadFromJsonAsync<Conversation>();
        Assert.NotNull(conv);

        // 3. Відправка Повідомлення
        var msgPayload = new { ConversationId = conv.Id, SenderId = user.Id, Text = "Hello .NET!" };
        var msgRes = await _client.PostAsJsonAsync("/messages", msgPayload);
        msgRes.EnsureSuccessStatusCode();

        // 4. Отримання Історії
        var historyRes = await _client.GetAsync($"/conversations/{conv.Id}/messages");
        historyRes.EnsureSuccessStatusCode();
        
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var messages = await historyRes.Content.ReadFromJsonAsync<List<Message>>(jsonOptions);
        
        Assert.NotNull(messages);
        Assert.Single(messages);
        Assert.Equal("Hello .NET!", messages[0].Text);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}