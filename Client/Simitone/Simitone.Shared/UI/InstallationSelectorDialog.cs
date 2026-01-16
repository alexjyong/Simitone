using Eto.Forms;
using Eto.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Windows.UI
{
    /// <summary>
    /// Cross-platform dialog for selecting The Sims 1 installation
    /// </summary>
    public class InstallationSelectorDialog : Dialog<InstallationSelectionResult?>
    {
        private GridView installationGrid;
        private List<InstallationInfo> installations;

        public InstallationSelectorDialog(List<InstallationInfo> installations)
        {
            this.installations = installations;
            
            Title = "Select The Sims 1 Installation";
            MinimumSize = new Size(700, 400);
            Resizable = true;

            BuildContent();
        }

        private void BuildContent()
        {
            // Header label
            var headerLabel = new Label
            {
                Text = "Multiple The Sims 1 installations were detected. Please select one:",
                Font = SystemFonts.Bold()
            };

            // GridView for installations (like ListBox but more flexible)
            installationGrid = new GridView
            {
                ShowHeader = true,
                AllowMultipleSelection = false,
                GridLines = GridLines.Horizontal
            };

            // Define columns
            installationGrid.Columns.Add(new GridColumn
            {
                HeaderText = "Type",
                DataCell = new TextBoxCell { Binding = Binding.Property<InstallationInfo, string>(i => i.TypeDescription) },
                Width = 200
            });

            installationGrid.Columns.Add(new GridColumn
            {
                HeaderText = "Path",
                DataCell = new TextBoxCell { Binding = Binding.Property<InstallationInfo, string>(i => i.Path) },
                AutoSize = true
            });

            installationGrid.Columns.Add(new GridColumn
            {
                HeaderText = "Steam",
                DataCell = new TextBoxCell { Binding = Binding.Property<InstallationInfo, string>(i => i.IsSteam ? "Yes" : "No") },
                Width = 80
            });

            // Populate data
            installationGrid.DataStore = installations;

            // Select first item by default
            if (installations.Count > 0)
                installationGrid.SelectedRow = 0;

            // Double-click to select
            installationGrid.CellDoubleClick += (s, e) => SelectCurrentInstallation();

            // Buttons
            var selectButton = new Button { Text = "Select" };
            selectButton.Click += (s, e) => SelectCurrentInstallation();

            var browseButton = new Button { Text = "Browse..." };
            browseButton.Click += (s, e) => BrowseForInstallation();

            var cancelButton = new Button { Text = "Cancel" };
            cancelButton.Click += (s, e) => Close(null);

            // Layout
            Content = new TableLayout
            {
                Padding = new Padding(10),
                Spacing = new Size(5, 5),
                Rows =
                {
                    new TableRow(headerLabel),
                    new TableRow(installationGrid) { ScaleHeight = true },
                    new TableLayout
                    {
                        Spacing = new Size(5, 5),
                        Rows =
                        {
                            new TableRow(
                                new TableCell { ScaleWidth = true }, // Spacer
                                selectButton,
                                browseButton,
                                cancelButton
                            )
                        }
                    }
                }
            };

            // Set default button
            DefaultButton = selectButton;
            AbortButton = cancelButton;
        }

        private void SelectCurrentInstallation()
        {
            if (installationGrid.SelectedItem is InstallationInfo selected)
            {
                Close(new InstallationSelectionResult
                {
                    Path = selected.Path,
                    IsSteam = selected.IsSteam,
                    Source = "selected"
                });
            }
        }

        private void BrowseForInstallation()
        {
            var dialog = new SelectFolderDialog
            {
                Title = "Select The Sims 1 Installation Folder"
            };

            if (dialog.ShowDialog(this) == DialogResult.Ok)
            {
                var selectedPath = dialog.Directory;
                
                // Validate it's a real TS1 installation
                var behaviorPath = System.IO.Path.Combine(selectedPath, "GameData", "Behavior.iff");
                if (System.IO.File.Exists(behaviorPath))
                {
                    // Normalize the path
                    var normalizedPath = selectedPath.Replace('\\', '/');
                    if (!normalizedPath.EndsWith("/")) normalizedPath += "/";
                    
                    // Add to the list and select it
                    var newInstall = new InstallationInfo("Custom Location", normalizedPath, GameLocator.TS1InstallationType.Portable);
                    installations.Add(newInstall);
                    
                    // Refresh the grid and select the new item
                    installationGrid.DataStore = installations;
                    installationGrid.SelectedRow = installations.Count - 1;
                }
                else
                {
                    MessageBox.Show(this,
                        "The selected folder does not appear to be a valid The Sims 1 installation.\n\n" +
                        "Please select the folder containing GameData/Behavior.iff",
                        "Invalid Installation",
                        MessageBoxType.Error);
                }
            }
        }
    }

    /// <summary>
    /// Information about a detected installation
    /// </summary>
    public class InstallationInfo
    {
        public string TypeDescription { get; set; }
        public string Path { get; set; }
        public bool IsSteam { get; set; }
        public GameLocator.TS1InstallationType Type { get; set; }

        public InstallationInfo(string description, string path, GameLocator.TS1InstallationType type)
        {
            TypeDescription = description;
            Path = path;
            Type = type;
            IsSteam = (type == GameLocator.TS1InstallationType.Steam);
        }
    }

    /// <summary>
    /// Result from the installation selector
    /// </summary>
    public class InstallationSelectionResult
    {
        public string Path { get; set; } = string.Empty;
        public bool IsSteam { get; set; }
        public string Source { get; set; } = string.Empty; // "selected" or "browse"
    }
}
