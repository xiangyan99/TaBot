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
using Windows.Devices.Gpio;
using Microsoft.Azure.Devices.Client;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;
using Newtonsoft.Json;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace tauwp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        GpioPinValue pinvalue = GpioPinValue.Low;
        GpioController gpio;
        GpioPin ledPin;
        GpioPin dhtPin;
        DeviceClient deviceClient;
        static string iotHubUri = "_IOTHUBURL_";
        static string deviceKey = "_DEVICEKEY_";
        DispatcherTimer timer;
        DispatcherTimer uitimer;
        GpioOneWire.DhtSensor sensor;
        double LastTemp;
        int offset = 0;
        bool heateron = false;

        public MainPage()
        {
            this.InitializeComponent();

            deviceClient = DeviceClient.Create(iotHubUri, new DeviceAuthenticationWithRegistrySymmetricKey("device1", deviceKey), TransportType.Http1);

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(20000);
            timer.Tick += Timer_Tick;

            uitimer = new DispatcherTimer();
            uitimer.Interval = TimeSpan.FromMilliseconds(5000);
            uitimer.Tick += UiTimer_Tick;

            gpio = GpioController.GetDefault();
            if (gpio != null)
            {
                ledPin = gpio.OpenPin(4);
                ledPin.SetDriveMode(GpioPinDriveMode.Output);

                Task.Run(() => ReceiveC2dAsync());

                dhtPin = gpio.OpenPin(22);

                sensor = new GpioOneWire.DhtSensor();
                sensor.Init(dhtPin);

                timer.Start();
                uitimer.Start();
            }

            LastTemp = 0;
        }

        private async void ReceiveC2dAsync()
        {
            while (true)
            {
                Message receivedMessage = await deviceClient.ReceiveAsync();
                if (receivedMessage == null) continue;

                var cmd = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                if (cmd == "heateron")
                {
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () => { this.HeaterStatus.Text = "HEATER ON"; });
                    heateron = true;
                    offset = 0;
                    ledPin.Write(GpioPinValue.Low);
                    pinvalue = GpioPinValue.Low;
                }
                else if(cmd == "heateroff")
                {
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                       () => { this.HeaterStatus.Text = "HEATER OFF"; });
                    heateron = false;
                    ledPin.Write(GpioPinValue.High);
                    pinvalue = GpioPinValue.High;
                }
                await deviceClient.CompleteAsync(receivedMessage);
                await Task.Delay(1000);
            }
        }

        private void ClickMe_Click(object sender, RoutedEventArgs e)
        {

            if (ledPin == null)
                return;
            
            if (pinvalue == GpioPinValue.High)
                pinvalue = GpioPinValue.Low;
            else
                pinvalue = GpioPinValue.High;
            ledPin.Write(pinvalue);
        }

        private async void Timer_Tick(object sender, object e)
        {
            int retryCount = 0;
            double temperature = -1;
            do
            {
                temperature = sensor.ReadTemperature();
                retryCount++;
            }
            while ((temperature == -1) && retryCount < 20);
            if (heateron)
            {
                offset = offset + 2;
                LastTemp = temperature + offset;
            }
            else
            {
                LastTemp = temperature;
            }
            if (temperature != -1)
            {
                var telemetryDataPoint = new
                {
                    temperature = LastTemp.ToString()
                };
                var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
                var message = new Message(Encoding.ASCII.GetBytes(messageString));
                message.Properties.Add("type", "telemetry");
                await deviceClient.SendEventAsync(message);
            }
        }

        private async void UiTimer_Tick(object sender, object e)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                       () => { this.HelloMessage.Text = "Temperature: " + LastTemp.ToString(); });
        }
    }
}
