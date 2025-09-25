using Microsoft.Maui.Controls;

namespace DFIComplianceApp.Helpers
{
    public static class NavigationPageExtensions
    {
        public static NavigationPage WithPage(this NavigationPage navPage, Page page)
        {
            navPage.PushAsync(page);
            return navPage;
        }
    }
}
