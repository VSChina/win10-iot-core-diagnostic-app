using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.DiagnosticProvider;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Gpio;

namespace Win10IoTCoreDiagnosticApp
{
    public sealed partial class MainPage : Page
    {
        private const string DeviceConnectionString = "HostName=erich-iothub1.azure-devices.net;DeviceId=d1;SharedAccessKey=sE2LiGxD9ekXNAs7tPZqRts5Jd0j00Qd6OKOBfeQnAg=";
        private CancellationTokenSource _tokenSource;
        private readonly ContinuousDiagnosticProvider _diagnosticProvider;
        private readonly DeviceClientWrapper _deviceClient;

        public MainPage()
        {
            this.InitializeComponent();
            StartBtn.IsEnabled = true;
            StopBtn.IsEnabled = false;
            HelloMessage.Text = "Click button to send Diagnostic messages";
            _diagnosticProvider = new ContinuousDiagnosticProvider(SamplingRateSource.Client, 100);
            _deviceClient = DeviceClientWrapper.CreateFromConnectionString(DeviceConnectionString, _diagnosticProvider);
        }

        private async void StartD2CMessageClick(object sender, RoutedEventArgs e)
        {
            HelloMessage.Text = "Start send D2C Message...";
            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;
            //await SendD2CMessage();
            DHT22.Start(HelloMessage);
        }

        private void StopD2CMessageClick(object sender, RoutedEventArgs e)
        {
            HelloMessage.Text = "Click button to send Diagnostic messages";
            StopBtn.IsEnabled = false;
            StartBtn.IsEnabled = true;
            _tokenSource.Cancel();
        }

        private async Task SendD2CMessage()
        {
            try
            {
                _tokenSource = new CancellationTokenSource();
                await SendDeviceToCloudMessageAsync(_tokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                TipBox.Text = "";
            }
        }

        public async Task SendDeviceToCloudMessageAsync(CancellationToken cancelToken)
        {
            if (cancelToken.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }

            const int avgWindSpeed = 10; // m/s
            var rand = new Random();

            while (true)
            {
                if (cancelToken.IsCancellationRequested)
                    throw new TaskCanceledException();

                var currentWindSpeed = avgWindSpeed + rand.NextDouble() * 4 - 2;

                var telemetryDataPoint = new
                {
                    windSpeed = currentWindSpeed
                };

                var messageString = JsonConvert.SerializeObject(telemetryDataPoint);
                while (true)
                {
                    try
                    {
                        var message = new Message(Encoding.ASCII.GetBytes(messageString));
                        await _deviceClient.SendEventAsync(message);
                        break;
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Error occur: " + e + "\n retry...");
                        TipBox.Text = "Error occur: " + e + "\n retry...";
                        await Task.Delay(500, cancelToken);
                    }
                }

                var tip = string.Format("{0} > Sending message: {1} | Count:{2}\n", DateTime.Now, messageString, _diagnosticProvider.MessageNumber);
                Debug.WriteLine(tip);
                TipBox.Text = tip;
                await Task.Delay(500, cancelToken);
            }
        }
    }
}
