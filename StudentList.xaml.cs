using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.ComponentModel;

namespace algoritm
{
    public partial class StudentList : Window, INotifyPropertyChanged
    {
        private List<Student> students;
        private List<Student> filteredStudents;

        public event PropertyChangedEventHandler PropertyChanged;

        private string _userFullName;
        private int _userId;

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

        public StudentList(string userFullName, int userId)
        {
            InitializeComponent();
            DataContext = this;

            UserFullName = userFullName;
            UserId = userId;

            LoadStudentData();
        }


        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Student studentToDelete = (sender as Button).DataContext as Student;

            MessageBoxResult result = MessageBox.Show($"Вы уверены, что хотите удалить студента {studentToDelete.FIO}?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                bool success = DeleteStudentFromDatabase(studentToDelete);

                if (success)
                {
                    students.Remove(studentToDelete);
                    FilterStudents(SearchBox.Text);
                }
            }
        }

        private bool DeleteStudentFromDatabase(Student student)
        {
            bool success = false;

            try
            {
                using (OleDbConnection connection = DatabaseHelper.GetConnection())
                {
                    connection.Open();
                    string query = "DELETE FROM [Student] WHERE ID_User = ?";

                    using (OleDbCommand command = new OleDbCommand(query, connection))
                    {
                        command.Parameters.Add(new OleDbParameter("ID_User", OleDbType.Integer)).Value = student.ID_User;
                        int rowsAffected = command.ExecuteNonQuery();
                        success = rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления студента из базы данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return success;
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
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

        private void LoadStudentData()
        {
            students = new List<Student>();

            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                string query = @"
                    SELECT S.ID_User, U.FIO, U.Email, G.Name AS GroupName, C.Name AS CourseName, S.Comments, S.Language
                    FROM [Student] S
                    INNER JOIN [User] U ON S.ID_User = U.ID_User
                    INNER JOIN [Groups] G ON S.ID_Group = G.ID_Group
                    INNER JOIN [Courses] C ON S.ID_Course = C.ID_Course
                    ORDER BY U.FIO";

                using (OleDbCommand command = new OleDbCommand(query, connection))
                {
                    OleDbDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        students.Add(new Student
                        {
                            ID_User = Convert.ToInt32(reader["ID_User"]),
                            FIO = reader["FIO"] as string,
                            Email = reader["Email"] as string,
                            GroupName = reader["GroupName"] as string,
                            CourseName = reader["CourseName"] as string,
                            Comments = reader["Comments"] as string,
                            Language = reader["Language"] as string
                        });
                    }
                }
            }

            FilterStudents("");
        }

        private void FilterStudents(string filter)
        {
            filteredStudents = students
                .Where(s => string.IsNullOrWhiteSpace(filter) || s.FIO.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            LoadPagination();
            DisplayStudentsPage(1, 8);
        }

        private void LoadPagination()
        {
            int pageSize = 8;
            int totalPages = (int)Math.Ceiling((double)filteredStudents.Count / pageSize);

            PaginationItemsControl.Items.Clear();

            for (int i = 1; i <= totalPages; i++)
            {
                Button button = new Button
                {
                    Content = i.ToString(),
                    Style = FindResource("RoundButtonStyle") as Style,
                    Margin = new Thickness(5),
                    Width = 30,
                    Height = 30
                };

                int page = i;
                button.Click += (sender, e) =>
                {
                    DisplayStudentsPage(page, pageSize);
                };

                PaginationItemsControl.Items.Add(button);
            }
        }

        private void DisplayStudentsPage(int page, int pageSize)
        {
            int startIndex = (page - 1) * pageSize;
            List<Student> studentsToShow = filteredStudents.Skip(startIndex).Take(pageSize).ToList();
            StudentItemsControl.ItemsSource = studentsToShow;
        }

        private void OpenMapWindow(object sender, RoutedEventArgs e)
        {
            var homeWindow = new Home(_userFullName, _userId);
            OpenWindowWithAnimation(homeWindow);
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

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterStudents(SearchBox.Text);
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class Student
    {
        public int ID_User { get; set; }
        public string FIO { get; set; }
        public string Email { get; set; }
        public string GroupName { get; set; }
        public string CourseName { get; set; }
        public string Comments { get; set; }
        public string Language { get; set; }
    }
}
