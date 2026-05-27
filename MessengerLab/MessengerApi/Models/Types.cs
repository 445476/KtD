namespace MessengerApi.Models;

public record User(string Id, string Name);

public record Conversation(string Id, string Type);

public record Message(string Id, string ConversationId, string SenderId, string Text, DateTime CreatedAt);

public class DatabaseSchema
{
    public List<User> Users { get; set; } = new();
    public List<Conversation> Conversations { get; set; } = new();
    public List<Message> Messages { get; set; } = new();
}