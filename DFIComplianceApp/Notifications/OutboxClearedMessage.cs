using CommunityToolkit.Mvvm.Messaging.Messages;
namespace DFIComplianceApp.Notifications;

public sealed class OutboxClearedMessage : ValueChangedMessage<int>   // value = unsent‑count
{
    public OutboxClearedMessage(int remaining) : base(remaining) { }
}
