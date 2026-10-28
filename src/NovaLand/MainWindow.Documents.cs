using System.IO;
using System.Windows;
using NovaIsland.Models;
using NovaIsland.Services;
namespace NovaIsland;
public partial class MainWindow
{
    private readonly DocumentHistory _documents = new();
    private void InitializeDocuments()
    {
        DocumentList.ItemsSource = _documents.Items;
        _documents.Items.CollectionChanged += (_, _) => DocumentEmpty.Visibility = _documents.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void DocumentHistory_Click(object sender, RoutedEventArgs e) => SetClipboardMode(2);
    private async void CopyDocument_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not DocumentEntry entry) return;
        try
        {
            bool exists = await Task.Run(() => File.Exists(entry.FullPath) || Directory.Exists(entry.FullPath)).WaitAsync(TimeSpan.FromSeconds(4));
            if (!exists) { StatusText.Text = "Dosya taşınmış veya silinmiş. Yeniden kopyala."; return; }
            if (await ClipboardService.CopyFilesAsync(new[] { entry.FullPath }))
            {
                await CopyFeedback(sender);
                StatusText.Text = "Dosya kopyalandı. Ctrl+V ile yapıştırabilirsin.";
            }
            else StatusText.Text = "Pano meşgul. Tekrar dene.";
        }
        catch (TimeoutException) { StatusText.Text = "Dosyaya erişilemiyor. Bağlantısını kontrol et."; }
        catch (Exception ex) { DiagnosticLog.Write("Copy document", ex); StatusText.Text = "Dosya kopyalanamadı."; }
    }
}
