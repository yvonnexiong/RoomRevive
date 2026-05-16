using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Thin shim that reads keyboard state through the new Input System while keeping
/// the legacy <see cref="KeyCode"/> API so existing inspector fields and call sites
/// don't have to change. Falls back to <see cref="Input"/> when the project is still
/// on the legacy input manager.
///
/// Usage: replace <c>Input.GetKeyDown(KeyCode.X)</c> with <c>KeyInput.GetKeyDown(KeyCode.X)</c>.
/// </summary>
public static class KeyInput
{
    public static bool GetKeyDown(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var control = GetControl(key);
        return control != null && control.wasPressedThisFrame;
#else
        return Input.GetKeyDown(key);
#endif
    }

    public static bool GetKey(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var control = GetControl(key);
        return control != null && control.isPressed;
#else
        return Input.GetKey(key);
#endif
    }

    public static bool GetKeyUp(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var control = GetControl(key);
        return control != null && control.wasReleasedThisFrame;
#else
        return Input.GetKeyUp(key);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    static readonly Dictionary<KeyCode, Key> _map = new Dictionary<KeyCode, Key>();

    static UnityEngine.InputSystem.Controls.KeyControl GetControl(KeyCode keyCode)
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return null;

        Key mapped = MapKeyCodeToKey(keyCode);
        if (mapped == Key.None) return null;

        return kb[mapped];
    }

    static Key MapKeyCodeToKey(KeyCode keyCode)
    {
        if (_map.TryGetValue(keyCode, out Key cached)) return cached;

        Key result = ComputeMapping(keyCode);
        _map[keyCode] = result;
        return result;
    }

    static Key ComputeMapping(KeyCode keyCode)
    {
        // Names that differ between KeyCode and Key.
        switch (keyCode)
        {
            case KeyCode.Return: return Key.Enter;
            case KeyCode.KeypadEnter: return Key.NumpadEnter;

            case KeyCode.Alpha0: return Key.Digit0;
            case KeyCode.Alpha1: return Key.Digit1;
            case KeyCode.Alpha2: return Key.Digit2;
            case KeyCode.Alpha3: return Key.Digit3;
            case KeyCode.Alpha4: return Key.Digit4;
            case KeyCode.Alpha5: return Key.Digit5;
            case KeyCode.Alpha6: return Key.Digit6;
            case KeyCode.Alpha7: return Key.Digit7;
            case KeyCode.Alpha8: return Key.Digit8;
            case KeyCode.Alpha9: return Key.Digit9;

            case KeyCode.Keypad0: return Key.Numpad0;
            case KeyCode.Keypad1: return Key.Numpad1;
            case KeyCode.Keypad2: return Key.Numpad2;
            case KeyCode.Keypad3: return Key.Numpad3;
            case KeyCode.Keypad4: return Key.Numpad4;
            case KeyCode.Keypad5: return Key.Numpad5;
            case KeyCode.Keypad6: return Key.Numpad6;
            case KeyCode.Keypad7: return Key.Numpad7;
            case KeyCode.Keypad8: return Key.Numpad8;
            case KeyCode.Keypad9: return Key.Numpad9;
            case KeyCode.KeypadPeriod: return Key.NumpadPeriod;
            case KeyCode.KeypadDivide: return Key.NumpadDivide;
            case KeyCode.KeypadMultiply: return Key.NumpadMultiply;
            case KeyCode.KeypadMinus: return Key.NumpadMinus;
            case KeyCode.KeypadPlus: return Key.NumpadPlus;
            case KeyCode.KeypadEquals: return Key.NumpadEquals;

            case KeyCode.LeftControl: return Key.LeftCtrl;
            case KeyCode.RightControl: return Key.RightCtrl;
            case KeyCode.LeftCommand: return Key.LeftMeta;
            case KeyCode.RightCommand: return Key.RightMeta;
            case KeyCode.LeftWindows: return Key.LeftMeta;
            case KeyCode.RightWindows: return Key.RightMeta;

            case KeyCode.Numlock: return Key.NumLock;
            case KeyCode.CapsLock: return Key.CapsLock;
            case KeyCode.ScrollLock: return Key.ScrollLock;

            case KeyCode.BackQuote: return Key.Backquote;
            case KeyCode.Quote: return Key.Quote;
            case KeyCode.Exclaim: return Key.Digit1;
            case KeyCode.Hash: return Key.Digit3;
            case KeyCode.Dollar: return Key.Digit4;
            case KeyCode.Percent: return Key.Digit5;
            case KeyCode.Ampersand: return Key.Digit7;
            case KeyCode.LeftParen: return Key.Digit9;
            case KeyCode.RightParen: return Key.Digit0;
            case KeyCode.Asterisk: return Key.Digit8;
            case KeyCode.Plus: return Key.Equals;
            case KeyCode.Colon: return Key.Semicolon;
            case KeyCode.Less: return Key.Comma;
            case KeyCode.Greater: return Key.Period;
            case KeyCode.Question: return Key.Slash;
            case KeyCode.At: return Key.Digit2;
            case KeyCode.Caret: return Key.Digit6;
            case KeyCode.Underscore: return Key.Minus;
        }

        // Same-name path covers letters A-Z, function keys, arrows, Space, Tab,
        // Escape, Backspace, Delete, Insert, Home, End, PageUp, PageDown, etc.
        if (System.Enum.TryParse(keyCode.ToString(), out Key parsed))
            return parsed;

        return Key.None;
    }
#endif
}
