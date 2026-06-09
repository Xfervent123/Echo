using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Echo
{
    public partial class Game
    {
        public Maze Maze { get; private set; }
        public Player Player { get; private set; }
        public InputManager Input { get; private set; }
        public Vec2 CursorDirection { get; private set; }
        public SoundManager Sound { get; private set; }
        public GameState CurrentState { get; private set; }
        public CutsceneManager Cutscene { get; private set; }

        // Список всех лучей, которые сейчас летят по карте
        public List<Ray> ActiveRays { get; private set; }
        public List<Ray> FinishEchoRays { get; private set; }
        public List<Ray> KeyEchoRays { get; private set; }
        public List<Ray> DangerEchoRays { get; private set; }
        public List<Enemy> Enemies { get; private set; }
        public List<PatrolEnemy> PatrolEnemies { get; private set; }

        private float StepEchoTimer;
        private bool WasMoving;
        private Random objectiveEchoRandom;
        private string mapsFolderPath;
        private string currentMapPath;
        private int currentMapNumber;
        private int totalLevelCount;
        private int maxUnlockedLevel;
        private List<Point> remainingKeyCells;
        private List<RectangleF> remainingKeyRegions;
        private static readonly List<RectangleF> EmptyRegions = new List<RectangleF>();
        private float levelIntroTimer;
        private bool isLevelIntroAwaitingDismiss;
        private bool isTutorialAwaitingDismiss;
        private float tutorialTime;
        private float deathAnimationTimer;
        private Vec2 deathAnimationCenter;
        private Vec2 deathAnimationDirection;
        private bool isDeathAnimationActive;

        private const int RayStepDegrees = 20;
        private const int PulseDegress = 12;
        private const float PulseMaxDistance = 500f;
        private const float PulseSpeed = 350f;
        private const float StepEchoInterval = 0.6f; 
        private const float StepEchoMaxDistance = 300f;
        private const float StepEchoSpeed = 200f;
        private const string MapsFolderName = "Maps";
        private const int StartMapNumber = 1;
        private const bool UnlockAllLevelsWithoutProgress = true;
        private const float SneakSpeedMultiplier = 0.5f;

        private const int FinishEchoRayCount = 10; // Лучей на 1 клетку выхода O. Диапазон: целое >= 0.
        private const int KeyEchoRayCount = 10; // Лучей на 1 клетку ключа K. Диапазон: целое >= 0.
        private const int DangerEchoRayCount = 10; // Лучей на 1 клетку опасной зоны R. Диапазон: целое >= 0.
        private const float ObjectiveEchoSpeedMin = 70f; // Минимальная скорость objective-луча (px/s). Диапазон: > 0 и <= ObjectiveEchoSpeedMax.
        private const float ObjectiveEchoSpeedMax = 125f; // Максимальная скорость objective-луча (px/s). Диапазон: >= ObjectiveEchoSpeedMin.
        private const float ObjectiveEchoDistanceMin = 240f; // Минимальная длина objective-луча (px). Диапазон: > 0 и <= ObjectiveEchoDistanceMax.
        private const float ObjectiveEchoDistanceMax = 380f; // Максимальная длина objective-луча (px). Диапазон: >= ObjectiveEchoDistanceMin.
        private const int ObjectiveEchoMaxBounces = 7; // Лимит отражений objective-луча. Диапазон: целое >= 0.
        private const float ObjectiveEchoOverflowChance = 0.45f; // Шанс, что луч слегка выйдет за границу области. Диапазон: [0..1].
        private const float ObjectiveEchoOverflowMaxTileRatio = 0.1f; // Максимум вылета за границу как доля тайла. Диапазон: [0..1].
        private const float EnemyRayFollowHitRadius = 26f; // Радиус захвата луча врагом при обычном состоянии (px). Диапазон: > 0.
        private const float EnemyRayReacquireRadius = 52f; // Расширенный радиус перезахвата луча при преследовании (px). Диапазон: >= EnemyRayFollowHitRadius.
        private const float EnemyRayPlayerDistanceWeight = 0.2f; // Вес близости к игроку в score выбора луча. Диапазон: >= 0 (обычно 0..2).
        private const float EnemyRayAlignmentBonus = 24f; // Бонус score за направление луча к игроку. Диапазон: >= 0.
        private const float EnemyRayFollowSpeed = 90f; // Базовая скорость движения врага по лучу (px/s). Диапазон: > 0.
        private const float EnemyCollisionRadius = 28f; // Радиус коллизии врага со стенами (px). Диапазон: > 0.
        private const float PatrolEnemySpeed = 70f; // Скорость патрульного врага (px/s). Диапазон: > 0.
        private const float PatrolEnemyChaseSpeed = 90f; // Скорость преследования патрульного врага по лучу (px/s). Диапазон: > 0.
        private const float PatrolEnemyCollisionRadius = 24f; // Радиус коллизии патрульного со стенами (px). Диапазон: > 0.
        private const float LevelIntroDuration = 2.2f;
        private const float DeathAnimationDuration = 0.85f;

        public Game()
        {
            ActiveRays = new List<Ray>();
            FinishEchoRays = new List<Ray>();
            KeyEchoRays = new List<Ray>();
            DangerEchoRays = new List<Ray>();
            Enemies = new List<Enemy>();
            PatrolEnemies = new List<PatrolEnemy>();
            Input = new InputManager();
            Sound = new SoundManager();
            objectiveEchoRandom = new Random();
            remainingKeyCells = new List<Point>();
            remainingKeyRegions = new List<RectangleF>();
            levelIntroTimer = LevelIntroDuration;
            deathAnimationTimer = DeathAnimationDuration;
            deathAnimationCenter = Vec2.Zero();
            deathAnimationDirection = new Vec2(1, 0);
            isDeathAnimationActive = false;

            mapsFolderPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, MapsFolderName));
            totalLevelCount = CountAvailableLevels();
            currentMapNumber = StartMapNumber;
            currentMapPath = BuildMapPath(currentMapNumber);
            maxUnlockedLevel = StartMapNumber;

            CurrentState = GameState.Menu;
            Cutscene = new CutsceneManager();
            Sound.PlayMenuMusic();
        }

        public int CurrentLevelNumber => currentMapNumber;
        public int TotalLevelCount => totalLevelCount;
        public int MaxUnlockedLevel => maxUnlockedLevel;
        public bool IsAllLevelsUnlocked => UnlockAllLevelsWithoutProgress;
        public bool IsLevelIntroActive => CurrentState == GameState.Playing && isLevelIntroAwaitingDismiss;
        public float LevelIntroProgress => Math.Clamp(levelIntroTimer / LevelIntroDuration, 0f, 1f);
        public bool IsTutorialActive => CurrentState == GameState.Playing && !isLevelIntroAwaitingDismiss && isTutorialAwaitingDismiss;
        public float TutorialTime => tutorialTime;
        public bool IsDeathAnimationActive => CurrentState == GameState.Playing && isDeathAnimationActive;
        public float DeathAnimationProgress => Math.Clamp(deathAnimationTimer / DeathAnimationDuration, 0f, 1f);
        public Vec2 DeathAnimationCenter => deathAnimationCenter;
        public Vec2 DeathAnimationDirection => deathAnimationDirection;

        public void StartGame()
        {
            Sound.StopMenuMusic();

            // Если игрок уже проходил уровни и сейчас не на первом — продолжаем с текущего без катсцены.
            if (currentMapNumber > StartMapNumber && File.Exists(currentMapPath))
            {
                CurrentState = GameState.Playing;
                RestartCurrentMap();
                StartLevelIntro();
                Sound.PlayEntering();
                return;
            }

            // Свежий старт — показываем катсцену.
            Cutscene = new CutsceneManager(CutsceneKind.Intro);
            CurrentState = GameState.Cutscene;
        }

        public void PlayIntroCutscene()
        {
            Sound.StopMenuMusic();
            Cutscene = new CutsceneManager(CutsceneKind.Intro);
            CurrentState = GameState.Cutscene;
        }

        public void PlayEndingCutscene()
        {
            Sound.StopEnemySound();
            Sound.StopMenuMusic();
            Cutscene = new CutsceneManager(CutsceneKind.Ending);
            CurrentState = GameState.Cutscene;
        }

        public void FinishCutscene()
        {
            if (CurrentState != GameState.Cutscene) return;

            // Концовка — возвращаемся в меню. Прогресс при этом сбрасывается на 1-й уровень.
            if (Cutscene.Kind == CutsceneKind.Ending)
            {
                currentMapNumber = StartMapNumber;
                currentMapPath = BuildMapPath(currentMapNumber);
                ReturnToMenu();
                return;
            }

            // Интро-катсцена — стартуем 1-й уровень.
            CurrentState = GameState.Playing;
            currentMapNumber = StartMapNumber;
            currentMapPath = BuildMapPath(currentMapNumber);
            RestartCurrentMap();
            StartLevelIntro();
            Sound.PlayEntering();
        }

        public void SkipCutscene()
        {
            if (CurrentState != GameState.Cutscene) return;
            FinishCutscene();
        }

        public void StartLevel(int levelNumber)
        {
            if (!UnlockAllLevelsWithoutProgress && levelNumber > maxUnlockedLevel) return;
            var mapPath = BuildMapPath(levelNumber);
            if (!File.Exists(mapPath)) return;

            Sound.StopMenuMusic();
            currentMapNumber = levelNumber;
            currentMapPath = mapPath;
            CurrentState = GameState.Playing;
            RestartCurrentMap();
            StartLevelIntro();
            Sound.PlayEntering();
        }

        public void OpenLevelSelect()
        {
            CurrentState = GameState.LevelSelect;
        }

        public void PauseGame()
        {
            if (CurrentState != GameState.Playing) return;
            CurrentState = GameState.Paused;
            Sound.StopEnemySound();
            Sound.PlayMenuMusic();
        }

        public void ResumeGame()
        {
            if (CurrentState != GameState.Paused) return;
            Sound.StopMenuMusic();
            CurrentState = GameState.Playing;
        }

        public void ReturnToMenu()
        {
            CurrentState = GameState.Menu;
            Sound.StopEnemySound();
            Sound.PlayMenuMusic();
        }

        public void Update(float dt)
        {
            if (IsLevelIntroActive)
            {
                levelIntroTimer += dt;
                if (levelIntroTimer > LevelIntroDuration)
                {
                    levelIntroTimer = LevelIntroDuration;
                }

                if (Input.IsKeyJustPressed(Keys.Space))
                {
                    isLevelIntroAwaitingDismiss = false;
                }

                Input.Update();
                return;
            }

            if (IsTutorialActive)
            {
                tutorialTime += dt;
                if (Input.IsKeyJustPressed(Keys.Space) || Input.IsKeyJustPressed(Keys.Enter))
                {
                    isTutorialAwaitingDismiss = false;
                }

                Input.Update();
                return;
            }

            if (IsDeathAnimationActive)
            {
                deathAnimationTimer += dt;
                if (deathAnimationTimer >= DeathAnimationDuration)
                {
                    isDeathAnimationActive = false;
                    deathAnimationTimer = DeathAnimationDuration;
                    RestartCurrentMap();
                }

                Input.Update();
                return;
            }

            var moveDir = new Vec2(0, 0);
            if (Input.IsKeyHeld(Keys.W) || Input.IsKeyHeld(Keys.Up)) moveDir.Y -= 1;
            if (Input.IsKeyHeld(Keys.S) || Input.IsKeyHeld(Keys.Down)) moveDir.Y += 1;
            if (Input.IsKeyHeld(Keys.A) || Input.IsKeyHeld(Keys.Left)) moveDir.X -= 1;
            if (Input.IsKeyHeld(Keys.D) || Input.IsKeyHeld(Keys.Right)) moveDir.X += 1;

            if (moveDir.Length > 0)
            {
                CursorDirection = moveDir.Normalized();
            }

            var isSneakingInput = Input.IsKeyHeld(Keys.ShiftKey)
                || Input.IsKeyHeld(Keys.LShiftKey)
                || Input.IsKeyHeld(Keys.RShiftKey)
                || Input.IsKeyHeld(Keys.Shift);

            var isSneaking = isSneakingInput && Player.CanSneak();

            if (isSneaking)
            {
                Player.UseSneakStamina(dt);
            }

            var speedMultiplier = isSneaking ? SneakSpeedMultiplier : 1f;
            var hasMoved = Player.Move(moveDir, Maze, dt, speedMultiplier);
            TryCollectTouchedKeys();
            if (TryGoToNextMapThroughExit())
            {
                Input.Update();
                return;
            }

            if (IsPlayerInDangerZone())
            {
                StartDeathAnimation();
                Input.Update();
                return;
            }
            if (IsPlayerCaughtByEnemy())
            {
                StartDeathAnimation();
                Input.Update();
                return;
            }

            var canEmitStepPulse = hasMoved && !isSneaking;

            StepEchoTimer += dt;

            if (canEmitStepPulse && !WasMoving && StepEchoTimer >= StepEchoInterval)
            {
                SpawnStepEcho();
                StepEchoTimer = 0f;
            }

            if (canEmitStepPulse)
            {
                while (StepEchoTimer >= StepEchoInterval)
                {
                    SpawnStepEcho();
                    StepEchoTimer -= StepEchoInterval;
                }
            }

            WasMoving = canEmitStepPulse;

            Player.Update(dt);

            if (Input.IsKeyJustPressed(Keys.Space) && Player.CanPulse())
            {
                SpawnPulse();
            }

            for (var i = ActiveRays.Count - 1; i >= 0; i--)
            {
                var ray = ActiveRays[i];
                ray.Update(dt);

                if (!ray.IsAlive)
                {
                    ActiveRays.RemoveAt(i);
                }
            }

            UpdateEnemyRayFollowing(dt);
            UpdatePatrolEnemies(dt);
            if (IsPlayerCaughtByEnemy())
            {
                StartDeathAnimation();
                Input.Update();
                return;
            }
            UpdateObjectiveEchoes(dt);
            UpdateEnemySound();
            Input.Update();
        }

        private void SpawnPulse()
        {
            SpawnRadialRays(PulseMaxDistance, PulseSpeed, PulseDegress, GetCursorAngleDegrees());
            Player.UseEnergy();
            Sound.PlayFireSpace();
        }

        private void SpawnStepEcho()
        {
            SpawnRadialRays(StepEchoMaxDistance, StepEchoSpeed, RayStepDegrees, GetCursorAngleDegrees());
            Sound.PlayFireMove();
        }

        private float GetCursorAngleDegrees()
        {
            var dir = CursorDirection.Normalized();
            return MathF.Atan2(dir.Y, dir.X) * 180f / MathF.PI;
        }

        private void SpawnRadialRays(float maxDistance, float speed, int rayDegrees, float baseAngleDegrees)
        {
            for (var degrees = 0; degrees <= 360; degrees += rayDegrees)
            {
                var rayDirection = Vec2.FromAngleDeg(baseAngleDegrees + degrees);
                var newRay = Ray.Create(Player.Position, rayDirection, Maze, maxDist: maxDistance, speed: speed);
                ActiveRays.Add(newRay);
            }
        }
    }
}
