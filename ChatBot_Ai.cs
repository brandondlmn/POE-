using System.Text.RegularExpressions;
using Microsoft.ML;
using Microsoft.ML.Data;
using System.IO;

namespace GUI
{
    public class ChatBot_Ai
    {
        public string UserName { get; private set; }
        public string UserInterest { get; private set; }
        public List<string> UserInterests { get; private set; }
        public MemoryManager memoryManager { get; private set; }
        private readonly MainWindow mainWindow;
        private readonly MLContext mlContext;
        private readonly PredictionEngine<SentimentData, SentimentPrediction> sentimentPredEngine;

        public class SentimentData // Changed from private to public
        {
            public string Text { get; set; }
            public bool Label { get; set; }
        }

        public class SentimentPrediction // Changed from private to public
        {
            [ColumnName("PredictedLabel")]
            public bool Prediction { get; set; }
            public float Probability { get; set; }
            public float Score { get; set; }
        }

        public ChatBot_Ai(MainWindow window, MLContext mlContext, PredictionEngine<SentimentData, SentimentPrediction> predEngine)
        {
            mainWindow = window;
            this.mlContext = mlContext;
            this.sentimentPredEngine = predEngine;
            memoryManager = new MemoryManager();
            UserInterests = new List<string>();
        }

        public bool IsValidName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && name.All(c => char.IsLetter(c) || char.IsWhiteSpace(c));
        }

        public void SetUserInfo(string name, string interest)
        {
            UserName = name;
            UserInterest = interest;
            if (!UserInterests.Contains(interest))
            {
                UserInterests.Add(interest);
            }
            SaveConversation($"User set name: {name}, interest: {interest}");
        }

        public string DetectSentiment(string input)
        {
            var prediction = sentimentPredEngine.Predict(new SentimentData { Text = input });
            float positiveScore = prediction.Probability * 100;
            string sentiment;

            if (positiveScore > 75)
                sentiment = "Positive";
            else if (positiveScore > 50)
                sentiment = "Neutral-Positive";
            else if (positiveScore > 30)
                sentiment = "Neutral-Negative";
            else
                sentiment = "Negative";

            SaveConversation($"Detected sentiment for '{input}': {sentiment} ({positiveScore:F1}% positive)");
            return sentiment;
        }

        public (string intent, (string title, string description, DateTime? reminderDate) taskDetails) DetectIntent(string input)
        {
            string inputLower = input.ToLower().Trim();

            // Task-related keywords
            string[] taskKeywords = { "add task", "set task", "create task", "remind me", "set reminder", "add reminder" };
            string[] showTasksKeywords = { "show tasks", "list tasks", "view tasks", "what are my tasks" };
            string[] showHistoryKeywords = { "what have you done", "show history", "conversation history", "recent actions" };

            // Check for task addition
            foreach (var keyword in taskKeywords)
            {
                if (inputLower.Contains(keyword))
                {
                    string title = inputLower;
                    string description = null;
                    DateTime? reminderDate = null;

                    // Extract title and description
                    foreach (var kw in taskKeywords)
                        title = title.Replace(kw, "").Trim();

                    var dateMatch = Regex.Match(inputLower, @"(by|for|on)\s+(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}|tomorrow|today)");
                    if (dateMatch.Success)
                    {
                        string dateStr = dateMatch.Groups[2].Value;
                        if (dateStr == "tomorrow")
                            reminderDate = DateTime.Now.AddDays(1).Date.AddHours(9); // Default to 9 AM tomorrow
                        else if (dateStr == "today")
                            reminderDate = DateTime.Now.Date.AddHours(18); // Default to 6 PM today
                        else if (DateTime.TryParse(dateStr, out var parsedDate))
                            reminderDate = parsedDate;

                        title = title.Replace(dateMatch.Groups[0].Value, "").Trim();
                    }

                    var descMatch = Regex.Match(inputLower, @"with\s+(.+)$");
                    if (descMatch.Success)
                    {
                        description = descMatch.Groups[1].Value.Trim();
                        title = title.Replace($"with {description}", "").Trim();
                    }

                    // Clean up title
                    title = title.Trim();
                    if (string.IsNullOrWhiteSpace(title))
                        title = description ?? "Untitled Task";

                    return ("add_task", (title, description, reminderDate));
                }
            }

            // Check for showing tasks
            foreach (var keyword in showTasksKeywords)
            {
                if (inputLower.Contains(keyword))
                    return ("show_tasks", (null, null, null));
            }

            // Check for showing history
            foreach (var keyword in showHistoryKeywords)
            {
                if (inputLower.Contains(keyword))
                    return ("show_history", (null, null, null));
            }

            // Default to general question
            return ("question", (null, null, null));
        }

        public string GenerateResponse(string question, string sentiment, bool isFollowUp, List<string> usedResponses)
        {
            string response;
            string inputLower = question.ToLower();

            if (isFollowUp)
            {
                response = GetFollowUpResponse(inputLower);
            }
            else
            {
                response = GetInitialResponse(inputLower, sentiment);
            }

            if (usedResponses.Contains(response))
            {
                response += " (Here's another perspective for you!)";
            }
            usedResponses.Add(response);
            return response;
        }

        private string GetInitialResponse(string inputLower, string sentiment)
        {
            string response;
            if (inputLower.Contains("purpose") || inputLower.Contains("what do you do"))
            {
                response = "I'm here to help you stay safe online by answering cybersecurity questions, managing tasks, and offering quizzes to test your knowledge!";
            }
            else if (inputLower.Contains("password"))
            {
                response = "Strong passwords are key! Use at least 12 characters, mix letters, numbers, and symbols, and avoid reusing them across sites.";
            }
            else if (inputLower.Contains("phishing"))
            {
                response = "Phishing attacks trick you into sharing sensitive info. Look out for urgent emails, suspicious links, and always verify the sender.";
            }
            else if (inputLower.Contains("safe browsing"))
            {
                response = "For safe browsing, use HTTPS websites, keep your browser updated, and avoid clicking unknown links.";
            }
            else if (inputLower.Contains("sql injection"))
            {
                response = "SQL injection is an attack where malicious code is inserted into a database query. Use parameterized queries to prevent it.";
            }
            else if (inputLower.Contains("cybersecurity tips"))
            {
                response = "Top tips: Use strong, unique passwords, enable 2FA, keep software updated, and be cautious with emails and links.";
            }
            else
            {
                response = "I’m not sure about that topic, but I can help with password safety, phishing, safe browsing, SQL injection, or general cybersecurity tips. What else would you like to know?";
            }

            // Add sentiment-based tone
            if (sentiment == "Positive")
                response += $" I'm glad you're excited about staying secure, {UserName}!";
            else if (sentiment == "Negative")
                response += $" I sense some concern, {UserName}. Let’s tackle this together!";
            else
                response += $" Thanks for asking, {UserName}!";

            return response;
        }

        private string GetFollowUpResponse(string inputLower)
        {
            if (inputLower.Contains("password"))
            {
                return "Want to make passwords even stronger? Consider using a password manager to generate and store unique passwords securely.";
            }
            else if (inputLower.Contains("phishing"))
            {
                return "To spot phishing, hover over links to check their URLs and never share personal info via email.";
            }
            else if (inputLower.Contains("safe browsing"))
            {
                return "Another tip for safe browsing: Use a reputable antivirus and consider a VPN for public Wi-Fi.";
            }
            else if (inputLower.Contains("sql injection"))
            {
                return "To prevent SQL injection, always validate user inputs and use ORM tools like Entity Framework.";
            }
            else if (inputLower.Contains("cybersecurity tips"))
            {
                return "More tips: Regularly back up your data and be cautious with public Wi-Fi to stay secure.";
            }
            else
            {
                return "I can dive deeper into password safety, phishing, safe browsing, SQL injection, or cybersecurity tips. What’s next?";
            }
        }

        public void SaveConversation(string message)
        {
            memoryManager.save_message(message);
        }

        public List<string> ViewConversationHistory()
        {
            return memoryManager.return_memory();
        }
    }

    public class MemoryManager
    {
        private readonly string memoryFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "conversation_memory.txt");

        public void save_message(string message)
        {
            try
            {
                File.AppendAllText(memoryFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}\n");
            }
            catch
            {
                // Ignore file errors
            }
        }

        public List<string> return_memory()
        {
            try
            {
                if (File.Exists(memoryFilePath))
                {
                    return File.ReadAllLines(memoryFilePath).ToList();
                }
            }
            catch
            {
                // Ignore file errors
            }
            return new List<string>();
        }

        public void save_memory(List<string> memory)
        {
            try
            {
                File.WriteAllLines(memoryFilePath, memory);
            }
            catch
            {
                // Ignore file errors
            }
        }
    }
}

