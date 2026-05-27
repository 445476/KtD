using MessengerApi.Models;
using MessengerApi.Storage;

namespace MessengerApi.Services;

public class MessageService
{
    private readonly JsonDatabase _db;

    public MessageService(JsonDatabase db)
    {
        _db = db;
    }

    public User CreateUser(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required");
        
        var db = _db.ReadDb();
        var newUser = new User(Guid.NewGuid().ToString(), name);
        db.Users.Add(newUser);
        _db.WriteDb(db);
        
        return newUser;
    }

    public Conversation CreateConversation(string type = "direct")
    {
        var db = _db.ReadDb();
        var newConv = new Conversation(Guid.NewGuid().ToString(), type);
        db.Conversations.Add(newConv);
        _db.WriteDb(db);
        
        return newConv;
    }

    public Message SendMessage(string conversationId, string senderId, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Message text cannot be empty");

        var db = _db.ReadDb();

        if (!db.Users.Any(u => u.Id == senderId)) throw new ArgumentException("User does not exist");
        if (!db.Conversations.Any(c => c.Id == conversationId)) throw new ArgumentException("Conversation does not exist");

        var newMessage = new Message(
            Guid.NewGuid().ToString(),
            conversationId,
            senderId,
            text,
            DateTime.UtcNow
        );

        db.Messages.Add(newMessage);
        _db.WriteDb(db);
        
        return newMessage;
    }

    public IEnumerable<Message> GetMessages(string conversationId)
    {
        var db = _db.ReadDb();
        return db.Messages.Where(m => m.ConversationId == conversationId).ToList();
    }
}