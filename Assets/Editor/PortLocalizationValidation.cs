using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using CandyFlick;
using UnityEngine;
namespace CandyFlickEditor
{
    public static class PortLocalizationValidation
    {
        public static string[] Check(CandyFlickGame game)
        {
            var checks=new List<string>();var catalog=PortLocalization.CatalogData();int previous=PortLocalization.Language;
            if(!catalog.languages.SequenceEqual(new[]{"en","ru","nl","fr","de","it","ko","br","es"}))throw new Exception("Port locale order differs from the original game");
            if(catalog.entries.Any(e=>e.text.Length!=9||e.text.Any(string.IsNullOrWhiteSpace)))throw new Exception("Incomplete port translation");
            checks.Add("Every port UI catalog entry has all nine original game languages");
            try
            {
                for(int language=0;language<9;language++)
                {
                    PortLocalization.Language=language;
                    foreach(var entry in catalog.entries)
                    {
                        if(PortLocalization.Translate(entry.key)!=entry.text[language])throw new Exception("Locale lookup: "+entry.key);
                        if(!entry.key.Contains("{0}"))continue;
                        object[] args={"17","29","31"};string raw=string.Format(CultureInfo.InvariantCulture,entry.key,args);
                        if(PortLocalization.Translate(raw)!=string.Format(CultureInfo.InvariantCulture,entry.text[language],args))throw new Exception("Received-message locale: "+entry.key);
                    }
                    string winner=PortLocalization.Translate("Guest2 wins!");
                    if(!winner.Contains("Guest2"))throw new Exception("Localizing results changed a player's name");
                    if(PortLocalization.Translate("James")!="James"||PortLocalization.Translate("An unknown status")!="An unknown status")throw new Exception("Unknown content was rewritten");
                    checks.Add("Static and formatted LAN text resolves locally without rewriting player names in "+catalog.languages[language]);
                }
                if(game.Achievements.Definitions.entries.Any(e=>catalog.entries.Any(t=>t.key==e.name||t.key==e.description)))throw new Exception("Official achievement strings must remain untranslated");
                checks.Add("Official achievement names and descriptions are excluded from the port translation catalog");
                var korean=Resources.Load<Font>("Localization/CandyFlickKorean");
                if(!korean||!korean.HasCharacter('\uc0ac'))throw new Exception("Bundled Korean font cannot render Hangul");
                checks.Add("The bundled Korean font supplies actual Hangul glyphs");
            }
            finally{PortLocalization.Language=previous;}
            return checks.ToArray();
        }
    }
}
