using LiveCharts;
using LiveCharts.Wpf;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.OleDb;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using ZXing;

namespace algoritm
{
    public partial class VisitWindow : Window, INotifyPropertyChanged
    {
        private int _currentPageIndex;
        private const int ItemsPerPage = 3;

        // URL вашего PHP-скрипта на хостинге
        private readonly string AttendancePageUrl = "http://f1041431.xsph.ru/attendance.php";

        // FTP параметры
        private readonly string FtpHost = "141.8.193.99";
        private readonly string FtpUser = "f1041431";
        private readonly string FtpPassword = "f1041431_PASS";
        private readonly string FtpUploadPath = "/domains/f1041431.xsph.ru/public_html/attendance.csv";

        // Экземпляр Random и словарь для хранения цветов групп
        private readonly Random _random = new Random();
        private readonly Dictionary<string, System.Windows.Media.Color> _groupColors = new Dictionary<string, System.Windows.Media.Color>();

        public ObservableCollection<AttendanceRecord> AllAttendanceRecords { get; set; } = new ObservableCollection<AttendanceRecord>();
        public ObservableCollection<AttendanceRecord> DisplayedAttendanceRecords { get; set; } = new ObservableCollection<AttendanceRecord>();

        private SeriesCollection _attendanceSeries = new SeriesCollection();
        public SeriesCollection AttendanceSeries
        {
            get => _attendanceSeries;
            set
            {
                _attendanceSeries = value;
                OnPropertyChanged(nameof(AttendanceSeries));
            }
        }

        private Func<double, string> _yFormatter;
        public Func<double, string> YFormatter
        {
            get => _yFormatter;
            set
            {
                _yFormatter = value;
                OnPropertyChanged(nameof(YFormatter));
            }
        }

        private string[] _dates = new string[0];
        public string[] Dates
        {
            get => _dates;
            set
            {
                _dates = value;
                OnPropertyChanged(nameof(Dates));
            }
        }

        public ObservableCollection<VisitStatus> Statuses { get; set; } = new ObservableCollection<VisitStatus>();

        private AttendanceRecord _selectedRecord;
        public AttendanceRecord SelectedRecord
        {
            get => _selectedRecord;
            set
            {
                _selectedRecord = value;
                OnPropertyChanged(nameof(SelectedRecord));
                LoadSelectedRecordStatus();
            }
        }

        private VisitStatus _selectedStatus;
        public VisitStatus SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                _selectedStatus = value;
                OnPropertyChanged(nameof(SelectedStatus));
            }
        }

        private string _userFullName;
        private int _userId;
        private DateTime _startSub;
        private DateTime _endSub;

        public string UserFullName
        {
            get => _userFullName;
            set
            {
                _userFullName = value;
                OnPropertyChanged(nameof(UserFullName));
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

        public VisitWindow(string userFullName, int userId, DateTime startSub, DateTime endSub)
        {
            InitializeComponent();
            DataContext = this;

            UserFullName = userFullName;
            UserId = userId;
            StartSub = startSub;
            EndSub = endSub;

            LoadAttendanceRecords();
            LoadStatuses();
            UpdateDisplayedAttendanceRecords();
            LoadChartData();
        }

        private void LoadAttendanceRecords()
        {
            AllAttendanceRecords.Clear();
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                try
                {
                    connection.Open();
                    string query = @"
                        SELECT V.ID_Visit, U.FIO, L.Public_name, V.ID_Lesson, V.ID_Student, 
                               VS.Name AS StatusName, VS.Color AS StatusColor, 
                               Sched.Lesson_date, G.Name AS GroupName
                        FROM Visit V
                        INNER JOIN Student S ON S.ID_Student = V.ID_Student
                        INNER JOIN [User] U ON U.ID_User = S.ID_User
                        INNER JOIN Lessons L ON L.ID_Lesson = V.ID_Lesson
                        INNER JOIN Visit_status VS ON VS.ID_VS = V.ID_VS
                        INNER JOIN Groups G ON G.ID_Group = S.ID_Group
                        INNER JOIN Schedule Sched ON Sched.ID_Group = G.ID_Group
                        ORDER BY Sched.Lesson_date DESC";

                    OleDbCommand command = new OleDbCommand(query, connection);
                    using (OleDbDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            AllAttendanceRecords.Add(new AttendanceRecord
                            {
                                ID_Visit = Convert.ToInt32(reader["ID_Visit"]),
                                StudentName = reader["FIO"].ToString(),
                                LessonName = reader["Public_name"].ToString(),
                                LessonDate = Convert.ToDateTime(reader["Lesson_date"]),
                                AttendanceStatus = reader["StatusName"].ToString(),
                                StatusColor = reader["StatusColor"]?.ToString() ?? "#161928",
                                ID_Student = Convert.ToInt32(reader["ID_Student"]),
                                ID_Lesson = Convert.ToInt32(reader["ID_Lesson"]),
                                GroupName = reader["GroupName"].ToString()
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при загрузке записей посещаемости: " + ex.Message);
                }
            }
        }

        private void LoadStatuses()
        {
            Statuses.Clear();
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                try
                {
                    connection.Open();
                    string query = "SELECT ID_VS, Name FROM Visit_status";
                    OleDbCommand command = new OleDbCommand(query, connection);
                    using (OleDbDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Statuses.Add(new VisitStatus
                            {
                                ID_VS = Convert.ToInt32(reader["ID_VS"]),
                                Name = reader["Name"].ToString()
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при загрузке статусов посещаемости: " + ex.Message);
                }
            }
        }

        private void UpdateDisplayedAttendanceRecords()
        {
            DisplayedAttendanceRecords.Clear();
            for (int i = _currentPageIndex * ItemsPerPage; i < (_currentPageIndex + 1) * ItemsPerPage && i < AllAttendanceRecords.Count; i++)
            {
                DisplayedAttendanceRecords.Add(AllAttendanceRecords[i]);
            }
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPageIndex > 0)
            {
                _currentPageIndex--;
                UpdateDisplayedAttendanceRecords();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if ((_currentPageIndex + 1) * ItemsPerPage < AllAttendanceRecords.Count)
            {
                _currentPageIndex++;
                UpdateDisplayedAttendanceRecords();
            }
        }

        private void LoadChartData()
        {
            AttendanceSeries.Clear();
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                try
                {
                    connection.Open();
                    // Измененный SQL-запрос для получения посещаемости по группам и датам
                    string query = @"
                        SELECT 
                            G.Name AS GroupName,
                            Sched.Lesson_date,
                            SUM(IIF(VS.Name = 'Присутствовал', 1, 0)) * 100.0 / COUNT(V.ID_Visit) AS DailyAttendance
                        FROM Schedule Sched
                        INNER JOIN Groups G ON G.ID_Group = Sched.ID_Group
                        INNER JOIN Student S ON S.ID_Group = G.ID_Group
                        INNER JOIN Visit V ON V.ID_Student = S.ID_Student
                        INNER JOIN Visit_status VS ON V.ID_VS = VS.ID_VS
                        GROUP BY G.Name, Sched.Lesson_date
                        ORDER BY Sched.Lesson_date, G.Name";

                    OleDbCommand command = new OleDbCommand(query, connection);
                    var datesSet = new SortedSet<DateTime>();
                    var groupAttendance = new Dictionary<string, List<double>>();

                    using (OleDbDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string groupName = reader["GroupName"].ToString();
                            DateTime lessonDate = Convert.ToDateTime(reader["Lesson_date"]);
                            double attendance = Math.Round(Convert.ToDouble(reader["DailyAttendance"]), 2);

                            datesSet.Add(lessonDate);

                            if (!groupAttendance.ContainsKey(groupName))
                            {
                                groupAttendance[groupName] = new List<double>();
                            }

                            groupAttendance[groupName].Add(attendance);
                        }
                    }

                    // Сортируем даты
                    var sortedDates = datesSet.ToList();
                    sortedDates.Sort();

                    // Обновляем ось X
                    Dates = sortedDates.Select(d => d.ToString("dd.MM.yyyy")).ToArray();

                    // Для каждой группы создаем LineSeries с уникальным цветом
                    foreach (var group in groupAttendance)
                    {
                        // Проверяем, совпадает ли количество точек с количеством дат
                        // Если нет, заполняем пропущенные даты значениями 0
                        List<double> attendanceValues = new List<double>();

                        attendanceValues.AddRange(group.Value);

                        while (attendanceValues.Count < sortedDates.Count)
                        {
                            attendanceValues.Add(0);
                        }

                        // Генерируем уникальный случайный цвет для группы, если он ещё не назначен
                        if (!_groupColors.ContainsKey(group.Key))
                        {
                            _groupColors[group.Key] = GenerateRandomColor();
                        }

                        var brush = new System.Windows.Media.SolidColorBrush(_groupColors[group.Key]);

                        AttendanceSeries.Add(new LineSeries
                        {
                            Title = group.Key,
                            Values = new ChartValues<double>(attendanceValues),
                            PointGeometry = DefaultGeometries.Circle,
                            PointGeometrySize = 10,
                            DataLabels = false,
                            Stroke = brush,
                            Fill = System.Windows.Media.Brushes.Transparent,
                            StrokeThickness = 2
                        });
                    }

                    YFormatter = value => value + "%";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при загрузке данных для графика: " + ex.Message);
                    Dates = new string[0];
                    AttendanceSeries = new SeriesCollection();
                    YFormatter = value => value + "%";
                }
            }
        }

        /// <summary>
        /// Генерирует случайный цвет
        /// </summary>
        private System.Windows.Media.Color GenerateRandomColor()
        {
            return System.Windows.Media.Color.FromRgb(
                (byte)_random.Next(0, 256),
                (byte)_random.Next(0, 256),
                (byte)_random.Next(0, 256));
        }

        private void Record_Click(object sender, MouseButtonEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            if (frameworkElement?.DataContext is AttendanceRecord record)
            {
                SelectedRecord = record;
            }
        }

        private void LoadSelectedRecordStatus()
        {
            if (SelectedRecord != null)
            {
                SelectedStatus = Statuses.FirstOrDefault(s => s.Name == SelectedRecord.AttendanceStatus);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedRecord != null && SelectedStatus != null)
            {
                using (OleDbConnection connection = DatabaseHelper.GetConnection())
                {
                    try
                    {
                        connection.Open();
                        string query = @"
                            UPDATE Visit SET ID_VS = ?
                            WHERE ID_Visit = ?";

                        OleDbCommand command = new OleDbCommand(query, connection);
                        command.Parameters.AddWithValue("?", SelectedStatus.ID_VS);
                        command.Parameters.AddWithValue("?", SelectedRecord.ID_Visit);
                        command.ExecuteNonQuery();

                        MessageBox.Show("Статус посещаемости успешно сохранён.");

                        LoadAttendanceRecords();
                        UpdateDisplayedAttendanceRecords();
                        LoadChartData();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка при сохранении статуса посещаемости: " + ex.Message);
                    }
                }
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private List<StudentAttendanceSummary> GetAttendanceSummaries()
        {
            var summaries = new List<StudentAttendanceSummary>();

            var students = AllAttendanceRecords.Select(r => new { r.ID_Student, r.StudentName, r.GroupName }).Distinct();

            foreach (var student in students)
            {
                var studentRecords = AllAttendanceRecords.Where(r => r.ID_Student == student.ID_Student);

                // Total classes: count of Schedule records for the group
                int totalClasses = GetTotalClassesForGroup(student.GroupName);

                // Attended classes: count of records where AttendanceStatus is 'Присутствовал'
                int attendedClasses = studentRecords.Count(r => r.AttendanceStatus == "Присутствовал");

                // Missed classes
                int missedClasses = totalClasses - attendedClasses;

                // Attendance percentage
                double percentage = totalClasses > 0 ? (double)attendedClasses / totalClasses * 100 : 0;

                summaries.Add(new StudentAttendanceSummary
                {
                    StudentName = student.StudentName,
                    GroupName = student.GroupName,
                    TotalClasses = totalClasses,
                    AttendedClasses = attendedClasses,
                    MissedClasses = missedClasses,
                    AttendancePercentage = Math.Round(percentage, 2)
                });
            }

            return summaries;
        }

        private int GetTotalClassesForGroup(string groupName)
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                try
                {
                    connection.Open();
                    string query = @"
                        SELECT COUNT(*) 
                        FROM Schedule S
                        INNER JOIN Groups G ON S.ID_Group = G.ID_Group
                        WHERE G.Name = ?";
                    OleDbCommand command = new OleDbCommand(query, connection);
                    command.Parameters.AddWithValue("?", groupName);
                    object result = command.ExecuteScalar();
                    return Convert.ToInt32(result);
                }
                catch
                {
                    return 0;
                }
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем суммарные данные по посещаемости
                List<StudentAttendanceSummary> summaries = GetAttendanceSummaries();

                // Определяем путь к PDF-файлу (например, на рабочий стол)
                string pdfFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AttendanceReport.pdf");

                // Экспортируем данные в CSV-файл
                string csvFilePath = Path.Combine(Path.GetTempPath(), "attendance.csv");
                using (StreamWriter sw = new StreamWriter(csvFilePath, false, System.Text.Encoding.UTF8))
                {
                    // Записываем заголовки
                    sw.WriteLine("Имя студента,Группа,Всего занятий,Посещено,Пропущено,Процент посещаемости");

                    // Записываем данные по каждому студенту
                    foreach (var summary in summaries)
                    {
                        sw.WriteLine($"\"{summary.StudentName}\",\"{summary.GroupName}\",{summary.TotalClasses},{summary.AttendedClasses},{summary.MissedClasses},{summary.AttendancePercentage}%");
                    }
                }

                // Загружаем CSV-файл на FTP-сервер
                UploadFileToFtp(csvFilePath, FtpUploadPath);

                // Генерируем QR-код, указывающий на PHP-скрипт на хостинге
                string qrCodeData = AttendancePageUrl; // Используем URL PHP-скрипта
                var qrCodeWriter = new BarcodeWriter
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = new ZXing.Common.EncodingOptions
                    {
                        Height = 200,
                        Width = 200
                    }
                };
                var qrCodeImage = qrCodeWriter.Write(qrCodeData);

                // Сохраняем QR-код во временный файл
                string qrCodePath = Path.Combine(Path.GetTempPath(), "QRCode.png");
                qrCodeImage.Save(qrCodePath, System.Drawing.Imaging.ImageFormat.Png);

                // Создаём PDF-документ
                Document document = new Document();
                Section section = document.AddSection();
                section.PageSetup.Orientation = Orientation.Landscape; // Устанавливаем альбомную ориентацию

                // Добавляем заголовок
                Paragraph title = section.AddParagraph("Отчет по посещаемости", "Heading1");
                title.Format.Alignment = ParagraphAlignment.Center;
                title.Format.SpaceAfter = "1cm";

                // Добавляем таблицу с суммарными данными
                Table table = section.AddTable();
                table.Borders.Width = 0.75;

                // Определяем колонки
                Column col1 = table.AddColumn("5cm"); // Имя студента
                col1.Format.Alignment = ParagraphAlignment.Left;

                Column col2 = table.AddColumn("3cm"); // Группа
                col2.Format.Alignment = ParagraphAlignment.Left;

                Column col3 = table.AddColumn("3cm"); // Всего занятий
                col3.Format.Alignment = ParagraphAlignment.Right;

                Column col4 = table.AddColumn("3cm"); // Посещено
                col4.Format.Alignment = ParagraphAlignment.Right;

                Column col5 = table.AddColumn("3cm"); // Пропущено
                col5.Format.Alignment = ParagraphAlignment.Right;

                Column col6 = table.AddColumn("4cm"); // Процент посещаемости
                col6.Format.Alignment = ParagraphAlignment.Right;

                // Добавляем заголовок таблицы
                Row headerRow = table.AddRow();
                headerRow.Shading.Color = Colors.LightGray;
                headerRow.Cells[0].AddParagraph("Имя студента");
                headerRow.Cells[1].AddParagraph("Группа");
                headerRow.Cells[2].AddParagraph("Всего занятий");
                headerRow.Cells[3].AddParagraph("Посещено");
                headerRow.Cells[4].AddParagraph("Пропущено");
                headerRow.Cells[5].AddParagraph("Процент посещаемости");
                headerRow.Format.Font.Bold = true;

                // Добавляем данные по каждому студенту
                foreach (var summary in summaries)
                {
                    Row row = table.AddRow();
                    row.Cells[0].AddParagraph(summary.StudentName);
                    row.Cells[1].AddParagraph(summary.GroupName);
                    row.Cells[2].AddParagraph(summary.TotalClasses.ToString());
                    row.Cells[3].AddParagraph(summary.AttendedClasses.ToString());
                    row.Cells[4].AddParagraph(summary.MissedClasses.ToString());
                    row.Cells[5].AddParagraph($"{summary.AttendancePercentage}%");
                }

                // Добавляем небольшой отступ
                section.AddParagraph("\n");

                // Добавляем QR-код в PDF
                if (File.Exists(qrCodePath))
                {
                    Image qrImage = section.AddImage(qrCodePath);
                    qrImage.Width = "5cm";
                    qrImage.Height = "5cm";
                    qrImage.LockAspectRatio = true;
                    qrImage.Left = ShapePosition.Center;
                    qrImage.Top = ShapePosition.Top;
                }

                // Рендерим PDF-документ
                PdfDocumentRenderer pdfRenderer = new PdfDocumentRenderer(unicode: true)
                {
                    Document = document
                };
                pdfRenderer.RenderDocument();

                // Сохраняем PDF-файл
                pdfRenderer.PdfDocument.Save(pdfFilePath);

                // Удаляем временные файлы
                if (File.Exists(qrCodePath))
                {
                    try
                    {
                        File.Delete(qrCodePath);
                    }
                    catch
                    {
                        // Игнорируем ошибки удаления
                    }
                }

                if (File.Exists(csvFilePath))
                {
                    try
                    {
                        File.Delete(csvFilePath);
                    }
                    catch
                    {
                        // Игнорируем ошибки удаления
                    }
                }

                // Открываем PDF-файл
                Process.Start(new ProcessStartInfo
                {
                    FileName = pdfFilePath,
                    UseShellExecute = true
                });

                MessageBox.Show("Отчет успешно сохранен и открыт.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при создании PDF отчета: " + ex.Message);
            }
        }

        /// <summary>
        /// Загружает файл на FTP-сервер.
        /// </summary>
        /// <param name="localFilePath">Путь к локальному файлу.</param>
        /// <param name="remoteFilePath">Путь на FTP-сервере.</param>
        private void UploadFileToFtp(string localFilePath, string remoteFilePath)
        {
            try
            {
                // Формируем полный FTP URL
                string ftpUrl = $"ftp://{FtpHost}{remoteFilePath}";

                // Создаём запрос
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUrl);
                request.Method = WebRequestMethods.Ftp.UploadFile;

                // Указываем учетные данные
                request.Credentials = new NetworkCredential(FtpUser, FtpPassword);

                // Читаем содержимое файла
                byte[] fileContents;
                using (FileStream sourceStream = new FileStream(localFilePath, FileMode.Open))
                {
                    fileContents = new byte[sourceStream.Length];
                    sourceStream.Read(fileContents, 0, fileContents.Length);
                }

                // Записываем содержимое в запрос
                request.ContentLength = fileContents.Length;
                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(fileContents, 0, fileContents.Length);
                }

                // Получаем ответ от сервера
                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == FtpStatusCode.ClosingData)
                    {
                        // Успешно загружено
                        Console.WriteLine($"Upload File Complete, status {response.StatusDescription}");
                    }
                    else
                    {
                        MessageBox.Show($"Ошибка при загрузке файла на FTP: {response.StatusDescription}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке файла на FTP: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OpenHomeWindow(object sender, RoutedEventArgs e)
        {
            var homeWindow = new Home(UserFullName, UserId);
            OpenWindowWithAnimation(homeWindow);
        }

        private void OpenStudentListWindow(object sender, RoutedEventArgs e)
        {
            var studentListWindow = new StudentList(UserFullName, UserId);
            OpenWindowWithAnimation(studentListWindow);
        }

        private void OpenMaterialsWindow(object sender, RoutedEventArgs e)
        {
            var materialsWindow = new MaterialsWindow(UserFullName, UserId, StartSub, EndSub);
            OpenWindowWithAnimation(materialsWindow);
        }

        private void OpenNeuralNetworksWindow(object sender, RoutedEventArgs e)
        {
            var neuralNetworksWindow = new NeuralNetworks(UserFullName, UserId, StartSub, EndSub);
            OpenWindowWithAnimation(neuralNetworksWindow);
        }

        // Анимация открытия окон
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
    }

    public class AttendanceRecord
    {
        public int ID_Visit { get; set; }
        public string StudentName { get; set; }
        public string LessonName { get; set; }
        public DateTime LessonDate { get; set; }
        public string AttendanceStatus { get; set; }
        public string StatusColor { get; set; }
        public int ID_Student { get; set; }
        public int ID_Lesson { get; set; }
        public string GroupName { get; set; }
    }

    public class VisitStatus
    {
        public int ID_VS { get; set; }
        public string Name { get; set; }
    }

    public class StudentAttendanceSummary
    {
        public string StudentName { get; set; }
        public string GroupName { get; set; }
        public int TotalClasses { get; set; }
        public int AttendedClasses { get; set; }
        public int MissedClasses { get; set; }
        public double AttendancePercentage { get; set; }
    }
}
