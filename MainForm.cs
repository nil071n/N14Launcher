using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace N14Launcher;

public sealed class MainForm : Form
{
    private const string DefaultApiBase = "https://n14.se";
    private const string DefaultSiteUrl = "https://n14.se";

    private readonly TextBox _apiBaseBox = new() { Text = DefaultApiBase, Width = 380 };
    private readonly TextBox _siteUrlBox = new() { Text = DefaultSiteUrl, Width = 380 };
    private readonly TextBox _usernameBox = new() { Width = 380 };
    private readonly TextBox _passwordBox = new() { Width = 380, UseSystemPasswordChar = true };
    private readonly TextBox _confirmBox = new() { Width = 380, UseSystemPasswordChar = true };
    private readonly Label _statusLabel = new() { AutoSize = true, Width = 380 };
    private readonly Button _pcUserButton = new() { Text = "Use PC User", Width = 120 };
    private readonly Button _registerButton = new() { Text = "Register Account", Width = 160 };
    private readonly Button _openSiteButton = new() { Text = "Open Website", Width = 120 };

    private static readonly HttpClient Http = new();

    public MainForm()
    {
        Text = "N14 Launcher";
        Width = 460;
        Height = 470;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(12),
            AutoScroll = true,
            WrapContents = false,
        };

        panel.Controls.Add(new Label { Text = "API Base URL", AutoSize = true });
        panel.Controls.Add(_apiBaseBox);

        panel.Controls.Add(new Label { Text = "Website URL", AutoSize = true });
        panel.Controls.Add(_siteUrlBox);

        panel.Controls.Add(new Label { Text = "Username", AutoSize = true });
        panel.Controls.Add(_usernameBox);
        panel.Controls.Add(_pcUserButton);

        panel.Controls.Add(new Label { Text = "Password", AutoSize = true });
        panel.Controls.Add(_passwordBox);

        panel.Controls.Add(new Label { Text = "Confirm Password", AutoSize = true });
        panel.Controls.Add(_confirmBox);

        panel.Controls.Add(_registerButton);
        panel.Controls.Add(_openSiteButton);
        panel.Controls.Add(_statusLabel);

        Controls.Add(panel);

        var envApi = Environment.GetEnvironmentVariable("N14_API_BASE");
        var envSite = Environment.GetEnvironmentVariable("N14_SITE_URL");
        if (!string.IsNullOrWhiteSpace(envApi)) _apiBaseBox.Text = envApi.Trim();
        if (!string.IsNullOrWhiteSpace(envSite)) _siteUrlBox.Text = envSite.Trim();

        _pcUserButton.Click += (_, _) => UsePcUser();
        _registerButton.Click += async (_, _) => await RegisterAsync();
        _openSiteButton.Click += (_, _) => OpenWebsite();
    }

    private void UsePcUser()
    {
        var raw = Environment.UserName?.Trim().ToLowerInvariant() ?? string.Empty;
        var cleaned = Regex.Replace(raw, "[^a-z0-9_]", string.Empty);
        if (cleaned.Length > 20) cleaned = cleaned[..20];
        _usernameBox.Text = cleaned;
        SetStatus($"Username set to \"{cleaned}\"", false);
    }

    private async Task RegisterAsync()
    {
        var username = (_usernameBox.Text ?? string.Empty).Trim().ToLowerInvariant();
        var password = _passwordBox.Text ?? string.Empty;
        var confirm = _confirmBox.Text ?? string.Empty;

        if (!Regex.IsMatch(username, "^[a-z0-9_]{2,20}$"))
        {
            SetStatus("Username must be 2-20 chars (a-z, 0-9, _).", true);
            return;
        }

        if (password.Length < 3)
        {
            SetStatus("Password must be at least 3 characters.", true);
            return;
        }

        if (password != confirm)
        {
            SetStatus("Passwords do not match.", true);
            return;
        }

        var apiBase = (_apiBaseBox.Text ?? string.Empty).Trim().TrimEnd('/');
        if (!Uri.TryCreate(apiBase, UriKind.Absolute, out _))
        {
            SetStatus("API Base URL is invalid.", true);
            return;
        }

        _registerButton.Enabled = false;
        SetStatus("Registering...", false);

        try
        {
            var payload = JsonSerializer.Serialize(new { username, password });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var res = await Http.PostAsync($"{apiBase}/api/register", content);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                var apiError = TryReadError(body);
                SetStatus(apiError ?? "Registration failed.", true);
                return;
            }

            SetStatus($"Registered \"{username}\". Login on website now.", false);
            _passwordBox.Text = string.Empty;
            _confirmBox.Text = string.Empty;
        }
        catch (Exception ex)
        {
            SetStatus($"Request failed: {ex.Message}", true);
        }
        finally
        {
            _registerButton.Enabled = true;
        }
    }

    private void OpenWebsite()
    {
        var site = (_siteUrlBox.Text ?? string.Empty).Trim();
        if (!Uri.TryCreate(site, UriKind.Absolute, out _))
        {
            SetStatus("Website URL is invalid.", true);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = site,
            UseShellExecute = true,
        });
    }

    private static string? TryReadError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var errorEl))
            {
                return errorEl.GetString();
            }
        }
        catch
        {
            // ignore malformed response
        }

        return null;
    }

    private void SetStatus(string text, bool isError)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = isError ? System.Drawing.Color.Firebrick : System.Drawing.Color.DarkGreen;
    }
}
