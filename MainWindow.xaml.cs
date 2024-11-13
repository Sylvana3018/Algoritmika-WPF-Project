using System;
using System.Data.OleDb;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace algoritm
{
    public partial class MainWindow : Window
    {
        Cursor Fiery;
        public MainWindow()
        {
            InitializeComponent();

            string cursorDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.FullName + "\\myCursors";
            Fiery = new Cursor($"{cursorDirectory}\\NORMALS.cur");

            this.Cursor = Fiery;

            // Начать воспроизведение видео при загрузке окна
            BackgroundVideo.Loaded += (s, e) => BackgroundVideo.Play();
            BackgroundVideo.MediaEnded += BackgroundVideo_MediaEnded;
        }


        private void BackgroundVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            BackgroundVideo.Position = TimeSpan.Zero;
            BackgroundVideo.Play();
        }



        // Остальной код без изменений
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            Point cursorPosition = e.GetPosition(this);
            double widthFactor = (cursorPosition.X - ActualWidth / 2) / ActualWidth;
            double heightFactor = (cursorPosition.Y - ActualHeight / 2) / ActualHeight;

            // Ограничение движения до небольших значений
            double offsetX = widthFactor * 10;  // Ограничение горизонтального движения
            double offsetY = heightFactor * 10; // Ограничение вертикального движения

            // Применение трансформаций к обоим текстовым блокам
            WelcomeTextTransform.X = offsetX;
            WelcomeTextTransform.Y = offsetY;

            WelcomeText2Transform.X = -offsetX; // Противоположное направление для "Пожаловать"
            WelcomeText2Transform.Y = -offsetY;
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

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            ValidateAndProceed();
        }

        private void ValidateAndProceed()
        {
            string username = Username.Text;
            string password = Password.Password;

            if (ValidateUser(username, password, out int userId))
            {
                UserSession.Username = username;
                UserSession.Password = password;
                UserSession.UserId = userId;

                string userFullName = GetUserFullName(username);

                // Проверка настройки Google Authenticator
                string authGoogle = GetUserAuthGoogle(userId);

                if (string.IsNullOrEmpty(authGoogle))
                {
                    // Открыть окно настройки Authenticator
                    AuthenticatorWindow authWindow = new AuthenticatorWindow(userId, isSetup: true);
                    bool? result = authWindow.ShowDialog();
                    if (result == true)
                    {
                        // Настройка успешна
                        MessageBox.Show("Успешная авторизация!");
                        Home dashboardWindow = new Home(userFullName, userId);
                        OpenWindowWithAnimation(dashboardWindow);
                    }
                    else
                    {
                        // Настройка не завершена
                        MessageBox.Show("Настройка двухфакторной аутентификации не завершена.");
                    }
                }
                else
                {
                    // Открыть окно для проверки кода
                    AuthenticatorWindow authWindow = new AuthenticatorWindow(userId, isSetup: false);
                    bool? result = authWindow.ShowDialog();
                    if (result == true)
                    {
                        // Код подтвержден
                        MessageBox.Show("Успешная авторизация!");
                        Home dashboardWindow = new Home(userFullName, userId);
                        OpenWindowWithAnimation(dashboardWindow);
                    }
                    else
                    {
                        // Проверка не удалась
                        MessageBox.Show("Неверный код двухфакторной аутентификации.");
                    }
                }
            }
            else
            {
                MessageBox.Show("Неверный логин или пароль");
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

        private bool ValidateUser(string username, string password, out int userId)
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                string query = @"
                        SELECT ID_User
                        FROM [User]
                        WHERE Login = ? AND Password = ?";
                OleDbCommand command = new OleDbCommand(query, connection);
                command.Parameters.AddWithValue("Login", username);
                command.Parameters.AddWithValue("Password", password);

                command.CommandTimeout = 30; // Установка времени ожидания команды

                object result = command.ExecuteScalar();
                if (result != null)
                {
                    userId = (int)result;
                    return true;
                }
                else
                {
                    userId = -1;
                    return false;
                }
            }
        }

        private string GetUserFullName(string username)
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                string query = @"
                SELECT FIO
                FROM [User]
                WHERE Login = ?";
                OleDbCommand command = new OleDbCommand(query, connection);
                command.Parameters.AddWithValue("Login", username);

                object result = command.ExecuteScalar();
                return result != null ? result.ToString() : string.Empty;
            }
        }

        private string GetUserAuthGoogle(int userId)
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                string query = @"
                SELECT AuthGoogle
                FROM [User]
                WHERE ID_User = ?";
                OleDbCommand command = new OleDbCommand(query, connection);
                command.Parameters.AddWithValue("ID_User", userId);

                object result = command.ExecuteScalar();
                return result != null ? result.ToString() : string.Empty;
            }
        }
    }
}
