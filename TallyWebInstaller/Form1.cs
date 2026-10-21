
using System;
using System.Drawing;
using System.Windows.Forms;

namespace TallyWebInstaller;

public partial class Form1 : Form
{
    private readonly Button installButton;
    private readonly ProgressBar progressBar;
    private readonly Label statusLabel;

    public Form1()
    {
        InitializeComponent();

        Text = "SAI INFOSYS | TallyWeb Setup";
        Size = new Size(650, 430);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.White;

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 105,
            BackColor = Color.FromArgb(15, 43, 85)
        };

        var companyLabel = new Label
        {
            Text = "SAI INFOSYS",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(28, 16)
        };

        var subtitle = new Label
        {
            Text = "TallyWeb Sync Agent Installer",
            ForeColor = Color.FromArgb(210, 225, 245),
            Font = new Font("Segoe UI", 11),
            AutoSize = true,
            Location = new Point(31, 65)
        };

        header.Controls.Add(companyLabel);
        header.Controls.Add(subtitle);
        Controls.Add(header);

        var heading = new Label
        {
            Text = "Welcome to TallyWeb Setup",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(30, 130)
        };

        var description = new Label
        {
            Text = "Install the TallyWeb Sync Agent to synchronize your\nTallyPrime data securely with your website.",
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.DimGray,
            AutoSize = true,
            Location = new Point(32, 178)
        };

        progressBar = new ProgressBar
        {
            Location = new Point(32, 260),
            Size = new Size(570, 18),
            Style = ProgressBarStyle.Continuous
        };

        statusLabel = new Label
        {
            Text = "Ready to install",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.Gray,
            AutoSize = true,
            Location = new Point(32, 287)
        };

        installButton = new Button
        {
            Text = "Install",
            Size = new Size(125, 40),
            Location = new Point(477, 325),
            BackColor = Color.FromArgb(15, 43, 85),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            UseVisualStyleBackColor = false
        };

        installButton.FlatAppearance.BorderSize = 0;


installButton.Click += (_, _) =>
{
    try
    {
        installButton.Enabled = false;
        statusLabel.Text = "Extracting Agent...";
        progressBar.Value = 20;

        string installPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "SaiInfosys",
            "TallyWebAgent-Local"
        );

        Directory.CreateDirectory(installPath);

        string exePath = Path.Combine(
            installPath,
            "TallyWebSyncAgent.exe"
        );

        ExtractEmbeddedResource("TallyWebSyncAgent.exe", exePath);
        ExtractEmbeddedResource(
            "TallyWebSyncAgent.appsettings.Local.json",
            Path.Combine(installPath, "appsettings.Local.json"));

        const string taskName = "TallyWeb Sync Agent (Local)";
        string taskAction =
            $"cmd.exe /c set DOTNET_ENVIRONMENT=Local&& \"{exePath}\"";

        RunScheduledTaskCommand("/Create", taskName, taskAction);
        RunScheduledTaskCommand("/Run", taskName);

        progressBar.Value = 100;
        statusLabel.Text = "Local sync agent installed and started.";

        MessageBox.Show(
            "The local TallyWeb sync agent was installed and started.\n\n" +
            "It will sync to the local API at https://localhost:7108/api/.\n" +
            "Installed at:\n" + installPath,
            "SAI INFOSYS",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }
    catch (Exception ex)
    {
        statusLabel.Text = "Installation test failed";
        MessageBox.Show(
            ex.Message,
            "SAI INFOSYS",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error
        );
    }
    finally
    {
        installButton.Enabled = true;
    }
};


        Controls.Add(heading);
        Controls.Add(description);
        Controls.Add(progressBar);
        Controls.Add(statusLabel);
        Controls.Add(installButton);
    }

    private static void ExtractEmbeddedResource(
        string resourceName,
        string destinationPath)
    {
        using Stream? resource = typeof(Form1).Assembly
            .GetManifestResourceStream(resourceName);

        if (resource == null)
        {
            throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' was not found.");
        }

        using FileStream output = new(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        resource.CopyTo(output);
    }

    private static void RunScheduledTaskCommand(
        string operation,
        string taskName,
        string? taskAction = null)
    {
        using var taskProcess = new System.Diagnostics.Process();
        taskProcess.StartInfo.FileName = "schtasks.exe";
        taskProcess.StartInfo.ArgumentList.Add(operation);
        taskProcess.StartInfo.ArgumentList.Add("/TN");
        taskProcess.StartInfo.ArgumentList.Add(taskName);

        if (taskAction != null)
        {
            taskProcess.StartInfo.ArgumentList.Add("/TR");
            taskProcess.StartInfo.ArgumentList.Add(taskAction);
            taskProcess.StartInfo.ArgumentList.Add("/SC");
            taskProcess.StartInfo.ArgumentList.Add("ONLOGON");
            taskProcess.StartInfo.ArgumentList.Add("/F");
        }

        taskProcess.StartInfo.UseShellExecute = false;
        taskProcess.StartInfo.CreateNoWindow = true;
        taskProcess.StartInfo.RedirectStandardOutput = true;
        taskProcess.StartInfo.RedirectStandardError = true;
        taskProcess.Start();

        string error = taskProcess.StandardError.ReadToEnd();
        taskProcess.WaitForExit();

        if (taskProcess.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Scheduled task operation '{operation}' failed: {error}");
        }
    }
}
