using System.Windows;

namespace PalworldModUploader
{
    public partial class ChangeNotesWindow : Window
    {
        public string? ChangeNotesText
        {
            get => ChangeNotesTextBox.Text;
            set => ChangeNotesTextBox.Text = value ?? string.Empty;
        }

        public ChangeNotesWindow()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}