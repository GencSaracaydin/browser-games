using System;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LongShadow.UI.Menus
{
    /// <summary>
    /// Terminal choice selector <c>&lt;  VALUE  &gt;</c> for toggles and enumerations (ON/OFF, S/M/L, difficulty, render
    /// scale). Left/right step; a click on the left third steps back, elsewhere forward (wrapping).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrtStepper : Selectable, IPointerClickHandler, ISubmitHandler, ICancelHandler
    {
        [SerializeField] private TextMeshProUGUI _value;
        [SerializeField] private TextMeshProUGUI _left;
        [SerializeField] private TextMeshProUGUI _right;

        private string[] _choices = Array.Empty<string>();
        private int _index;
        private UIAudio _audio;
        private bool _highlighted;

        /// <summary>Raised when the user changes the choice (index).</summary>
        public event Action<int> ValueChanged;

        /// <summary>Raised on cancel (Esc) while selected.</summary>
        public event Action Cancelled;

        /// <summary>Raised when the highlight changes.</summary>
        public event Action<bool> HighlightChanged;

        /// <summary>Current index.</summary>
        public int Index => _index;

        /// <summary>Builds a stepper under a parent.</summary>
        public static CrtStepper Create(string name, Transform parent, FontSet fonts, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, UIAudio audio)
        {
            var rt = UIFactory.Rect(name, parent, anchor, pivot, position, size);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var left = UIFactory.Text("Left", rt, fonts, MenuStyle.Font, MenuStyle.RowSize, MenuStyle.Text, TextAlignmentOptions.MidlineLeft,
                UIFactory.MiddleLeft, UIFactory.MiddleLeft, Vector2.zero, new Vector2(30f, size.y), false, MenuStyle.Glow);
            left.SetText("<");
            var right = UIFactory.Text("Right", rt, fonts, MenuStyle.Font, MenuStyle.RowSize, MenuStyle.Text, TextAlignmentOptions.MidlineRight,
                UIFactory.MiddleRight, UIFactory.MiddleRight, Vector2.zero, new Vector2(30f, size.y), false, MenuStyle.Glow);
            right.SetText(">");
            var value = UIFactory.Text("Value", rt, fonts, MenuStyle.Font, MenuStyle.RowSize, MenuStyle.Text, TextAlignmentOptions.Midline,
                UIFactory.Center, UIFactory.Center, Vector2.zero, new Vector2(size.x - 60f, size.y), false, MenuStyle.Glow);
            var s = rt.gameObject.AddComponent<CrtStepper>();
            s._value = value;
            s._left = left;
            s._right = right;
            s._audio = audio;
            s.targetGraphic = hit;
            s.transition = Transition.None;
            s.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            return s;
        }

        /// <summary>Sets the choices.</summary>
        public void Configure(string[] choices)
        {
            _choices = choices ?? Array.Empty<string>();
            _index = Mathf.Clamp(_index, 0, Mathf.Max(0, _choices.Length - 1));
            Refresh();
        }

        /// <summary>Sets the index without raising <see cref="ValueChanged"/>.</summary>
        public void SetIndexWithoutNotify(int index)
        {
            _index = _choices.Length == 0 ? 0 : Mathf.Clamp(index, 0, _choices.Length - 1);
            Refresh();
        }

        /// <summary>Steps the choice (wrapping) and raises <see cref="ValueChanged"/>.</summary>
        public void StepBy(int delta)
        {
            if (_choices.Length == 0) return;
            _index = (_index + delta % _choices.Length + _choices.Length) % _choices.Length;
            Refresh();
            _audio?.Play(UiCues.MenuMove, 0.4f);
            ValueChanged?.Invoke(_index);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left)
            {
                StepBy(-1);
                eventData.Use();
                return;
            }
            if (eventData.moveDir == MoveDirection.Right)
            {
                StepBy(1);
                eventData.Use();
                return;
            }
            base.OnMove(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsInteractable()) return;
            var rt = (RectTransform)transform;
            var back = RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out var local) &&
                       local.x < rt.rect.xMin + rt.rect.width / 3f;
            StepBy(back ? -1 : 1);
        }

        public void OnSubmit(BaseEventData eventData) => StepBy(1);

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
            if (hi == _highlighted) return;
            _highlighted = hi;
            HighlightChanged?.Invoke(hi);
            Refresh();
        }

        private void Refresh()
        {
            if (_value == null) return;
            _value.SetText(_choices.Length > 0 ? _choices[_index] : string.Empty);
            var c = _highlighted ? UIColors.PhosphorBright : MenuStyle.Text;
            _value.color = c;
            _left.color = _right.color = _highlighted ? UIColors.PhosphorBright : UIColors.WithAlpha(MenuStyle.Text, 0.55f);
        }
    }
}
