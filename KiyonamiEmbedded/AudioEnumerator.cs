using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Kiyonami
{
    class AudioEnumerator
    {
        public string[] WaveIns
        {
            get 
            {
                MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
                string[] output = new string[WaveIn.DeviceCount];

                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    output[i] = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)[i].FriendlyName;
                }
                enumerator.Dispose();
                
                return output; 
            }
        }

        public string[] WaveInIds
        {
            get
            {
                MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
                string[] output = new string[WaveIn.DeviceCount];

                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    output[i] = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)[i].ID;
                }
                enumerator.Dispose();

                return output;
            }
        }

        public MMDevice[] WaveInDevices
        {
            get
            {
                MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
                MMDevice[] output = new MMDevice[WaveIn.DeviceCount];

                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    output[i] = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)[i];
                }
                enumerator.Dispose();

                return output;
            }
        }

        public string[] WaveOuts
        {
            get
            {
                MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
                string[] output = new string[WaveOut.DeviceCount];

                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    output[i] = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)[i].FriendlyName;
                }
                enumerator.Dispose();

                return output;
            }
        }

        public string[] WaveOutIds
        {
            get
            {
                MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
                string[] output = new string[WaveOut.DeviceCount];

                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    output[i] = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)[i].ID;
                }
                enumerator.Dispose();

                return output;
            }
        }

        public MMDevice[] WaveOutDevices
        {
            get
            {
                MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
                MMDevice[] output = new MMDevice[WaveOut.DeviceCount];

                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    output[i] = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)[i];
                }
                enumerator.Dispose();

                return output;
            }
        }
    }
}
