using System.Collections.Concurrent;

namespace Web.Security.Testing.Infrastructure.Identity;

public record SentEmail(string Email, string Subject, string Body);

// Registered as a singleton so tests can inspect what the controllers asked the mailer to send
// (e.g. to extract a reset/confirmation token from the email body).
public class SentEmailStore
{
    private readonly ConcurrentQueue<SentEmail> _messages = new();

    public void Add(SentEmail message) => _messages.Enqueue(message);
    public IReadOnlyCollection<SentEmail> Messages => _messages.ToArray();
    public SentEmail? Last => _messages.LastOrDefault();
}
