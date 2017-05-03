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
using Sensors.Dht;

namespace Win10IoTCoreDiagnosticApp
{
    public sealed partial class MainPage : Page
    {
        private const string DeviceConnectionString = "HostName=erich-iothub1.azure-devices.net;DeviceId=d1;SharedAccessKey=ChraBvdqFY1fp2br6+sfGcOj9M2qcvOJhn/wCyHc62A=";
        private CancellationTokenSource _tokenSource;
        private readonly ContinuousDiagnosticProvider _diagnosticProvider;
        private readonly DeviceClientWrapper _deviceClient;
        private DispatcherTimer _timer = new DispatcherTimer();
        private Dht22 _dht;
        private GpioPin _pin;
        private long _continousErrorCount = 0;
        private float _lastTemperature;
        private float _lastHumidity;
        private bool _hasLastValue = false;

        public MainPage()
        {
            this.InitializeComponent();
            StartBtn.IsEnabled = true;
            StopBtn.IsEnabled = false;
            HelloMessage.Text = "Click button to send Diagnostic messages";

            _diagnosticProvider = new ContinuousDiagnosticProvider(SamplingRateSource.Server, 10);
            _deviceClient = DeviceClientWrapper.CreateFromConnectionString(DeviceConnectionString, _diagnosticProvider);

            _timer.Interval = TimeSpan.FromSeconds(2);
            _timer.Tick += OnTimerTick;

            HelloMessage.Text = "Start send D2C Message...";
            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;

            _pin = GpioController.GetDefault().OpenPin(17, GpioSharingMode.Exclusive);
            _dht = new Dht22(_pin, GpioPinDriveMode.Input);
            _timer.Start();
        }

        private async void StartD2CMessageClick(object sender, RoutedEventArgs e)
        {
            //await SendD2CMessage();
        }

        private async void OnTimerTick(object sender, object e)
        {
            DhtReading reading = new DhtReading();

            reading = await _dht.GetReadingAsync().AsTask();

            var messageString = "";
            if (reading.IsValid)
            {
                _lastTemperature = Convert.ToSingle(reading.Temperature);
                _lastHumidity = Convert.ToSingle(reading.Humidity);
                _hasLastValue = true;
                _continousErrorCount = 0;
            }
            else
            {
                _continousErrorCount++;
            }

            bool isSensorOff = _continousErrorCount > 3;

            if(!isSensorOff && _hasLastValue)
            {
                var telemetryDataPoint = new
                {
                    temperature = _lastTemperature,
                    humidity = _lastHumidity
                };
                messageString = JsonConvert.SerializeObject(telemetryDataPoint);
            }
            else
            {
                var telemetryDataPoint = new
                {
                    humidity = ""
                };
                messageString = JsonConvert.SerializeObject(telemetryDataPoint);
            }

            if (isSensorOff || _hasLastValue)
            {
                while (true)
                {
                    try
                    {
                        var message = new Message(Encoding.ASCII.GetBytes(messageString));
                        await _deviceClient.SendEventAsync(message);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error occur: " + ex + "\n retry...");
                        TipBox.Text = "Error occur: " + ex + "\n retry...";
                        await Task.Delay(500);
                    }
                }
            }
        }

        private void StopD2CMessageClick(object sender, RoutedEventArgs e)
        {
            HelloMessage.Text = "Click button to send Diagnostic messages";
            StopBtn.IsEnabled = false;
            StartBtn.IsEnabled = true;
            _timer.Stop();
            //_tokenSource.Cancel();
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
