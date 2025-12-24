using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Kiyonami;
using Microsoft.ML.OnnxRuntime;
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

        private TextTransClient? _textTransClient;
        private bool _isTranslatingText = false;

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

            //foreach (MMDevice waveOut in _enumerator.WaveOutDevices)
            //{
            //    ComboBoxWaveOut.Items.Add(waveOut.FriendlyName);
            //}
            //if (_enumerator.WaveOutDevices.Length > 0)
            //{
            //    ComboBoxWaveOut.SelectedIndex = 0;
            //    _selWaveOut = _enumerator.WaveOutDevices[0];
            //}


            foreach (AzureSTTWinUI.AzureSttLanguage language in Enum.GetValues(typeof(AzureSTTWinUI.AzureSttLanguage)))
            {
                ComboBoxLangInput.Items.Add(language);
            }

            _textTransClient = new TextTransClient();
            foreach (string lang in _textTransClient.languages)
            {
                ComboBoxLangOutput.Items.Add(lang);
            }
        }

        private void SttEventHandler(object sender, AzureSTTWinUI.ProcessEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                TextInput.Text += e.Output.message + " ";
            });
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

        private async void ButtonChatClick(object sender, RoutedEventArgs e)
        {
            if (_isListening)
            {
                stt.StopRecognition();
                ButtonChat.Content = "Start Listening";
                _isListening = false;
            }
            else
            {
                AzureSTTWinUI.AzureSttLanguage selectedLang = (AzureSTTWinUI.AzureSttLanguage)ComboBoxLangInput.SelectedValue;
                
                stt = new AzureSTTWinUI(
                    Marshal.StringToHGlobalAuto("YOUR EMBEDDED SPEECH KEY"),
                    selectedLang,
                    _selWaveIn.ID,
                    true,
                    "keyword_getacLow.table");
                stt.OnProcessed += SttEventHandler;

                ButtonChat.Content = "Stop Listening";
                _isListening = true;
            }
        }

        private async void ButtonTransClick(object sender, RoutedEventArgs e)
        {
            if (_textTransClient == null) return;

            if (_isTranslatingText)
            {
                _textTransClient.StopGenerating();
                ButtonTrans.Content = "Translate Text";
                _isTranslatingText = false;
            }
            else
            { 
                string inputText = TextInput.Text;
                string targetLanguage = (string)ComboBoxLangOutput.SelectedValue;
                string translatedText = await _textTransClient.TranslateText(inputText, targetLanguage);
                TextOutput.Text = translatedText;
                ButtonTrans.Content = "Stop Translating";
            }
        }
    }
}
