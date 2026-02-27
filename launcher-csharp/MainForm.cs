using System;
using System.Diagnostics;
using System.Drawing;
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
    private const string DefaultLauncherToken = "n14-launcher";

    private static readonly Color Bg = Color.FromArgb(8, 14, 8);
    private static readonly Color PanelBg = Color.FromArgb(12, 24, 12);
    private static readonly Color Border = Color.FromArgb(46, 92, 46);
    private static readonly Color TextColor = Color.FromArgb(184, 255, 184);
    private static readonly Color Muted = Color.FromArgb(130, 190, 130);
    private static readonly Color InputBg = Color.FromArgb(7, 16, 7);
    private static readonly Color BtnBg = Color.FromArgb(16, 48, 16);
    private static readonly Font UiFont = new("Consolas", 10F, FontStyle.Regular);
    private static readonly Font TitleFont = new("Consolas", 15F, FontStyle.Bold);

    private readonly TextBox _apiBaseBox = new() { Text = DefaultApiBase };
    private readonly TextBox _siteUrlBox = new() { Text = DefaultSiteUrl };
    private readonly TextBox _launcherTokenBox = new() { Text = DefaultLauncherToken };
    private readonly TextBox _usernameBox = new();
    private readonly TextBox _passwordBox = new() { UseSystemPasswordChar = true };
    private readonly TextBox _confirmBox = new() { UseSystemPasswordChar = true };
    private readonly Label _statusLabel = new();
    private readonly Button _pcUserButton = new() { Text = "[ Use PC User ]" };
    private readonly Button _registerButton = new() { Text = "[ Register Account ]" };
    private readonly Button _openSiteButton = new() { Text = "[ Open Website ]" };

    private static readonly HttpClient Http = new();

    public MainForm()
    {
        Text = "N14 Launcher";
        Width = 540;
        Height = 630;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg;
        ForeColor = TextColor;
        Font = UiFont;
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // keep default icon if extraction fails
        }

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            BackColor = PanelBg,
            ColumnCount = 1,
            RowCount = 20,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.Paint += (_, e) =>
        {
            ControlPaint.DrawBorder(e.Graphics, panel.ClientRectangle, Border, ButtonBorderStyle.Solid);
        };

        var title = new Label
        {
            Text = "N14_LAUNCHER",
            AutoSize = true,
            Font = TitleFont,
            ForeColor = TextColor,
            Margin = new Padding(3, 0, 3, 2),
        };
        panel.Controls.Add(title);
        panel.Controls.Add(new Label
        {
            Text = "register from launcher, then login on n14.se",
            AutoSize = true,
            ForeColor = Muted,
            Margin = new Padding(3, 0, 3, 12),
        });

        panel.Controls.Add(MakeLabel("API Base URL"));
        panel.Controls.Add(_apiBaseBox);

        panel.Controls.Add(MakeLabel("Website URL"));
        panel.Controls.Add(_siteUrlBox);

        panel.Controls.Add(MakeLabel("Launcher Register Token"));
        panel.Controls.Add(_launcherTokenBox);

        panel.Controls.Add(MakeLabel("Username"));
        panel.Controls.Add(_usernameBox);
        panel.Controls.Add(_pcUserButton);

        panel.Controls.Add(MakeLabel("Password"));
        panel.Controls.Add(_passwordBox);

        panel.Controls.Add(MakeLabel("Confirm Password"));
        panel.Controls.Add(_confirmBox);

        panel.Controls.Add(_registerButton);
        panel.Controls.Add(_openSiteButton);
        panel.Controls.Add(_statusLabel);

        foreach (var box in new[] { _apiBaseBox, _siteUrlBox, _launcherTokenBox, _usernameBox, _passwordBox, _confirmBox })
        {
            StyleTextBox(box);
        }
        foreach (var btn in new[] { _pcUserButton, _registerButton, _openSiteButton })
        {
            StyleButton(btn);
        }
        _statusLabel.AutoSize = false;
        _statusLabel.Height = 56;
        _statusLabel.ForeColor = Muted;
        _statusLabel.Margin = new Padding(3, 10, 3, 0);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.BorderStyle = BorderStyle.FixedSingle;
        _statusLabel.Padding = new Padding(8, 0, 8, 0);
        _statusLabel.BackColor = InputBg;

        Controls.Add(panel);

        var envApi = Environment.GetEnvironmentVariable("N14_API_BASE");
        var envSite = Environment.GetEnvironmentVariable("N14_SITE_URL");
        var envLauncherToken = Environment.GetEnvironmentVariable("N14_LAUNCHER_REGISTER_TOKEN");
        if (!string.IsNullOrWhiteSpace(envApi)) _apiBaseBox.Text = envApi.Trim();
        if (!string.IsNullOrWhiteSpace(envSite)) _siteUrlBox.Text = envSite.Trim();
        if (!string.IsNullOrWhiteSpace(envLauncherToken)) _launcherTokenBox.Text = envLauncherToken.Trim();

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
            var payload = JsonSerializer.Serialize(new { username, password, pcName = Environment.UserName });
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBase}/api/register");
            request.Headers.Add("x-n14-launcher-token", (_launcherTokenBox.Text ?? string.Empty).Trim());
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var res = await Http.SendAsync(request);
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
        _statusLabel.ForeColor = isError ? Color.FromArgb(255, 138, 138) : Muted;
    }

    private static Label MakeLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = TextColor,
            Margin = new Padding(3, 8, 3, 3),
        };
    }

    private static void StyleTextBox(TextBox box)
    {
        box.Width = 480;
        box.BackColor = InputBg;
        box.ForeColor = TextColor;
        box.BorderStyle = BorderStyle.FixedSingle;
        box.Margin = new Padding(3, 0, 3, 6);
    }

    private static void StyleButton(Button button)
    {
        button.Width = 220;
        button.Height = 34;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.BorderSize = 1;
        button.BackColor = BtnBg;
        button.ForeColor = TextColor;
        button.Margin = new Padding(3, 4, 3, 0);
        button.Cursor = Cursors.Hand;
    }
}
