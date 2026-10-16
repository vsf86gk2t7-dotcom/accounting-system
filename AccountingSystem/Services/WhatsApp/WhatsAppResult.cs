namespace AccountingSystem.Services.WhatsApp
{
// =========================================
// حالة إرسال رسالة واتساب
// =========================================
public enum WhatsAppStatus
{
Sent = 1,
Simulated = 2,
Failed = 3
}

// =========================================
// نتيجة إرسال رسالة واتساب
// =========================================
public sealed class WhatsAppResult
{
public WhatsAppStatus Status { get; init; }
public string? MessageId { get; init; }
public string? Error { get; init; }

public bool Success =>
Status == WhatsAppStatus.Sent ||
Status == WhatsAppStatus.Simulated;

public bool IsSimulated =>
Status == WhatsAppStatus.Simulated;

public static WhatsAppResult Sent(string? messageId = null) =>
new()
{
Status = WhatsAppStatus.Sent,
MessageId = messageId
};

public static WhatsAppResult Simulated(string? reason = null) =>
new()
{
Status = WhatsAppStatus.Simulated,
Error = reason
};

public static WhatsAppResult Failed(string error) =>
new()
{
Status = WhatsAppStatus.Failed,
Error = error
};
}
}
