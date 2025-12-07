using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;

namespace MouseDroid
{
    /// <summary>
    /// Dialog for configuring speedrun timing thresholds, key bindings, and theme.
    /// Changes take effect after saving and restarting the application.
    /// </summary>
    public class ConfigDialog : Form
    {
        /// <summary>
        /// Contains the updated configuration after dialog confirmation
        /// </summary>
        public TimingConfig UpdatedConfig { get; private set; } = new TimingConfig();

        private TextBox timer1Min, timer1Max, timer1HardMax;
        private TextBox timer2Min, timer2Max, timer2HardMax;
        private TextBox timer3Min, timer3Max, timer3HardMax;
        private Button saveButton, cancelButton;
        private TextBox startKeyBox, splitKeyBox, endKeyBox;
        private TextBox? activeKeyBox = null;
        private CheckBox darkModeCheckBox;

        public ConfigDialog(TimingConfig currentConfig)
        {
            Text = "Configure Timing Zones";
            Width = 400;
            Height = 350;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;


            bool isDark = currentConfig.DarkMode;
            BackColor = isDark ? Color.FromArgb(18, 18, 18) : Color.White;
            ForeColor = isDark ? Color.White : Color.Black;

            var layout = new TableLayoutPanel
            {
                RowCount = 10,
                ColumnCount = 4,
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                AutoSize = true
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

            layout.Controls.Add(new Label { Text = "Timer", Anchor = AnchorStyles.Left }, 0, 0);
            layout.Controls.Add(new Label { Text = "Min (ms)", Anchor = AnchorStyles.Left }, 1, 0);
            layout.Controls.Add(new Label { Text = "Max (ms)", Anchor = AnchorStyles.Left }, 2, 0);
            layout.Controls.Add(new Label { Text = "Hard Max", Anchor = AnchorStyles.Left }, 3, 0);

            AddRow(layout, 1, "Timer 1", currentConfig.Timer1, out timer1Min, out timer1Max, out timer1HardMax);
            AddRow(layout, 2, "Timer 2", currentConfig.Timer2, out timer2Min, out timer2Max, out timer2HardMax);
            AddRow(layout, 3, "Timer 3", currentConfig.Timer3, out timer3Min, out timer3Max, out timer3HardMax);


            layout.Controls.Add(new Label { Text = "Key Bindings", Font = new Font(Font, FontStyle.Bold) }, 0, 4);



            layout.Controls.Add(new Label { Text = "Crouch Key:", Anchor = AnchorStyles.Left }, 0, 5);
            startKeyBox = new TextBox { 
                Text = currentConfig.Keys.StartKey.ToString(),
                ReadOnly = true,
                Width = 100
            };
            startKeyBox.Click += (s, e) => StartKeyCapture(startKeyBox);
            layout.Controls.Add(startKeyBox, 1, 5);


            layout.Controls.Add(new Label { Text = "Pause Key:", Anchor = AnchorStyles.Left }, 0, 6);
            splitKeyBox = new TextBox { 
                Text = currentConfig.Keys.SplitKey.ToString(),
                ReadOnly = true,
                Width = 100
            };
            splitKeyBox.Click += (s, e) => StartKeyCapture(splitKeyBox);
            layout.Controls.Add(splitKeyBox, 1, 6);

            layout.Controls.Add(new Label { Text = "Use Key:", Anchor = AnchorStyles.Left }, 0, 7);
            endKeyBox = new TextBox { 
                Text = currentConfig.Keys.EndKey.ToString(),
                ReadOnly = true,
                Width = 100
            };
            endKeyBox.Click += (s, e) => StartKeyCapture(endKeyBox);
            layout.Controls.Add(endKeyBox, 1, 7);


            layout.Controls.Add(new Label { Text = "Dark Mode", Anchor = AnchorStyles.Left }, 0, 8);
            darkModeCheckBox = new CheckBox { 
                Text = "",
                Checked = currentConfig.DarkMode,
                Anchor = AnchorStyles.Left,
                FlatStyle = FlatStyle.Flat,
                BackColor = currentConfig.DarkMode ? Color.FromArgb(30, 30, 30) : SystemColors.Window,
                ForeColor = currentConfig.DarkMode ? Color.White : Color.Black
            };
            darkModeCheckBox.CheckedChanged += DarkMode_CheckedChanged;
            layout.Controls.Add(darkModeCheckBox, 1, 8);

            saveButton = new Button { Text = "Save", DialogResult = DialogResult.OK, Dock = DockStyle.Fill };
            cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Dock = DockStyle.Fill };


            KeyPreview = true;
            KeyDown += ConfigDialog_KeyDown;

            layout.Controls.Add(saveButton, 2, 5);
            layout.Controls.Add(cancelButton, 3, 5);

            saveButton.Click += SaveButton_Click;

            Controls.Add(layout);
        }

        private void AddRow(TableLayoutPanel panel, int rowIndex, string label, TimingZone zone,
            out TextBox minBox, out TextBox maxBox, out TextBox hardMaxBox)
        {
            panel.Controls.Add(new Label { Text = label, Anchor = AnchorStyles.Left }, 0, rowIndex);

            minBox = new TextBox { Text = zone.Min.ToString(), Width = 60 };
            maxBox = new TextBox { Text = zone.Max.ToString(), Width = 60 };
            hardMaxBox = new TextBox { Text = zone.HardMax.ToString(), Width = 60 };

            panel.Controls.Add(minBox, 1, rowIndex);
            panel.Controls.Add(maxBox, 2, rowIndex);
            panel.Controls.Add(hardMaxBox, 3, rowIndex);
        }

        /// <summary>
        /// Validates and saves the configuration:
        /// - Ensures all key bindings are unique
        /// - Verifies timing thresholds are properly ordered (Min < Max < HardMax)
        /// - Creates new config with current values if validation passes
        /// </summary>
        private void SaveButton_Click(object? sender, EventArgs e)
        {
            try
            {
                var startKey = (Keys)Enum.Parse(typeof(Keys), startKeyBox.Text);
                var splitKey = (Keys)Enum.Parse(typeof(Keys), splitKeyBox.Text);
                var endKey = (Keys)Enum.Parse(typeof(Keys), endKeyBox.Text);

                if (startKey == splitKey || startKey == endKey || splitKey == endKey)
                {
                    throw new ArgumentException("All key bindings must be different");
                }

                UpdatedConfig = new TimingConfig
                {
                    Timer1 = ParseZone(timer1Min, timer1Max, timer1HardMax),
                    Timer2 = ParseZone(timer2Min, timer2Max, timer2HardMax),
                    Timer3 = ParseZone(timer3Min, timer3Max, timer3HardMax),
                    Keys = new KeyBindings
                    {
                        StartKey = startKey,
                        SplitKey = splitKey,
                        EndKey = endKey
                    },
                    DarkMode = darkModeCheckBox.Checked
                };

                ValidateZone(UpdatedConfig.Timer1, "Timer 1");
                ValidateZone(UpdatedConfig.Timer2, "Timer 2");
                ValidateZone(UpdatedConfig.Timer3, "Timer 3");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.None;
            }
        }

        private TimingZone ParseZone(TextBox minBox, TextBox maxBox, TextBox hardMaxBox)
        {
            return new TimingZone
            {
                Min = double.Parse(minBox.Text),
                Max = double.Parse(maxBox.Text),
                HardMax = double.Parse(hardMaxBox.Text)
            };
        }

        private void ValidateZone(TimingZone zone, string label)
        {
            if (zone.Min >= zone.Max || zone.Max >= zone.HardMax)
            {
                throw new ArgumentException($"{label}: Ensure Min < Max < HardMax");
            }
        }

        /// <summary>
        /// Initiates key binding capture mode when user clicks a key binding textbox.
        /// Changes appearance to indicate waiting for key press.
        /// </summary>
        private void StartKeyCapture(TextBox keyBox)
        {
            activeKeyBox = keyBox;
            keyBox.Text = "Press any key...";
            keyBox.BackColor = Color.LightYellow;
        }

        /// <summary>
        /// Handles key capture for binding configuration:
        /// - Ignores modifier keys when pressed alone
        /// - Prevents using Escape key as it's reserved for dialog cancellation
        /// - Updates key binding text box with captured key
        /// </summary>
        private void ConfigDialog_KeyDown(object? sender, KeyEventArgs e)
        {
            if (activeKeyBox == null) return;


            if (e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.ControlKey || 
                e.KeyCode == Keys.Menu || e.KeyCode == Keys.None)
            {
                return;
            }


            if (e.KeyCode == Keys.Escape)
            {
                activeKeyBox.Text = activeKeyBox == startKeyBox ? Keys.C.ToString() :
                                  activeKeyBox == splitKeyBox ? Keys.Escape.ToString() :
                                  Keys.E.ToString();
                activeKeyBox.BackColor = SystemColors.Window;
                activeKeyBox = null;
                e.Handled = true;
                return;
            }

            activeKeyBox.Text = e.KeyCode.ToString();
            activeKeyBox.BackColor = SystemColors.Window;
            activeKeyBox = null;
            e.Handled = true;
        }

        private void DarkMode_CheckedChanged(object? sender, EventArgs e)
        {

            bool isDark = darkModeCheckBox.Checked;
            BackColor = isDark ? Color.FromArgb(18, 18, 18) : Color.White;
            ForeColor = isDark ? Color.White : Color.Black;


            saveButton.BackColor = isDark ? Color.FromArgb(40, 40, 40) : Color.LightGray;
            saveButton.ForeColor = isDark ? Color.White : Color.Black;
            cancelButton.BackColor = isDark ? Color.FromArgb(40, 40, 40) : Color.LightGray;
            cancelButton.ForeColor = isDark ? Color.White : Color.Black;


            foreach (var tb in new[] { timer1Min, timer1Max, timer1HardMax, 
                                     timer2Min, timer2Max, timer2HardMax,
                                     timer3Min, timer3Max, timer3HardMax,
                                     startKeyBox, splitKeyBox, endKeyBox })
            {
                tb.BackColor = isDark ? Color.FromArgb(30, 30, 30) : SystemColors.Window;
                tb.ForeColor = isDark ? Color.White : Color.Black;
            }


            darkModeCheckBox.BackColor = isDark ? Color.FromArgb(30, 30, 30) : SystemColors.Window;
            darkModeCheckBox.ForeColor = isDark ? Color.White : Color.Black;
        }
    }
}
