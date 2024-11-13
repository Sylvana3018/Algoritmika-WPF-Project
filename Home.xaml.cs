using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.OleDb;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using MaterialDesignThemes.Wpf;
using OfficeOpenXml;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace algoritm
{
    public partial class Home : Window, INotifyPropertyChanged
    {
        private readonly string FtpHost = "141.8.193.99";
        private readonly string FtpUser = "f1041431";
        private readonly string FtpPassword = "f1041431_PASS";

        // Секретный ключ для генерации токена авторизации
        private readonly string SecretKey = "your_secret_key"; // Замените на свой секретный ключ
        private string _userName;
        private int _currentPageIndex;
        private int _userId;
        private int _commentPageIndex;
        private int _resultPageIndex;
        private string _avatarPath;
        private DateTime _startSub;
        private DateTime _endSub;
        private DispatcherTimer _subscriptionTimer;

        public DateTime StartSub
        {
            get => _startSub;
            set
            {
                _startSub = value;
                OnPropertyChanged(nameof(StartSub));
            }
        }

        public DateTime EndSub
        {
            get => _endSub;
            set
            {
                _endSub = value;
                OnPropertyChanged(nameof(EndSub));
            }
        }

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
        private int _teacherId;
        public int TeacherId
        {
            get => _teacherId;
            set
            {
                _teacherId = value;
                OnPropertyChanged(nameof(TeacherId));
            }
        }

        private int _numberOfGroups;
        public int NumberOfGroups
        {
            get => _numberOfGroups;
            set
            {
                _numberOfGroups = value;
                OnPropertyChanged(nameof(NumberOfGroups));
                OnPropertyChanged(nameof(ConnectedGroupsText));
            }
        }

        public string ConnectedGroupsText
        {
            get => $"Сейчас к вам подключено {NumberOfGroups} {GetGroupWord(NumberOfGroups)}                       Удачных занятий!";
        }

        public ObservableCollection<Border> AllItems { get; set; }
        public ObservableCollection<Border> DisplayedItems { get; set; }

        private ObservableCollection<CommentData> _comments = new ObservableCollection<CommentData>();
        public ObservableCollection<CommentData> DisplayedComments { get; set; }

        private ObservableCollection<ResultData> _latestResults = new ObservableCollection<ResultData>();
        public ObservableCollection<ResultData> DisplayedResults { get; set; }

        public ObservableCollection<CommentData> Comments
        {
            get => _comments;
            set
            {
                _comments = value;
                OnPropertyChanged(nameof(Comments));
            }
        }

        public ObservableCollection<ResultData> LatestResults
        {
            get => _latestResults;
            set
            {
                _latestResults = value;
                OnPropertyChanged(nameof(LatestResults));
            }
        }

        private string _userComment;

        public string UserComment
        {
            get => _userComment;
            set
            {
                _userComment = value;
                OnPropertyChanged(nameof(UserComment));
            }
        }

        public string AvatarPath
        {
            get => _avatarPath;
            set
            {
                _avatarPath = value;
                OnPropertyChanged(nameof(AvatarPath));
            }
        }

        private string _userFullName;
        private string _userRole;

        public string UserFullName
        {
            get => _userFullName;
            set
            {
                _userFullName = value;
                OnPropertyChanged(nameof(UserFullName));
            }
        }

        public string UserRole
        {
            get => _userRole;
            set
            {
                _userRole = value;
                OnPropertyChanged(nameof(UserRole));
            }
        }

        Cursor Fiery;
        public Home(string userFullName, int userId)
        {
            InitializeComponent();
            DataContext = this;
            CurrentDate = DateTime.Now.ToString("dd MMMM yyyy");
            UserName = GetFirstName(userFullName);
            UserId = userId;

            SubscriptionBackgroundVideo.Loaded += (s, e) => SubscriptionBackgroundVideo.Play();
            SubscriptionBackgroundVideo.MediaEnded += SubscriptionBackgroundVideo_MediaEnded;

            AllItems = new ObservableCollection<Border>();
            DisplayedItems = new ObservableCollection<Border>();
            DisplayedComments = new ObservableCollection<CommentData>();
            DisplayedResults = new ObservableCollection<ResultData>();
            _currentPageIndex = 0;
            _commentPageIndex = 0;
            _resultPageIndex = 0;

            LoadAvatarPath();
            LoadUserDetails();
            LoadCourses();
            LoadComments();
            LoadLatestResults();

            string cursorDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.FullName + "\\myCursors";
            Fiery = new Cursor($"{cursorDirectory}\\NORMALS.cur");

            this.Cursor = Fiery;
        }

        private void SubscriptionBackgroundVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            SubscriptionBackgroundVideo.Position = TimeSpan.Zero;
            SubscriptionBackgroundVideo.Play();
        }

        // Метод генерации CSV из комментариев
        private string GenerateCommentsCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("ID_Student,FullName,Role,Comment");

            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                string query = @"
                    SELECT S.ID_Student, U.FIO, R.Name AS RoleName, S.Comments
                    FROM Student S
                    INNER JOIN [User] U ON S.ID_User = U.ID_User
                    INNER JOIN [Role] R ON U.ID_Role = R.ID_Role
                    WHERE S.ID_Group IN (SELECT ID_Group FROM Groups WHERE ID_Teacher = ?)";

                OleDbCommand command = new OleDbCommand(query, connection);
                command.Parameters.AddWithValue("ID_Teacher", TeacherId);

                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int studentId = Convert.ToInt32(reader["ID_Student"]);
                        string fullName = reader["FIO"].ToString();
                        string role = reader["RoleName"].ToString();
                        string comment = reader["Comments"].ToString().Replace(",", ";"); // Замена запятых для корректного CSV
                        sb.AppendLine($"{studentId},{fullName},{role},{comment}");
                    }
                }
            }

            return sb.ToString();
        }

        // Метод генерации токена авторизации
        private string GenerateAuthToken(int userId)
        {
            string tokenSource = $"{userId}{SecretKey}";
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(tokenSource));
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// Обработчик события нажатия кнопки "Открыть сайт"
        /// Экспортирует комментарии в CSV, загружает на FTP и открывает веб-страницу
        /// </summary>
        private void OpenWebsite_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Экспорт комментариев в CSV
                string csvContent = GenerateCommentsCsv();
                string localCsvPath = Path.Combine(Path.GetTempPath(), "comments.csv");
                File.WriteAllText(localCsvPath, csvContent, Encoding.UTF8);

                // Загрузка CSV на FTP-сервер
                string remoteCsvPath = "/domains/f1041431.xsph.ru/public_html/comments.csv"; // Корректный путь на сервере
                UploadFileToFtp(localCsvPath, remoteCsvPath);

                // Формирование URL с параметрами авторизации
                string authToken = GenerateAuthToken(UserId);
                string url = $"http://f1041431.xsph.ru/comments.php?userId={UserId}&authToken={Uri.EscapeDataString(authToken)}";

                // Открытие браузера с URL
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });

                MessageBox.Show("Данные успешно загружены на сервер и открыта веб-страница.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии сайта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Метод загрузки файла на FTP
        /// </summary>
        /// <param name="localFilePath">Путь к локальному файлу</param>
        /// <param name="remoteFilePath">Путь на FTP-сервере</param>
        private void UploadFileToFtp(string localFilePath, string remoteFilePath)
        {
            try
            {
                string ftpUrl = $"ftp://{FtpHost}{remoteFilePath}";
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.UploadFile;
                request.Credentials = new NetworkCredential(FtpUser, FtpPassword);
                request.UsePassive = true;
                request.UseBinary = true;
                request.KeepAlive = false;

                byte[] fileContents = File.ReadAllBytes(localFilePath);
                request.ContentLength = fileContents.Length;

                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(fileContents, 0, fileContents.Length);
                }

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                    Console.WriteLine($"Upload File Complete, status {response.StatusDescription}");
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is FtpWebResponse response)
                {
                    MessageBox.Show($"FTP Error: {response.StatusCode} - {response.StatusDescription}", "FTP Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"Ошибка при загрузке файла на FTP: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке файла на FTP: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обработчик события нажатия кнопки "Обновить комментарии"
        /// Скачивает обновлённые комментарии с FTP и обновляет базу данных
        /// </summary>
        private void UpdateComments_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Загрузка обновлённых комментариев с FTP
                string remoteCsvPath = "/domains/f1041431.xsph.ru/public_html/comments.csv"; // Корректный путь на сервере
                string localCsvPath = DownloadFileFromFtp(remoteCsvPath);

                // Обновление базы данных
                UpdateCommentsFromCsv(localCsvPath);

                MessageBox.Show("Комментарии успешно обновлены из веб-сайта.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении комментариев: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Метод для загрузки файла с FTP
        /// </summary>
        /// <param name="remoteFilePath">Путь к файлу на FTP-сервере</param>
        /// <returns>Путь к локальному скачанному файлу</returns>
        private string DownloadFileFromFtp(string remoteFilePath)
        {
            try
            {
                string ftpUrl = $"ftp://{FtpHost}{remoteFilePath}";
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.Credentials = new NetworkCredential(FtpUser, FtpPassword);
                request.UsePassive = true;
                request.UseBinary = true;
                request.KeepAlive = false;

                string localFilePath = Path.Combine(Path.GetTempPath(), Path.GetFileName(remoteFilePath));

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                using (FileStream fs = new FileStream(localFilePath, FileMode.Create))
                {
                    responseStream.CopyTo(fs);
                }

                return localFilePath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при загрузке файла с FTP: {ex.Message}");
            }
        }

        /// <summary>
        /// Метод для обновления базы данных из CSV
        /// </summary>
        /// <param name="csvFilePath">Путь к локальному CSV-файлу</param>
        private void UpdateCommentsFromCsv(string csvFilePath)
        {
            try
            {
                var lines = File.ReadAllLines(csvFilePath);
                if (lines.Length <= 1)
                    return; // Нет данных для обновления

                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length < 4)
                        continue; // Некорректная строка

                    int studentId = int.Parse(parts[0]);
                    string comment = parts[3].Replace(";", ","); // Восстановление запятых

                    using (OleDbConnection connection = DatabaseHelper.GetConnection())
                    {
                        connection.Open();
                        string query = "UPDATE Student SET Comments = ? WHERE ID_Student = ?";
                        OleDbCommand command = new OleDbCommand(query, connection);
                        command.Parameters.AddWithValue("Comments", comment);
                        command.Parameters.AddWithValue("ID_Student", studentId);
                        command.ExecuteNonQuery();
                    }
                }

                // Перезагрузка комментариев в приложении
                LoadComments();

                MessageBox.Show("Комментарии успешно обновлены из веб-сайта.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при обновлении комментариев: {ex.Message}");
            }
        }
        private void LoadUserDetails()
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                string query = @"
                    SELECT U.FIO, R.Name AS RoleName, T.Comments, T.ID_Teacher, T.StartSub, T.EndSub
                    FROM [User] U
                    INNER JOIN [Role] R ON U.ID_Role = R.ID_Role
                    INNER JOIN [Teacher] T ON U.ID_User = T.ID_User
                    WHERE U.ID_User = ?";

                OleDbCommand command = new OleDbCommand(query, connection);
                command.Parameters.AddWithValue("ID_User", UserId);

                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string fullName = reader["FIO"].ToString();
                        string roleName = reader["RoleName"].ToString();
                        string comment = reader["Comments"].ToString();
                        int teacherId = (int)reader["ID_Teacher"];
                        DateTime startSub = reader.GetDateTime(reader.GetOrdinal("StartSub"));
                        DateTime endSub = reader.GetDateTime(reader.GetOrdinal("EndSub"));

                        UserFullName = GetFirstNameAndLastName(fullName);
                        UserRole = roleName;
                        UserComment = comment;
                        TeacherId = teacherId;
                        StartSub = startSub;
                        EndSub = endSub;

                        StartSubscriptionTimer();
                    }
                }
            }

            LoadNumberOfGroups();
        }

        // Метод для загрузки количества групп
        private void LoadNumberOfGroups()
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                string query = @"SELECT COUNT(*) FROM [Groups] WHERE ID_Teacher = ?";

                OleDbCommand command = new OleDbCommand(query, connection);
                command.Parameters.AddWithValue("ID_Teacher", TeacherId);

                NumberOfGroups = (int)command.ExecuteScalar();
            }
        }

        // Метод для корректного склонения слова "группа"
        private string GetGroupWord(int count)
        {
            if (count % 100 >= 11 && count % 100 <= 14)
            {
                return "групп";
            }
            switch (count % 10)
            {
                case 1:
                    return "группа";
                case 2:
                case 3:
                case 4:
                    return "группы";
                default:
                    return "групп";
            }
        }

        public string CurrentDate { get; set; }

        public string UserName
        {
            get => _userName;
            set
            {
                _userName = value;
                OnPropertyChanged(nameof(UserName));
            }
        }

        public int UserId
        {
            get => _userId;
            set
            {
                _userId = value;
                OnPropertyChanged(nameof(UserId));
            }
        }

        private string GetFirstName(string fullName)
        {
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName.Split(' ')[0];
            }
            return "User";
        }

        private string GetFirstNameAndLastName(string fullName)
        {
            var names = fullName.Split(' ');
            if (names.Length >= 2)
            {
                return $"{names[0]} {names[1]}";
            }
            return fullName;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OpenMapWindow(object sender, RoutedEventArgs e)
        {
            var newWindow = new StudentList(UserName, UserId);
            OpenWindowWithAnimation(newWindow);
        }

        private void OpenDataWindow(object sender, RoutedEventArgs e)
        {
            var newWindow = new VisitWindow(UserName, UserId, StartSub, EndSub);

            OpenWindowWithAnimation(newWindow);
        }


        private void OpenMaterialsWindow(object sender, RoutedEventArgs e)
        {


            var materialsWindow = new MaterialsWindow(UserName, UserId, StartSub, EndSub);
            OpenWindowWithAnimation(materialsWindow);

            /* АЛЬФА ТЕСТИРОВАНИЕ ПОДПИСКИ
            if (DateTime.Now <= EndSub)
            {
                var newWindow = new MaterialsWindow();
                OpenWindowWithAnimation(newWindow);
            }
            else
            {
                MessageBox.Show("Срок действия подписки истек. Раздел 'Материалы' недоступен.", "Подписка истекла", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            */
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
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

        // Загрузка курсов
        private void LoadCourses()
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                string query = @"
                    SELECT C.Name, C.SubName, C.PackIconKind, C.BlockColor
                    FROM [Courses] C
                    INNER JOIN [Groups] G ON C.ID_Group = G.ID_Group
                    INNER JOIN [Teacher] T ON G.ID_Teacher = T.ID_Teacher
                    WHERE T.ID_User = ?";

                OleDbCommand command = new OleDbCommand(query, connection);
                command.Parameters.AddWithValue("ID_User", UserId);

                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string name = reader["Name"].ToString();
                        string subName = reader["SubName"].ToString();
                        string iconKind = reader["PackIconKind"].ToString();
                        string blockColor = reader["BlockColor"].ToString();

                        AllItems.Add(CreateCourseBorder(name, subName, iconKind, blockColor));
                    }
                }
            }

            UpdateDisplayedItems();
        }

        // Оформление курсов в зависимости от их данных
        private Border CreateCourseBorder(string name, string subName, string iconKind, string blockColor)
        {
            var transparentBrush = new SolidColorBrush(Colors.Transparent);

            Border border = new Border
            {
                Height = 100,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(blockColor)),
                CornerRadius = new CornerRadius(25),
                Margin = new Thickness(10, 0, 10, 0),
                BorderBrush = transparentBrush,
                BorderThickness = new Thickness(2),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new PackIcon
                        {
                            Kind = (PackIconKind)Enum.Parse(typeof(PackIconKind), iconKind),
                            Width = 30,
                            Height = 30,
                            Margin = new Thickness(30, 5, 10, 0),
                            Foreground = Brushes.White
                        },
                        new StackPanel
                        {
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = name,
                                    Foreground = Brushes.White,
                                    FontSize = 14,
                                    Margin = new Thickness(10, 0, 0, 5)
                                },
                                new TextBlock
                                {
                                    Text = subName,
                                    Foreground = Brushes.White,
                                    FontWeight = FontWeights.Bold,
                                    FontSize = 18,
                                    Margin = new Thickness(10, 0, 0, 0)
                                }
                            }
                        }
                    }
                }
            };

            var gradientBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop((Color)ColorConverter.ConvertFromString("#161928"), 0.0),
                    new GradientStop((Color)ColorConverter.ConvertFromString("#154AEE"), 1.0)
                }
            };

            var fadeInAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(0.2)
            };

            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(0.2)
            };

            border.MouseEnter += (sender, args) =>
            {
                var borderElement = sender as Border;
                gradientBrush.Opacity = 0;
                borderElement.BorderBrush = gradientBrush;
                gradientBrush.BeginAnimation(Brush.OpacityProperty, fadeInAnimation);
            };

            border.MouseLeave += (sender, args) =>
            {
                var borderElement = sender as Border;
                gradientBrush.BeginAnimation(Brush.OpacityProperty, fadeOutAnimation);
                fadeOutAnimation.Completed += (s, e) => borderElement.BorderBrush = transparentBrush;
            };

            return border;
        }

        // Загрузка комментариев
        private void LoadComments()
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                string query = @"
                    SELECT U.FIO, R.Name AS RoleName, S.Comments
                    FROM [User] U
                    INNER JOIN [Role] R ON U.ID_Role = R.ID_Role
                    INNER JOIN [Student] S ON U.ID_User = S.ID_User";

                OleDbCommand command = new OleDbCommand(query, connection);

                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string fullName = reader["FIO"].ToString();
                        string roleName = reader["RoleName"].ToString();
                        string comment = reader["Comments"].ToString();

                        Comments.Add(new CommentData
                        {
                            FullName = GetFirstNameAndLastName(fullName),
                            Role = roleName,
                            Comment = comment
                        });
                    }
                }
            }

            UpdateDisplayedComments();
        }

        // Загрузка посещаемости
        private void LoadLatestResults()
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                string query = @"
                    SELECT G.Name AS GroupName, G.Description, 
                           SUM(CASE WHEN V.ID_VS = 3 THEN 1 ELSE 0 END) * 100.0 / COUNT(V.ID_Visit) AS AttendancePercentage
                    FROM [Groups] G
                    INNER JOIN [Student] S ON G.ID_Group = S.ID_Group
                    INNER JOIN [Visit] V ON S.ID_Student = V.ID_Student
                    GROUP BY G.Name, G.Description";

                OleDbCommand command = new OleDbCommand(query, connection);

                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string groupName = reader["GroupName"].ToString();
                        string description = reader["Description"].ToString();
                        double attendancePercentage = Convert.ToDouble(reader["AttendancePercentage"]);

                        LatestResults.Add(new ResultData
                        {
                            GroupName = groupName,
                            Description = description,
                            AttendancePercentage = attendancePercentage
                        });
                    }
                }
            }

            UpdateDisplayedResults();
        }

        // Загрузка аватарки
        private void LoadAvatarPath()
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();

                string query = "SELECT AvatarPath FROM [User] WHERE ID_User = ?";
                OleDbCommand command = new OleDbCommand(query, connection);
                command.Parameters.AddWithValue("ID_User", UserId);

                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        AvatarPath = reader["AvatarPath"].ToString();
                    }
                }
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "Save Attendance Data"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string filePath = saveFileDialog.FileName;
                ExportToExcel(filePath);
            }
        }

        // Функция экспорта в Excel
        private void ExportToExcel(string filePath)
        {
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using (ExcelPackage package = new ExcelPackage())
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("Посещаемость");

                worksheet.Cells[1, 1].Value = "Группа";
                worksheet.Cells[1, 2].Value = "Описание";
                worksheet.Cells[1, 3].Value = "Посещаемость (%)";

                int row = 2;
                foreach (var result in DisplayedResults)
                {
                    worksheet.Cells[row, 1].Value = result.GroupName;
                    worksheet.Cells[row, 2].Value = result.Description;
                    worksheet.Cells[row, 3].Value = result.AttendancePercentage;
                    row++;
                }

                FileInfo file = new FileInfo(filePath);
                package.SaveAs(file);
            }

            MessageBox.Show("Экспорт завершён!", "Экспорт посещаемости", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateDisplayedItems()
        {
            DisplayedItems.Clear();
            for (int i = _currentPageIndex; i < _currentPageIndex + 3 && i < AllItems.Count; i++)
            {
                DisplayedItems.Add(AllItems[i]);
            }
        }

        private void UpdateDisplayedComments()
        {
            DisplayedComments.Clear();
            for (int i = _commentPageIndex; i < _commentPageIndex + 2 && i < Comments.Count; i++)
            {
                DisplayedComments.Add(Comments[i]);
            }
        }

        private void UpdateDisplayedResults()
        {
            DisplayedResults.Clear();
            for (int i = _resultPageIndex; i < _resultPageIndex + 4 && i < LatestResults.Count; i++)
            {
                DisplayedResults.Add(LatestResults[i]);
            }
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPageIndex > 0)
            {
                _currentPageIndex--;
                UpdateDisplayedItems();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPageIndex + 3 < AllItems.Count)
            {
                _currentPageIndex++;
                UpdateDisplayedItems();
            }
        }

        private void PrevComment_Click(object sender, RoutedEventArgs e)
        {
            if (_commentPageIndex > 0)
            {
                _commentPageIndex--;
                UpdateDisplayedComments();
            }
        }
        private void OpenNeuralNetworksWindow(object sender, RoutedEventArgs e)
        {
            var neuralNetworksWindow = new NeuralNetworks(UserFullName, UserId, StartSub, EndSub);
            OpenWindowWithAnimation(neuralNetworksWindow);
        }



        private void NextComment_Click(object sender, RoutedEventArgs e)
        {
            if (_commentPageIndex + 2 < Comments.Count)
            {
                _commentPageIndex++;
                UpdateDisplayedComments();
            }
        }

        private void PrevResultPage_Click(object sender, RoutedEventArgs e)
        {
            if (_resultPageIndex > 0)
            {
                _resultPageIndex--;
                UpdateDisplayedResults();
            }
        }

        private void NextResultPage_Click(object sender, RoutedEventArgs e)
        {
            if (_resultPageIndex + 4 < LatestResults.Count)
            {
                _resultPageIndex++;
                UpdateDisplayedResults();
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.png;*.jpg)|*.png;*.jpg";
            try
            {
                if (openFileDialog.ShowDialog() == true)
                {
                    string sourceFilePath = openFileDialog.FileName;
                    string targetDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AvatarPictures");
                    Directory.CreateDirectory(targetDirectory);
                    string targetFilePath = Path.Combine(targetDirectory, Path.GetFileName(sourceFilePath));

                    File.Copy(sourceFilePath, targetFilePath, true);

                    using (OleDbConnection connection = DatabaseHelper.GetConnection())
                    {
                        connection.Open();

                        string query = "UPDATE [User] SET AvatarPath = ? WHERE ID_User = ?";
                        OleDbCommand command = new OleDbCommand(query, connection);
                        command.Parameters.AddWithValue("AvatarPath", targetFilePath);
                        command.Parameters.AddWithValue("ID_User", UserId);
                        command.ExecuteNonQuery();
                    }

                    AvatarPath = targetFilePath;
                }
            }
            catch
            {
                MessageBox.Show("Ошибка при добавлении аватарки!", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            TimeSpan timeLeft = EndSub - DateTime.Now;

            if (timeLeft.TotalSeconds > 0)
            {
                SubscriptionTimeLeft = $"{timeLeft.Days} дня {timeLeft:hh\\:mm\\:ss}";
            }
            else
            {
                SubscriptionTimeLeft = "Подписка истекла";
                _subscriptionTimer.Stop();
                // Сделать кнопку недоступной
                // MaterialsButton.IsEnabled = false;
            }
        }

        private void OpenAiPlusWindow(object sender, RoutedEventArgs e)
        {
            if (DateTime.Now <= EndSub)
            {
                MessageBox.Show("В данный момент раздел недоступен", "Отказ в доступе", MessageBoxButton.OK, MessageBoxImage.Warning);
                //var aiPlusWindow = new AiPlusWindow(UserFullName, UserId, EndSub);
                //OpenWindowWithAnimation(aiPlusWindow);
            }
            else
            {
                MessageBox.Show("Срок действия подписки истек. Раздел 'Нейросети' недоступен.", "Подписка истекла", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }



    }

    public class CommentData
    {
        public string FullName { get; set; }
        public string Role { get; set; }
        public string Comment { get; set; }
    }

    public class ResultData
    {
        public string GroupName { get; set; }
        public string Description { get; set; }
        public double AttendancePercentage { get; set; }
    }
}
