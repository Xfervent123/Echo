using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Echo
{
    // Меню, фон меню и выбор уровней.
    public partial class GameForm
    {
        private MenuManager menu;
        private float menuAnimationTime;

        private Vec2 menuPlayerPos;
        private Vec2 menuPlayerDir;
        private List<Ray> menuRays;
        private List<Enemy> menuEnemies;
        private List<Vec2> menuEnemyPrevPos;
        private Vec2 menuKeyPos;
        private float menuPlayerSpeed;
        private List<Ray> menuKeyRays; // Лучи от ключа
        private Random menuRandom;
        private Size menuViewportSize;
        private bool isMenuSceneInitialized;
        private float menuStepEchoTimer;
        private bool menuWasMoving;
        private bool menuKeyCollected;
        private float menuSceneTime;
        private float menuDodgeCooldown;
        private float menuDodgeTimer;
        private float menuDodgeDirection;

        private int levelSelectIndex;
        private float levelSelectAnimTime;
        private float levelSelectTransitionProgress;
        private bool levelSelectTransitionIn;
        private List<List<Ray>> levelCellRays; // Лучи внутри каждой ячейки уровня
        private Random levelCellRandom;

        private const float MenuKeyHorizontalMargin = 170f;
        private const float MenuPlayerRadius = 24f;
        private const float MenuPlayerSpeed = 220f;
        private const float MenuPlayerResetMargin = 100f;
        private const float MenuEnemySpeed = 165f;
        private const float MenuStepEchoInterval = 0.85f;
        private const int MenuStepEchoDegrees = 20;
        private const float MenuStepEchoMaxDistance = 380f;
        private const float MenuStepEchoSpeed = 270f;
        private const float MenuKeyRadius = 52f;
        private const float MenuKeyPickupDistance = MenuPlayerRadius + (MenuKeyRadius * 0.45f);
        private const float MenuBackgroundAnimationSpeedMultiplier = 1.35f;
        private const float MenuBackgroundEnemyScale = 1.25f;
        private const float MenuBackgroundEnemyTrailSpacing = 11f;
        private const float MenuBackgroundPlayerRayWidth = 4.8f;
        private const float MenuBackgroundKeyRayWidth = 5f;
        private const float MenuPlayerVerticalLookahead = 92f;
        private const float MenuPlayerVerticalWaveA = 58f;
        private const float MenuPlayerVerticalWaveB = 22f;
        private const float MenuPlayerTurnResponsiveness = 8f;
        private const float MenuPlayerVerticalMargin = 90f;
        private const float MenuDodgeTriggerDistance = 210f;
        private const float MenuDodgeVerticalOffset = 130f;
        private const float MenuDodgeDuration = 0.40f;
        private const float MenuDodgeCooldownDuration = 1.20f;
        private const float MenuKeyApproachDistance = 230f;
        private const float MenuEnemyVerticalFollowStrength = 2.8f;

        private void InitializeMenuScene()
        {
            var screenWidth = ClientSize.Width > 0 ? ClientSize.Width : 800;
            var screenHeight = ClientSize.Height > 0 ? ClientSize.Height : 450;
            menuViewportSize = new Size(screenWidth, screenHeight);

            menuPlayerPos = new Vec2(100f, screenHeight * 0.5f);
            menuPlayerDir = new Vec2(1, 0);
            menuPlayerSpeed = MenuPlayerSpeed;

            menuKeyPos = new Vec2(screenWidth - MenuKeyHorizontalMargin, screenHeight * 0.5f);

            menuRays = new List<Ray>();
            menuKeyRays = new List<Ray>();
            menuEnemies = new List<Enemy>();
            menuEnemyPrevPos = new List<Vec2>();
            menuRandom = new Random();

            var enemy1Pos = new Vec2(menuPlayerPos.X - 300f, menuPlayerPos.Y);
            var enemy2Pos = new Vec2(menuPlayerPos.X - 200f, menuPlayerPos.Y + 50f);
            menuEnemies.Add(new Enemy(enemy1Pos));
            menuEnemies.Add(new Enemy(enemy2Pos));
            menuEnemyPrevPos.Add(enemy1Pos);
            menuEnemyPrevPos.Add(enemy2Pos);
            menuStepEchoTimer = MenuStepEchoInterval;
            menuWasMoving = false;
            menuKeyCollected = false;
            menuSceneTime = 0f;
            menuDodgeCooldown = 0f;
            menuDodgeTimer = 0f;
            menuDodgeDirection = 0f;
            isMenuSceneInitialized = true;
        }

        private void UpdateMenuScene(float dt)
        {
            var screenWidth = ClientSize.Width;
            var screenHeight = ClientSize.Height;
            RepositionMenuSceneToViewport(screenWidth, screenHeight, clearEchoRays: false);

            var previousPlayerPos = menuPlayerPos;
            menuSceneTime += dt;
            if (menuDodgeCooldown > 0f)
            {
                menuDodgeCooldown = MathF.Max(0f, menuDodgeCooldown - dt);
            }

            // Игрок движется по волнообразной траектории, совершая уклонения от ближайшего врага.
            var centerY = screenHeight * 0.5f;
            var waveOffset =
                (MathF.Sin(menuSceneTime * 1.15f) * MenuPlayerVerticalWaveA) +
                (MathF.Sin((menuSceneTime * 2.1f) + 0.8f) * MenuPlayerVerticalWaveB);

            if (menuDodgeTimer <= 0f && menuDodgeCooldown <= 0f)
            {
                var nearestThreatDistance = float.MaxValue;
                var threatY = menuPlayerPos.Y;

                for (var i = 0; i < menuEnemies.Count; i++)
                {
                    var enemyPos = menuEnemies[i].Position;
                    var distanceBehind = menuPlayerPos.X - enemyPos.X;
                    if (distanceBehind <= 0f || distanceBehind >= nearestThreatDistance) continue;

                    nearestThreatDistance = distanceBehind;
                    threatY = enemyPos.Y;
                }

                var sameLane = MathF.Abs(threatY - menuPlayerPos.Y) < 95f;
                if (nearestThreatDistance < MenuDodgeTriggerDistance && sameLane)
                {
                    menuDodgeTimer = MenuDodgeDuration;
                    menuDodgeCooldown = MenuDodgeCooldownDuration;

                    var awayFromThreat = menuPlayerPos.Y - threatY;
                    if (MathF.Abs(awayFromThreat) < 5f)
                    {
                        awayFromThreat = menuRandom.NextDouble() < 0.5 ? -1f : 1f;
                    }

                    menuDodgeDirection = MathF.Sign(awayFromThreat);
                    if (menuDodgeDirection == 0f) menuDodgeDirection = 1f;
                }
            }

            var dodgeOffset = 0f;
            if (menuDodgeTimer > 0f)
            {
                var dodgeProgress = 1f - (menuDodgeTimer / MenuDodgeDuration);
                dodgeOffset = MathF.Sin(dodgeProgress * MathF.PI) * MenuDodgeVerticalOffset * menuDodgeDirection;
                menuDodgeTimer = MathF.Max(0f, menuDodgeTimer - dt);
            }

            var desiredY = centerY + waveOffset + dodgeOffset;
            if (!menuKeyCollected && (menuKeyPos.X - menuPlayerPos.X) < MenuKeyApproachDistance)
            {
                desiredY += (menuKeyPos.Y - desiredY) * 0.72f;
            }

            var minY = MenuPlayerVerticalMargin;
            var maxY = MathF.Max(minY, screenHeight - MenuPlayerVerticalMargin);
            desiredY = Math.Clamp(desiredY, minY, maxY);

            var desiredDir = new Vec2(MenuPlayerVerticalLookahead, desiredY - menuPlayerPos.Y).Normalized();
            var turnLerp = MathF.Min(1f, dt * MenuPlayerTurnResponsiveness);
            menuPlayerDir = Vec2.Lerp(menuPlayerDir, desiredDir, turnLerp).Normalized();
            if (menuPlayerDir.Length <= 0.001f)
            {
                menuPlayerDir = new Vec2(1, 0);
            }

            var dynamicSpeedPulse = 0.5f + (0.5f * MathF.Sin(menuSceneTime * 4.4f));
            var dynamicSpeed = menuPlayerSpeed * (1f + (dynamicSpeedPulse * 0.10f) + (menuDodgeTimer > 0f ? 0.20f : 0f));
            menuPlayerPos += menuPlayerDir * dynamicSpeed * dt;

            var hasMoved = Vec2.Distance(previousPlayerPos, menuPlayerPos) > 0.01f;
            menuStepEchoTimer += dt;

            if (hasMoved && !menuWasMoving && menuStepEchoTimer >= MenuStepEchoInterval)
            {
                SpawnMenuStepEcho();
                menuStepEchoTimer = 0f;
            }

            if (hasMoved)
            {
                while (menuStepEchoTimer >= MenuStepEchoInterval)
                {
                    SpawnMenuStepEcho();
                    menuStepEchoTimer -= MenuStepEchoInterval;
                }
            }

            menuWasMoving = hasMoved;

            if (!menuKeyCollected)
            {
                var keyDistance = Vec2.Distance(menuPlayerPos, menuKeyPos);
                if (keyDistance <= MenuKeyPickupDistance)
                {
                    menuKeyCollected = true;
                    menuKeyRays.Clear();
                }
            }

            for (var i = menuRays.Count - 1; i >= 0; i--)
            {
                menuRays[i].Update(dt);
                if (!menuRays[i].IsAlive)
                {
                    menuRays.RemoveAt(i);
                }
            }

            var keyBounds = new RectangleF(
                menuKeyPos.X - MenuKeyRadius,
                menuKeyPos.Y - MenuKeyRadius,
                MenuKeyRadius * 2,
                MenuKeyRadius * 2);

            if (!menuKeyCollected)
            {
                for (var i = menuKeyRays.Count - 1; i >= 0; i--)
                {
                    menuKeyRays[i].Update(dt);
                    if (!menuKeyRays[i].IsAlive)
                    {
                        menuKeyRays.RemoveAt(i);
                    }
                }

                var targetKeyRays = 10;
                while (menuKeyRays.Count < targetKeyRays)
                {
                    var angle = (float)(menuRandom.NextDouble() * Math.PI * 2.0);
                    var dir = new Vec2(MathF.Cos(angle), MathF.Sin(angle));
                    var x = keyBounds.Left + (float)menuRandom.NextDouble() * keyBounds.Width;
                    var y = keyBounds.Top + (float)menuRandom.NextDouble() * keyBounds.Height;
                    var origin = new Vec2(x, y);

                    var speed = 95f + (float)menuRandom.NextDouble() * 75f;
                    var maxDistance = 300f + (float)menuRandom.NextDouble() * 200f;

                    var ray = Ray.CreateInBounds(
                        origin,
                        dir,
                        keyBounds,
                        maxBounces: 7,
                        maxDist: maxDistance,
                        speed: speed,
                        stopOnBoundsHit: false);
                    menuKeyRays.Add(ray);
                }
            }

            // Враги не просто едут по прямой, а подстраиваются под манёвры игрока.
            for (var i = 0; i < menuEnemies.Count; i++)
            {
                var enemyPos = menuEnemies[i].Position;
                menuEnemyPrevPos[i] = enemyPos;

                var laneOffset = i == 0 ? -24f : 36f;
                var targetY = menuPlayerPos.Y + laneOffset;
                var followLerp = MathF.Min(1f, dt * MenuEnemyVerticalFollowStrength);
                var chasedY = enemyPos.Y + ((targetY - enemyPos.Y) * followLerp);
                var newPos = new Vec2(enemyPos.X + (MenuEnemySpeed * dt), chasedY);
                menuEnemies[i] = new Enemy(newPos);
            }

            if (HasMenuSceneFullyExited(screenWidth))
            {
                ResetMenuSceneLoop(screenWidth, screenHeight);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (!isMenuSceneInitialized || (game.CurrentState != GameState.Menu && game.CurrentState != GameState.LevelSelect)) return;

            RepositionMenuSceneToViewport(ClientSize.Width, ClientSize.Height, clearEchoRays: true);
            Invalidate();
        }

        private void RepositionMenuSceneToViewport(int screenWidth, int screenHeight, bool clearEchoRays)
        {
            if (!isMenuSceneInitialized || screenWidth <= 0 || screenHeight <= 0) return;

            var previousCenterY = menuViewportSize.Height > 0
                ? menuViewportSize.Height * 0.5f
                : screenHeight * 0.5f;
            var currentCenterY = screenHeight * 0.5f;
            var offsetY = currentCenterY - previousCenterY;

            if (MathF.Abs(offsetY) > 0.01f)
            {
                menuPlayerPos = new Vec2(menuPlayerPos.X, menuPlayerPos.Y + offsetY);

                for (var i = 0; i < menuEnemies.Count; i++)
                {
                    var enemyPos = menuEnemies[i].Position;
                    menuEnemies[i] = new Enemy(new Vec2(enemyPos.X, enemyPos.Y + offsetY));
                }

                for (var i = 0; i < menuEnemyPrevPos.Count; i++)
                {
                    var previousPos = menuEnemyPrevPos[i];
                    menuEnemyPrevPos[i] = new Vec2(previousPos.X, previousPos.Y + offsetY);
                }
            }

            menuKeyPos = new Vec2(screenWidth - MenuKeyHorizontalMargin, currentCenterY);
            menuViewportSize = new Size(screenWidth, screenHeight);

            if (clearEchoRays)
            {
                menuRays.Clear();
                if (!menuKeyCollected)
                {
                    menuKeyRays.Clear();
                }
            }
        }

        private void DrawMenu(Graphics g)
        {
            var centerX = ClientSize.Width / 2f;
            var centerY = ClientSize.Height / 2f;

            DrawMenuBackground(g);

            using var overlay = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
            g.FillRectangle(overlay, 0, 0, ClientSize.Width, ClientSize.Height);

            var rayCount = 15;
            var rayLength = MathF.Sqrt((ClientSize.Width * ClientSize.Width) + (ClientSize.Height * ClientSize.Height));
            var rotationSpeed = 10f;
            var baseAngle = menuAnimationTime * rotationSpeed;

            using var rayPen = new Pen(Color.FromArgb(40, 255, 255, 255), 2f);
            for (var i = 0; i < rayCount; i++)
            {
                var angle = baseAngle + (i * 360f / rayCount);
                var radians = angle * MathF.PI / 180f;
                var endX = centerX + MathF.Cos(radians) * rayLength;
                var endY = centerY + MathF.Sin(radians) * rayLength;
                g.DrawLine(rayPen, centerX, centerY, endX, endY);
            }

            using var titleFont = new Font("Arial", 72, FontStyle.Bold);
            using var titleBrush = new SolidBrush(Color.White);
            var title = "ECHO";
            var titleSize = g.MeasureString(title, titleFont);
            g.DrawString(title, titleFont, titleBrush, centerX - (titleSize.Width / 2), centerY - 150);

            using var menuFont = new Font("Arial", 32, FontStyle.Regular);
            using var menuBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
            using var underlinePen = new Pen(Color.White, 3f);

            var menuItems = menu.MenuItems;
            var menuStartY = centerY + 50;
            var menuSpacing = 60;

            for (var i = 0; i < menuItems.Length; i++)
            {
                var itemSize = g.MeasureString(menuItems[i], menuFont);
                var itemX = centerX - (itemSize.Width / 2);
                var itemY = menuStartY + (i * menuSpacing);
                g.DrawString(menuItems[i], menuFont, menuBrush, itemX, itemY);

                if (i == menu.SelectedIndex)
                {
                    var underlineY = itemY + itemSize.Height - 5;
                    g.DrawLine(underlinePen, itemX, underlineY, itemX + itemSize.Width, underlineY);
                }
            }
        }

        private void DrawMenuBackground(Graphics g)
        {
            DrawRaySet(g, menuRays, Color.FromArgb(255, 255, 255, 255), MenuBackgroundPlayerRayWidth);

            DrawMenuKey(g);

            for (var i = 0; i < menuEnemies.Count; i++)
            {
                var center = menuEnemies[i].Position;
                var previous = menuEnemyPrevPos[i];
                var movement = center - previous;
                var movementLength = movement.Length;

                if (movementLength > 0.35f)
                {
                    var movementDir = movement.Normalized();
                    for (var layer = EnemyMotionTrailLayers; layer >= 1; layer--)
                    {
                        var ghostCenter = center - (movementDir * (MenuBackgroundEnemyTrailSpacing * layer));
                        var ghostAlpha = EnemyMotionTrailBaseAlphaScale * MathF.Pow(EnemyMotionTrailAlphaFalloff, layer - 1);
                        DrawMenuEnemyBurst(g, ghostCenter, i + (layer * 101), ghostAlpha * 0.8f);
                    }
                }

                DrawMenuEnemyBurst(g, center, i, 1.2f);
            }

            DrawMenuPlayer(g);
        }

        private void DrawLevelSelect(Graphics g)
        {
            var centerX = ClientSize.Width / 2f;
            var centerY = ClientSize.Height / 2f;

            DrawMenuBackground(g);

            var overlayAlpha = (int)(180 * levelSelectTransitionProgress);
            using var overlay = new SolidBrush(Color.FromArgb(overlayAlpha, 0, 0, 0));
            g.FillRectangle(overlay, 0, 0, ClientSize.Width, ClientSize.Height);

            var ease = levelSelectTransitionProgress * levelSelectTransitionProgress * (3f - 2f * levelSelectTransitionProgress);

            var titleAlpha = (int)(255 * ease);
            using var titleFont = new Font("Arial", 42, FontStyle.Bold);
            using var titleBrush = new SolidBrush(Color.FromArgb(titleAlpha, 255, 255, 255));
            var title = "SELECT LEVEL";
            var titleSize = g.MeasureString(title, titleFont);
            var titleY = 50f + (1f - ease) * (-40f);
            g.DrawString(title, titleFont, titleBrush, centerX - titleSize.Width / 2f, titleY);

            var lineAlpha = (int)(140 * ease);
            using var linePen = new Pen(Color.FromArgb(lineAlpha, 150, 210, 255), 2f);
            var lineWidth = 180f * ease;
            var lineY = titleY + titleSize.Height + 8f;
            g.DrawLine(linePen, centerX - lineWidth, lineY, centerX + lineWidth, lineY);

            // Сетка уровней: ячейка 0 = катсцена-интро, 1..N = обычные уровни, N+1 = финальная катсцена.
            var totalLevels = game.TotalLevelCount;
            var totalCells = totalLevels + 2;
            var endingCellIndex = totalLevels + 1;
            var columns = 3;
            var cellWidth = 160f;
            var cellHeight = 120f;
            var cellSpacing = 20f;
            var gridWidth = columns * cellWidth + (columns - 1) * cellSpacing;
            var rows = (int)MathF.Ceiling((float)totalCells / columns);
            var gridHeight = rows * cellHeight + (rows - 1) * cellSpacing;

            var gridStartX = centerX - gridWidth / 2f;
            var gridStartY = lineY + 40f;

            var availableHeight = ClientSize.Height - gridStartY - 80f;
            if (gridHeight > availableHeight && availableHeight > 0)
            {
                var scale = availableHeight / gridHeight;
                cellHeight *= scale;
                cellSpacing *= scale;
                gridHeight = rows * cellHeight + (rows - 1) * cellSpacing;
            }

            while (levelCellRays.Count < totalCells)
            {
                levelCellRays.Add(new List<Ray>());
            }

            using var cellFont = new Font("Arial", 28, FontStyle.Bold);
            using var hintFont = new Font("Arial", 12, FontStyle.Regular);
            using var introFont = new Font("Arial", 22, FontStyle.Bold);

            for (var i = 0; i < totalCells; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var cellX = gridStartX + col * (cellWidth + cellSpacing);
                var cellY = gridStartY + row * (cellHeight + cellSpacing);

                var cellDelay = i * 0.06f;
                var cellProgress = Math.Clamp((levelSelectAnimTime - cellDelay) * 4f, 0f, 1f);
                var cellEase = cellProgress * cellProgress * (3f - 2f * cellProgress);

                var isSelected = i == levelSelectIndex;
                var isIntro = i == 0;
                var isEnding = i == endingCellIndex;
                // Катсцена-интро и 1-й уровень всегда открыты; остальные — по прогрессу.
                var isLocked = !isIntro && !game.IsAllLevelsUnlocked && i > game.MaxUnlockedLevel;
                var cellAlpha = (int)(255 * cellEase * ease);
                if (cellAlpha <= 0) continue;

                var bgAlpha = isLocked
                    ? (int)(10 * cellEase * ease)
                    : isSelected ? (int)(50 * cellEase * ease) : (int)(20 * cellEase * ease);
                using var bgBrush = new SolidBrush(Color.FromArgb(bgAlpha, 255, 255, 255));
                var cellRect = new RectangleF(cellX, cellY, cellWidth, cellHeight);
                g.FillRectangle(bgBrush, cellRect);

                // Лучи внутри ячейки — только для выбранной и разблокированной
                if (isSelected && !isLocked)
                {
                    var cellRays = levelCellRays[i];
                    var targetRayCount = 8;
                    var raySpeed = 120f;
                    var rayMaxDist = 350f;

                    while (cellRays.Count < targetRayCount)
                    {
                        var angle = (float)(levelCellRandom.NextDouble() * Math.PI * 2.0);
                        var dir = new Vec2(MathF.Cos(angle), MathF.Sin(angle));
                        var ox = cellX + (float)levelCellRandom.NextDouble() * cellWidth;
                        var oy = cellY + (float)levelCellRandom.NextDouble() * cellHeight;
                        var origin = new Vec2(ox, oy);

                        var ray = Ray.CreateInBounds(
                            origin, dir, cellRect,
                            maxBounces: 12,
                            maxDist: rayMaxDist,
                            speed: raySpeed + (float)levelCellRandom.NextDouble() * 40f,
                            stopOnBoundsHit: false);
                        cellRays.Add(ray);
                    }

                    var rayColor = Color.FromArgb((int)(200 * cellEase * ease), 150, 220, 255);
                    var rayWidth = 2.8f;

                    var state = g.Save();
                    g.SetClip(cellRect);
                    DrawRaySet(g, cellRays, rayColor, rayWidth);
                    g.Restore(state);
                }
                else
                {
                    levelCellRays[i].Clear();
                }

                if (isLocked)
                {
                    var borderAlpha = (int)(35 * cellEase * ease);
                    using var lockedPen = new Pen(Color.FromArgb(borderAlpha, 100, 100, 100), 1.5f);
                    g.DrawRectangle(lockedPen, cellX, cellY, cellWidth, cellHeight);

                    var diagAlpha = (int)(25 * cellEase * ease);
                    using var diagPen = new Pen(Color.FromArgb(diagAlpha, 100, 100, 100), 1f);
                    var diagSpacing = 20f;
                    for (var d = diagSpacing; d < cellWidth + cellHeight; d += diagSpacing)
                    {
                        var x1 = cellX + MathF.Min(d, cellWidth);
                        var y1 = cellY + MathF.Max(0f, d - cellWidth);
                        var x2 = cellX + MathF.Max(0f, d - cellHeight);
                        var y2 = cellY + MathF.Min(d, cellHeight);
                        g.DrawLine(diagPen, x1, y1, x2, y2);
                    }
                }
                else if (isSelected)
                {
                    var pulse = 0.6f + 0.4f * MathF.Sin(levelSelectAnimTime * 4f);
                    var borderAlpha = (int)(255 * pulse * cellEase * ease);
                    using var selectedPen = new Pen(Color.FromArgb(borderAlpha, 150, 220, 255), 3f);
                    g.DrawRectangle(selectedPen, cellX, cellY, cellWidth, cellHeight);

                    var cornerRayLength = 18f + 8f * MathF.Sin(levelSelectAnimTime * 3f);
                    var cornerAlpha = (int)(180 * pulse * cellEase * ease);
                    using var cornerPen = new Pen(Color.FromArgb(cornerAlpha, 150, 220, 255), 2f);

                    g.DrawLine(cornerPen, cellX, cellY, cellX + cornerRayLength, cellY);
                    g.DrawLine(cornerPen, cellX, cellY, cellX, cellY + cornerRayLength);
                    g.DrawLine(cornerPen, cellX + cellWidth, cellY, cellX + cellWidth - cornerRayLength, cellY);
                    g.DrawLine(cornerPen, cellX + cellWidth, cellY, cellX + cellWidth, cellY + cornerRayLength);
                    g.DrawLine(cornerPen, cellX, cellY + cellHeight, cellX + cornerRayLength, cellY + cellHeight);
                    g.DrawLine(cornerPen, cellX, cellY + cellHeight, cellX, cellY + cellHeight - cornerRayLength);
                    g.DrawLine(cornerPen, cellX + cellWidth, cellY + cellHeight, cellX + cellWidth - cornerRayLength, cellY + cellHeight);
                    g.DrawLine(cornerPen, cellX + cellWidth, cellY + cellHeight, cellX + cellWidth, cellY + cellHeight - cornerRayLength);
                }
                else
                {
                    var borderAlpha = (int)(60 * cellEase * ease);
                    using var normalPen = new Pen(Color.FromArgb(borderAlpha, 200, 200, 200), 1.5f);
                    g.DrawRectangle(normalPen, cellX, cellY, cellWidth, cellHeight);
                }

                if (isLocked)
                {
                    var lockCenterX = cellX + cellWidth / 2f;
                    var lockCenterY = cellY + cellHeight / 2f;
                    var lockBodyAlpha = (int)(cellAlpha * 0.4f);

                    var bodyWidth = 22f;
                    var bodyHeight = 16f;
                    var bodyTop = lockCenterY;
                    var bodyLeft = lockCenterX - bodyWidth / 2f;

                    // Дужка замка — полукруг, нижний край совпадает с верхом тела
                    var arcWidth = bodyWidth * 0.65f;
                    var arcHeight = bodyWidth * 0.65f;
                    using var arcPen = new Pen(Color.FromArgb(lockBodyAlpha, 140, 140, 140), 2.5f);
                    g.DrawArc(arcPen,
                        lockCenterX - arcWidth / 2f,
                        bodyTop - arcHeight,
                        arcWidth, arcHeight, 180, 180);

                    var legLeft = lockCenterX - arcWidth / 2f;
                    var legRight = lockCenterX + arcWidth / 2f;
                    g.DrawLine(arcPen, legLeft, bodyTop - arcHeight / 2f, legLeft, bodyTop);
                    g.DrawLine(arcPen, legRight, bodyTop - arcHeight / 2f, legRight, bodyTop);

                    using var bodyBrush = new SolidBrush(Color.FromArgb(lockBodyAlpha, 140, 140, 140));
                    g.FillRectangle(bodyBrush, bodyLeft, bodyTop, bodyWidth, bodyHeight);
                }
                else
                {
                    if (isIntro)
                    {
                        var introText = "НАЧАЛО";
                        var introSize = g.MeasureString(introText, introFont);

                        var textAlpha = isSelected ? cellAlpha : (int)(cellAlpha * 0.8f);
                        var textColor = isSelected
                            ? Color.FromArgb(textAlpha, 255, 230, 130)
                            : Color.FromArgb(textAlpha, 220, 200, 130);
                        using var introBrush = new SolidBrush(textColor);

                        g.DrawString(introText, introFont, introBrush,
                            cellX + cellWidth / 2f - introSize.Width / 2f,
                            cellY + cellHeight / 2f - introSize.Height / 2f);
                    }
                    else if (isEnding)
                    {
                        var endText = "КОНЕЦ?";
                        var endSize = g.MeasureString(endText, introFont);

                        var textAlpha = isSelected ? cellAlpha : (int)(cellAlpha * 0.8f);
                        var textColor = isSelected
                            ? Color.FromArgb(textAlpha, 255, 130, 130)
                            : Color.FromArgb(textAlpha, 220, 130, 130);
                        using var endBrush = new SolidBrush(textColor);

                        g.DrawString(endText, introFont, endBrush,
                            cellX + cellWidth / 2f - endSize.Width / 2f,
                            cellY + cellHeight / 2f - endSize.Height / 2f);
                    }
                    else
                    {
                        var levelText = i.ToString();
                        var textSize = g.MeasureString(levelText, cellFont);
                        var textAlpha = isSelected ? cellAlpha : (int)(cellAlpha * 0.7f);
                        var textColor = isSelected
                            ? Color.FromArgb(textAlpha, 220, 245, 255)
                            : Color.FromArgb(textAlpha, 200, 200, 200);
                        using var textBrush = new SolidBrush(textColor);
                        g.DrawString(levelText, cellFont, textBrush,
                            cellX + cellWidth / 2f - textSize.Width / 2f,
                            cellY + cellHeight / 2f - textSize.Height / 2f);
                    }
                }
            }

            var hintAlpha = (int)(180 * ease);
            using var hintBrush = new SolidBrush(Color.FromArgb(hintAlpha, 180, 200, 220));
            var hint = "WASD / ARROWS — NAVIGATE    ENTER / SPACE — SELECT    ESC — BACK";
            var hintSize = g.MeasureString(hint, hintFont);
            g.DrawString(hint, hintFont, hintBrush, centerX - hintSize.Width / 2f, ClientSize.Height - 50f);
        }

        private void UpdateLevelCellRays(float dt)
        {
            for (var i = 0; i < levelCellRays.Count; i++)
            {
                var rays = levelCellRays[i];
                for (var j = rays.Count - 1; j >= 0; j--)
                {
                    rays[j].Update(dt);
                    if (!rays[j].IsAlive)
                    {
                        rays.RemoveAt(j);
                    }
                }
            }
        }

        private void DrawMenuPlayer(Graphics g)
        {
            using var playerBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
            var center = menuPlayerPos;
            var dir = menuPlayerDir.Normalized();
            var right = new Vec2(-dir.Y, dir.X);
            var radius = MenuPlayerRadius;

            var tip = center + dir * (radius + 12f);
            var wingBackCenter = center - dir * (radius * 0.7f);
            var leftWing = wingBackCenter + right * (radius * 0.85f);
            var rightWing = wingBackCenter - right * (radius * 0.85f);
            var tail = center - dir * (radius * 1.25f);

            var cursorShape = new[]
            {
                tip.ToPointF(),
                leftWing.ToPointF(),
                tail.ToPointF(),
                rightWing.ToPointF()
            };

            g.FillPolygon(playerBrush, cursorShape);
        }

        private void DrawMenuKey(Graphics g)
        {
            if (menuKeyCollected) return;

            DrawRaySet(g, menuKeyRays, Color.FromArgb(145, 255, 215, 0), MenuBackgroundKeyRayWidth);
        }

        private void SpawnMenuStepEcho()
        {
            var largeBounds = new RectangleF(-2000, -2000, 6000, 6000);
            var baseAngleDegrees = MathF.Atan2(menuPlayerDir.Y, menuPlayerDir.X) * 180f / MathF.PI;

            for (var degrees = 0; degrees <= 360; degrees += MenuStepEchoDegrees)
            {
                var rayDirection = Vec2.FromAngleDeg(baseAngleDegrees + degrees);
                var ray = Ray.CreateInBounds(
                    menuPlayerPos,
                    rayDirection,
                    largeBounds,
                    maxBounces: 1,
                    maxDist: MenuStepEchoMaxDistance,
                    speed: MenuStepEchoSpeed,
                    stopOnBoundsHit: true);
                menuRays.Add(ray);
            }
        }

        private bool HasMenuSceneFullyExited(int screenWidth)
        {
            var edge = screenWidth + MenuPlayerResetMargin;
            if (menuPlayerPos.X < edge) return false;

            for (var i = 0; i < menuEnemies.Count; i++)
            {
                if (menuEnemies[i].Position.X < edge) return false;
            }

            return true;
        }

        private void ResetMenuSceneLoop(int screenWidth, int screenHeight)
        {
            menuPlayerPos = new Vec2(-50f, screenHeight * 0.5f);
            menuPlayerDir = new Vec2(1, 0);
            menuKeyPos = new Vec2(screenWidth - MenuKeyHorizontalMargin, screenHeight * 0.5f);
            menuKeyCollected = false;
            menuStepEchoTimer = MenuStepEchoInterval;
            menuWasMoving = false;
            menuSceneTime = 0f;
            menuDodgeCooldown = 0f;
            menuDodgeTimer = 0f;
            menuDodgeDirection = 0f;
            menuRays.Clear();
            menuKeyRays.Clear();

            menuEnemies.Clear();
            menuEnemyPrevPos.Clear();
            var enemy1Pos = new Vec2(menuPlayerPos.X - 300f, screenHeight * 0.5f);
            var enemy2Pos = new Vec2(menuPlayerPos.X - 200f, screenHeight * 0.5f + 50f);
            menuEnemies.Add(new Enemy(enemy1Pos));
            menuEnemies.Add(new Enemy(enemy2Pos));
            menuEnemyPrevPos.Add(enemy1Pos);
            menuEnemyPrevPos.Add(enemy2Pos);
        }

        private void DrawMenuEnemyBurst(Graphics g, Vec2 center, int enemyIndex, float opacityScale = 1f)
        {
            var state = g.Save();
            g.TranslateTransform(center.X, center.Y);
            g.ScaleTransform(MenuBackgroundEnemyScale, MenuBackgroundEnemyScale);
            g.TranslateTransform(-center.X, -center.Y);
            DrawEnemyBurst(g, center, enemyIndex, opacityScale);
            g.Restore(state);
        }
    }
}
