using CommunityToolkit.Mvvm.Messaging.Messages;
using DFIComplianceApp.Models;

namespace DFIComplianceApp.Messages
{
    /// <summary>
    /// Sent whenever a new <see cref="User"/> is inserted into the database.
    /// The payload is the user just created.
    /// </summary>
    public sealed class UserAddedMessage : ValueChangedMessage<User>
    {
        public UserAddedMessage(User value) : base(value) { }
    }
}
