using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CandyFlick.Original26
{
    // TextManager.LoadStrings reads key, value, separator with StringReader.ReadLine, 0x51ce8.
    public sealed class TextManager26
    {
        public int CurrentLanguage { get; private set; }
        public string Flag => new[] { "en", "ru", "nl", "fr", "de", "it", "ko", "br", "es" }[CurrentLanguage];
        readonly TextAsset[] sources;
        readonly Dictionary<string, string> table = new Dictionary<string, string>();
        public TextManager26(TextAsset[] originals)
        {
            sources = originals;
            SystemLanguage[] languages = { SystemLanguage.English, SystemLanguage.Russian, SystemLanguage.Dutch,
                SystemLanguage.French, SystemLanguage.German, SystemLanguage.Italian, SystemLanguage.Korean,
                SystemLanguage.Portuguese, SystemLanguage.Spanish };
            int deviceLanguage = System.Array.IndexOf(languages, Application.systemLanguage);
            ChangeLanguage(PlayerPrefs.GetInt("Language", Mathf.Max(0, deviceLanguage)));
        }
        public void ChangeLanguage(int index)
        {
            CurrentLanguage = Mathf.Clamp(index, 0, 8);
            PlayerPrefs.SetInt("Language", CurrentLanguage);
            table.Clear();
            using (var reader = new StringReader(sources[CurrentLanguage].text))
            {
                while (true)
                {
                    string key = reader.ReadLine(), value = reader.ReadLine(); reader.ReadLine();
                    if (key == null || value == null) break;
                    table.Add(key, value);
                }
            }
        }
        public string GetStringByKey(string key) => table.TryGetValue(key, out string value) ? value : null;
    }
}
