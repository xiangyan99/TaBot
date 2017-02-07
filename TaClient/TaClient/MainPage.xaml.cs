using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Media.SpeechRecognition;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Bot.Connector.DirectLine;
using Microsoft.Bot.Connector.DirectLine.Models;
using Newtonsoft.Json;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace TaClient
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private static string directLineSecret = "_DIRECTLINESECRET_";
        private static string botId = "tabotapp";
        private static string fromUser = "TaClientUser";
        private SpeechRecognizer _recognizer = null;
        DirectLineClient client;
        Conversation conversation;

        public MainPage()
        {
            this.InitializeComponent();
            InitBotClient();
        }

        private async void InitBotClient()
        {
            client = new DirectLineClient(directLineSecret);
            conversation = await client.Conversations.NewConversationAsync();

            ReadBotMessagesAsync(client, conversation.ConversationId);
        }

        private void Button_Pressed(object sender, PointerRoutedEventArgs e)
        {
            this.ClickMe.Content = "Pressed";
        }

        private void Button_Released(object sender, PointerRoutedEventArgs e)
        {
            this.ClickMe.Content = "Released";
        }

        private async void ClickMe_Click(object sender, RoutedEventArgs e)
        {
            await InitializeRecognizerAsync();
            var result = await _recognizer.RecognizeWithUIAsync();
            this.Output.Text += result.Text + "\n";

            Message userMessage = new Message
            {
                FromProperty = fromUser,
                Text = result.Text
            };
            await client.Conversations.PostMessageAsync(conversation.ConversationId, userMessage);
        }

        private async void ReadBotMessagesAsync(DirectLineClient client, string conversationId)
        {
            string watermark = null;

            while (true)
            {
                var messages = await client.Conversations.GetMessagesAsync(conversationId, watermark);
                watermark = messages?.Watermark;

                var messagesFromBotText = from x in messages.Messages
                                          where x.FromProperty == botId
                                          select x;

                foreach (Message message in messagesFromBotText)
                {
                    await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                            () => { this.Output.Text += "------" + message.Text + "\n"; });
                }

                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
        }

        public async Task<bool> InitializeRecognizerAsync()
        {
            Debug.WriteLine("[Speech to Text]: initializing Speech Recognizer...");
            if (_recognizer != null)
                return true;

            _recognizer = new SpeechRecognizer(SpeechRecognizer.SystemSpeechLanguage);
            // Set UI text
            _recognizer.UIOptions.AudiblePrompt = "What you want to do...";

            // This requires internet connection
            SpeechRecognitionTopicConstraint topicConstraint = new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "Development");
            _recognizer.Constraints.Add(topicConstraint);

            SpeechRecognitionCompilationResult result = await _recognizer.CompileConstraintsAsync();   // Required

            if (result.Status != SpeechRecognitionResultStatus.Success)
            {
                Debug.WriteLine("[Speech to Text]: Grammar Compilation Failed: " + result.Status.ToString());
                return false;
            }
            
            _recognizer.ContinuousRecognitionSession.ResultGenerated += (s, e) => { Debug.WriteLine($"[Speech to Text]: recognizer results: {e.Result.Text}, {e.Result.RawConfidence.ToString()}, {e.Result.Confidence.ToString()}"); };
            Debug.WriteLine("[Speech to Text]: done initializing Speech Recognizer");
            return true;
        }
    }
}
