using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Text;

namespace algoritm
{
    public class ChatGPTClient
    {
        private static readonly HttpClient client = new HttpClient();
        private readonly string apiUrl = "http://127.0.0.1:5000/chat"; // Python

        public async Task<string> SendMessageAsync(string userMessage, double temperature, int maxTokens, string model)
        {
            var requestData = new
            {
                message = userMessage,
                temperature = temperature,
                max_tokens = maxTokens,
                model = model
            };

            var json = JsonConvert.SerializeObject(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Response content: " + responseContent);

                dynamic responseJson = JsonConvert.DeserializeObject(responseContent);
                if (responseJson != null && responseJson.reply != null)
                {
                    return responseJson.reply;
                }
                else
                {
                    Console.WriteLine("Ошибка: Некорректный ответ от API!");
                    return "Некорректный ответ от API!";
                }
            }
            catch (Exception ex)
            {
                return $"Ошибка: {ex.Message}";
            }
        }
    }

    public class Message
    {
        public ObservableCollection<MessagePart> Parts { get; set; }
        public Brush BackgroundColor { get; set; }
        public Brush ForegroundColor { get; set; }
        public HorizontalAlignment Alignment { get; set; }
        public HorizontalAlignment TimestampAlignment { get; set; }
        public Style BubbleStyle { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class MessagePart
    {
        public string Text { get; set; }
        public bool IsCode { get; set; }
        public Brush ForegroundColor { get; set; }
    }

    public partial class NeuralNetworks : Window
    {
        private ChatGPTClient chatClient;
        private bool isProcessing;

        public NeuralNetworks(string userFullName, int userId, DateTime startSub, DateTime endSub)
        {
            InitializeComponent();
            DataContext = this;
            chatClient = new ChatGPTClient();
            isProcessing = false;

            UserName = userFullName;
            UserId = userId;
            StartSub = startSub;
            EndSub = endSub;

            SubscriptionBackgroundVideo.Loaded += (s, e) => SubscriptionBackgroundVideo.Play();
            SubscriptionBackgroundVideo.MediaEnded += SubscriptionBackgroundVideo_MediaEnded;

            string cursorDirectory = System.IO.Directory.GetParent(Environment.CurrentDirectory).Parent.FullName + "\\myCursors";
            Cursor customCursor = new Cursor($"{cursorDirectory}\\NORMALS.cur");
            this.Cursor = customCursor;

            StartSubscriptionTimer();
        }

        public string UserName { get; set; }
        public int UserId { get; set; }

        private DateTime _startSub;
        private DateTime _endSub;
        private DispatcherTimer _subscriptionTimer;

        public DateTime StartSub
        {
            get => _startSub;
            set => _startSub = value;
        }

        public DateTime EndSub
        {
            get => _endSub;
            set => _endSub = value;
        }

        private string _subscriptionTimeLeft;
        public string SubscriptionTimeLeft
        {
            get => _subscriptionTimeLeft;
            set
            {
                _subscriptionTimeLeft = value;
                SubscriptionTimerTextBlock.Text = _subscriptionTimeLeft;
            }
        }

        private void SubscriptionBackgroundVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            SubscriptionBackgroundVideo.Position = TimeSpan.Zero;
            SubscriptionBackgroundVideo.Play();
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
                SubscriptionTimeLeft = $"{timeLeft.Days} дн. {timeLeft:hh\\:mm\\:ss}";
            }
            else
            {
                SubscriptionTimeLeft = "Подписка истекла";
                _subscriptionTimer.Stop();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OpenHomeWindow(object sender, RoutedEventArgs e)
        {
            var homeWindow = new Home(UserName, UserId);
            OpenWindowWithAnimation(homeWindow);
        }

        private void OpenStudentListWindow(object sender, RoutedEventArgs e)
        {
            var studentListWindow = new StudentList(UserName, UserId);
            OpenWindowWithAnimation(studentListWindow);
        }

        private void OpenMaterialsWindow(object sender, RoutedEventArgs e)
        {
            var materialsWindow = new MaterialsWindow(UserName, UserId, StartSub, EndSub);
            OpenWindowWithAnimation(materialsWindow);
        }

        private void OpenVisitWindow(object sender, RoutedEventArgs e)
        {
            var visitWindow = new VisitWindow(UserName, UserId, StartSub, EndSub);
            OpenWindowWithAnimation(visitWindow);
        }

        private void OpenNeuralNetworksWindow(object sender, RoutedEventArgs e)
        {
            var neuralNetworksWindow = new NeuralNetworks(UserName, UserId, StartSub, EndSub);
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

        private async void SendMessageButton_Click(object sender, RoutedEventArgs e)
        {
            if (isProcessing) return;

            string message = ChatInputTextBox.Text;
            if (!string.IsNullOrEmpty(message))
            {
                isProcessing = true;
                ChatInputTextBox.IsEnabled = false;

                AddMessageToChat(message, true);
                ChatInputTextBox.Clear();

                ThinkingTextBlock.Visibility = Visibility.Visible;
                var thinkingStoryboard = (Storyboard)this.Resources["ThinkingAnimation"];
                thinkingStoryboard.Begin();

                double temperature = TemperatureSlider.Value;
                int maxTokens = int.Parse(MaxTokensTextBox.Text);
                string selectedModel = ((ComboBoxItem)ModelComboBox.SelectedItem).Content.ToString();

                string response = await chatClient.SendMessageAsync(message, temperature, maxTokens, selectedModel);

                thinkingStoryboard.Stop();
                ThinkingTextBlock.Visibility = Visibility.Collapsed;

                AddMessageToChat(response, false);

                isProcessing = false;
                ChatInputTextBox.IsEnabled = true;
            }
        }

        private void AddMessageToChat(string message, bool isUser)
        {
            var parts = ParseMessage(message);

            Brush backgroundColor = isUser ? (Brush)FindResource("UserMessageBackground") : (Brush)FindResource("BotMessageBackground");
            Brush foregroundColor = isUser ? (Brush)FindResource("UserMessageForeground") : (Brush)FindResource("BotMessageForeground");
            Style bubbleStyle = isUser ? (Style)FindResource("UserMessageBubbleStyle") : (Style)FindResource("BotMessageBubbleStyle");
            HorizontalAlignment timestampAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;

            var chatMessage = new Message
            {
                Parts = parts,
                BackgroundColor = backgroundColor,
                ForegroundColor = foregroundColor,
                BubbleStyle = bubbleStyle,
                Alignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                TimestampAlignment = timestampAlignment,
                Timestamp = DateTime.Now
            };

            ChatMessagesPanel.Items.Add(chatMessage);
            ChatMessagesPanel.ScrollIntoView(chatMessage);
        }

        private ObservableCollection<MessagePart> ParseMessage(string message)
        {
            var parts = new ObservableCollection<MessagePart>();
            var lines = message.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            bool inCodeBlock = false;
            StringBuilder sb = new StringBuilder();

            foreach (var line in lines)
            {
                if (line.StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        // End of code block
                        parts.Add(new MessagePart
                        {
                            Text = sb.ToString(),
                            IsCode = true,
                            ForegroundColor = Brushes.White
                        });
                        sb.Clear();
                        inCodeBlock = false;
                    }
                    else
                    {
                        if (sb.Length > 0)
                        {
                            parts.Add(new MessagePart
                            {
                                Text = sb.ToString(),
                                IsCode = false,
                                ForegroundColor = Brushes.Black
                            });
                            sb.Clear();
                        }
                        inCodeBlock = true;
                    }
                }
                else
                {
                    sb.AppendLine(line);
                }
            }
            if (sb.Length > 0)
            {
                parts.Add(new MessagePart
                {
                    Text = sb.ToString(),
                    IsCode = inCodeBlock,
                    ForegroundColor = inCodeBlock ? Brushes.White : Brushes.Black
                });
            }
            return parts;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            double temperature = TemperatureSlider.Value;
            int maxTokens = int.Parse(MaxTokensTextBox.Text);
            double topP = TopPSlider.Value;
            double frequencyPenalty = FrequencyPenaltySlider.Value;
            double presencePenalty = PresencePenaltySlider.Value;

            var requestData = new
            {
                temperature = temperature,
                max_tokens = maxTokens,
                top_p = topP,
                frequency_penalty = frequencyPenalty,
                presence_penalty = presencePenalty
            };

            var json = JsonConvert.SerializeObject(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.PostAsync("http://127.0.0.1:5000/update_settings", content);
                    response.EnsureSuccessStatusCode();
                    MessageBox.Show("Settings applied successfully.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save settings: {ex.Message}");
                }
            }
        }

        private void FrequencyPenaltySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
        }
    }
}
