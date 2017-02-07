using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Data.SqlClient;
using TaApis.Models;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Azure.Devices;
using System.Text;

namespace TaApis.Controllers
{
    public class TaController : ApiController
    {
        static ServiceClient serviceClient = null;
        static string connectionString = "_IOTCONNECTIONSTRING_";
        static string sqlConn = "_SQLCONNECTIONSQL_";
        static string weatherApiBaseAddress = "http://api.openweathermap.org/data/2.5/";
        static string weatherApi = "weather?q={0}&appid=_WEATHERAPI_";

        [Route("querytemperature")]
        [HttpGet]
        public double QueryTemperature()
        {
            SqlConnection connection = null;
            try
            {
                connection = new SqlConnection(sqlConn);
                connection.Open();

                var command = new SqlCommand("SELECT TOP (1) [Id],[temperature],[timestamp] FROM [telemetry] order by [id] desc", connection);

                SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    var result = double.Parse(reader["temperature"].ToString());
                    return result;
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
            return 0;
        }

        [Route("queryweather")]
        [HttpGet]
        public async Task<string> QueryWeather(string city)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(weatherApiBaseAddress);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            string parameter = string.Format(weatherApi, city);
            HttpResponseMessage response = client.GetAsync(parameter).Result;
            if (response.IsSuccessStatusCode)
            {
                JsonSerializerSettings settings = new JsonSerializerSettings();
                String responseString = await response.Content.ReadAsStringAsync();
                var responseElement = JsonConvert.DeserializeObject<Models.WeatherResponse>(responseString, settings);
                return responseElement.weather[0].main;
            }
            else
            {
                return "unknown";
            }
        }

        [Route("turnonheater")]
        [HttpGet]
        public async Task<bool> TurnOnHeater()
        {
            await SendCloudToDeviceMessageAsync("heateron");
            return true;
        }

        [Route("turnoffheater")]
        [HttpGet]
        public async Task<bool> TurnOffHeater()
        {
            await SendCloudToDeviceMessageAsync("heateroff");
            return true;
        }

        [Route("setalarm")]
        [HttpPost]
        public void SetAlarm([FromBody]Alarm value)
        {
            try
            {
                using (SqlConnection openCon = new SqlConnection(sqlConn))
                {
                    SqlCommand cmd = new SqlCommand("DELETE FROM ALARMS");
                    cmd.Connection = openCon;
                    openCon.Open();
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "INSERT INTO ALARMS (id,name,fromId,fromName,serviceurl,conversationid,target) VALUES ('"
                        + value.id + "','" + value.name + "','" + value.fromId + "','" + 
                        value.fromName + "', '" + value.serviceUrl + "','" + value.conversationId + "',"
                        + value.temperature + ")";
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception) { }
        }

        [Route("getinformation")]
        [HttpGet]
        public Alarm GetInformation(int id)
        {
            string queryString = "SELECT * FROM ALARMS WHERE uid=" + id;
            using (SqlConnection connection = new SqlConnection(sqlConn))
            {
                SqlCommand command = new SqlCommand(queryString, connection);
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();
                try
                {
                    if (reader.Read())
                    {
                        Alarm result = new Alarm();
                        result.id = reader["id"].ToString().Trim();
                        result.name = reader["name"].ToString().Trim();
                        result.fromId = reader["fromId"].ToString().Trim();
                        result.fromName = reader["fromName"].ToString().Trim();
                        result.serviceUrl = reader["serviceUrl"].ToString().Trim();
                        result.conversationId = reader["conversationId"].ToString().Trim();
                        return result;
                    }
                    else
                    {
                        return null;
                    }
                }
                finally
                {
                    // Always call Close when done reading.
                    reader.Close();
                }
            }
        }

        private async static Task SendCloudToDeviceMessageAsync(string text)
        {
            if(serviceClient == null)
                serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
            var commandMessage = new Message(Encoding.ASCII.GetBytes(text));
            commandMessage.Properties.Add("type", "command");
            await serviceClient.SendAsync("device1", commandMessage);
        }
    }
}
