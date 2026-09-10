using System.Net;
using System.Net.Mail;
using System.Text;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

/// <summary>
/// Email markdown content as an attachment. Fire-and-forget via background thread.
/// Token-guarded, rate-limited.
/// </summary>
public sealed class SaveNotesService : ISaveNotesService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUser;
    private readonly string _smtpPass;
    private readonly string _sendTo;
    private DateTime _lastRequest = DateTime.MinValue;
    private readonly TimeSpan _rateLimit = TimeSpan.FromSeconds(30);
    private readonly object _rateLock = new();

    public SaveNotesService(string smtpHost, int smtpPort, string smtpUser, string smtpPass, string sendTo)
    {
        _smtpHost = smtpHost;
        _smtpPort = smtpPort;
        _smtpUser = smtpUser;
        _smtpPass = smtpPass;
        _sendTo = sendTo;
    }

    public SaveNotesResponse SaveAndEmail(string subject, string content)
    {
        lock (_rateLock)
        {
            if (DateTime.UtcNow - _lastRequest < _rateLimit)
                throw new InvalidOperationException("Rate limited. Try again shortly.");
            _lastRequest = DateTime.UtcNow;
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var filename = $"notes-{timestamp}.md";

        // Fire and forget
        _ = Task.Run(() => SendEmailAsync(subject, content, timestamp, filename));

        return new SaveNotesResponse
        {
            Status = "accepted",
            Filename = filename,
            To = _sendTo
        };
    }

    private async Task SendEmailAsync(string subject, string content, string timestamp, string filename)
    {
        try
        {
            using var message = new MailMessage(_smtpUser, _sendTo)
            {
                Subject = subject,
                Body = $"LucyAPI saved a conversation summary.\n\nSubject: {subject}\nTimestamp: {timestamp} UTC"
            };

            var attachment = Attachment.CreateAttachmentFromString(content, filename, Encoding.UTF8, "application/octet-stream");
            message.Attachments.Add(attachment);

            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                Credentials = new NetworkCredential(_smtpUser, _smtpPass),
                EnableSsl = true
            };

            await client.SendMailAsync(message);
        }
        catch
        {
            // Fire and forget — no one to report to
        }
    }
}
