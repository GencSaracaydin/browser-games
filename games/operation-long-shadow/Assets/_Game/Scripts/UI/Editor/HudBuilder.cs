using System.Text;
using LongShadow.Core.Editor;
using LongShadow.UI.Common;
using LongShadow.UI.Hud;
using LongShadow.UI.Menus;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace LongShadow.UI.Editor
{
    /// <summary>
    /// Builds the two UI prefabs from code (TECH_PLAN §7.13 "HudBuilder"):
    /// <list type="bullet">
    /// <item><c>Assets/_Game/UI/Prefabs/PF_HUD.prefab</c> (UI/** is WP08's; Prefabs/Systems is WP11's): <see cref="HudRoot"/>
    /// with the theme; it builds the whole HUD, menus, title card and debrief at runtime. WP11 places it in S_Persistent.</item>
    /// <item><c>Assets/_Game/Prefabs/Systems/PF_ClickGate.prefab</c>: <see cref="ClickGateView"/> (registers IClickGate) with
    /// only the typewriter font, its phosphor glow material and the two click-gate sprites, plus its own EventSystem
    /// (S_Boot has none; WP01's BootSceneBuilder instantiates the prefab when it exists).</item>
    /// </list>
    /// GUIDs are kept on rebuild (BuilderUtil.SavePrefab).
    /// </summary>
    public static class HudBuilder
    {
        /// <summary>HUD prefab path.</summary>
        public const string HudPrefabPath = "Assets/_Game/UI/Prefabs/PF_HUD.prefab";
        /// <summary>Click-gate prefab path (WP01 request).</summary>
        public const string GatePrefabPath = "Assets/_Game/Prefabs/Systems/PF_ClickGate.prefab";
        /// <summary>Click-gate glow material path.</summary>
        public const string GateGlowPath = "Assets/_Game/UI/Materials/M_UI_ClickGateGlow.mat";

        /// <summary>Builds both prefabs.</summary>
        public static void BuildAll(StringBuilder log)
        {
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(UIAssetPaths.Theme);
            if (theme == null) log?.AppendLine("No theme at " + UIAssetPaths.Theme + ": the prefabs use runtime fallbacks (run Build Theme first).");
            BuildHud(theme, log);
            BuildGate(theme, log);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Builds PF_HUD.</summary>
        public static GameObject BuildHud(UITheme theme, StringBuilder log)
        {
            var go = new GameObject("PF_HUD");
            var hud = go.AddComponent<HudRoot>();
            var so = new SerializedObject(hud);
            so.FindProperty("_theme").objectReferenceValue = theme;
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = BuilderUtil.SavePrefab(go, HudPrefabPath);
            log?.AppendLine("Saved " + HudPrefabPath);
            return prefab;
        }

        /// <summary>Builds PF_ClickGate.</summary>
        public static GameObject BuildGate(UITheme theme, StringBuilder log)
        {
            var go = new GameObject("PF_ClickGate");
            var gate = go.AddComponent<ClickGateView>();
            var font = theme != null ? theme.GetFont(FontRole.TypewriterClean) : null;
            if (font == null && theme != null) font = theme.GetFont(FontRole.Fallback);
            var glow = font != null ? GateGlow(font) : null;
            var so = new SerializedObject(gate);
            so.FindProperty("_font").objectReferenceValue = font;
            so.FindProperty("_glowMaterial").objectReferenceValue = glow;
            so.FindProperty("_background").objectReferenceValue = theme != null ? theme.GetSprite(UISpriteId.ClickGateBG) : null;
            so.FindProperty("_cursor").objectReferenceValue = theme != null ? theme.GetSprite(UISpriteId.CrtCursor) : null;
            so.ApplyModifiedPropertiesWithoutUndo();

            var esGo = new GameObject("EventSystem");
            esGo.transform.SetParent(go.transform, false);
            esGo.AddComponent<EventSystem>();
            var module = esGo.AddComponent<InputSystemUIInputModule>();
            if (!BindPersistent(module)) log?.AppendLine("  EventSystem: project-wide UI map not found; the module uses the Input System defaults.");

            var prefab = BuilderUtil.SavePrefab(go, GatePrefabPath);
            log?.AppendLine("Saved " + GatePrefabPath + " (font " + (font != null ? font.name : "runtime fallback") + ")");
            return prefab;
        }

        /// <summary>
        /// Binds a module saved in a prefab to the project-wide "UI" map through the InputActionReference sub-assets of
        /// the actions asset (InputActionReference.Create would make in-memory objects that do not survive saving).
        /// </summary>
        public static bool BindPersistent(InputSystemUIInputModule module)
        {
            var asset = UnityEngine.InputSystem.InputSystem.actions;
            var path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
            if (string.IsNullOrEmpty(path)) return false;
            InputActionReference Find(string action)
            {
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (o is InputActionReference r && r.action != null && r.action.name == action &&
                        r.action.actionMap != null && r.action.actionMap.name == "UI")
                        return r;
                }
                return null;
            }
            var point = Find("Point");
            var click = Find("Click");
            var navigate = Find("Navigate");
            var submit = Find("Submit");
            var cancel = Find("Cancel");
            if (point == null || click == null || navigate == null || submit == null || cancel == null) return false;
            module.actionsAsset = asset;
            module.point = point;
            module.leftClick = click;
            module.move = navigate;
            module.submit = submit;
            module.cancel = cancel;
            var scroll = Find("ScrollWheel");
            if (scroll != null) module.scrollWheel = scroll;
            var right = Find("RightClick");
            if (right != null) module.rightClick = right;
            var middle = Find("MiddleClick");
            if (middle != null) module.middleClick = middle;
            EditorUtility.SetDirty(module);
            return true;
        }

        private static Material GateGlow(TMP_FontAsset font)
        {
            BuilderUtil.EnsureFolder("Assets/_Game/UI/Materials");
            var m = AssetDatabase.LoadAssetAtPath<Material>(GateGlowPath);
            if (m == null)
            {
                m = new Material(font.material) { name = "M_UI_ClickGateGlow" };
                AssetDatabase.CreateAsset(m, GateGlowPath);
            }
            else
            {
                m.shader = font.material.shader;
                m.CopyPropertiesFromMaterial(font.material);
            }
            UIFonts.SetUnderlay(m, UIColors.WithAlpha(UIColors.Phosphor, 0.32f), 0f, 0f, 0.35f, 0.9f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
