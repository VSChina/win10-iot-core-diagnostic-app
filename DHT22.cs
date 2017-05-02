using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.UI.Xaml.Controls;

namespace Win10IoTCoreDiagnosticApp
{
    static class DHT22
    {
        //private const int intGpioData = 18;
        private const int intGpioData = 17;
        private static GpioPin gpioData;
        private static TextBox textbox;

        public static void Start(TextBox box)
        {
            //Init GPIO And Capture Data Every 2000ms
            GpioController gpioController = GpioController.GetDefault();
            gpioData = gpioController.OpenPin(intGpioData);
            gpioData.SetDriveMode(GpioPinDriveMode.InputPullUp);
            textbox = box;
            new Timer(new TimerCallback((obj) => { GetData(); }), null, 2000, 2000);
        }

        private static void GetData()
        {
            byte[] data = new byte[40];
            gpioData.SetDriveMode(GpioPinDriveMode.Output);
            gpioData.Write(GpioPinValue.Low);
            Task.Delay(1).Wait();
            gpioData.SetDriveMode(GpioPinDriveMode.InputPullUp);

            //Record Data
            while (gpioData.Read() == GpioPinValue.High) ;
            while (gpioData.Read() == GpioPinValue.Low) ;
            while (gpioData.Read() == GpioPinValue.High) ;
            byte low;
            for (int i = 0; i < 40; i++)
            {
                low = 0;
                data[i] = 0;
                while (gpioData.Read() == GpioPinValue.Low && low <= byte.MaxValue) low++;
                while (gpioData.Read() == GpioPinValue.High && data[i] <= byte.MaxValue) data[i]++;
            }

            //Analyze Data
            byte humiH = 0;
            byte humiL = 0;
            byte tempH = 0;
            byte tempL = 0;
            byte sum = 0;
            for (short i = 7; i >= 0; i--)
            {
                byte bit = data[7 - i] >= 11 ? (byte)1 : (byte)0;
                humiH += (byte)(bit << i);
            }
            for (short i = 7; i >= 0; i--)
            {
                byte bit = data[15 - i] >= 11 ? (byte)1 : (byte)0;
                humiL += (byte)(bit << i);
            }
            for (short i = 7; i >= 0; i--)
            {
                byte bit = data[23 - i] >= 11 ? (byte)1 : (byte)0;
                tempH += (byte)(bit << i);
            }
            for (short i = 7; i >= 0; i--)
            {
                byte bit = data[31 - i] >= 11 ? (byte)1 : (byte)0;
                tempL += (byte)(bit << i);
            }
            for (short i = 7; i >= 0; i--)
            {
                byte bit = data[39 - i] >= 11 ? (byte)1 : (byte)0;
                sum += (byte)(bit << i);
            }

            //Verify Data
            string textTip = "Invalid temp";
            if ((byte)(humiH + humiL + tempH + tempL) == sum)
            {
                double humidity = (double)(humiH * 256 + humiL) / 10;
                double temperature = (double)(tempH * 256 + tempL) / 10;
                Debug.WriteLine(humidity + "% " + temperature + "°C");
                textTip = string.Format($"{humidity}% {temperature} C");
            }
            Windows.UI.Core.CoreWindow.GetForCurrentThread().Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                async () =>
                {
                    textbox.Text = textTip;
                });
        }
    }
}
