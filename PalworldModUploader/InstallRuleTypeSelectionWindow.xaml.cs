using System.Windows;

namespace PalworldModUploader
{
    public partial class InstallRuleTypeSelectionWindow : Window
    {
        public bool IsLuaSelected => LuaCheckBox.IsChecked == true;
        public bool IsPaksSelected => PaksCheckBox.IsChecked == true;
        public bool IsLogicModsSelected => LogicModsCheckBox.IsChecked == true;
        public bool IsPalSchemaSelected => PalSchemaCheckBox.IsChecked == true;

        public InstallRuleTypeSelectionWindow()
        {
            InitializeComponent();
        }

        public void SetSelections(bool lua, bool paks, bool logicMods, bool palSchema)
        {
            LuaCheckBox.IsChecked = lua;
            PaksCheckBox.IsChecked = paks;
            LogicModsCheckBox.IsChecked = logicMods;
            PalSchemaCheckBox.IsChecked = palSchema;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
