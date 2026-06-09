using Timer = System.Windows.Forms.Timer;

namespace Echo
{
    public partial class GameForm : Form
    {
        private Game game;
        private Timer gameTimer;
        private DateTime lastFrameTime;
        private bool isFullscreen;
        private Rectangle windowedBounds;
        private FormBorderStyle windowedBorderStyle;
        private FormWindowState windowedState;

        public GameForm()
        {
            KeyPreview = true;
            InitializeComponent();
            DoubleBuffered = true;
            game = new Game();
            menu = new MenuManager();
            enemyPreviousFramePositions = new List<Vec2>();
            menuAnimationTime = 0f;
            levelCellRays = new List<List<Ray>>();
            levelCellRandom = new Random();
            pauseAnimTime = 0f;
            pauseTransitionProgress = 0f;
            pauseSelectedIndex = 0;

            InitializeMenuScene();

            KeyDown += GameForm_KeyDown;
            KeyUp += GameForm_KeyUp;
            gameTimer = new Timer();
            gameTimer.Interval = 16;
            gameTimer.Tick += GameTimer_Tick;
            gameTimer.Start();
        }

        private void GameForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F11)
            {
                ToggleFullscreen();
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

            game.Input.OnKeyDown(e.KeyCode);
        }

        private void GameForm_KeyUp(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F11)
            {
                e.Handled = true;
                return;
            }

            game.Input.OnKeyUp(e.KeyCode);
        }

        private void ToggleFullscreen()
        {
            if (!isFullscreen)
            {
                windowedState = WindowState;
                windowedBorderStyle = FormBorderStyle;
                windowedBounds = windowedState == FormWindowState.Normal ? Bounds : RestoreBounds;

                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Normal;
                Bounds = Screen.FromControl(this).Bounds;
                isFullscreen = true;
                return;
            }

            FormBorderStyle = windowedBorderStyle;
            WindowState = FormWindowState.Normal;
            Bounds = windowedBounds;
            WindowState = windowedState;
            isFullscreen = false;
        }

        private void GameTimer_Tick(object? sender, EventArgs e)
        {
            var currentTime = DateTime.Now;
            var elapsed = (float)(currentTime - lastFrameTime).TotalSeconds;
            lastFrameTime = currentTime;

            if (elapsed > 0.1f) elapsed = 0.016f;

            if (game.CurrentState == GameState.Menu)
            {
                menuAnimationTime += elapsed;
                enemyVisualTime += elapsed * MenuBackgroundAnimationSpeedMultiplier; // Для анимации врагов в меню
                menu.Update(game.Input, elapsed);

                var action = menu.GetSelectedAction(game.Input);
                if (action == MenuAction.StartGame)
                {
                    game.StartGame();
                }
                else if (action == MenuAction.SelectLevel)
                {
                    levelSelectIndex = 1; // По умолчанию подсвечен 1-й уровень, не катсцена.
                    levelSelectAnimTime = 0f;
                    levelSelectTransitionProgress = 0f;
                    levelSelectTransitionIn = true;
                    game.OpenLevelSelect();
                }
                else if (action == MenuAction.ExitGame)
                {
                    Application.Exit();
                }

                UpdateMenuScene(elapsed);
                game.Input.Update();
            }
            else if (game.CurrentState == GameState.Cutscene)
            {
                if (game.Input.IsKeyJustPressed(Keys.Escape))
                {
                    game.Sound.StopCutsceneType();
                    game.SkipCutscene();
                }
                else
                {
                    game.Cutscene.Update(game.Input, elapsed);

                    game.Cutscene.ConsumeNewlyRevealedCount();

                    // Зацикленный звук печати: играет пока текст печатается.
                    if (game.Cutscene.IsFullyRevealed)
                    {
                        game.Sound.StopCutsceneType();
                    }
                    else
                    {
                        game.Sound.StartCutsceneType();
                    }

                    if (game.Cutscene.IsFinished)
                    {
                        game.Sound.StopCutsceneType();
                        game.FinishCutscene();
                    }
                }
                game.Input.Update();
            }
            else if (game.CurrentState == GameState.LevelSelect)
            {
                menuAnimationTime += elapsed;
                enemyVisualTime += elapsed * MenuBackgroundAnimationSpeedMultiplier;
                levelSelectAnimTime += elapsed;

                if (levelSelectTransitionIn && levelSelectTransitionProgress < 1f)
                {
                    levelSelectTransitionProgress = MathF.Min(1f, levelSelectTransitionProgress + elapsed * 3.5f);
                }

                UpdateLevelCellRays(elapsed);

                var maxUnlockedIndex = game.IsAllLevelsUnlocked
                    ? game.TotalLevelCount
                    : game.MaxUnlockedLevel;
                // Ячейка 0 — катсцена-интро (всегда),
                // 1..N — уровни,
                // N+1 — финальная катсцена (всегда).
                var endingIndex = game.TotalLevelCount + 1;
                var maxSelectableIndex = Math.Max(endingIndex, Math.Clamp(maxUnlockedIndex, 0, game.TotalLevelCount));
                if (game.IsAllLevelsUnlocked) maxSelectableIndex = endingIndex;
                if (levelSelectIndex > maxSelectableIndex) levelSelectIndex = maxSelectableIndex;

                if (game.Input.IsKeyJustPressed(Keys.A) || game.Input.IsKeyJustPressed(Keys.Left))
                {
                    if (levelSelectIndex > 0)
                        levelSelectIndex--;
                }
                if (game.Input.IsKeyJustPressed(Keys.D) || game.Input.IsKeyJustPressed(Keys.Right))
                {
                    if (levelSelectIndex < maxSelectableIndex)
                        levelSelectIndex++;
                }
                if (game.Input.IsKeyJustPressed(Keys.W) || game.Input.IsKeyJustPressed(Keys.Up))
                {
                    var target = levelSelectIndex - 3;
                    if (target >= 0)
                        levelSelectIndex = target;
                }
                if (game.Input.IsKeyJustPressed(Keys.S) || game.Input.IsKeyJustPressed(Keys.Down))
                {
                    var target = levelSelectIndex + 3;
                    if (target <= maxSelectableIndex)
                        levelSelectIndex = target;
                }

                if (game.Input.IsKeyJustPressed(Keys.Space) || game.Input.IsKeyJustPressed(Keys.Enter))
                {
                    var isLockedCell = levelSelectIndex != 0 && !game.IsAllLevelsUnlocked && levelSelectIndex > game.MaxUnlockedLevel;
                    if (!isLockedCell)
                    {
                        levelCellRays.Clear();
                        if (levelSelectIndex == 0)
                        {
                            game.PlayIntroCutscene();
                        }
                        else if (levelSelectIndex == game.TotalLevelCount + 1)
                        {
                            game.PlayEndingCutscene();
                        }
                        else
                        {
                            game.StartLevel(levelSelectIndex);
                        }
                    }
                }
                if (game.Input.IsKeyJustPressed(Keys.Escape))
                {
                    levelCellRays.Clear();
                    game.ReturnToMenu();
                }

                UpdateMenuScene(elapsed);
                game.Input.Update();
            }
            else if (game.CurrentState == GameState.Playing)
            {
                if (game.Input.IsKeyJustPressed(Keys.Escape))
                {
                    pauseAnimTime = 0f;
                    pauseTransitionProgress = 0f;
                    pauseSelectedIndex = 0;
                    game.PauseGame();
                    game.Input.Update();
                }
                else
                {
                    enemyVisualTime += elapsed;
                    game.Update(elapsed);
                }
            }
            else if (game.CurrentState == GameState.Paused)
            {
                pauseAnimTime += elapsed;
                pauseTransitionProgress = MathF.Min(1f, pauseTransitionProgress + elapsed * 4f);

                if (game.Input.IsKeyJustPressed(Keys.W) || game.Input.IsKeyJustPressed(Keys.Up))
                {
                    pauseSelectedIndex--;
                    if (pauseSelectedIndex < 0) pauseSelectedIndex = 2;
                }
                if (game.Input.IsKeyJustPressed(Keys.S) || game.Input.IsKeyJustPressed(Keys.Down))
                {
                    pauseSelectedIndex++;
                    if (pauseSelectedIndex > 2) pauseSelectedIndex = 0;
                }

                if (game.Input.IsKeyJustPressed(Keys.Escape))
                {
                    game.ResumeGame();
                }
                else if (game.Input.IsKeyJustPressed(Keys.Space) || game.Input.IsKeyJustPressed(Keys.Enter))
                {
                    if (pauseSelectedIndex == 0) // RESUME
                    {
                        game.ResumeGame();
                    }
                    else if (pauseSelectedIndex == 1) // LEVELS
                    {
                        levelSelectIndex = game.CurrentLevelNumber;
                        levelSelectAnimTime = 0f;
                        levelSelectTransitionProgress = 0f;
                        levelSelectTransitionIn = true;
                        levelCellRays.Clear();
                        game.OpenLevelSelect();
                    }
                    else if (pauseSelectedIndex == 2) // MENU
                    {
                        game.ReturnToMenu();
                    }
                }

                game.Input.Update();
            }

            Invalidate();
        }
    }
}
