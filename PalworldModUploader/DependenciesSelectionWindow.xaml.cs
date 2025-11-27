using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace PalworldModUploader
{
    public partial class DependenciesSelectionWindow : Window
    {
        private readonly List<DependencyItem> _items = new();

        public string[] SelectedPackageNames => _items
            .Where(i => i.IsSelected)
            .Select(i => i.PackageName)
            .ToArray();

        public DependenciesSelectionWindow()
        {
            InitializeComponent();
        }

        public void SetAvailableMods(IEnumerable<(string ModName, string PackageName)> mods, string[]? currentDependencies)
        {
            _items.Clear();
            var currentSet = new HashSet<string>(currentDependencies ?? System.Array.Empty<string>());

            foreach (var (modName, packageName) in mods)
            {
                if (string.IsNullOrWhiteSpace(packageName))
                {
                    continue;
                }

                _items.Add(new DependencyItem
                {
                    ModName = modName ?? packageName,
                    PackageName = packageName,
                    IsSelected = currentSet.Contains(packageName)
                });
            }

            ModListItemsControl.ItemsSource = _items;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }

    public class DependencyItem
    {
        public string ModName { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }
}
