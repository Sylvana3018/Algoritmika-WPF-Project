using System;
using System.Collections.ObjectModel;
using System.Data.OleDb;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.Linq;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Media.Animation;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace algoritm
{
    public partial class MaterialsWindow : Window, INotifyPropertyChanged
    {
        private string _userFullName;
        private int _userId;
        private DateTime _startSub;
        private DateTime _endSub;

        private int _currentPageIndex;
        private const int ItemsPerPage = 4;
        public ObservableCollection<Material> AllMaterials { get; set; }
        public ObservableCollection<Material> DisplayedMaterials { get; set; }
        public ObservableCollection<CustomMaterial> AllCustomMaterials { get; set; }
        public ObservableCollection<CustomMaterial> DisplayedCustomMaterials { get; set; }

        // Collection for Recent Materials
        public ObservableCollection<CustomMaterial> RecentMaterials { get; set; }

        private int _currentCustomPageIndex;
        private const int CustomItemsPerPage = 5;
        private bool _isExporting;
        private bool _isMouseOverInteractiveElement;

        // Subscription properties
        private DispatcherTimer _subscriptionTimer;

        private string _subscriptionTimeLeft;
        public string SubscriptionTimeLeft
        {
            get => _subscriptionTimeLeft;
            set
            {
                _subscriptionTimeLeft = value;
                OnPropertyChanged(nameof(SubscriptionTimeLeft));
            }
        }

        Cursor Fiery;

        public MaterialsWindow(string userFullName, int userId, DateTime startSub, DateTime endSub)
        {
            InitializeComponent();

            DataContext = this;

            // Initialize variables
            _userFullName = userFullName;
            _userId = userId;
            _startSub = startSub;
            _endSub = endSub;

            AllMaterials = new ObservableCollection<Material>();
            DisplayedMaterials = new ObservableCollection<Material>();
            AllCustomMaterials = new ObservableCollection<CustomMaterial>();
            DisplayedCustomMaterials = new ObservableCollection<CustomMaterial>();
            RecentMaterials = new ObservableCollection<CustomMaterial>();

            _currentPageIndex = 0;
            _currentCustomPageIndex = 0;
            _isExporting = false;
            _isMouseOverInteractiveElement = false;

            LoadMaterials();
            LoadCustomMaterials();
            UpdateDisplayedMaterials();
            UpdateDisplayedCustomMaterials();
            UpdateRecentMaterials();

            // Start the subscription timer
            StartSubscriptionTimer();

            // Play the background video
            SubscriptionBackgroundVideo.Loaded += (s, e) => SubscriptionBackgroundVideo.Play();
            SubscriptionBackgroundVideo.MediaEnded += SubscriptionBackgroundVideo_MediaEnded;

            string cursorDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.FullName + "\\myCursors";
            Fiery = new Cursor($"{cursorDirectory}\\NORMALS.cur");

            this.Cursor = Fiery;
        }

        private void StartSubscriptionTimer()
        {
            _subscriptionTimer = new DispatcherTimer();
            _subscriptionTimer.Interval = TimeSpan.FromSeconds(1);
            _subscriptionTimer.Tick += SubscriptionTimer_Tick;
            _subscriptionTimer.Start();
        }

        private void SubscriptionTimer_Tick(object sender, EventArgs e)
        {
            TimeSpan timeLeft = _endSub - DateTime.Now;

            if (timeLeft.TotalSeconds > 0)
            {
                SubscriptionTimeLeft = $"{timeLeft.Days} дня {timeLeft:hh\\:mm\\:ss}";
            }
            else
            {
                SubscriptionTimeLeft = "Подписка истекла";
                _subscriptionTimer.Stop();
                // Optionally, disable certain features if needed
            }
        }

        private void SubscriptionBackgroundVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            SubscriptionBackgroundVideo.Position = TimeSpan.Zero;
            SubscriptionBackgroundVideo.Play();
        }

        private void LoadMaterials()
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                // No changes needed here; 'Path_Material' contains the URL
                string query = "SELECT Path_Material, Name, Description, Color, Icon FROM Materials";
                OleDbCommand command = new OleDbCommand(query, connection);
                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        AllMaterials.Add(new Material
                        {
                            PathMaterial = reader["Path_Material"].ToString(), // This is the URL
                            Name = reader["Name"].ToString(),
                            Description = reader["Description"].ToString(),
                            Color = reader["Color"].ToString(),
                            Icon = reader["Icon"].ToString()
                        });
                    }
                }
            }
        }

        private void LoadCustomMaterials()
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                string query = @"
                    SELECT CM.Path, CM.Description, CM.AddDate, CM.Color, CM.Icon
                    FROM CustomMaterials CM
                    INNER JOIN Teacher T ON CM.ID_Teacher = T.ID_Teacher
                    WHERE T.ID_User = ?";
                OleDbCommand command = new OleDbCommand(query, connection);
                command.Parameters.AddWithValue("ID_User", _userId);
                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string path = reader["Path"].ToString();
                        AllCustomMaterials.Add(new CustomMaterial
                        {
                            Path = path,
                            Name = Path.GetFileNameWithoutExtension(path),
                            Extension = Path.GetExtension(path),
                            Description = reader["Description"].ToString(),
                            AddDate = Convert.ToDateTime(reader["AddDate"]),
                            Color = reader["Color"].ToString(),
                            Icon = reader["Icon"].ToString()
                        });
                    }
                }
            }
        }

        private void UpdateDisplayedMaterials()
        {
            DisplayedMaterials.Clear();
            for (int i = _currentPageIndex * ItemsPerPage; i < (_currentPageIndex + 1) * ItemsPerPage && i < AllMaterials.Count; i++)
            {
                DisplayedMaterials.Add(AllMaterials[i]);
            }
        }

        private void UpdateDisplayedCustomMaterials()
        {
            DisplayedCustomMaterials.Clear();
            for (int i = _currentCustomPageIndex * CustomItemsPerPage; i < (_currentCustomPageIndex + 1) * CustomItemsPerPage && i < AllCustomMaterials.Count; i++)
            {
                DisplayedCustomMaterials.Add(AllCustomMaterials[i]);
            }
        }

        private void UpdateRecentMaterials()
        {
            var recent = AllCustomMaterials.OrderByDescending(m => m.AddDate).Take(5);
            RecentMaterials.Clear();
            foreach (var material in recent)
            {
                RecentMaterials.Add(material);
            }
        }

        private void OpenHomeWindow(object sender, RoutedEventArgs e)
        {
            var homeWindow = new Home(_userFullName, _userId);
            OpenWindowWithAnimation(homeWindow);
        }

        private void OpenStudentListWindow(object sender, RoutedEventArgs e)
        {
            var studentListWindow = new StudentList(_userFullName, _userId);
            OpenWindowWithAnimation(studentListWindow);
        }

        private void OpenMaterialsWindow(object sender, RoutedEventArgs e)
        {
            var materialsWindow = new MaterialsWindow(_userFullName, _userId, _startSub, _endSub);
            OpenWindowWithAnimation(materialsWindow);
        }

        private void OpenVisitWindow(object sender, RoutedEventArgs e)
        {
            var visitWindow = new VisitWindow(_userFullName, _userId, _startSub, _endSub);
            OpenWindowWithAnimation(visitWindow);
        }

        private void OpenNeuralNetworksWindow(object sender, RoutedEventArgs e)
        {
            var neuralNetworksWindow = new NeuralNetworks(_userFullName, _userId, _startSub, _endSub);
            OpenWindowWithAnimation(neuralNetworksWindow);
        }
        private void OpenWindowWithAnimation(Window newWindow)
        {
            newWindow.Opacity = 0;
            newWindow.Loaded += (s, e) =>
            {
                DoubleAnimation animation = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromSeconds(0.5)
                };
                newWindow.BeginAnimation(UIElement.OpacityProperty, animation);
            };
            newWindow.Show();
            CloseWindow();
        }

        private void CloseWindow()
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(0.5)
            };
            animation.Completed += (s, a) => this.Close();
            this.BeginAnimation(UIElement.OpacityProperty, animation);
        }


        private void AddFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Word files (*.docx)|*.docx|PowerPoint files (*.pptx)|*.pptx",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string sourceFilePath = openFileDialog.FileName;
                string targetDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LocalMaterials", _userId.ToString());
                Directory.CreateDirectory(targetDirectory);
                string targetFilePath = Path.Combine(targetDirectory, Path.GetFileName(sourceFilePath));

                File.Copy(sourceFilePath, targetFilePath, true);

                string description, color, icon;
                if (targetFilePath.EndsWith(".docx"))
                {
                    description = "Word File";
                    color = "#2391ec";
                    icon = "MicrosoftWord";
                }
                else if (targetFilePath.EndsWith(".pptx"))
                {
                    description = "PowerPoint File";
                    color = "#f69a76";
                    icon = "MicrosoftPowerpoint";
                }
                else
                {
                    MessageBox.Show("Unsupported file type.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                CustomMaterial newMaterial = new CustomMaterial
                {
                    Path = targetFilePath,
                    Name = Path.GetFileNameWithoutExtension(targetFilePath),
                    Extension = Path.GetExtension(targetFilePath),
                    Description = description,
                    AddDate = DateTime.Now,
                    Color = color,
                    Icon = icon
                };

                AddMaterialToDatabase(newMaterial);
                AllCustomMaterials.Add(newMaterial);
                UpdateDisplayedCustomMaterials();
                UpdateRecentMaterials();
            }
        }

        private void AddMaterialToDatabase(CustomMaterial material)
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                // Шаг 1: Получение ID_Teacher для текущего пользователя
                string getTeacherIdQuery = "SELECT ID_Teacher FROM Teacher WHERE ID_User = ?";
                OleDbCommand getTeacherCommand = new OleDbCommand(getTeacherIdQuery, connection);
                getTeacherCommand.Parameters.AddWithValue("@ID_User", _userId); // Используйте @ вместо имени параметра для ясности

                object result = getTeacherCommand.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                {
                    // Шаг 2: Обработка случая, когда преподаватель не найден
                    MessageBox.Show("Текущий пользователь не связан с преподавателем. Не удалось добавить материал.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int teacherId = Convert.ToInt32(result);

                // Шаг 3: Выполнение вставки записи в CustomMaterials с использованием полученного ID_Teacher
                string insertQuery = @"
            INSERT INTO CustomMaterials (ID_Teacher, Path, Description, AddDate, Color, Icon)
            VALUES (?, ?, ?, ?, ?, ?)";
                OleDbCommand insertCommand = new OleDbCommand(insertQuery, connection);
                insertCommand.Parameters.AddWithValue("@ID_Teacher", teacherId);
                insertCommand.Parameters.AddWithValue("@Path", material.Path);
                insertCommand.Parameters.AddWithValue("@Description", material.Description);
                insertCommand.Parameters.AddWithValue("@AddDate", material.AddDate);
                insertCommand.Parameters.AddWithValue("@Color", material.Color);
                insertCommand.Parameters.AddWithValue("@Icon", material.Icon);

                try
                {
                    insertCommand.ExecuteNonQuery();
                    MessageBox.Show("Материал успешно добавлен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (OleDbException ex)
                {
                    // Обработка возможных ошибок при вставке
                    MessageBox.Show($"Ошибка при добавлении материала: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void DeleteCustomMaterial_Click(object sender, RoutedEventArgs e)
        {
            Button deleteButton = sender as Button;
            string path = deleteButton.CommandParameter as string;

            if (!string.IsNullOrEmpty(path))
            {
                var materialToRemove = AllCustomMaterials.FirstOrDefault(m => m.Path == path);
                if (materialToRemove != null)
                {
                    AllCustomMaterials.Remove(materialToRemove);
                    DeleteMaterialFromDatabase(materialToRemove);

                    // Delete the file from the local file system
                    if (File.Exists(materialToRemove.Path))
                    {
                        File.Delete(materialToRemove.Path);
                    }

                    UpdateDisplayedCustomMaterials();
                    UpdateRecentMaterials();
                }
            }
        }

        private void DeleteMaterialFromDatabase(CustomMaterial material)
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                string query = "DELETE FROM CustomMaterials WHERE Path = ?";
                OleDbCommand command = new OleDbCommand(query, connection);
                command.Parameters.AddWithValue("Path", material.Path);
                command.ExecuteNonQuery();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string searchText = textBox.Text.ToLower();

            DisplayedCustomMaterials.Clear();
            foreach (var material in AllCustomMaterials)
            {
                if (material.Name.ToLower().Contains(searchText))
                {
                    DisplayedCustomMaterials.Add(material);
                }
            }
        }

        private void SortByComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = ((ComboBoxItem)((ComboBox)sender).SelectedItem).Content.ToString();

            if (selectedItem == "Сортировать по имени")
            {
                AllCustomMaterials = new ObservableCollection<CustomMaterial>(AllCustomMaterials.OrderBy(m => m.Name));
            }
            else if (selectedItem == "Сортировать по дате добавления")
            {
                AllCustomMaterials = new ObservableCollection<CustomMaterial>(AllCustomMaterials.OrderByDescending(m => m.AddDate));
            }
            UpdateDisplayedCustomMaterials();

            // Refresh the DataContext to update bindings
            DataContext = null;
            DataContext = this;
        }

        private void OpenCustomMaterial_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            string filePath = button.CommandParameter as string;

            if (!string.IsNullOrEmpty(filePath))
            {
                OpenFile(filePath);
            }
        }

        private void OpenFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show($"Файл не найден: {filePath}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrevCustomPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCustomPageIndex > 0)
            {
                _currentCustomPageIndex--;
                UpdateDisplayedCustomMaterials();
            }
        }

        private void NextCustomPage_Click(object sender, RoutedEventArgs e)
        {
            if ((_currentCustomPageIndex + 1) * CustomItemsPerPage < AllCustomMaterials.Count)
            {
                _currentCustomPageIndex++;
                UpdateDisplayedCustomMaterials();
            }
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPageIndex > 0)
            {
                _currentPageIndex--;
                UpdateDisplayedMaterials();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if ((_currentPageIndex + 1) * ItemsPerPage < AllMaterials.Count)
            {
                _currentPageIndex++;
                UpdateDisplayedMaterials();
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && !_isExporting && !_isMouseOverInteractiveElement)
            {
                try
                {
                    this.DragMove();
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void InteractiveElement_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseOverInteractiveElement = true;
        }

        private void InteractiveElement_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseOverInteractiveElement = false;
        }

        // Modified event handler for Material_Click
        private void Material_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                Border border = sender as Border;
                if (border.DataContext is Material material)
                {
                    if (!string.IsNullOrEmpty(material.PathMaterial))
                    {
                        OpenURL(material.PathMaterial);
                    }
                    else
                    {
                        MessageBox.Show("Ссылка отсутствует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else if (border.DataContext is CustomMaterial customMaterial)
                {
                    OpenFile(customMaterial.Path);
                }
            }
        }

        // New method to open URL in default browser
        private void OpenURL(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть ссылку: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Implement INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public class Material
        {
            public string PathMaterial { get; set; } // This contains the URL
            public string Name { get; set; }
            public string Description { get; set; }
            public string Color { get; set; }
            public string Icon { get; set; }
            // Removed the URL property since PathMaterial contains it
        }

        public class CustomMaterial
        {
            public string Path { get; set; }
            public string Name { get; set; }
            public string Extension { get; set; }
            public string Description { get; set; }
            public DateTime AddDate { get; set; }
            public string Color { get; set; }
            public string Icon { get; set; }
        }
    }
}
