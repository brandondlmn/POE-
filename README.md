# GUI
Project Details Project Name: POE
Overview
A C# WPF application that educates users about cybersecurity through an interactive chatbot interface. The chatbot covers topics like phishing, password safety, safe browsing, SQL injection, and general cybersecurity tips, offering a user-friendly experience with sentiment analysis, task management, and quizzes.
Key Features
Interactive Menu System
View user details, ask questions, manage tasks, take quizzes, or see conversation history.

Navigate easily through a clean WPF interface.

Smart Conversations
Ask follow-up questions on the same topic.

Detects user sentiment (e.g., positive, worried) using ML.NET.

Remembers conversation history and tasks in a persistent file.

Engaging Interface
ASCII art-inspired welcome screen with a logo (chat.jpeg).

Audio greeting (greeting.wav) on startup.

Typewriter-style message display with simulated typing delay.

Visual borders around messages for clarity.

Quick Start
Requirements
.NET Framework: 4.7.2 or later (or .NET Core 3.1+).

Visual Studio: 2019 or later recommended.

NuGet Packages: Microsoft.ML for sentiment analysis.

Files: chat.jpeg (logo) and greeting.wav (audio) in the program’s output directory.

Setup
bash

git clone [https://github.com/your-repo/cybersecurity-chatbot.git
cd cybersecurity-chatbot](https://github.com/brandondlmn/POE-)

Open the solution (.sln) in Visual Studio.

Install Microsoft.ML via NuGet:
bash

Install-Package Microsoft.ML

Place chat.jpeg and greeting.wav in the project’s output directory (e.g., bin/Debug).

Build and run the solution (F5 in Visual Studio).

Run
Execute the compiled program from Visual Studio or the output executable.
How to Use
Start the Chatbot:
The welcome screen appears with a "Start" button.

Enter your name and a cybersecurity interest (e.g., phishing, password safety).

Main Menu:
Choose from:
1: View your details – See your name and interests.

2: Ask cybersecurity questions – Open the chat interface.

3: Manage tasks – Add, complete, or delete tasks.

4: Take cybersecurity quiz – Test your knowledge.

5: View conversation history – Review past interactions.

6: Exit – Close the application.

Chat Interface:
Ask questions (e.g., "What is phishing?") or use commands like "add task Backup data tomorrow".

Type "menu" to return to the main menu.

Respond to follow-up prompts (yes/no).

Tasks and Quiz:
Add tasks with optional reminders (e.g., "yyyy-MM-dd HH:mm").

Take the quiz to answer multiple-choice questions with feedback.

Example Interaction

ChatBot AI -> Welcome, Brandon! Ask away about cybersecurity.
You -> What is phishing?
ChatBot AI -> Phishing attacks trick you into sharing sensitive info. Look out for urgent emails, suspicious links, and always verify the sender. Thanks for asking, Alice!
Would you like a follow-up? (yes/no)
You -> Yes
ChatBot AI -> To spot phishing, hover over links to check their URLs and never share personal info via email.
Troubleshooting
Audio or logo doesn’t load:
Ensure greeting.wav and chat.jpeg are in the program’s output directory (e.g., bin/Debug).

Verify file paths in MainWindow.xaml and MainWindow.xaml.cs.

ML.NET errors:
Install Microsoft.ML via NuGet:
bash

Install-Package Microsoft.ML

Task or history not saving:
Check write permissions for conversation_memory.txt in the program directory.

UI issues:
Ensure .NET Framework 4.7.2+ is installed.

Verify WPF dependencies (System.Windows, System.Windows.Controls).

Extending the Bot
Add New Responses:
Modify GetInitialResponse and GetFollowUpResponse in ChatBot_Ai.cs to support new topics:
csharp

if (inputLower.Contains("new topic"))
{
    response = "Example response text for new topic.";
}

Expand Quiz Questions:
Add new entries to LoadQuizData in MainWindow.xaml.cs:
csharp

quizData.Add(new QuizQuestion
{
    Question = "New question?",
    CorrectChoice = "Correct answer",
    Choices = new List<string> { "Option 1", "Option 2", "Option 3" },
    Feedback = "Correct: Explanation.\nIncorrect: Explanation."
});

Enhance Intents:
Update DetectIntent in ChatBot_Ai.cs with new keywords or regex patterns:
csharp

string[] newIntentKeywords = { "new intent" };
if (inputLower.Contains("new intent"))
    return ("new_intent", (null, null, null));

File Structure

Troubleshooting
Audio or logo doesn’t load:
Ensure greeting.wav and chat.jpeg are in the program’s output directory (e.g., bin/Debug).

Verify file paths in MainWindow.xaml and MainWindow.xaml.cs.

ML.NET errors:
Install Microsoft.ML via NuGet:
bash

Install-Package Microsoft.ML

Task or history not saving:
Check write permissions for conversation_memory.txt in the program directory.

UI issues:
Ensure .NET Framework 4.7.2+ is installed.

Verify WPF dependencies (System.Windows, System.Windows.Controls).

Extending the Bot
Add New Responses:
Modify GetInitialResponse and GetFollowUpResponse in ChatBot_Ai.cs to support new topics:
csharp

if (inputLower.Contains("new topic"))
{
    response = "Example response text for new topic.";
}

Expand Quiz Questions:
Add new entries to LoadQuizData in MainWindow.xaml.cs:
csharp

quizData.Add(new QuizQuestion
{
    Question = "New question?",
    CorrectChoice = "Correct answer",
    Choices = new List<string> { "Option 1", "Option 2", "Option 3" },
    Feedback = "Correct: Explanation.\nIncorrect: Explanation."
});

Enhance Intents:
Update DetectIntent in ChatBot_Ai.cs with new keywords or regex patterns:
csharp

string[] newIntentKeywords = { "new intent" };
if (inputLower.Contains("new intent"))
    return ("new_intent", (null, null, null));

File Structure

cybersecurity-chatbot/
├── MainWindow.xaml          # WPF UI layout (welcome, user info, menu, chat, tasks, quiz)
├── MainWindow.xaml.cs       # UI logic, event handling, task management, quiz, sentiment training
├── ChatBot_Ai.cs            # Core chatbot logic (intent, responses, sentiment, memory)
├── chat.jpeg                # Logo image for UI header
├── greeting.wav             # Welcome audio played on startup
├── conversation_memory.txt  # Stores conversation history and tasks (generated)
├── debug.log                # Debug log for errors and actions (generated)
