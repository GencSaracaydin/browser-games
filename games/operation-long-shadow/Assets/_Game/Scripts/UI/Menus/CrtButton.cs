using System;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LongShadow.UI.Menus
{
    /// <summary>
    /// Terminal-style menu button: phosphor text; selected or hovered it turns to inverse video (a phosphor block with
    /// dark text) with a leading caret. Mouse hover selects it, so keyboard and mouse share one highlight. Click or
    /// Submit raises <see cref="Clicked"/> (inside the browser gesture); Cancel raises <see cref="Cancelled"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrtButton : Selectable, IPointerClickHandler, ISubmitHandler, ICancelHandler
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Image _block;
        [SerializeField] private TextMeshProUGUI _caret;

        private UIAudio _audio;
        private bool _highlighted;

        /// <summary>Raised on click or submit.</summary>
        public event Action Clicked;

        /// <summary>Raised on cancel (Esc) while selected.</summary>
        public event Action Cancelled;

        /// <summary>Raised when the highlight changes (rows tint their labels).</summary>
        public event Action<bool> HighlightChanged;

        /// <summary>The label.</summary>
        public TextMeshProUGUI Label => _label;

        /// <summary>True while lit (selected, hovered or pressed).</summary>
        public bool IsLit => _highlighted;

        /// <summary>Builds a button under a parent.</summary>
        public static CrtButton Create(string name, Transform parent, FontSet fonts, UITheme theme, string text, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, UIAudio audio, float fontSize = MenuStyle.ItemSize)
        {
            var rt = UIFactory.Rect(name, parent, anchor, pivot, position, size);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f); // raycast target
            hit.raycastTarget = true;
            var block = UIFactory.StretchImage("Block", rt, UISprites.Get(UISpriteId.Square, theme), MenuStyle.Block);
            block.enabled = false;
            var label = UIFactory.Text("Label", rt, fonts, MenuStyle.Font, fontSize, MenuStyle.Text, TMPro.TextAlignmentOptions.MidlineLeft,
                UIFactory.MiddleLeft, UIFactory.MiddleLeft, new Vector2(30f, 0f), new Vector2(size.x - 36f, size.y), false, MenuStyle.Glow);
            label.SetText(text);
            var caret = UIFactory.Text("Caret", rt, fonts, MenuStyle.Font, fontSize, MenuStyle.TextInverse, TMPro.TextAlignmentOptions.MidlineLeft,
                UIFactory.MiddleLeft, UIFactory.MiddleLeft, new Vector2(8f, 0f), new Vector2(24f, size.y), false);
            caret.SetText(">");
            caret.enabled = false;
            var b = rt.gameObject.AddComponent<CrtButton>();
            b._label = label;
            b._block = block;
            b._caret = caret;
            b._audio = audio;
            b.targetGraphic = hit;
            b.transition = Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            return b;
        }

        /// <summary>Sets the label text.</summary>
        public void SetText(string text)
        {
            if (_label != null) _label.SetText(text);
        }

        /// <summary>Raises <see cref="Clicked"/> as if pressed.</summary>
        public void Press()
        {
            if (!IsActive() || !IsInteractable()) return;
            _audio?.Play(UiCues.MenuSelect, 0.8f);
            Clicked?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Press();
        }

        public void OnSubmit(BaseEventData eventData) => Press();

        public void OnCancel(BaseEventData eventData) => Cancelled?.Invoke();

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (IsInteractable() && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject) Select();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            if (!MenuSelection.Quiet) _audio?.Play(UiCues.MenuMove, 0.5f);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            var hi = state == SelectionState.Highlighted || state == SelectionState.Selected || state == SelectionState.Pressed;
            if (_block != null) _block.enabled = hi;
            if (_caret != null) _caret.enabled = hi;
            if (_label != null)
                _label.color = state == SelectionState.Disabled ? MenuStyle.Disabled : hi ? MenuStyle.TextInverse : MenuStyle.Text;
            if (hi != _highlighted)
            {
                _highlighted = hi;
                HighlightChanged?.Invoke(hi);
            }
        }
    }
}
