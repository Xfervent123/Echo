using System;
using System.Collections.Generic;
using System.Text;

namespace Echo
{
    public class InputManager
    {
        // Хранит кнопки, которые зажаты прямо сейчас (для WASD)
        private HashSet<Keys> heldKeys;
        
        // Хранит кнопки, которые были нажаты только в текущем кадре (для одиночного пробела)
        private HashSet<Keys> justPressedKeys;

        public InputManager()
        {
            heldKeys = new HashSet<Keys>();
            justPressedKeys = new HashSet<Keys>();
        }

        public void OnKeyDown(Keys key)
        {
            if (!heldKeys.Contains(key))
            {
                heldKeys.Add(key);
                justPressedKeys.Add(key);
            }
        }

        public void OnKeyUp(Keys key)
        {
            if (heldKeys.Contains(key)) heldKeys.Remove(key);
        }

        public void Update()
        {
            justPressedKeys.Clear();
        }

        public bool IsKeyHeld(Keys key)
        {
            return heldKeys.Contains(key);
        }

        public bool IsKeyJustPressed(Keys key)
        {
            if (justPressedKeys.Contains(key))
            {
                return true;
            }

            return false;
        }

        public bool WasAnyKeyJustPressed()
        {
            return justPressedKeys.Count > 0;
        }
    }
}
