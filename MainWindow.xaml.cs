using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging; // For BitmapImage
using System.Media; // For SoundPlayer
using Microsoft.ML;
using Microsoft.ML.Data;

namespace GUI
{
    // Main window class for the Cybersecurity Awareness Chatbot
    public partial class MainWindow : Window
    {
        // Instance variables for managing chatbot state and functionality
        private ChatBot_Ai chatbot; // Chatbot logic handler
        private string currentQuestion; // Stores the current user question
        private string currentSentiment; // Stores sentiment of the current question
        private List<string> usedResponses = new List<string>(); // Tracks used responses to avoid repetition
        private List<TaskItem> tasks = new List<TaskItem>(); // Stores user tasks
        private DispatcherTimer reminderTimer; // Timer for task reminders
        private readonly string debugLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log"); // Path for debug log
        private List<QuizQuestion> quizData; // Stores quiz questions
        private int questionIndex = 0; // Current quiz question index
        private int currentScore = 0; // Current quiz score
        private Button selectedChoice = null; // Tracks selected quiz answer
        private readonly Random random = new Random(); // For shuffling quiz answers
        private readonly MLContext mlContext; // ML context for sentiment analysis
        private PredictionEngine<ChatBot_Ai.SentimentData, ChatBot_Ai.SentimentPrediction> sentimentPredEngine; // Sentiment prediction engine
        private List<string> activityLog = new List<string>(); // Stores recent user actions
        private const int MaxLogEntries = 10; // Limits activity log to 10 entries
        private System.Media.SoundPlayer soundPlayer; // Plays greeting audio

        // Constructor: Initializes the window and chatbot components
        public MainWindow()
        {
            InitializeComponent(); // Loads XAML components
            try
            {
                LogDebug("Starting application initialization.");
                mlContext = new MLContext(); // Initialize ML context
                TrainSentimentModel(); // Train sentiment analysis model
                chatbot = new ChatBot_Ai(this, mlContext, sentimentPredEngine); // Initialize chatbot
                LoadTasks(); // Load saved tasks
                LoadQuizData(); // Load quiz questions
                SetupReminderTimer(); // Set up task reminder timer
                TasksListView.ItemsSource = tasks; // Bind tasks to ListView
                LogDebug("Application initialized successfully.");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Initialization error: {ex.Message}\n"; // Display error in UI
                LogDebug($"Initialization error: {ex.Message}"); // Log error
            }
        }

        // Handles window load event to display logo and play welcome audio
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Load logo image (optional, as XAML handles it)
            try
            {
                string imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chat.jpeg");
                if (File.Exists(imagePath))
                {
                    LogoImage.Source = new BitmapImage(new Uri(imagePath, UriKind.Absolute)); // Set logo image
                    LogDebug($"Loaded logo image from {imagePath}");
                }
                else
                {
                    throw new FileNotFoundException($"Logo image not found at {imagePath}");
                }
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error loading logo: {ex.Message}\n"; // Display error in UI
                LogDebug($"Failed to load logo image: {ex.Message}"); // Log error
            }

            // Play welcome audio
            try
            {
                string audioPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "greeting.wav");
                if (File.Exists(audioPath))
                {
                    soundPlayer = new System.Media.SoundPlayer(audioPath); // Initialize audio player
                    soundPlayer.Play(); // Play welcome audio
                    LogDebug($"Played welcome audio from {audioPath}");
                }
                else
                {
                    throw new FileNotFoundException($"Audio file not found at {audioPath}");
                }
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error playing audio: {ex.Message}\n"; // Display error in UI
                LogDebug($"Failed to play audio: {ex.Message}"); // Log error
            }
        }

        // Trains the sentiment analysis model using sample data
        private void TrainSentimentModel()
        {
            var trainingData = new List<ChatBot_Ai.SentimentData>
            {
                new ChatBot_Ai.SentimentData { Text = "I am happy", Label = true },
                new ChatBot_Ai.SentimentData { Text = "I hate this", Label = false },
                new ChatBot_Ai.SentimentData { Text = "I am sad", Label = false },
                new ChatBot_Ai.SentimentData { Text = "I am good", Label = true },
                new ChatBot_Ai.SentimentData { Text = "I love learning about cybersecurity", Label = true },
                new ChatBot_Ai.SentimentData { Text = "I'm worried about phishing", Label = false },
                new ChatBot_Ai.SentimentData { Text = "This is frustrating", Label = false },
                new ChatBot_Ai.SentimentData { Text = "Excited to secure my accounts", Label = true }
            };

            var trainDataView = mlContext.Data.LoadFromEnumerable(trainingData); // Load training data
            var pipeline = mlContext.Transforms.Text.FeaturizeText("Features", nameof(ChatBot_Ai.SentimentData.Text))
                .Append(mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(labelColumnName: "Label", featureColumnName: "Features")); // Define ML pipeline
            var model = pipeline.Fit(trainDataView); // Train model
            sentimentPredEngine = mlContext.Model.CreatePredictionEngine<ChatBot_Ai.SentimentData, ChatBot_Ai.SentimentPrediction>(model); // Create prediction engine
            LogDebug("Sentiment model trained.");
        }

        // Logs debug messages to a file
        private void LogDebug(string message)
        {
            try
            {
                File.AppendAllText(debugLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n"); // Append message with timestamp
            }
            catch
            {
                // Ignore logging errors
            }
        }

        // Sets up a timer to check for task reminders every minute
        private void SetupReminderTimer()
        {
            reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) }; // Timer runs every minute
            reminderTimer.Tick += (s, e) =>
            {
                foreach (var task in tasks.Where(t => t.ReminderDate.HasValue && t.ReminderDate <= DateTime.Now && t.Status != "Done"))
                {
                    MessageBox.Show($"Reminder: Task '{task.Title}' is due!\nDescription: {task.Description}", "Task Reminder", MessageBoxButton.OK, MessageBoxImage.Information); // Show reminder
                    LogDebug($"Reminder triggered for task: {task.Title}");
                    task.ReminderDate = null; // Clear reminder
                    SaveTasks(); // Save updated tasks
                    TasksListView.Items.Refresh(); // Refresh task list
                    AddToActivityLog($"Reminder triggered for task: {task.Title}"); // Log activity
                }
            };
            reminderTimer.Start(); // Start timer
        }

        // Loads tasks from chatbot memory
        private void LoadTasks()
        {
            var memory = chatbot.memoryManager.return_memory(); // Get memory from chatbot
            tasks.Clear(); // Clear existing tasks
            foreach (var line in memory.Where(l => l.StartsWith("task:")))
            {
                var parts = line.Substring(5).Split('|'); // Parse task data
                if (parts.Length >= 4)
                {
                    var task = new TaskItem
                    {
                        Title = parts[0],
                        Description = parts[1],
                        Status = parts[2]
                    };
                    if (DateTime.TryParse(parts[3], out var reminderDate))
                        task.ReminderDate = reminderDate; // Set reminder if valid
                    tasks.Add(task); // Add task to list
                }
            }
            TasksListView.Items.Refresh(); // Update UI
            LogDebug($"Loaded {tasks.Count} tasks.");
        }

        // Saves tasks to chatbot memory
        private void SaveTasks()
        {
            var memory = chatbot.memoryManager.return_memory(); // Get current memory
            var nonTaskLines = memory.Where(l => !l.StartsWith("task:")).ToList(); // Keep non-task entries
            var taskLines = tasks.Select(t => $"task:{t.Title}|{t.Description}|{t.Status}|{(t.ReminderDate.HasValue ? t.ReminderDate.Value.ToString("yyyy-MM-dd HH:mm") : "")}"); // Format tasks
            chatbot.memoryManager.save_memory(nonTaskLines.Concat(taskLines).ToList()); // Save combined memory
            LogDebug("Tasks saved to memory.");
        }

        // Loads predefined quiz questions
        private void LoadQuizData()
        {
            quizData = new List<QuizQuestion>
            {
                // Quiz questions with correct answers and feedback
                new QuizQuestion
                {
                    Question = "What is a common sign of a phishing email?",
                    CorrectChoice = "Urgent language demanding immediate action",
                    Choices = new List<string> { "A professional company logo", "Correct grammar and spelling", "A personalized greeting" },
                    Feedback = "Correct! Phishing emails often use urgent language to trick you into acting quickly. Always verify the sender before clicking links.\nIncorrect: Phishing emails may include logos or greetings to seem legitimate, and they often have spelling or grammar errors."
                },
                // Additional quiz questions omitted for brevity (same structure)
            };
            LogDebug("Quiz data loaded.");
        }

        // Handles clicking the "Take Quiz" button
        private void TakeQuizButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MainMenuPanel.Visibility = Visibility.Collapsed; // Hide main menu
                ChatPanel.Visibility = Visibility.Collapsed; // Hide chat panel
                FollowUpPanel.Visibility = Visibility.Collapsed; // Hide follow-up panel
                UserInfoPanel.Visibility = Visibility.Collapsed; // Hide user info panel
                TasksPanel.Visibility = Visibility.Collapsed; // Hide tasks panel
                QuizPanel.Visibility = Visibility.Visible; // Show quiz panel
                questionIndex = 0; // Reset quiz index
                currentScore = 0; // Reset score
                ShowQuiz(); // Display first question
                chatbot.SaveConversation("User started the cybersecurity quiz."); // Log action
                ConversationTextBlock.Text += "==================================================\n";
                ConversationTextBlock.Text += "Cybersecurity Quiz Started\n";
                ConversationTextBlock.Text += "==================================================\n";
                AddToActivityLog("User started the cybersecurity quiz."); // Log activity
                LogDebug("Quiz panel opened.");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error: {ex.Message}\n"; // Display error
                LogDebug($"Error in TakeQuizButton_Click: {ex.Message}");
            }
        }

        // Displays the current quiz question
        private void ShowQuiz()
        {
            try
            {
                if (questionIndex >= quizData.Count) // Check if quiz is complete
                {
                    string feedback;
                    if (currentScore >= 8)
                        feedback = $"Great job, {chatbot.UserName}! You're a cybersecurity pro! Score: {currentScore}/{quizData.Count}";
                    else if (currentScore >= 5)
                        feedback = $"Nice effort! You got {currentScore}/{quizData.Count}. Review the feedback to boost your cybersecurity skills!";
                    else
                        feedback = $"You scored {currentScore}/{quizData.Count}. Keep learning to stay safe online! Try the quiz again.";
                    ConversationTextBlock.Text += $"Quiz Completed: {feedback}\n"; // Display results
                    chatbot.SaveConversation($"Quiz completed with score: {currentScore}/{quizData.Count}"); // Log results
                    AddToActivityLog($"Quiz completed with score: {currentScore}/{quizData.Count}"); // Log activity
                    LogDebug($"Quiz completed with score: {currentScore}/{quizData.Count}");
                    MessageBox.Show(feedback, "Quiz Completed", MessageBoxButton.OK, MessageBoxImage.Information); // Show results popup
                    currentScore = 0; // Reset score
                    questionIndex = 0; // Reset index
                    QuizPanel.Visibility = Visibility.Collapsed; // Hide quiz
                    MainMenuPanel.Visibility = Visibility.Visible; // Show main menu
                    DisplayScore.Text = "Score: 0 / 0"; // Reset score display
                    FeedbackTextBlock.Visibility = Visibility.Collapsed; // Hide feedback
                    return;
                }

                selectedChoice = null; // Reset selected answer
                var currentQuiz = quizData[questionIndex]; // Get current question
                DisplayedQuestion.Text = $"{questionIndex + 1}. {currentQuiz.Question}"; // Display question
                FeedbackTextBlock.Text = ""; // Clear feedback
                FeedbackTextBlock.Visibility = Visibility.Collapsed; // Hide feedback

                var allChoices = new List<string>(currentQuiz.Choices) { currentQuiz.CorrectChoice }; // Combine choices
                var shuffled = allChoices.OrderBy(_ => random.Next()).ToList(); // Shuffle answers

                FirstChoiceButton.Visibility = Visibility.Visible; // Show first choice
                SecondChoiceButton.Visibility = Visibility.Visible; // Show second choice
                ThirdChoiceButton.Visibility = shuffled.Count > 2 ? Visibility.Visible : Visibility.Collapsed; // Show third if needed
                FourthChoiceButton.Visibility = shuffled.Count > 3 ? Visibility.Visible : Visibility.Collapsed; // Show fourth if needed

                FirstChoiceButton.Content = shuffled[0]; // Set first choice text
                SecondChoiceButton.Content = shuffled[1]; // Set second choice text
                if (shuffled.Count > 2) ThirdChoiceButton.Content = shuffled[2]; // Set third choice text
                if (shuffled.Count > 3) FourthChoiceButton.Content = shuffled[3]; // Set fourth choice text

                ClearStyle(); // Reset button styles
                DisplayScore.Text = $"Score: {currentScore} / {questionIndex}"; // Update score
                LogDebug($"Displayed quiz question {questionIndex + 1}: {currentQuiz.Question}");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error displaying quiz: {ex.Message}\n"; // Display error
                LogDebug($"Error in ShowQuiz: {ex.Message}");
            }
        }

        // Resets quiz answer button styles
        private void ClearStyle()
        {
            foreach (var choice in new[] { FirstChoiceButton, SecondChoiceButton, ThirdChoiceButton, FourthChoiceButton })
            {
                choice.Background = Brushes.LightGray; // Reset to default color
                choice.IsEnabled = true; // Enable buttons
            }
        }

        // Handles quiz answer selection
        private void HandleAnswerSelection(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedChoice != null) return; // Prevent multiple selections
                selectedChoice = sender as Button; // Get selected button
                var currentQuiz = quizData[questionIndex]; // Get current question
                string chosen = selectedChoice.Content.ToString(); // Get chosen answer
                string correct = currentQuiz.CorrectChoice; // Get correct answer

                foreach (var choice in new[] { FirstChoiceButton, SecondChoiceButton, ThirdChoiceButton, FourthChoiceButton })
                {
                    choice.IsEnabled = false; // Disable buttons
                    if (choice.Content.ToString() == correct)
                        choice.Background = Brushes.Green; // Highlight correct answer
                    else if (choice == selectedChoice)
                        choice.Background = Brushes.DarkRed; // Highlight incorrect selection
                }

                FeedbackTextBlock.Text = chosen == correct
                    ? $"Correct! {currentQuiz.Feedback.Split('\n')[0]}" // Show correct feedback
                    : $"Incorrect. {currentQuiz.Feedback.Split('\n')[1]}"; // Show incorrect feedback
                FeedbackTextBlock.Visibility = Visibility.Visible; // Show feedback

                if (chosen == correct) currentScore++; // Increment score if correct
                DisplayScore.Text = $"Score: {currentScore} / {questionIndex + 1}"; // Update score
                chatbot.SaveConversation($"Quiz question {questionIndex + 1}: User chose '{chosen}', Correct: '{correct}', Feedback: {FeedbackTextBlock.Text}"); // Log answer
                AddToActivityLog($"Quiz question {questionIndex + 1}: User chose '{chosen}', Correct: '{correct}'"); // Log activity
                LogDebug($"Quiz question {questionIndex + 1}: User chose '{chosen}', Correct: '{correct}'");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error handling answer: {ex.Message}\n"; // Display error
                LogDebug($"Error in HandleAnswerSelection: {ex.Message}");
            }
        }

        // Proceeds to the next quiz question
        private void HandleNextQuestion(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedChoice == null)
                {
                    MessageBox.Show("Please select an answer before proceeding.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); // Require answer selection
                    LogDebug("No answer selected for quiz question.");
                    return;
                }

                questionIndex++; // Move to next question
                ShowQuiz(); // Display next question
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error proceeding to next question: {ex.Message}\n"; // Display error
                LogDebug($"Error in HandleNextQuestion: {ex.Message}");
            }
        }

        // Returns to main menu from quiz
        private void BackToMenuFromQuizButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                QuizPanel.Visibility = Visibility.Collapsed; // Hide quiz
                ChatPanel.Visibility = Visibility.Collapsed; // Hide chat
                FollowUpPanel.Visibility = Visibility.Collapsed; // Hide follow-up
                UserInfoPanel.Visibility = Visibility.Collapsed; // Hide user info
                TasksPanel.Visibility = Visibility.Collapsed; // Hide tasks
                MainMenuPanel.Visibility = Visibility.Visible; // Show main menu
                chatbot.SaveConversation("User returned to main menu from quiz."); // Log action
                LogDebug("Returned to main menu from quiz.");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error: {ex.Message}\n"; // Display error
                LogDebug($"Error in BackToMenuFromQuizButton_Click: {ex.Message}");
            }
        }

        // Handles submission of user name and interest
        private void SubmitUserInfoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string name = NameTextBox.Text.Trim(); // Get user name
                string interest = InterestTextBox.Text.Trim().ToLower(); // Get user interest

                if (string.IsNullOrWhiteSpace(name) || !chatbot.IsValidName(name))
                {
                    ConversationTextBlock.Text += "Invalid input! Names should contain only letters and spaces.\n"; // Validate name
                    LogDebug("Invalid name entered.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(interest))
                {
                    ConversationTextBlock.Text += "Invalid input! Please enter a cybersecurity topic.\n"; // Validate interest
                    LogDebug("Invalid interest entered.");
                    return;
                }

                chatbot.SetUserInfo(name, interest); // Save user info
                UserInfoPanel.Visibility = Visibility.Collapsed; // Hide user info panel
                MainMenuPanel.Visibility = Visibility.Visible; // Show main menu
                MenuGreetingTextBlock.Text = $"Welcome to the main menu, {name}"; // Set greeting
                ConversationTextBlock.Text += $"I have saved your name as {name} and your interest as {interest}\n"; // Display confirmation
                AddToActivityLog($"User set name: {name}, interest: {interest}"); // Log activity
                LogDebug($"User info saved: Name={name}, Interest={interest}");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error: {ex.Message}\n"; // Display error
                LogDebug($"Error in SubmitUserInfoButton_Click: {ex.Message}");
            }
        }

        // Displays user details
        private void ViewDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConversationTextBlock.Text += "==================================================\n";
                ConversationTextBlock.Text += $"Your full name is {chatbot.UserName}\n"; // Show name
                ConversationTextBlock.Text += $"Your cybersecurity interest is {chatbot.UserInterest}\n"; // Show interest
                if (chatbot.UserInterests.Count > 0)
                    ConversationTextBlock.Text += $"Your interests are: {string.Join(", ", chatbot.UserInterests)}\n"; // Show all interests
                ConversationTextBlock.Text += "==================================================\n";
                AddToActivityLog("User viewed their details."); // Log activity
                LogDebug("User details displayed.");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error: {ex.Message}\n"; // Display error
                LogDebug($"Error in ViewDetailsButton_Click: {ex.Message}");
            }
        }

        // Opens the chat interface
        private void AskQuestionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MainMenuPanel.Visibility = Visibility.Collapsed; // Hide main menu
                ChatPanel.Visibility = Visibility.Visible; // Show chat panel
                UserInfoPanel.Visibility = Visibility.Collapsed; // Hide user info
                FollowUpPanel.Visibility = Visibility.Collapsed; // Hide follow-up
                TasksPanel.Visibility = Visibility.Collapsed; // Hide tasks
                QuizPanel.Visibility = Visibility.Collapsed; // Hide quiz
                ConversationTextBlock.Text += "==================================================\n";
                ConversationTextBlock.Text += "Ask Away About Cybersecurity\n";
                ConversationTextBlock.Text += "==================================================\n";
                AddToActivityLog("User opened the chat interface."); // Log activity
                LogDebug("Chat panel opened.");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error: {ex.Message}\n"; // Display error
                LogDebug($"Error in AskQuestionButton_Click: {ex.Message}");
            }
        }

        // Opens the task management interface
        private void ManageTasksButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MainMenuPanel.Visibility = Visibility.Collapsed; // Hide main menu
                ChatPanel.Visibility = Visibility.Collapsed; // Hide chat
                FollowUpPanel.Visibility = Visibility.Collapsed; // Hide follow-up
                UserInfoPanel.Visibility = Visibility.Collapsed; // Hide user info
                TasksPanel.Visibility = Visibility.Visible; // Show tasks panel
                QuizPanel.Visibility = Visibility.Collapsed; // Hide quiz
                ConversationTextBlock.Text += "==================================================\n";
                ConversationTextBlock.Text += "Manage Your Cybersecurity Tasks\n";
                if (tasks.Count == 0)
                {
                    ConversationTextBlock.Text += "No tasks found.\n"; // Display if no tasks
                }
                else
                {
                    ConversationTextBlock.Text += "Your Tasks:\n";
                    foreach (var task in tasks)
                    {
                        ConversationTextBlock.Text += $"Title: {task.Title}, Description: {task.Description}, Status: {task.Status}, Reminder: {(task.ReminderDate.HasValue ? task.ReminderDate.Value.ToString("yyyy-MM-dd HH:mm") : "None")}\n"; // List tasks
                    }
                }
                ConversationTextBlock.Text += "==================================================\n";
                LoadTasks(); // Refresh task list
                AddToActivityLog("User opened the tasks interface."); // Log activity
                LogDebug("Tasks panel opened.");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error: {ex.Message}\n"; // Display error
                LogDebug($"Error in ManageTasksButton_Click: {ex.Message}");
            }
        }

        // Displays conversation history
        private void ViewHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var history = chatbot.ViewConversationHistory(); // Get history from chatbot
                ConversationTextBlock.Text = "==================================================\n";
                ConversationTextBlock.Text += "Conversation History:\n";
                ConversationTextBlock.Text += "==================================================\n";
                if (history.Count == 0)
                {
                    ConversationTextBlock.Text += "No conversation history found.\n"; // Display if empty
                }
                else
                {
                    foreach (string entry in history)
                    {
                        ConversationTextBlock.Text += $"{entry}\n";
                        ConversationTextBlock.Text += "==================================================\n";
                    }
                }
                AddToActivityLog("User viewed conversation history."); // Log activity
                LogDebug("Conversation history displayed.");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error: {ex.Message}\n"; // Display error
                LogDebug($"Error in ViewHistoryButton_Click: {ex.Message}");
            }
        }

        // Closes the application
        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                chatbot.SaveConversation("User exited the application"); // Log exit
                ConversationTextBlock.Text += "==================================================\n";
                ConversationTextBlock.Text += $"Goodbye {chatbot.UserName}! Stay safe online.\n"; // Farewell message
                ConversationTextBlock.Text += "==================================================\n";
                AddToActivityLog("User exited the application."); // Log activity
                LogDebug("Application closing.");
                Close(); // Close window
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error: {ex.Message}\n"; // Display error
                LogDebug($"Error in ExitButton_Click: {ex.Message}");
            }
        }

        // Handles question submission in chat interface
        private async void SubmitQuestionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string question = QuestionTextBox.Text.Trim(); // Get user question
                if (string.IsNullOrWhiteSpace(question))
                {
                    ConversationTextBlock.Text += "==================================================\n";
                    ConversationTextBlock.Text += "Invalid input! Please enter a question or 'menu' to return\n"; // Validate input
                    ConversationTextBlock.Text += "==================================================\n";
                    LogDebug("Empty question input.");
                    return;
                }

                if (question.ToLower() == "menu")
                {
                    chatbot.SaveConversation($"User returned to menu after asking: {question}"); // Log menu return
                    ChatPanel.Visibility = Visibility.Collapsed; // Hide chat
                    MainMenuPanel.Visibility = Visibility.Visible; // Show main menu
                    UserInfoPanel.Visibility = Visibility.Collapsed; // Hide user info
                    FollowUpPanel.Visibility = Visibility.Collapsed; // Hide follow-up
                    TasksPanel.Visibility = Visibility.Collapsed; // Hide tasks
                    QuizPanel.Visibility = Visibility.Collapsed; // Hide quiz
                    LogDebug("Returned to main menu from chat.");
                    return;
                }

                if (question.ToLower() == "show activity log" || question.ToLower() == "what have you done for me?")
                {
                    LogDebug("Activity log command detected.");
                    DisplayActivityLog(); // Show activity log
                    QuestionTextBox.Text = string.Empty; // Clear input
                    return;
                }

                // Detect intent using ChatBot_Ai
                var (intent, taskDetails) = chatbot.DetectIntent(question); // Get intent and task details
                chatbot.SaveConversation($"User input: {question}, Detected intent: {intent}"); // Log intent

                if (intent == "add_task")
                {
                    await HandleAddTaskCommand(taskDetails); // Handle task addition
                    return;
                }
                else if (intent == "show_tasks")
                {
                    ConversationTextBlock.Text += "==================================================\n";
                    ConversationTextBlock.Text += "Your Tasks:\n";
                    if (tasks.Count == 0)
                    {
                        ConversationTextBlock.Text += "No tasks found.\n"; // Display if no tasks
                    }
                    else
                    {
                        foreach (var task in tasks)
                        {
                            ConversationTextBlock.Text += $"Title: {task.Title}\nDescription: {task.Description}\nStatus: {task.Status}\nReminder: {(task.ReminderDate.HasValue ? task.ReminderDate.Value.ToString("yyyy-MM-dd HH:mm") : "None")}\n\n"; // List tasks
                        }
                    }
                    ConversationTextBlock.Text += "==================================================\n";
                    LogDebug("Displayed tasks via chat command.");
                    AddToActivityLog("User viewed tasks via chat command."); // Log activity
                    QuestionTextBox.Text = string.Empty; // Clear input
                    return;
                }
                else if (intent == "show_history")
                {
                    ViewHistoryButton_Click(sender, e); // Show history
                    return;
                }

                chatbot.SaveConversation($"User asked: {question}"); // Log question
                currentQuestion = question; // Store question
                currentSentiment = chatbot.DetectSentiment(question); // Detect sentiment
                ConversationTextBlock.Text += "==================================================\n";
                ConversationTextBlock.Text += "ChatBot is typing...\n"; // Show typing indicator
                await Task.Delay(new Random().Next(1000, 3000)); // Simulate typing delay
                string response = chatbot.GenerateResponse(question, currentSentiment, false, usedResponses); // Generate response
                chatbot.SaveConversation($"Bot replied: {response}"); // Log response
                ConversationTextBlock.Text += $"ChatBot AI -> {response}\n"; // Display response
                ConversationTextBlock.Text += "==================================================\n";
                AddToActivityLog($"User asked: {question}, Bot replied: {response}"); // Log activity
                FollowUpPanel.Visibility = Visibility.Visible; // Show follow-up prompt
                QuestionTextBox.Text = string.Empty; // Clear input
                LogDebug($"Processed question: {question}, Response: {response}");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error: {ex.Message}\n"; // Display error
                LogDebug($"Error in SubmitQuestionButton_Click: {ex.Message}");
            }
        }

        // Handles adding tasks via chat command
        private async Task HandleAddTaskCommand((string title, string description, DateTime? reminderDate) taskDetails)
        {
            try
            {
                string title = taskDetails.title; // Get task title
                string description = taskDetails.description ?? "No description"; // Default description
                DateTime? reminderDate = taskDetails.reminderDate; // Get reminder date

                if (string.IsNullOrWhiteSpace(title))
                {
                    ConversationTextBlock.Text += "==================================================\n";
                    ConversationTextBlock.Text += "Task title is required.\n"; // Validate title
                    ConversationTextBlock.Text += "==================================================\n";
                    LogDebug("Task title missing in command.");
                    return;
                }

                var task = new TaskItem
                {
                    Title = title,
                    Description = description,
                    ReminderDate = reminderDate,
                    Status = "Pending"
                };
                tasks.Add(task); // Add task
                SaveTasks(); // Save tasks
                TasksListView.Items.Refresh(); // Update UI
                ConversationTextBlock.Text += "==================================================\n";
                ConversationTextBlock.Text += $"Task added: {title}\nDescription: {description}\nReminder: {(reminderDate.HasValue ? reminderDate.Value.ToString("yyyy-MM-dd HH:mm") : "None")}\n";
                if (reminderDate.HasValue)
                    ConversationTextBlock.Text += "Would you like to set another reminder?\n"; // Prompt for more reminders
                ConversationTextBlock.Text += "==================================================\n";
                AddToActivityLog($"Task added: {title} (Reminder: {(reminderDate.HasValue ? reminderDate.Value.ToString("yyyy-MM-dd HH:mm") : "None")})"); // Log activity
                LogDebug($"Task added: {title}");
                QuestionTextBox.Text = string.Empty; // Clear input
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error adding task: {ex.Message}\n"; // Display error
                LogDebug($"Error in HandleAddTaskCommand: {ex.Message}");
            }
        }

        // Handles adding tasks via UI
        private void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string title = TaskTitleTextBox.Text.Trim(); // Get task title
                string description = TaskDescriptionTextBox.Text.Trim(); // Get task description
                DateTime? reminderDate = null; // Initialize reminder date

                if (string.IsNullOrWhiteSpace(title) || title == "Task Title")
                {
                    MessageBox.Show("Task title is required!", "Error", MessageBoxButton.OK, MessageBoxImage.Error); // Validate title
                    LogDebug("Task title missing in UI.");
                    return;
                }

                if (DateTime.TryParse(TaskReminderTextBox.Text, out var parsedDate))
                {
                    reminderDate = parsedDate; // Set reminder if valid
                }
                else if (!string.IsNullOrWhiteSpace(TaskReminderTextBox.Text) && TaskReminderTextBox.Text != "YYYY-MM-DD HH:MM")
                {
                    MessageBox.Show("Invalid reminder date format. Use yyyy-MM-dd HH:mm or leave empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error); // Validate date
                    LogDebug("Invalid reminder date format in UI.");
                    return;
                }

                var task = new TaskItem
                {
                    Title = title,
                    Description = description,
                    ReminderDate = reminderDate,
                    Status = "Pending"
                };
                tasks.Add(task); // Add task
                SaveTasks(); // Save tasks
                TasksListView.Items.Refresh(); // Update UI
                ConversationTextBlock.Text += $"Task added: {title}\nDescription: {description}\nReminder: {(reminderDate.HasValue ? reminderDate.Value.ToString("yyyy-MM-dd HH:mm") : "None")}\n"; // Display confirmation
                AddToActivityLog($"Task added via UI: {title} (Reminder: {(reminderDate.HasValue ? reminderDate.Value.ToString("yyyy-MM-dd HH:mm") : "None")})"); // Log activity
                TaskTitleTextBox.Text = "Task Title"; // Reset title
                TaskDescriptionTextBox.Text = "Task Description"; // Reset description
                TaskReminderTextBox.Text = "YYYY-MM-DD HH:MM"; // Reset reminder
                LogDebug($"Task added via UI: {title}");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error adding task: {ex.Message}\n"; // Display error
                LogDebug($"Error in AddTaskButton_Click: {ex.Message}");
            }
        }

        // Marks a selected task as complete
        private void MarkTaskCompleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TasksListView.SelectedItem is TaskItem selectedTask)
                {
                    selectedTask.Status = "Done"; // Update status
                    SaveTasks(); // Save tasks
                    TasksListView.Items.Refresh(); // Update UI
                    ConversationTextBlock.Text += $"Task '{selectedTask.Title}' marked as completed.\n"; // Display confirmation
                    AddToActivityLog($"Task marked as completed: {selectedTask.Title}"); // Log activity
                    LogDebug($"Task marked as done: {selectedTask.Title}");
                }
                else
                {
                    MessageBox.Show("Please select a task to mark as complete.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); // Require selection
                    LogDebug("No task selected for marking complete.");
                }
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error marking task as complete: {ex.Message}\n"; // Display error
                LogDebug($"Error in MarkTaskCompleteButton_Click: {ex.Message}");
            }
        }

        // Deletes a selected task
        private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TasksListView.SelectedItem is TaskItem selectedTask)
                {
                    tasks.Remove(selectedTask); // Remove task
                    SaveTasks(); // Save tasks
                    TasksListView.Items.Refresh(); // Update UI
                    ConversationTextBlock.Text += $"Task '{selectedTask.Title}' deleted.\n"; // Display confirmation
                    AddToActivityLog($"Task deleted: {selectedTask.Title}"); // Log activity
                    LogDebug($"Task deleted: {selectedTask.Title}");
                }
                else
                {
                    MessageBox.Show("Please select a task to delete.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); // Require selection
                    LogDebug("No task selected for deletion.");
                }
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error deleting task: {ex.Message}\n"; // Display error
                LogDebug($"Error in DeleteTaskButton_Click: {ex.Message}");
            }
        }

        // Returns to main menu from tasks
        private void BackToMenuFromTasksButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TasksPanel.Visibility = Visibility.Collapsed; // Hide tasks
                ChatPanel.Visibility = Visibility.Collapsed; // Hide chat
                FollowUpPanel.Visibility = Visibility.Collapsed; // Hide follow-up
                UserInfoPanel.Visibility = Visibility.Collapsed; // Hide user info
                QuizPanel.Visibility = Visibility.Collapsed; // Hide quiz
                MainMenuPanel.Visibility = Visibility.Visible; // Show main menu
                LogDebug("Returned to main menu from tasks.");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error: {ex.Message}\n"; // Display error
                LogDebug($"Error in BackToMenuFromTasksButton_Click: {ex.Message}");
            }
        }

        // Handles follow-up request
        private async void FollowUpYesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                chatbot.SaveConversation("User requested follow-up"); // Log follow-up
                FollowUpPanel.Visibility = Visibility.Collapsed; // Hide follow-up panel
                ConversationTextBlock.Text += "==================================================\n";
                ConversationTextBlock.Text += "ChatBot is typing...\n"; // Show typing indicator
                await Task.Delay(new Random().Next(1000, 3000)); // Simulate typing delay
                string response = chatbot.GenerateResponse(currentQuestion, currentSentiment, true, usedResponses); // Generate follow-up response
                chatbot.SaveConversation($"Bot follow-up: {response}"); // Log response
                ConversationTextBlock.Text += $"ChatBot AI -> {response}\n"; // Display response
                ConversationTextBlock.Text += "==================================================\n";
                FollowUpPanel.Visibility = Visibility.Visible; // Show follow-up prompt again
                AddToActivityLog($"User requested follow-up on: {currentQuestion}"); // Log activity
                LogDebug($"Follow-up response: {response}");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error: {ex.Message}\n"; // Display error
                LogDebug($"Error in FollowUpYesButton_Click: {ex.Message}");
            }
        }

        // Declines follow-up request
        private void FollowUpNoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                chatbot.SaveConversation("User declined follow-up"); // Log decline
                FollowUpPanel.Visibility = Visibility.Collapsed; // Hide follow-up panel
                usedResponses.Clear(); // Clear used responses
                AddToActivityLog("User declined follow-up."); // Log activity
                LogDebug("Follow-up declined.");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error: {ex.Message}\n"; // Display error
                LogDebug($"Error in FollowUpNoButton_Click: {ex.Message}");
            }
        }

        // Returns to main menu from chat
        private void BackToMenuButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ChatPanel.Visibility = Visibility.Collapsed; // Hide chat
                TasksPanel.Visibility = Visibility.Collapsed; // Hide tasks
                FollowUpPanel.Visibility = Visibility.Collapsed; // Hide follow-up
                UserInfoPanel.Visibility = Visibility.Collapsed; // Hide user info
                QuizPanel.Visibility = Visibility.Collapsed; // Hide quiz
                MainMenuPanel.Visibility = Visibility.Visible; // Show main menu
                usedResponses.Clear(); // Clear used responses
                LogDebug("Returned to main menu from chat.");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error: {ex.Message}\n"; // Display error
                LogDebug($"Error in BackToMenuButton_Click: {ex.Message}");
            }
        }

        // Adds an action to the activity log
        private void AddToActivityLog(string action)
        {
            activityLog.Insert(0, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {action}"); // Add action with timestamp
            if (activityLog.Count > MaxLogEntries)
                activityLog.RemoveRange(MaxLogEntries, activityLog.Count - MaxLogEntries); // Limit to 10 entries
            LogDebug($"Added to activity log: {action}, Current count: {activityLog.Count}");
        }

        // Displays the activity log
        private void DisplayActivityLog()
        {
            ConversationTextBlock.Text += "==================================================\n";
            ConversationTextBlock.Text += "Here's a summary of recent actions:\n";
            if (activityLog.Count == 0)
            {
                ConversationTextBlock.Text += "No recent actions recorded.\n"; // Display if empty
                LogDebug("Activity log is empty.");
            }
            else
            {
                foreach (var entry in activityLog.Take(MaxLogEntries))
                {
                    ConversationTextBlock.Text += $"{entry}\n"; // List actions
                }
                LogDebug($"Displayed {activityLog.Count} activity log entries.");
            }
            ConversationTextBlock.Text += "==================================================\n";
            AddToActivityLog("User viewed activity log."); // Log activity
            LogDebug("Activity log displayed.");
        }

        // Handles clicking the Start button
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WelcomePanel.Visibility = Visibility.Collapsed; // Hide welcome panel
                UserInfoPanel.Visibility = Visibility.Visible; // Show user info panel
                AddToActivityLog("User started the application."); // Log activity
                LogDebug("User clicked Start, transitioned to UserInfoPanel.");
            }
            catch (Exception ex)
            {
                ConversationTextBlock.Text += $"Error: {ex.Message}\n"; // Display error
                LogDebug($"Error in StartButton_Click: {ex.Message}");
            }
        }
    }

    // Class to represent a task item
    public class TaskItem
    {
        public string Title { get; set; } // Task title
        public string Description { get; set; } // Task description
        public DateTime? ReminderDate { get; set; } // Optional reminder date
        public string Status { get; set; } // Task status (e.g., Pending, Done)
    }

    // Class to represent a quiz question
    public class QuizQuestion
    {
        public string Question { get; set; } // Quiz question text
        public string CorrectChoice { get; set; } // Correct answer
        public List<string> Choices { get; set; } // Incorrect answer choices
        public string Feedback { get; set; } // Feedback for correct/incorrect answers
    }
}
