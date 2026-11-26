using System.Reflection;
using DriftPaint.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DriftPaint.Tests
{
    public sealed class ButtonStateColorsTests
    {
        GameObject _root;
        Image _fill;
        TextMeshProUGUI _label;
        Button _button;
        ButtonStateColors _colors;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Button", typeof(RectTransform));
            _fill = _root.AddComponent<Image>();
            _fill.color = DriftPaintPalette.Cyan;
            _button = _root.AddComponent<Button>();
            _button.targetGraphic = _fill;
            var text = new GameObject("Label", typeof(RectTransform));
            text.transform.SetParent(_root.transform, false);
            _label = text.AddComponent<TextMeshProUGUI>();
            _label.color = DriftPaintPalette.HudNavy;

            _colors = _root.AddComponent<ButtonStateColors>();
            var data = new SerializedObject(_colors);
            data.FindProperty("_fill").objectReferenceValue = _fill;
            SerializedProperty labels = data.FindProperty("_labels");
            labels.arraySize = 1;
            labels.GetArrayElementAtIndex(0).objectReferenceValue = _label;
            data.FindProperty("_fadeSeconds").floatValue = 0f;
            data.ApplyModifiedPropertiesWithoutUndo();
            Call("Awake");
            Call("OnEnable");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        void Call(string method) =>
            typeof(ButtonStateColors).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                     .Invoke(_colors, method == "Refresh" ? new object[] { true } : null);

        static PointerEventData Pointer() => new PointerEventData(null) { button = PointerEventData.InputButton.Left };

        [Test]
        public void HoverTurnsACyanButtonOrange()
        {
            _colors.OnPointerEnter(Pointer());
            Call("Refresh");

            Assert.That(_colors.Current, Is.EqualTo(ButtonStateColors.State.Highlighted));
            Assert.That(_fill.color, Is.EqualTo(DriftPaintPalette.Orange));
            Assert.That(_label.color, Is.EqualTo(DriftPaintPalette.HudNavy));
        }

        [Test]
        public void HoldingTheButtonShowsThePressedColor()
        {
            _colors.OnPointerEnter(Pointer());
            _colors.OnPointerDown(Pointer());
            Call("Refresh");

            Assert.That(_colors.Current, Is.EqualTo(ButtonStateColors.State.Pressed));
            Assert.That(_fill.color, Is.EqualTo(DriftPaintPalette.CyanBright));
        }

        [Test]
        public void LeavingReturnsToTheColorsFromTheScene()
        {
            _colors.OnPointerEnter(Pointer());
            Call("Refresh");
            _colors.OnPointerExit(Pointer());
            Call("Refresh");

            Assert.That(_colors.Current, Is.EqualTo(ButtonStateColors.State.Normal));
            Assert.That(_fill.color, Is.EqualTo(DriftPaintPalette.Cyan));
        }

        [Test]
        public void SelectionIsHighlightedWhileNavigating()
        {
            _colors.OnSelect(new BaseEventData(null));
            _colors.OnMove(new AxisEventData(null));
            Call("Refresh");

            Assert.That(_colors.Current, Is.EqualTo(ButtonStateColors.State.Highlighted));
        }

        [Test]
        public void DisabledButtonsKeepTheirLabelAndDimTheFill()
        {
            _button.interactable = false;
            _colors.OnPointerEnter(Pointer());
            Call("Refresh");

            Assert.That(_colors.Current, Is.EqualTo(ButtonStateColors.State.Disabled));
            Assert.That(_label.color, Is.EqualTo(DriftPaintPalette.HudNavy));
            Assert.That(_fill.color.a, Is.LessThan(1f));
        }
    }
}
