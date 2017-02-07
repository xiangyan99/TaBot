using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.IO;
using Microsoft.ServiceBus.Messaging;
using System.Data.SqlClient;
using Microsoft.Bot.Connector.DirectLine;
using Microsoft.Bot.Connector.DirectLine.Models;
using Newtonsoft.Json;

namespace AlarmMonitor
{
    public class AlarmHandler
    {
        private static string sqlConn = "_SQLCONNCTIONSTRING_";
        private static string directLineSecret = "_DIRECTLINESECRET_";
        private static string botId = "_BOTID_";
        private static string fromUser = "AlarmAgent";
        public int id;
        double target;
        DirectLineClient client;
        Conversation conversation;

        public AlarmHandler()
        {
            id = 0;
            target = 1000;
            InitBotClient();
        }
        private async void InitBotClient()
        {
            client = new DirectLineClient(directLineSecret);
            conversation = await client.Conversations.NewConversationAsync();
        }

        public void Reload(Object source, ElapsedEventArgs e)
        {
            SqlConnection connection = null;
            try
            {
                connection = new SqlConnection(sqlConn);
                connection.Open();

                var command = new SqlCommand("SELECT * FROM [alarms]", connection);

                SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    id = int.Parse(reader["uid"].ToString());
                    target = double.Parse(reader["target"].ToString());
                    Console.WriteLine(String.Format("Alarm {0}: {1}", id.ToString(), target.ToString()));
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
            finally
            {
                if (connection != null)
                {
                    connection.Close();
                }
            }
        }

        public async Task Validate(double value)
        {
            if (id != 0 && value >= target)
            {
                //send directline message to bot
                if(conversation != null)
                {
                    Message userMessage = new Message
                    {
                        FromProperty = fromUser,
                        Text = string.Format("alarmtriggeredforuser: {0} with value {1}", id.ToString(), value.ToString())
                    };
                    Console.WriteLine(String.Format("Sending notification: {0}", userMessage.Text));
                    await client.Conversations.PostMessageAsync(conversation.ConversationId, userMessage);
                }

            }
        }
    }

    public class TaTelemetry
    {
        public double temperature;
    }
    class Program
    {
        static Timer timer = null;
        static void Main(string[] args)
        {
            AlarmHandler alarm = new AlarmHandler();
            alarm.Reload(null, null);
            JsonSerializerSettings settings = new JsonSerializerSettings();
            timer = new Timer(10000);
            timer.Elapsed += alarm.Reload;
            timer.Start();

            Console.WriteLine("Receive critical messages. Ctrl-C to exit.\n");
            var connectionString = "Endpoint=sb://taservicebus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=i4LCdpkFd9TcIdkTFIHPrKlBN3B+23gyrGrb/iy2Bao=";
            var queueName = "tatelemetry";

            var client = QueueClient.CreateFromConnectionString(connectionString, queueName);

            client.OnMessage(message =>
            {
                Stream stream = message.GetBody<Stream>();
                StreamReader reader = new StreamReader(stream, Encoding.ASCII);
                string s = reader.ReadToEnd();
                var value = JsonConvert.DeserializeObject<TaTelemetry>(s, settings);
                alarm.Validate(value.temperature).Wait();
                Console.WriteLine(String.Format("Message body: {0}", s));
            });

            Console.ReadLine();
        }
    }
}
