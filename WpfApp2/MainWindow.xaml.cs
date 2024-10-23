using System.IO; 
using System.Windows;
using Microsoft.Win32; 

namespace ForbiddenWordsFinder
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private string selectedFolderPath;

        private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Виберіть папку",
                Filter = "Папка | *.*",
                CheckFileExists = false,
                FileName = "Оберіть папку"
            };

            if (dialog.ShowDialog() == true)
            {
                selectedFolderPath = Path.GetDirectoryName(dialog.FileName);
                MessageBox.Show("Вибрано папку: " + selectedFolderPath);
            }
        }

        


        private string[] GetAllFilesFromDirectory(string folderPath)
        {
            return Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
        }







        private bool ContainsForbiddenWords(string filePath, string[] forbiddenWords)
        {
            string fileContent = File.ReadAllText(filePath);


            foreach (var word in forbiddenWords)
            {
                if (fileContent.Contains(word))
                {
                    return true;
                }
            }
            return false;
        }

        private void CopyFileToFolder(string filePath, string destinationFolder)
        {
            string fileName = Path.GetFileName(filePath);
            string destinationPath = Path.Combine(destinationFolder, fileName);

            File.Copy(filePath, destinationPath, overwrite: true);
        }



        private void ReplaceForbiddenWords(string filePath, string[] forbiddenWords)
        {
            string fileContent = File.ReadAllText(filePath);

            foreach (var word in forbiddenWords)
            {

                string replacement = new string('*', word.Length);
                fileContent = fileContent.Replace(word, replacement);
            }

            string newFilePath = Path.Combine(Path.GetDirectoryName(filePath), "Modified_" + Path.GetFileName(filePath));
            File.WriteAllText(newFilePath, fileContent);
        }




        private CancellationTokenSource cancellationTokenSource;
        private bool isPaused = false;

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ForbiddenWordsTextBox.Text) || string.IsNullOrEmpty(selectedFolderPath))
            {
                MessageBox.Show("Будь ласка, введіть заборонені слова та виберіть папку.");
                return;
            }

            string[] forbiddenWords = ForbiddenWordsTextBox.Text.Split(',');


            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = cancellationTokenSource.Token;


            string destinationFolder = Path.Combine(selectedFolderPath, "ForbiddenWordsFiles");
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            try
            {
                await Task.Run(() =>
                {
                    SearchFilesWithPause(forbiddenWords, destinationFolder, token);
                }, token);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Пошук було зупинено.");
            }
        }
        private void SearchFilesWithPause(string[] forbiddenWords, string destinationFolder, CancellationToken token)
        {
            string[] allFiles = GetAllFilesFromDirectory(selectedFolderPath);
            int totalFiles = allFiles.Length;
            int processedFiles = 0;

            foreach (var file in allFiles)
            {
                if (token.IsCancellationRequested)
                {
                    token.ThrowIfCancellationRequested();
                }

                while (isPaused)
                {
                    Task.Delay(100).Wait(); 
                }

                int wordCount = 0; 
                bool containsForbiddenWords = false;

                string fileContent = File.ReadAllText(file);
                foreach (var word in forbiddenWords)
                {
                    if (fileContent.Contains(word))
                    {
                        containsForbiddenWords = true;
                        int occurrences = fileContent.Split(new string[] { word }, StringSplitOptions.None).Length - 1;
                        wordCount += occurrences;

                        
                        if (wordFrequency.ContainsKey(word))
                        {
                            wordFrequency[word] += occurrences;
                        }
                        else
                        {
                            wordFrequency[word] = occurrences;
                        }
                    }
                }

                if (containsForbiddenWords)
                {
                    CopyFileToFolder(file, destinationFolder);
                    ReplaceForbiddenWords(file, forbiddenWords);

                    long fileSize = new FileInfo(file).Length;

                    reportEntries.Add(new ReportEntry
                    {
                        FilePath = file,
                        WordCount = wordCount,
                        FileSize = fileSize
                    });


                    Dispatcher.Invoke(() =>
                    {
                        ResultsListBox.Items.Add($"Знайдено заборонені слова у файлі: {file}, Замін: {wordCount}");
                    });
                }


                processedFiles++;
                Dispatcher.Invoke(() =>
                {
                    SearchProgressBar.Value = (double)processedFiles / totalFiles * 100;
                });
            }

            Dispatcher.Invoke(() =>
            {
                MessageBox.Show("Пошук завершено!");
                GenerateReport(); 
            });
        }
        private void GenerateReport()
        {
            string reportPath = Path.Combine(selectedFolderPath, "ForbiddenWordsReport.txt");

            using (StreamWriter writer = new StreamWriter(reportPath))
            {
                writer.WriteLine("Звіт про знайдені файли із забороненими словами:");
                writer.WriteLine("-------------------------------------------------");
                foreach (var entry in reportEntries)
                {
                    writer.WriteLine($"Файл: {entry.FilePath}");
                    writer.WriteLine($"Кількість замін: {entry.WordCount}");
                    writer.WriteLine($"Розмір файлу: {entry.FileSize} байт");
                    writer.WriteLine();
                }

                writer.WriteLine("Топ-10 найчастіше вживаних заборонених слів:");
                writer.WriteLine("-------------------------------------------------");


                var topWords = wordFrequency.OrderByDescending(x => x.Value).Take(10);
                foreach (var word in topWords)
                {
                    writer.WriteLine($"Слово: {word.Key}, Кількість вживань: {word.Value}");
                }
            }


            MessageBox.Show($"Звіт створено: {reportPath}");
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            isPaused = true;
            MessageBox.Show("Процес поставлено на паузу.");
        }


        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            isPaused = false;
            MessageBox.Show("Процес відновлено.");
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel(); 
                MessageBox.Show("Процес зупинено.");
            }
        }

        public class ReportEntry
        {
            public string FilePath { get; set; }
            public int WordCount { get; set; }
            public long FileSize { get; set; }
        }
        private List<ReportEntry> reportEntries = new List<ReportEntry>();
        private Dictionary<string, int> wordFrequency = new Dictionary<string, int>(); 


    }
}
