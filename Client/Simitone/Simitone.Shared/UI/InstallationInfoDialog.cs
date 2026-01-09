using Eto.Forms;
using Eto.Drawing;
using System;

namespace Simitone.Windows.UI
{
    /// <summary>
    /// Cross-platform dialog showing configured installation details
    /// </summary>
    public class InstallationInfoDialog : Dialog
    {
        public InstallationInfoDialog(string installPath, string savesPath, string simitoneSavesPath, bool isSteam)
        {
            Title = "Installation Configured";
            MinimumSize = new Size(500, 300);
            Resizable = false;

            var infoLabel = new Label
            {
                Text = "Simitone has been configured with the following settings:",
                Font = SystemFonts.Bold()
            };

            var installLabel = new Label
            {
                Text = $"Game Installation:\n{installPath}\n"
            };

            var steamLabel = new Label
            {
                Text = $"Steam Installation: {(isSteam ? "Yes" : "No")}\n"
            };

            var ts1SavesLabel = new Label
            {
                Text = $"The Sims 1 Saves:\n{savesPath}\n"
            };

            var simitoneSavesLabel = new Label
            {
                Text = $"Simitone Saves (New):\n{simitoneSavesPath}\n",
                Font = SystemFonts.Bold()
            };

            var noteLabel = new Label
            {
                Text = "Note: Simitone uses separate save files from The Sims 1.\n" +
                       "Your original saves will not be modified.",
                TextColor = Colors.DarkBlue
            };

            var okButton = new Button { Text = "OK" };
            okButton.Click += (s, e) => Close();

            Content = new TableLayout
            {
                Padding = new Padding(10),
                Spacing = new Size(5, 5),
                Rows =
                {
                    infoLabel,
                    new TableRow { ScaleHeight = true },
                    installLabel,
                    steamLabel,
                    ts1SavesLabel,
                    simitoneSavesLabel,
                    new TableRow { ScaleHeight = true },
                    noteLabel,
                    new TableRow { ScaleHeight = true },
                    new TableLayout
                    {
                        Rows = { new TableRow(null, okButton) }
                    }
                }
            };

            DefaultButton = okButton;
        }
    }
}
