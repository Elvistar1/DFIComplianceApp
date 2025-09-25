using DFIComplianceApp.Models;
using System.Collections.ObjectModel;

namespace DFIComplianceApp.Services
{
    public static class UserStore
    {
        public static ObservableCollection<User> Users { get; } = new ObservableCollection<User>();
    }
}
