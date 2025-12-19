using Kiyonami;
using Microsoft.ML.OnnxRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace KiyonamiWinUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private WaveInEvent? waveIn;
        private MemoryStream? audioStream;
        private WhisperWrapper whisper = null!;
        private System.Timers.Timer? recordingTimer;
        private CancellationTokenSource? cts;
        private bool isRecording;

        private TextTransClient? _textTransClient;
        private bool _isTranslatingText = false;

        public MainWindow()
        {
            InitializeComponent();

            InitialFunction();

            _textTransClient = new TextTransClient();
            foreach (string lang in _textTransClient.languages)
            {
                ComboBoxLangOutput.Items.Add(lang);
            }
        }

        private async void InitialFunction()
        {
            whisper = await WhisperWrapper.CreateAsync("./Models/whisper_small_int8_cpu_ort_1.18.0.onnx", ExecutionProviderDevicePolicy.MAX_EFFICIENCY, null, false, null);
            foreach (var language in WhisperWrapper.LanguageCodes.Keys)
            {
                ComboBoxLangInput.Items.Add(language);
            }
        }

        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            StartStopButton.IsEnabled = false;
            isRecording = !isRecording;

            if (isRecording)
            {
                TextInput.Text = "Recording...";
                StartRecording();
                StartStopButton.Content = "Stop Recording";
            }
            else
            {
                StartStopButton.Content = "Processing...";
                StopRecording();
                TextInput.Text = "Processing...";
                await TranslateAudioAsync();
                StartStopButton.Content = "Start Recording";
            }

            StartStopButton.IsEnabled = true;
        }

        private async Task TranslateAudioAsync()
        {
            if (cts == null || cts.IsCancellationRequested)
            {
                return;
            }

            if (audioStream == null)
            {
                TextInput.Text = "Please record audio first.";
                return;
            }

            var sourceLanguage = ComboBoxLangInput.SelectedItem.ToString();

            if (sourceLanguage == null || !WhisperWrapper.LanguageCodes.ContainsKey(sourceLanguage))
            {
                TextInput.Text = "Please select a source language.";
                return;
            }

            try
            {
                var audioData = audioStream.ToArray();
                var transcribedChunks = await whisper.TranscribeAsync(audioData, sourceLanguage, WhisperWrapper.TaskType.Translate, false, cts.Token);

                TextInput.Text = transcribedChunks;
            }
            catch
            {
                TextInput.Text = "Error processing audio!";
            }
        }

        private void StartRecording()
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }

            cts = new CancellationTokenSource();

            audioStream = new MemoryStream();
            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1)
            };
            waveIn.DataAvailable += (s, a) =>
            {
                audioStream.Write(a.Buffer, 0, a.BytesRecorded);
            };
            waveIn.StartRecording();

            recordingTimer = new System.Timers.Timer(29999);
            recordingTimer.Elapsed += OnRecordingTimerElapsed;
            recordingTimer.AutoReset = false;
            recordingTimer.Start();
        }

        private void OnRecordingTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                StartStopButton.IsEnabled = false;
                isRecording = false;
                StartStopButton.Content = "Start Recording";
                StopRecording();
                TextInput.Text = "Processing...";
                await TranslateAudioAsync();
                StartStopButton.IsEnabled = true;
            });
        }

        private void StopRecording()
        {
            recordingTimer?.Stop();
            recordingTimer?.Dispose();

            if (waveIn != null)
            {
                waveIn.StopRecording();
                waveIn.Dispose();
            }
        }

        private void DisposeMemory()
        {
            StopRecording();
            cts?.Cancel();
            cts?.Dispose();
            whisper.Dispose();
            waveIn?.StopRecording();
            waveIn?.Dispose();
            audioStream?.Dispose();
            recordingTimer?.Stop();
            recordingTimer?.Dispose();
        }
    }
}
