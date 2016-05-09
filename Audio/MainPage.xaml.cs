using LibAudio;
using System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Audio
{
    public sealed partial class MainPage : Page
    {
        private bool running;
        private bool stopping;
        private uint threshold = 1000;
        private WASAPIEngine audioEngine;
        private DispatcherTimer startTimer = new DispatcherTimer();

        public MainPage()
        {
            InitializeComponent();
        }

        ~MainPage()
        {
            audioEngine.Finish();
        }

        void SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            stopping = true;
            if (canvas != null)
            {
                canvas.Children.Clear();
            }
            if (running)
            {
                audioEngine.Finish();
                canvas.Children.Clear();
                running = false;
            }
            start.Text = "INITIALIZING ENGINE ...";
            if (startTimer.IsEnabled) startTimer.Stop();
            startTimer.Interval = new TimeSpan(0, 0, 5);
            startTimer.Tick += Start;
            startTimer.Start();
        }

        private void ThreadDelegateDevices(uint type, string device, bool def)
        {
            var ign = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (type != 0)
                {
                    deviceIDs.Items.Add((def ? "*" : "") + (type == 1 ? "C/" : "R/") + device.Substring(35, 36));
                }
                else
                {
                    deviceIDs.Items.Add(device);
                }

                deviceIDs.SelectedIndex = 0;
            });
        }

        private void ThreadDelegateVolume(HeartBeatType t, uint beginR, uint endR, uint beginL, uint endL, int[] memoryR, int[] memoryL, ulong[] timeStampR, ulong[] timeStampL)
        {
            if (stopping) return;

            var ign = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                start.Text = "";
                if (t == HeartBeatType.DATA)
                {
                    if (endR < beginL)
                    {
                        SoundVolume(beginR, 1000000, beginL, endL, memoryR, memoryL, timeStampR, timeStampL);
                        SoundVolume(0, endR, beginL, endL, memoryR, memoryL, timeStampR, timeStampL);
                    }
                    else
                    {
                        SoundVolume(beginR, endR, beginL, endL, memoryR, memoryL, timeStampR, timeStampL);
                    }
                }
            });
        }

        private void ThreadDelegateGraph(HeartBeatType t, uint beginR, uint endR, uint beginL, uint endL, int[] memoryR, int[] memoryL, ulong[] timeStampR, ulong[] timeStampL)
        {
            if (stopping) return;

            var ign = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                start.Text = "";
                if (t == HeartBeatType.DATA)
                {
                    if (endR < beginL)
                    {
                        SoundGraph(beginR, 1000000, beginL, endL, memoryR, memoryL, timeStampR, timeStampL);
                        SoundGraph(0, endR, beginL, endL, memoryR, memoryL, timeStampR, timeStampL);
                    }
                    else
                    {
                        SoundGraph(beginR, endR, beginL, endL, memoryR, memoryL, timeStampR, timeStampL);
                    }
                }
            });
        }

        private void ThreadDelegateDirection(HeartBeatType t, TimeWindow w, TDESample[] tde, AudioSample[] a)
        {
            if (stopping) return;

            var ign = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                start.Text = "";
                if (t == HeartBeatType.DATA)
                {
                    canvas.Children.Clear();
                    SoundDirection(w.Samples(), 0.05, tde[0].Value(), 700, 50, 0, Math.Min(700, a[0].MaxAmplitude() / 20), new SolidColorBrush(Colors.Red), 10);
                    SoundDirection(w.Samples(), 0.05, tde[1].Value(), 700, 50, 0, Math.Min(700, a[0].Average()), new SolidColorBrush(Colors.Green), 5);
                    SoundDirection(w.Samples(), 0.05, tde[2].Value(), 700, 50, 0, Math.Min(700, a[0].Average()), new SolidColorBrush(Colors.Blue), 3);
                }
            });
        }

        void SoundDirection(double rate, double dist, int delay, int x, int y, double a, uint length, SolidColorBrush color, int thickness)
        {
            double val = delay / rate * 343.2 / dist;
            if (val > 1) val = 1;
            if (val < -1) val = -1;
            double ang = Math.Asin(val) + a;

            Line line = new Line()
            {
                X1 = x,
                Y1 = y,
                X2 = x + Math.Sin(ang) * length,
                Y2 = y + Math.Cos(ang) * length,
                Stroke = color,
                StrokeThickness = thickness,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeStartLineCap = PenLineCap.Round
            };

            canvas.Children.Add(line);
        }

        void SoundGraph(uint beginR, uint endR, uint beginL, uint endL, int[] memoryR, int[] memoryL, ulong[] timeStampR, ulong[] timeStampL)
        {
            int step = 10;
            if ((bool)continueAudio.IsChecked)
            {
                step = 1;
            }

            int x0 = 10; uint bR = beginR, bL = beginL;
            canvas.Children.Clear();

            for (; bR < endR && bR < endL;)
            {
                if (Math.Abs(memoryR[bR]) > threshold) break;
                if (Math.Abs(memoryL[bL]) > threshold) break;
                bR++;
                bL++;
            }

            if (timeStampR[bR] > timeStampL[bL])
            {
                do
                {
                    bL++;
                }
                while (timeStampR[bR] > timeStampL[bL]);
            }
            else
            {
                do
                {
                    bR++;
                }
                while (timeStampR[bR] < timeStampL[bL]);
            }

            while (bR < endR - 1 && bL < endL - 1)
            {
                Line lineR = new Line()
                {
                    X1 = x0,
                    Y1 = 300 + (memoryR[bR] / 100),
                    X2 = x0 + step,
                    Y2 = 300 + (memoryR[bR + 1] / 100),
                    StrokeThickness = 3.0,
                    Stroke = new SolidColorBrush(Colors.Green),
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeStartLineCap = PenLineCap.Round
                };

                Line lineL = new Line()
                {
                    X1 = x0,
                    Y1 = 500 + (memoryL[bL] / 100),
                    X2 = x0 + step,
                    Y2 = 500 + (memoryL[bL + 1] / 100),
                    StrokeThickness = 3.0,
                    Stroke = new SolidColorBrush(Colors.Red),
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeStartLineCap = PenLineCap.Round
                };
                canvas.Children.Add(lineR);
                canvas.Children.Add(lineL);
                x0 += step;
                if (x0 > 1400) break;
                bR++;
                bL++;
            }
        }

        void SoundVolume(uint beginR, uint endR, uint beginL, uint endL, int[] memoryR, int[] memoryL, ulong[] timeStampR, ulong[] timeStampL)
        {
            int maxR = 0, maxL = 0;
            canvas.Children.Clear();

            for (var i = beginR; i < endR; i++) if (Math.Abs(memoryR[i]) > maxR) maxR = Math.Abs(memoryR[i]);
            for (var i = beginL; i < endL; i++) if (Math.Abs(memoryL[i]) > maxL) maxL = Math.Abs(memoryL[i]);

            int y = 900;
            int m = 0;

            while (y > 100 && (m < maxR || m < maxL))
            {
                if (m < maxR)
                {
                    Line lineR = new Line()
                    {
                        X1 = 200,
                        X2 = 600,
                        Y1 = y,
                        Y2 = y,
                        StrokeThickness = 5.0,
                        Stroke = m < 5000 ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red),
                        StrokeEndLineCap = PenLineCap.Round,
                        StrokeStartLineCap = PenLineCap.Round
                    };
                    canvas.Children.Add(lineR);
                }

                if (m < maxL)
                {
                    Line lineL = new Line()
                    {
                        X1 = 800,
                        X2 = 1200,
                        Y1 = y,
                        Y2 = y,
                        StrokeThickness = 5.0,
                        Stroke = m < 5000 ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red),
                        StrokeEndLineCap = PenLineCap.Round,
                        StrokeStartLineCap = PenLineCap.Round
                    };
                    canvas.Children.Add(lineL);
                }

                y -= 6;
                m += 100;
            }
        }

        private async void Start(object sender, object e)
        {
            startTimer.Stop();
            if (running) return;
            running = true;

            start.Text = "INITIALIZING ENGINE ...";
            audioEngine = new WASAPIEngine();

            deviceIDs.Items.Clear();
            await audioEngine.GetRendererDevicesAsync(ThreadDelegateDevices);
            await audioEngine.GetCaptureDevicesAsync(ThreadDelegateDevices);

            var playChannels = new AudioChannel[2]
            {
                new AudioChannel(0, 0, "3e3e", 440),
                new AudioChannel(1, 0, "5ed6", 880)
            };

            await audioEngine.InitializeRendererAsync(
                new AudioDevices(playChannels, 2),
                new AudioParameters(5000, threshold, true, (bool)continueAudio.IsChecked));

            var recChannels = new AudioChannel[2]
            {
                new AudioChannel(0, 0, "e7d4", 0),
                new AudioChannel(1, 0, "880e", 0)
            };

            threshold = uint.Parse((volume.Items[volume.SelectedIndex] as ComboBoxItem).Content.ToString());            

            switch (feature.SelectedIndex)
            {
                case 0:
                    await audioEngine.InitializeCaptureAsync(
                        ThreadDelegateVolume,
                        new AudioDevices(recChannels, 2),
                        new AudioParameters(5000, threshold, false, (bool)continueAudio.IsChecked));
                    break;

                case 1:
                    await audioEngine.InitializeCaptureAsync(
                        ThreadDelegateGraph,
                        new AudioDevices(recChannels, 2),
                        new AudioParameters(5000, threshold, false, (bool)continueAudio.IsChecked));
                    break;

                default:
                    await audioEngine.InitializeCaptureAsync(
                        ThreadDelegateDirection,
                        new AudioDevices(recChannels, 2),
                        new AudioParameters(50000, 500, 30, threshold, -50, (bool)continueAudio.IsChecked, false, "sample.txt", 1000, 212));
                    break;
            }
            stopping = false;
        }
    }
}
