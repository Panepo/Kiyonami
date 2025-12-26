using Kiyonami;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NAudio.CoreAudioApi;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using static Kiyonami.Languages;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace KiyonamiEmbedded
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private AudioEnumerator _enumerator = null!;
        private MMDevice _selWaveIn = null!;
        private MMDevice _selWaveOut = null!;

        private AzureSTTWinUI stt = null!;
        private bool _isListening = false;
        private bool _recognizingDirection = false;
        private string _recognizedTextTemp = string.Empty;

        private TextTransClient? _textTransClient;
        private AzureTTSWinUI tts = null!;

        public MainWindow()
        {
            InitializeComponent();

            _enumerator = new AudioEnumerator();
            foreach (MMDevice waveIn in _enumerator.WaveInDevices)
            {
                ComboBoxWaveIn.Items.Add(waveIn.FriendlyName);
            }
            if (_enumerator.WaveInDevices.Length > 0)
            {
                ComboBoxWaveIn.SelectedIndex = 0;
                _selWaveIn = _enumerator.WaveInDevices[0];
            }

            foreach (MMDevice waveOut in _enumerator.WaveOutDevices)
            {
                ComboBoxWaveOut.Items.Add(waveOut.FriendlyName);
            }
            if (_enumerator.WaveOutDevices.Length > 0)
            {
                ComboBoxWaveOut.SelectedIndex = 0;
                _selWaveOut = _enumerator.WaveOutDevices[0];
            }

            foreach (LanguageInfo lang in Languages.SupportedLanguages)
            {
                ComboBoxLangInput.Items.Add(lang.Name);
                ComboBoxLangInput2.Items.Add(lang.Name);
            }
            ComboBoxLangInput.SelectedIndex = 0;
            ComboBoxLangInput2.SelectedIndex = 1;

            _textTransClient = new TextTransClient();
        }

        private void SttEventHandler(object sender, AzureSTTWinUI.ProcessEventArgs e)
        {
            if (e.Output.status == AzureSTTWinUI.AzureSttStatus.RECOGNIZED)
            {
                if (_recognizingDirection)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        TextInput.Text += e.Output.message + " ";
                    });
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        TextInput2.Text += e.Output.message + " ";
                    });
                }
            }
            //else if (e.Output.status == AzureSTTWinUI.AzureSttStatus.RECOGNIZING)
            //{
            //    DispatcherQueue.TryEnqueue(() =>
            //    {
            //        if (_recognizingDirection)
            //        {
            //            // Remove the previous temporary text
            //            if (!string.IsNullOrEmpty(_recognizedTextTemp))
            //            {
            //                int startIndex = TextInput.Text.LastIndexOf(_recognizedTextTemp);
            //                if (startIndex >= 0)
            //                {
            //                    TextInput.Text = TextInput.Text.Remove(startIndex, _recognizedTextTemp.Length);
            //                }
            //            }
            //            // Add the new temporary text
            //            _recognizedTextTemp = e.Output.message;
            //            TextInput.Text += _recognizedTextTemp;
            //        }
            //        else
            //        {
            //            // Remove the previous temporary text
            //            if (!string.IsNullOrEmpty(_recognizedTextTemp))
            //            {
            //                int startIndex = TextInput2.Text.LastIndexOf(_recognizedTextTemp);
            //                if (startIndex >= 0)
            //                {
            //                    TextInput2.Text = TextInput2.Text.Remove(startIndex, _recognizedTextTemp.Length);
            //                }
            //            }
            //            // Add the new temporary text
            //            _recognizedTextTemp = e.Output.message;
            //            TextInput2.Text += _recognizedTextTemp;
            //        }
            //    });
            //}
        }

        private void ComboBoxWaveInChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedWaveIn = (string)ComboBoxWaveIn.SelectedValue;

            for (var i = 0; i < _enumerator.WaveInDevices.Count(); i++)
            {
                if (selectedWaveIn == _enumerator.WaveInDevices[i].FriendlyName)
                {
                    this._selWaveIn = _enumerator.WaveInDevices[i];
                    break;
                }
            }
        }

        private void ComboBoxWaveOutChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedWaveOut = (string)ComboBoxWaveOut.SelectedValue;

            for (var i = 0; i < _enumerator.WaveOutDevices.Count(); i++)
            {
                if (selectedWaveOut == _enumerator.WaveOutDevices[i].FriendlyName)
                {
                    this._selWaveOut = _enumerator.WaveOutDevices[i];
                    break;
                }
            }
        }

        private async void ButtonChatClick(object sender, RoutedEventArgs e)
        {
            if (_textTransClient == null) throw new Exception("TextTransClient is not initialized.");

            if (_isListening)
            {
                stt.StopRecognition();

                string inputText = TextInput.Text;
                string? targetLanguage = ComboBoxLangInput2.SelectedValue?.ToString() ?? "";
                LanguageInfo? targetLang = Languages.SupportedLanguages.FirstOrDefault(lang => lang.Name == targetLanguage);
                if (targetLang == null)
                    throw new Exception("Selected language is not valid.");
                
                string translatedText = await _textTransClient.TranslateText(inputText, targetLang.Value.Name);
                TextInput2.Text = translatedText;

                tts = new AzureTTSWinUI(
                    Marshal.StringToHGlobalAuto(Secret.GetAzureSpeechKey()),
                    targetLang.Value.TtsCode,
                    _selWaveOut.ID);

                tts.SynthesisToSpeakerAsync(translatedText);

                ButtonChat.Content = "Start Listening";
                _isListening = false;
                ButtonChat2.IsEnabled = true;
            }
            else
            {
                string selectedLangName = ComboBoxLangInput.SelectedValue?.ToString() ?? "";
                LanguageInfo? selectedLang = Languages.SupportedLanguages.FirstOrDefault(lang => lang.Name == selectedLangName);
                if (selectedLang == null)
                    throw new Exception("Selected language is not valid.");

                stt = new AzureSTTWinUI(
                    Marshal.StringToHGlobalAuto(Secret.GetAzureSpeechKey()),
                    selectedLang.Value.SttCode,
                    _selWaveIn.ID,
                    false,
                    "");
                stt.OnProcessed += SttEventHandler;
                _recognizingDirection = true;
                ButtonChat.Content = "Stop Listening";
                ButtonChat2.IsEnabled = false;
                _isListening = true;
                TextInput.Text = string.Empty;
                await stt.RecognitionAsync();
            }
        }

        private async void ButtonChatClick2(object sender, RoutedEventArgs e)
        {
            if (_textTransClient == null) throw new Exception("TextTransClient is not initialized.");

            if (_isListening)
            {
                stt.StopRecognition();

                string inputText = TextInput2.Text;
                string? targetLanguage = ComboBoxLangInput.SelectedValue?.ToString() ?? "";
                LanguageInfo? targetLang = Languages.SupportedLanguages.FirstOrDefault(lang => lang.Name == targetLanguage);
                if (targetLang == null)
                    throw new Exception("Selected language is not valid.");

                string translatedText = await _textTransClient.TranslateText(inputText, targetLang.Value.Name);
                TextInput.Text = translatedText;

                tts = new AzureTTSWinUI(
                    Marshal.StringToHGlobalAuto(Secret.GetAzureSpeechKey()),
                    targetLang.Value.TtsCode,
                    _selWaveOut.ID);

                tts.SynthesisToSpeakerAsync(translatedText);

                ButtonChat2.Content = "Start Listening";
                _isListening = false;
                ButtonChat.IsEnabled = true;
            }
            else
            {
                string selectedLangName = ComboBoxLangInput2.SelectedValue?.ToString() ?? "";
                LanguageInfo? selectedLang = Languages.SupportedLanguages.FirstOrDefault(lang => lang.Name == selectedLangName);
                if (selectedLang == null)
                    throw new Exception("Selected language is not valid.");

                stt = new AzureSTTWinUI(
                    Marshal.StringToHGlobalAuto(Secret.GetAzureSpeechKey()),
                    selectedLang.Value.SttCode,
                    _selWaveIn.ID,
                    false,
                    "");
                stt.OnProcessed += SttEventHandler;
                _recognizingDirection = false;
                ButtonChat2.Content = "Stop Listening";
                ButtonChat.IsEnabled = false;
                _isListening = true;
                TextInput2.Text = string.Empty;
                await stt.RecognitionAsync();
            }
        }
    }
}
