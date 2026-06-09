using System;

namespace Echo
{
    public enum MenuAction
    {
        None,
        StartGame,
        SelectLevel,
        ExitGame
    }

    public class MenuManager
    {
        private string[] menuItems;
        private int selectedIndex;
        private float pulseTime;
        private const float PulseSpeed = 3f;

        public int SelectedIndex => selectedIndex;
        public string[] MenuItems => menuItems;

        public MenuManager()
        {
            menuItems = new[] { "START", "LEVELS", "EXIT" };
            selectedIndex = 0;
            pulseTime = 0f;
        }

        public void Update(InputManager input, float dt)
        {
            pulseTime += dt;

            if (input.IsKeyJustPressed(Keys.W) || input.IsKeyJustPressed(Keys.Up))
            {
                selectedIndex--;
                if (selectedIndex < 0) selectedIndex = menuItems.Length - 1;
            }

            if (input.IsKeyJustPressed(Keys.S) || input.IsKeyJustPressed(Keys.Down))
            {
                selectedIndex++;
                if (selectedIndex >= menuItems.Length) selectedIndex = 0;
            }
        }

        public MenuAction GetSelectedAction(InputManager input)
        {
            if (input.IsKeyJustPressed(Keys.Space) || input.IsKeyJustPressed(Keys.Enter))
            {
                return selectedIndex switch
                {
                    0 => MenuAction.StartGame,
                    1 => MenuAction.SelectLevel,
                    2 => MenuAction.ExitGame,
                    _ => MenuAction.None
                };
            }

            return MenuAction.None;
        }

        public float GetPulseAlpha()
        {
            var pulse = MathF.Sin(pulseTime * PulseSpeed);
            return 0.5f + (pulse * 0.5f);
        }
    }
}
