namespace TABot
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Luis;
    using Microsoft.Bot.Builder.Luis.Models;
    using Microsoft.Bot.Connector;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using Newtonsoft.Json;
    using System.Net;
    using System.Text;

    public enum CmdType
    {
        TurnOnHeater,
        TurnOffHeater,
        SetAlarm,
    };
    public class Alarm
    {
        public string id;
        public string name;
        public string fromId;
        public string fromName;
        public string serviceUrl;
        public string conversationId;
        public double temperature;
    };

    public class ConversationInformation
    {
        public string fromId;
        public string fromName;
        public string recipientId;
        public string recipientName;
        public string serviceUrl;
        public string conversationId;
    }

    [Serializable]
    [LuisModel("_YOULUISAPPID_", "_YOURLUISSECRET_")]
    public class TaLuisDialog : LuisDialog<object>
    {
        private const string EntityNumber = "builtin.number";
        private const string TaApiBaseAddress = "_YOURAPIURL_";

        [LuisIntent("")]
        [LuisIntent("None")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            try
            {
                context.Wait(this.MessageReceived);
            }
            catch (Exception)
            {
                context.Wait(this.MessageReceived);
            }
        }

        [LuisIntent("queryweather")]
        public async Task QueryWeather(IDialogContext context, IAwaitable<IMessageActivity> activity, LuisResult result)
        {
            try
            {
                //await context.PostAsync("Query Weather");
                                
                if (result.Entities.Count == 0)
                {
                    await context.PostAsync("Fail");
                    context.Wait(this.MessageReceived);
                    return;
                }
                var city = result.Entities[0].Entity;
                var temp = await QueryWeather(city);
                await context.PostAsync("Current weather in " + city + " is " + temp);
                context.Wait(this.MessageReceived);
            }
            catch (Exception)
            {
                context.Wait(this.MessageReceived);
            }
        }
        
        [LuisIntent("querytemperature")]
        public async Task QueryTemperature(IDialogContext context, IAwaitable<IMessageActivity> activity, LuisResult result)
        {
            try
            {
                //await context.PostAsync("Query Temperature");
                var temp = await ReadTemperature();
                await context.PostAsync("Current temperature is " + temp.ToString() + "f");
                context.Wait(this.MessageReceived);
            }
            catch (Exception)
            {
                context.Wait(this.MessageReceived);
            }
        }

        [LuisIntent("turnoffheater")]
        public async Task TurnOffHeater(IDialogContext context, IAwaitable<IMessageActivity> activity, LuisResult result)
        {
            try
            {
                if(await SendCmd(CmdType.TurnOffHeater))
                    await context.PostAsync("Heater is off");
                context.Wait(this.MessageReceived);
            }
            catch (Exception)
            {
                context.Wait(this.MessageReceived);
            }
        }
        
        [LuisIntent("turnonheater")]
        public async Task TurnOnHeater(IDialogContext context, IAwaitable<IMessageActivity> activity, LuisResult result)
        {
            try
            {                             
                if(await SendCmd(CmdType.TurnOnHeater))
                    await context.PostAsync("Heater is on");
                context.Wait(this.MessageReceived);                
            }
            catch (Exception)
            {
                await context.PostAsync("Fail");
                context.Wait(this.MessageReceived);
            }
        }

        [LuisIntent("setalarm")]
        public async Task SetAlarm(IDialogContext context, IAwaitable<IMessageActivity> activity, LuisResult result)
        {
            try
            {
                EntityRecommendation numberEntityRecommendation;
                if (result.TryFindEntity(EntityNumber, out numberEntityRecommendation))
                {
                    double temperature;
                    var succeed = double.TryParse(numberEntityRecommendation.Entity, out temperature);
                    if (!succeed)
                    {
                        await context.PostAsync("Fail");
                        context.Wait(this.MessageReceived);
                        return;
                    }

                    var message = await activity;
                    Alarm alarm = new Alarm();
                    alarm.id = message.From.Id;
                    alarm.name = message.From.Name;
                    alarm.fromId = message.Recipient.Id;
                    alarm.fromName = message.Recipient.Name;
                    alarm.serviceUrl = message.ServiceUrl;
                    alarm.conversationId = message.Conversation.Id;
                    alarm.temperature = temperature;
                    if(await RegisterAlarm(alarm))
                        await context.PostAsync("Alarm is set on " + temperature.ToString());
                }

                context.Wait(this.MessageReceived);
            }
            catch (Exception)
            {
                await context.PostAsync("Fail");
                context.Wait(this.MessageReceived);
            }
        }

        [LuisIntent("alarmtriggered")]
        public async Task AlarmTriggered(IDialogContext context, IAwaitable<IMessageActivity> activity, LuisResult result)
        {
            try
            {
                if (result.Entities.Count > 1)
                {
                    int uid;
                    int temperature;
                    var succeed = int.TryParse(result.Entities[0].Entity, out uid);
                    if (!succeed)
                    {
                        await context.PostAsync("Fail");
                        context.Wait(this.MessageReceived);
                        return;
                    }
                    succeed = int.TryParse(result.Entities[1].Entity, out temperature);
                    if (!succeed)
                    {
                        await context.PostAsync("Fail");
                        context.Wait(this.MessageReceived);
                        return;
                    }
                    var message = await activity;
                    var user = await LoadInformation(uid);
                    var connector = new ConnectorClient(new Uri(user.serviceUrl));
                    IMessageActivity mm = Activity.CreateMessageActivity();
                    var fromAccount = new ChannelAccount(name: user.fromName, id: user.fromId);
                    mm.From = fromAccount;
                    var userAccount = new ChannelAccount(name: user.name, id: user.id);
                    mm.Recipient = userAccount;
                    mm.Locale = "en-Us";
                    mm.Text = "Notification: temperature is " + temperature.ToString();
                    await connector.Conversations.SendToConversationAsync((Activity)mm, user.conversationId);
                }

                context.Wait(this.MessageReceived);
            }
            catch (Exception)
            {
                await context.PostAsync("Fail");
                context.Wait(this.MessageReceived);
            }
        }

        private async Task<bool> SendCmd(CmdType cmdType)
        {
            string cmd = "";
            switch (cmdType)
            {
                case CmdType.TurnOffHeater:
                    cmd = "turnoffheater";
                    break;
                case CmdType.TurnOnHeater:
                    cmd = "turnonheater";
                    break;
                default:
                    return false;
            }
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(TaApiBaseAddress);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage response = client.GetAsync(cmd).Result;
            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            else
                return false;
        }

        private async Task<double> ReadTemperature()
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(TaApiBaseAddress);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            HttpResponseMessage response = client.GetAsync("querytemperature").Result;
            if (response.IsSuccessStatusCode)
            {
                var str = await response.Content.ReadAsStringAsync();
                var result = double.Parse(str);
                return result;
            }
            else
                return 0;
        }

        private async Task<string> QueryWeather(string city)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(TaApiBaseAddress);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            string parameter = "QueryWeather?city=" + city;
            HttpResponseMessage response = client.GetAsync(parameter).Result;
            if (response.IsSuccessStatusCode)
            {
                var str = await response.Content.ReadAsStringAsync();
                return str;
            }
            else
                return "unknown";
        }

        public static async Task<bool> RegisterAlarm(Alarm alarm)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(TaApiBaseAddress);

            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            
            StringContent content = new StringContent(JsonConvert.SerializeObject(alarm), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync("setalarm", content);
            response.EnsureSuccessStatusCode();
            return true;
        }

        public static async Task<Alarm> LoadInformation(int uid)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(TaApiBaseAddress);

            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = client.GetAsync("getinformation?id=" + uid.ToString()).Result;
            if (response.IsSuccessStatusCode)
            {
                JsonSerializerSettings settings = new JsonSerializerSettings();
                var responseString = await response.Content.ReadAsStringAsync();
                var responseElement = JsonConvert.DeserializeObject<Alarm>(responseString, settings);
                return responseElement;
            }
            else
                return null;
        }
    }
}
