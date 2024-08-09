using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using HtmlAgilityPack;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.UI.Common.Helper;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Button = Avalonia.Controls.Button;


namespace Ryujinx.Ava.UI.Windows
{
    public partial class ChangelogWindow : StyleableWindow
    {
        public ChangelogWindow()
        {
            DataContext = this;
            InitializeComponent();
            InitializeAsync();
            Title = $"Ryujinx {Program.Version} - " + LocaleManager.Instance[LocaleKeys.ChangelogWindowTitle];
        }

        private async void InitializeAsync()
        {
            try
            {
                LoadingTextBlock.IsVisible = true;  // Show the loading text
                ChangelogTextBlock.IsVisible = false;  // Hide the changelog text initially

                string changelogHtml = await FetchChangelogHtml();
                string changelog = ParseChangelogForRecentVersions(changelogHtml, 10);
                LoadChangelog(changelog);

                LoadingTextBlock.IsVisible = false;  // Hide the loading text
                ChangelogTextBlock.IsVisible = true;  // Show the changelog text
            }
            catch (Exception ex)
            {
                LoadingTextBlock.Text = "Failed to load changelog: " + ex.Message;
            }
        }

        private void LoadChangelog(string changelog)
        {
            ChangelogTextBlock.Text = changelog;
        }

        private static async Task<string> FetchChangelogHtml()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Ryujinx-Updater/1.0.0");
            return await client.GetStringAsync("https://github.com/Ryujinx/Ryujinx/wiki/Changelog");
        }

        private static string ParseChangelogForRecentVersions(string html, int count)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var headers = doc.DocumentNode.SelectNodes("//div[contains(@class, 'markdown-heading')]//h2");
            if (headers != null)
            {
                var content = new StringBuilder();
                int versionsFound = 0;

                foreach (var header in headers)
                {
                    if (versionsFound >= count)
                        break; // Stop after finding the desired number of versions

                    content.Append(header.OuterHtml);
                    var currentNode = header.ParentNode.NextSibling;

                    while (currentNode != null && versionsFound < count)
                    {
                        if (currentNode.Name == "div" && currentNode.SelectSingleNode("h2") != null)
                        {
                            versionsFound++; // Increment for each version header found
                            if (versionsFound >= count)
                                break;
                        }
                        content.Append(currentNode.OuterHtml);
                        currentNode = currentNode.NextSibling;
                    }
                }

                return ConvertHtmlToPlainText(content.ToString());
            }
            return "No changelog found.";
        }

        private static string ConvertHtmlToPlainText(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Recursively format nodes
            string formattedText = FormatNode(doc.DocumentNode);

            return HtmlEntity.DeEntitize(formattedText);
        }

        private static string FormatNode(HtmlNode node, int depth = 0)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var child in node.ChildNodes)
            {
                switch (child.Name)
                {
                    case "ul":
                        // Recursively format the list items
                        sb.Append(FormatNode(child, depth));
                        sb.AppendLine();
                        break;

                    case "li":
                        // Format list item based on depth: "-" for top-level, "+" for nested items
                        string prefix = (depth == 0 ? "- " : new string(' ', depth * 4) + "+ ");
                        sb.AppendLine($"{prefix}{FormatNode(child, depth + 1).Trim()}");
                        break;

                    case "p":
                    case "#text":  // Handling direct text nodes
                        if (!string.IsNullOrWhiteSpace(child.InnerText))
                        {
                            // Trim the text
                            string text = HtmlEntity.DeEntitize(child.InnerText).Trim();
                            sb.Append($"{text}\n");
                        }
                        break;

                    default:
                        // Recursively process other types of nodes
                        if (child.HasChildNodes)
                        {
                            sb.Append(FormatNode(child, depth));  // Keep current depth for other types
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
