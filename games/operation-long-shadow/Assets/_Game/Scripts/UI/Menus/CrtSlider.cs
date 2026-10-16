using System;
using LongShadow.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LongShadow.UI.Menus
{
    /// <summary>
    /// Terminal slider: a bar of 20 phosphor cells and a numeric readout. Left/right (keys or pad) step it; click or drag
    /// on the bar sets it. Values snap to <see cref="Step"/>. <see cref="ValueChanged"/> fires on user changes only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrtSlider : Selectable, IDragHandler, IInitializePotentialDragHandler, ICancelHandler
    {
        private const int Cells = 20;

        [SerializeField] private RectTransform _track;
        [SerializeField] private Image[] _cells;
        [SerializeField] private TextMeshProUGUI _readout;

        private float _min, _max = 1f, _step = 0.1f, _value;
        private Action<float, TextBuffer> _format;
        private readonly TextBuffer _buf = new TextBuffer(24);
        private UIAudio _audio;
        private int _filled = -1;
        private bool _highlighted;

        /// <summary>Raised when the user changes the value.</summary>
        public event Action<float> ValueChanged;

        /// <summary>Raised on cancel (Esc) while selected.</summary>
        public event Action Cancelled;

        /// <summary>Raised when the highlight changes.</summary>
        public event Action<bool> HighlightChanged;

        /// <summary>Current value.</summary>
        public float Value => _value;

        /// <summary>Snap step.</summary>
        public float Step => _step;

        /// <summary>Builds a slider control (bar + readout) under a parent.</summary>
        public static CrtSlider Create(string name, Transform parent, FontSet fonts, UITheme theme, Vector2 anchor, Vector2 pivot, Vector2 position,
            Vector2 size, UIAudio audio)
        {
            var rt = UIFactory.Rect(name, parent, anchor, pivot, position, size);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var readoutWidth = 120f;
            var track = UIFactory.Rect("Track", rt, UIFactory.MiddleLeft, UIFactory.MiddleLeft, Vector2.zero, new Vector2(size.x - readoutWidth - 12f, size.y * 0.5f));
            var square = UISprites.Get(UISpriteId.Square, theme);
            var cells = new Image[Cells];
            var cellWidth = track.sizeDelta.x / Cells;
            for (var i = 0; i < Cells; i++)
            {
                cells[i] = UIFactory.Image("Cell" + i, track, square, UIColors.PhosphorDim, UIFactory.MiddleLeft, UIFactory.MiddleLeft,
                    new Vector2(i * cellWidth + 1f, 0f), new Vector2(cellWidth - 3f, track.sizeDelta.y));
            }
            var readout = UIFactory.Text("Readout", rt, fonts, MenuStyle.Font, MenuStyle.RowSize, MenuStyle.Text, TextAlignmentOptions.MidlineRight,
                UIFactory.MiddleRight, UIFactory.MiddleRight, Vector2.zero, new Vector2(readoutWidth, size.y), false, MenuStyle.Glow);
            var s = rt.gameObject.AddComponent<CrtSlider>();
            s._track = track;
            s._cells = cells;
            s._readout = readout;
            s._audio = audio;
            s.targetGraphic = hit;
            s.transition = Transition.None;
            s.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            return s;
        }

        /// <summary>Sets range, step and readout format.</summary>
        public void Configure(float min, float max, float step, Action<float, TextBuffer> format)
        {
            _min = min;
            _max = Mathf.Max(min + 1e-4f, max);
            _step = Mathf.Max(0f, step);
            _format = format;
            Refresh(true);
        }

        /// <summary>Sets the value without raising <see cref="ValueChanged"/>.</summary>
        public void SetValueWithoutNotify(float value)
        {
            _value = Snap(value);
            Refresh(true);
        }

        /// <summary>Steps by a number of steps (keyboard); raises <see cref="ValueChanged"/> if it changed.</summary>
        public void StepBy(int steps)
        {
            var step = _step > 0f ? _step : (_max - _min) / Cells;
            SetFromUser(_value + steps * step);
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

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (eventData.button == PointerEventData.InputButton.Left) SetFromPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) SetFromPointer(eventData);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

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
            if (hi != _highlighted)
            {
                _highlighted = hi;
                HighlightChanged?.Invoke(hi);
                Refresh(true);
            }
        }

        private void SetFromPointer(PointerEventData e)
        {
            if (_track == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_track, e.position, e.pressEventCamera, out var local)) return;
            var r = _track.rect;
            var t = Mathf.Clamp01((local.x - r.xMin) / Mathf.Max(1f, r.width));
            SetFromUser(Mathf.Lerp(_min, _max, t));
        }

        private void SetFromUser(float value)
        {
            var v = Snap(value);
            if (Mathf.Approximately(v, _value)) return;
            _value = v;
            Refresh(false);
            _audio?.Play(UiCues.MenuMove, 0.35f);
            ValueChanged?.Invoke(_value);
        }

        private float Snap(float v)
        {
            v = Mathf.Clamp(v, _min, _max);
            if (_step > 0f) v = Mathf.Clamp(_min + Mathf.Round((v - _min) / _step) * _step, _min, _max);
            return v;
        }

        private void Refresh(bool force)
        {
            if (_cells == null) return;
            var filled = Mathf.RoundToInt(Mathf.InverseLerp(_min, _max, _value) * Cells);
            if (force || filled != _filled)
            {
                _filled = filled;
                var on = _highlighted ? UIColors.PhosphorBright : UIColors.Phosphor;
                var off = UIColors.WithAlpha(UIColors.PhosphorDim, _highlighted ? 1f : 0.75f);
                for (var i = 0; i < _cells.Length; i++) _cells[i].color = i < filled ? on : off;
            }
            if (_readout != null)
            {
                _buf.Clear();
                if (_format != null) _format(_value, _buf);
                else _buf.Append(_value, 1);
                _buf.ApplyTo(_readout);
                _readout.color = _highlighted ? UIColors.PhosphorBright : MenuStyle.Text;
            }
        }
    }
}
