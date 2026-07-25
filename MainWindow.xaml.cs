using System.Windows;
using TozoWindowsApp.ViewModels;

namespace TozoWindowsApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

    }
}