using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Kiyonami.AzureSTTWinUI;
using static Kiyonami.AzureTTSWinUI;

namespace Kiyonami
{
    public static class Languages
    {
        public struct LanguageInfo(string name, AzureSTTWinUI.AzureSttLanguage sttCode, AzureTTSWinUI.AzureTtsVoiceName ttsCode) // Changed from private to public to fix CS0052
        {
            public string Name = name;
            public AzureSttLanguage SttCode = sttCode;
            public AzureTtsVoiceName TtsCode = ttsCode;
        }

        public static readonly List<LanguageInfo> SupportedLanguages =
        [
            new("English", AzureSttLanguage.ENGLISH, AzureTtsVoiceName.ARIA),
            new("Chinese Traditional", AzureSttLanguage.CHINESE, AzureTtsVoiceName.XIAOXIAO),
            //new("Chinese Simplified", AzureSttLanguage.CHINESE, AzureTtsVoiceName.XIAOXIAO),
            //new("Italian", AzureSttLanguage.ITIALIAN, AzureTtsVoiceName.DIEGO),
        ];
    }
}
