using DFIComplianceApp.Models;
using Microsoft.Maui.Controls;
using MyEmailMessage = DFIComplianceApp.Models.EmailMessage;

namespace DFIComplianceApp.Views
{
    public partial class EmailDetailPage : ContentPage
    {
        public EmailDetailPage(MyEmailMessage email)
        {
            InitializeComponent();
            BindingContext = email;

            // Prepare the HTML content for display
            string htmlContent;

            if (!string.IsNullOrEmpty(email.BodyHtml))
            {
                // Wrap the HTML body in a minimal styled HTML document for better rendering
                htmlContent = $@"
                <html>
                <head>
                    <meta charset='utf-8' />
                    <style>
                        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, 'Open Sans', 'Helvetica Neue', sans-serif; padding: 10px; }}
                        img {{ max-width: 100%; height: auto; }}
                        pre {{ white-space: pre-wrap; word-wrap: break-word; }}
                    </style>
                </head>
                <body>{email.BodyHtml}</body>
                </html>";
            }
            else
            {
                // Use a <pre> block with basic styling to display plain text nicely
                var encodedText = System.Net.WebUtility.HtmlEncode(email.BodyPlainText ?? "");
                htmlContent = $@"
                <html>
                <head>
                    <meta charset='utf-8' />
                    <style>
                        body {{ font-family: monospace; padding: 10px; background-color: #f9f9f9; }}
                        pre {{ white-space: pre-wrap; word-wrap: break-word; }}
                    </style>
                </head>
                <body><pre>{encodedText}</pre></body>
                </html>";
            }

            EmailBodyWebView.Source = new HtmlWebViewSource { Html = htmlContent };
        }
    }
}
