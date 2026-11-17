using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using Path = System.IO.Path;
using ModTestRunner;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace SevenUI.Tests
{
    // Lists what Drifter's bag can take (interactables' prefabs, anything with SpecialObjectAttributes, and the bodies she
    // can grab) with the name, portrait and mass the bag card would show, as a markdown table in the runner's folder.
    // Reads prefab assets only: nothing is spawned, and no game method with side effects is called.
    internal static class DrifterBagIconCatalog
    {
        public const string ScenarioName = "drifter_bag_icon_catalog";
        private const string RunnerFolderName = "ModTestRunner";
        private const string FileName = "SevenUI-bag-icons.md";
        // The game's own limit on a carried object's mass (DrifterBagController.maxMass).
        private const float MaxMass = 700f;
        private const float DefaultMass = 1f;
        private const string AttributeSource = "attribute field";
        private const string InspectSource = "inspect info";
        private const string BodySource = "body portrait";
        private const string NoSource = "none";

        private sealed class Row
        {
            public string Prefab;
            public string DisplayName;
            public string Portrait;
            public string Source;
            public float Mass;
        }

        public static Scenario Scenario() => new Scenario(1, ScenarioName, Stages.TitanicPlains, Run);

        private static IEnumerator Run(Harness h)
        {
            List<Row> rows = Collect();
            string path = Path.Combine(Paths.BepInExRootPath, RunnerFolderName, FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Format(rows));
            h.Log($"bag icon catalog: {rows.Count} rows in {path}");
            if (rows.Count == 0) throw h.Fail($"no prefab the bag could take was found {h.Diagnosis()}");
            yield break;
        }

        private static List<Row> Collect()
        {
            Dictionary<GameObject, Row> rows = new Dictionary<GameObject, Row>();
            foreach (InteractableSpawnCard card in Resources.FindObjectsOfTypeAll<InteractableSpawnCard>())
            {
                AddPrefab(rows, card ? card.prefab : null);
            }
            // Prefab assets have no scene; the ones in the scene are instances of them.
            foreach (SpecialObjectAttributes attributes in Resources.FindObjectsOfTypeAll<SpecialObjectAttributes>())
            {
                if (attributes && !attributes.gameObject.scene.IsValid())
                {
                    AddPrefab(rows, attributes.gameObject);
                }
            }
            foreach (CharacterBody body in BodyCatalog.allBodyPrefabBodyBodyComponents)
            {
                if (body && (body.bodyFlags & CharacterBody.BodyFlags.Ungrabbable) == 0)
                {
                    AddPrefab(rows, body.gameObject);
                }
            }
            return rows.Values.OrderBy(row => row.Prefab, StringComparer.Ordinal)
                .ThenBy(row => row.Source, StringComparer.Ordinal).ThenBy(row => row.Portrait, StringComparer.Ordinal).ToList();
        }

        private static void AddPrefab(Dictionary<GameObject, Row> rows, GameObject prefab)
        {
            if (!prefab || rows.ContainsKey(prefab)) return;
            rows[prefab] = Describe(prefab);
        }

        private static Row Describe(GameObject prefab)
        {
            SpecialObjectAttributes attributes = prefab.GetComponent<SpecialObjectAttributes>();
            CharacterBody body = prefab.GetComponent<CharacterBody>();
            // Awake takes the token and texture from the provider's InspectInfo when the fields are empty; the field is
            // read here and not GetInfo, which some providers answer by changing shared data.
            InspectInfo info = prefab.GetComponent<GenericInspectInfoProvider>()?.InspectInfo?.Info;
            string token = attributes ? attributes.bestName : null;
            if (string.IsNullOrEmpty(token))
            {
                token = info != null && !string.IsNullOrEmpty(info.TitleToken) ? info.TitleToken : body ? body.baseNameToken : null;
            }
            Texture portrait = null;
            string source = NoSource;
            if (attributes && attributes.portraitIcon)
            {
                portrait = attributes.portraitIcon;
                source = AttributeSource;
            }
            else if (info != null && info.Visual && info.Visual.texture)
            {
                portrait = info.Visual.texture;
                source = InspectSource;
            }
            else if (body && body.portraitIcon)
            {
                portrait = body.portraitIcon;
                source = BodySource;
            }
            return new Row
            {
                Prefab = prefab.name,
                DisplayName = string.IsNullOrEmpty(token) ? "" : Language.GetString(token),
                Portrait = portrait ? portrait.name : "",
                Source = source,
                Mass = Mass(prefab, attributes)
            };
        }

        // As DrifterBagController.CalcBaggedMass does, without the bagged inner object's.
        private static float Mass(GameObject prefab, SpecialObjectAttributes attributes)
        {
            float mass = DefaultMass;
            IPhysMotor motor = prefab.GetComponent<IPhysMotor>();
            if (motor != null)
            {
                mass = motor.mass;
            }
            else if (attributes)
            {
                mass = attributes.massOverride;
            }
            return Mathf.Clamp(mass, 0f, MaxMass);
        }

        private static string Format(List<Row> rows)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("| Prefab | Display name | Portrait texture | Portrait source | Mass |");
            text.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (Row row in rows)
            {
                text.AppendLine($"| {Cell(row.Prefab)} | {Cell(row.DisplayName)} | {Cell(row.Portrait)} | {row.Source} | " +
                                $"{row.Mass.ToString("0.##", CultureInfo.InvariantCulture)} |");
            }
            return text.ToString();
        }

        private static string Cell(string value) =>
            (value ?? "").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }
}
