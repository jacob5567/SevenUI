using System.IO;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using RiskOfOptions;
using RiskOfOptions.Options;
using UnityEngine;

namespace SevenUI
{
    // Every RiskOfOptions type is touched only inside NoInlining methods, so the mod loads fine without it.
    internal static class RiskOfOptionsCompat
    {
        public const string GUID = "com.rune580.riskofoptions";
        private const string IconResource = "SevenUI.icon.png";

        public static bool Enabled => Chainloader.PluginInfos.ContainsKey(GUID);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Register(PluginConfig settings)
        {
            ModSettingsManager.SetModDescription(
                "Restyles the HUD: the Barlow font, flat translucent panels, and flat bars.");
            Sprite icon = LoadIcon();
            if (icon)
            {
                ModSettingsManager.SetModIcon(icon);
            }

            ModSettingsManager.AddOption(new CheckBoxOption(settings.BarlowFont));
            ModSettingsManager.AddOption(new CheckBoxOption(settings.MonospaceDamageNumbers));
            ModSettingsManager.AddOption(new CheckBoxOption(settings.FlatPanels));

            // The options menu groups these under a "Debug" header, named after their config section.
            ModSettingsManager.AddOption(new CheckBoxOption(settings.DebugLogging));
            ModSettingsManager.AddOption(new CheckBoxOption(settings.DebugAlwaysShow));
            ModSettingsManager.AddOption(new KeyBindOption(settings.DumpKey));
        }

        // The Thunderstore icon, embedded in the DLL by the csproj.
        private static Sprite LoadIcon()
        {
            using (Stream stream = typeof(RiskOfOptionsCompat).Assembly.GetManifestResourceStream(IconResource))
            {
                if (stream == null) return null;
                using (MemoryStream bytes = new MemoryStream())
                {
                    stream.CopyTo(bytes);
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!texture.LoadImage(bytes.ToArray())) return null;
                    return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                }
            }
        }
    }
}
