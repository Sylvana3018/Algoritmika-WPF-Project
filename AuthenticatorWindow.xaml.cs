using OtpNet;
using QRCoder;
using System;
using System.Data.OleDb;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace algoritm
{
    public partial class AuthenticatorWindow : Window
    {
        private int userId;
        private bool isSetup;
        private string secretKey;

        public AuthenticatorWindow(int userId, bool isSetup)
        {
            InitializeComponent();
            this.userId = userId;
            this.isSetup = isSetup;

            if (isSetup)
            {
                var key = KeyGeneration.GenerateRandomKey(20);
                secretKey = Base32Encoding.ToString(key);

                string user = GetUserLogin(userId);
                string issuer = "Алгоритмика";

                string otpauth = $"otpauth://totp/{issuer}:{user}?secret={secretKey}&issuer={issuer}&digits=6";

                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(otpauth, QRCodeGenerator.ECCLevel.Q);

                BitmapByteQRCode qrCode = new BitmapByteQRCode(qrCodeData);
                byte[] qrCodeImageData = qrCode.GetGraphic(20);

                QRCodeImage.Source = LoadBitmapImage(qrCodeImageData);
                QRCodeImage.Visibility = Visibility.Visible;

                InstructionText.Text = "Пожалуйста, отсканируйте QR-код с помощью Google Authenticator и введите код ниже.";
            }
            else
            {
                secretKey = GetUserAuthGoogle(userId);
                InstructionText.Text = "Пожалуйста, введите код из Google Authenticator.";
            }
        }

        private BitmapImage LoadBitmapImage(byte[] imageData)
        {
            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream ms = new MemoryStream(imageData))
            {
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = ms;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
            }
            return bitmapImage;
        }

        private string GetUserLogin(int userId)
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                string query = @"
                        SELECT Login
                        FROM [User]
                        WHERE ID_User = ?";
                OleDbCommand command = new OleDbCommand(query, connection);
                command.Parameters.AddWithValue("ID_User", userId);

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

        private void VerifyButton_Click(object sender, RoutedEventArgs e)
        {
            string code = CodeTextBox.Text.Trim();

            if (string.IsNullOrEmpty(code))
            {
                MessageBox.Show("Пожалуйста, введите код.");
                return;
            }

            var totp = new Totp(Base32Encoding.ToBytes(secretKey));
            bool isValid = totp.VerifyTotp(code, out long timeStepMatched, new VerificationWindow(2, 2));

            if (isValid)
            {
                if (isSetup)
                {
                    SaveAuthGoogle(userId, secretKey);
                }
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("Неверный код. Пожалуйста, попробуйте снова.");
            }
        }

        private void SaveAuthGoogle(int userId, string secretKey)
        {
            using (OleDbConnection connection = DatabaseHelper.GetConnection())
            {
                connection.Open();
                string query = @"
                        UPDATE [User]
                        SET AuthGoogle = ?
                        WHERE ID_User = ?";
                OleDbCommand command = new OleDbCommand(query, connection);
                command.Parameters.AddWithValue("AuthGoogle", secretKey);
                command.Parameters.AddWithValue("ID_User", userId);

                command.ExecuteNonQuery();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
    }
}
