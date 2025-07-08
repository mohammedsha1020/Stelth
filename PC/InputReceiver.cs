using WindowsInput;
using WindowsInput.Native;
using Stealth.Shared;
using System.Runtime.InteropServices;

namespace Stealth.PC
{
    /// <summary>
    /// Handles receiving and simulating input events from remote devices
    /// </summary>
    public class InputReceiver
    {
        private readonly InputSimulator _inputSimulator;

        public InputReceiver()
        {
            _inputSimulator = new InputSimulator();
        }

        /// <summary>
        /// Processes an input event message from a remote device
        /// </summary>
        public void ProcessInputEvent(InputEventMessage inputEvent)
        {
            try
            {
                switch (inputEvent.Type)
                {
                    case Protocol.InputType.MouseMove:
                        SimulateMouseMove(inputEvent.X, inputEvent.Y);
                        break;
                    case Protocol.InputType.MouseClick:
                        SimulateMouseClick(inputEvent.Button, inputEvent.X, inputEvent.Y);
                        break;
                    case Protocol.InputType.MouseScroll:
                        SimulateMouseScroll((int)inputEvent.Y);
                        break;
                    case Protocol.InputType.KeyPress:
                        SimulateKeyPress(inputEvent.Button, inputEvent.KeyChar);
                        break;
                    default:
                        Console.WriteLine($"Unsupported input type: {inputEvent.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing input event: {ex.Message}");
            }
        }

        private void SimulateMouseMove(float x, float y)
        {
            // Convert relative coordinates to absolute screen coordinates
            var screenBounds = GetScreenBounds();
            var absoluteX = (int)(x * 65535 / screenBounds.Width);
            var absoluteY = (int)(y * 65535 / screenBounds.Height);
            
            _inputSimulator.Mouse.MoveMouseTo(absoluteX, absoluteY);
        }

        private void SimulateMouseClick(int button, float x, float y)
        {
            // Move to position first
            SimulateMouseMove(x, y);
            
            // Simulate click based on button
            switch (button)
            {
                case 0: // Left click
                    _inputSimulator.Mouse.LeftButtonClick();
                    break;
                case 1: // Right click
                    _inputSimulator.Mouse.RightButtonClick();
                    break;
                case 2: // Middle click - fallback to left click since InputSimulator doesn't support middle click
                    _inputSimulator.Mouse.LeftButtonClick();
                    break;
                default:
                    _inputSimulator.Mouse.LeftButtonClick();
                    break;
            }
        }

        private void SimulateMouseScroll(int scrollDelta)
        {
            if (scrollDelta > 0)
            {
                _inputSimulator.Mouse.VerticalScroll(scrollDelta);
            }
            else if (scrollDelta < 0)
            {
                _inputSimulator.Mouse.VerticalScroll(scrollDelta);
            }
        }

        private void SimulateKeyPress(int keyCode, string? keyChar)
        {
            if (!string.IsNullOrEmpty(keyChar))
            {
                // Type the character
                _inputSimulator.Keyboard.TextEntry(keyChar);
            }
            else if (TryConvertToVirtualKeyCode(keyCode, out var virtualKey))
            {
                // Press the key
                _inputSimulator.Keyboard.KeyPress(virtualKey);
            }
        }

        private static bool TryConvertToVirtualKeyCode(int keyCode, out VirtualKeyCode virtualKey)
        {
            virtualKey = VirtualKeyCode.SPACE;
            
            // Map common key codes to VirtualKeyCode
            switch (keyCode)
            {
                case 8: virtualKey = VirtualKeyCode.BACK; return true;
                case 9: virtualKey = VirtualKeyCode.TAB; return true;
                case 13: virtualKey = VirtualKeyCode.RETURN; return true;
                case 16: virtualKey = VirtualKeyCode.SHIFT; return true;
                case 17: virtualKey = VirtualKeyCode.CONTROL; return true;
                case 18: virtualKey = VirtualKeyCode.MENU; return true; // Alt
                case 20: virtualKey = VirtualKeyCode.CAPITAL; return true; // Caps Lock
                case 27: virtualKey = VirtualKeyCode.ESCAPE; return true;
                case 32: virtualKey = VirtualKeyCode.SPACE; return true;
                case 37: virtualKey = VirtualKeyCode.LEFT; return true;
                case 38: virtualKey = VirtualKeyCode.UP; return true;
                case 39: virtualKey = VirtualKeyCode.RIGHT; return true;
                case 40: virtualKey = VirtualKeyCode.DOWN; return true;
                case 46: virtualKey = VirtualKeyCode.DELETE; return true;
                
                // Function keys
                case 112: virtualKey = VirtualKeyCode.F1; return true;
                case 113: virtualKey = VirtualKeyCode.F2; return true;
                case 114: virtualKey = VirtualKeyCode.F3; return true;
                case 115: virtualKey = VirtualKeyCode.F4; return true;
                case 116: virtualKey = VirtualKeyCode.F5; return true;
                case 117: virtualKey = VirtualKeyCode.F6; return true;
                case 118: virtualKey = VirtualKeyCode.F7; return true;
                case 119: virtualKey = VirtualKeyCode.F8; return true;
                case 120: virtualKey = VirtualKeyCode.F9; return true;
                case 121: virtualKey = VirtualKeyCode.F10; return true;
                case 122: virtualKey = VirtualKeyCode.F11; return true;
                case 123: virtualKey = VirtualKeyCode.F12; return true;
                
                // Number keys
                case 48: virtualKey = VirtualKeyCode.VK_0; return true;
                case 49: virtualKey = VirtualKeyCode.VK_1; return true;
                case 50: virtualKey = VirtualKeyCode.VK_2; return true;
                case 51: virtualKey = VirtualKeyCode.VK_3; return true;
                case 52: virtualKey = VirtualKeyCode.VK_4; return true;
                case 53: virtualKey = VirtualKeyCode.VK_5; return true;
                case 54: virtualKey = VirtualKeyCode.VK_6; return true;
                case 55: virtualKey = VirtualKeyCode.VK_7; return true;
                case 56: virtualKey = VirtualKeyCode.VK_8; return true;
                case 57: virtualKey = VirtualKeyCode.VK_9; return true;
                
                // Letter keys (A-Z)
                case int k when k >= 65 && k <= 90:
                    virtualKey = (VirtualKeyCode)k;
                    return true;
                
                default:
                    return false;
            }
        }

        private static System.Drawing.Rectangle GetScreenBounds()
        {
            var left = int.MaxValue;
            var top = int.MaxValue;
            var right = int.MinValue;
            var bottom = int.MinValue;

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                left = Math.Min(left, screen.Bounds.Left);
                top = Math.Min(top, screen.Bounds.Top);
                right = Math.Max(right, screen.Bounds.Right);
                bottom = Math.Max(bottom, screen.Bounds.Bottom);
            }

            return new System.Drawing.Rectangle(left, top, right - left, bottom - top);
        }

        /// <summary>
        /// Simulates a keyboard combination (e.g., Ctrl+C)
        /// </summary>
        public void SimulateKeyboardShortcut(params VirtualKeyCode[] keys)
        {
            try
            {
                if (keys.Length == 1)
                {
                    _inputSimulator.Keyboard.KeyPress(keys[0]);
                }
                else if (keys.Length == 2)
                {
                    _inputSimulator.Keyboard.ModifiedKeyStroke(keys[0], keys[1]);
                }
                else if (keys.Length > 2)
                {
                    _inputSimulator.Keyboard.ModifiedKeyStroke(keys.Take(keys.Length - 1), keys.Last());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error simulating keyboard shortcut: {ex.Message}");
            }
        }

        /// <summary>
        /// Types a string of text
        /// </summary>
        public void TypeText(string text)
        {
            try
            {
                _inputSimulator.Keyboard.TextEntry(text);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error typing text: {ex.Message}");
            }
        }
    }
}
