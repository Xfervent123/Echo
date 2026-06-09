using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Echo
{
    // Рендер основной сцены, HUD и оверлеев.
    public partial class GameForm
    {
        private float enemyVisualTime; // Накопленное время анимации визуала врагов E
        private List<Vec2> enemyPreviousFramePositions; // Позиции врагов на прошлом кадре для следа движения

        private float pauseAnimTime;
        private float pauseTransitionProgress;
        private int pauseSelectedIndex; // 0 = RESUME, 1 = LEVELS, 2 = MENU

        private const float FinishObjectiveRayWidth = 4f; // Толщина лучей выхода O
        private const float KeyObjectiveRayWidth = 4f; // Толщина лучей ключа K
        private const float DangerObjectiveRayWidth = 4f; // Толщина лучей опасной зоны R
        private const int EnemyVisualLineCount = 6; // Количество "иголок" у врага E
        private const float EnemyVisualRadius = 28f; // Радиус опорного шестиугольника врага E
        private const float EnemyVisualLineExtension = 45f; // Максимальная длина иглы за пределами фигуры
        private const float EnemyVisualLineMinExtension = 12f; // Минимальная длина иглы
        private const float EnemyVisualLineStartRadiusFactor = 1f; // Откуда стартует игла (доля радиуса от центра)
        private const float EnemyVisualLineWidth = 5.5f; // Толщина игл
        private const float EnemyVisualPulseSpeed = 2.3f; // Скорость цикла "выплеска" игл
        private const float EnemyVisualEnemyPhaseStep = 0.18f; // Смещение фазы между разными врагами E
        private const int EnemyVisualTrailLayers = 4; // Сколько слоев "следа" рисовать за движущейся иглой
        private const float EnemyVisualTrailTimeStep = 0.06f; // Временной шаг между слоями следа
        private const float EnemyVisualTrailAlphaFalloff = 0.58f; // Насколько быстро затухает каждый следующий слой следа
        private const int EnemyMotionTrailLayers = 3; // Количество слоев следа при движении врага
        private const float EnemyMotionTrailSpacing = 8f; // Интервал между слоями следа в пикселях
        private const float EnemyMotionTrailAlphaFalloff = 0.62f; // Затухание прозрачности следа при удалении
        private const float EnemyMotionTrailBaseAlphaScale = 0.70f; // Базовый множитель прозрачности следа
        private const int EnemyVisualNeedleSegments = 7; // Количество сегментов для затухания прозрачности иглы
        private const int EnemyVisualAlphaMin = 45; // Минимальная непрозрачность игл у начала импульса
        private const int EnemyVisualAlphaMax = 120; // Максимальная непрозрачность игл в активной фазе
        private const float LevelIntroTextRevealStartProgress = 0.08f;
        private const float LevelIntroTextRevealEndProgress = 0.36f;
        private const int DeathOverlayRayCount = 22;
        private const float DeathOverlayBaseRayLength = 90f;
        private const float DeathOverlayExtraRayLength = 310f;
        private const float DeathOverlayRingMaxRadius = 220f;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.Black);

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            if (game.CurrentState == GameState.Menu)
            {
                DrawMenu(g);
                return;
            }

            if (game.CurrentState == GameState.LevelSelect)
            {
                DrawLevelSelect(g);
                return;
            }

            if (game.CurrentState == GameState.Cutscene)
            {
                DrawCutscene(g);
                return;
            }

            var camX = (ClientSize.Width / 2f) - game.Player.Position.X;
            var camY = (ClientSize.Height / 2f) - game.Player.Position.Y;
            g.TranslateTransform(camX, camY);

            using (var wallBrush = new SolidBrush(Color.FromArgb(0, 0, 0)))
            {
                var gridX = (int)(game.Player.Position.X / game.Maze.TileSize);
                var gridY = (int)(game.Player.Position.Y / game.Maze.TileSize);
                var viewRadius = 15;

                for (var x = gridX - viewRadius; x <= gridX + viewRadius; x++)
                {
                    for (var y = gridY - viewRadius; y <= gridY + viewRadius; y++)
                    {
                        if (game.Maze.IsWall(x, y))
                        {
                            g.FillRectangle(wallBrush,
                                x * game.Maze.TileSize,
                                y * game.Maze.TileSize,
                                game.Maze.TileSize,
                                game.Maze.TileSize);
                        }
                    }
                }
            }

            DrawObjectiveEchoes(g);
            DrawEnemyVisuals(g);
            DrawPatrolEnemyVisuals(g);
            DrawRaySet(g, game.ActiveRays, Color.White, 3.8f);

            using (var playerBrush = new SolidBrush(Color.White))
            {
                var center = game.Player.Position;
                var dir = game.CursorDirection.Normalized();
                var right = new Vec2(-dir.Y, dir.X);
                var radius = game.Player.Radius;

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

            g.ResetTransform();
            if (game.IsDeathAnimationActive)
            {
                DrawDeathOverlay(g);
                return;
            }

            if (game.IsLevelIntroActive)
            {
                DrawLevelIntroOverlay(g);
                return;
            }

            if (game.IsTutorialActive)
            {
                DrawEnergyBar(g);
                DrawTutorialOverlay(g);
                return;
            }

            DrawEnergyBar(g);

            if (game.CurrentState == GameState.Paused)
            {
                DrawPauseOverlay(g);
            }
        }

        private void DrawLevelIntroOverlay(Graphics g)
        {
            var progress = game.LevelIntroProgress;
            var holdAlpha = 1f;
            var overlayAlpha = (int)(235f * holdAlpha);
            if (overlayAlpha < 0) overlayAlpha = 0;

            using var blackout = new SolidBrush(Color.FromArgb(overlayAlpha, 0, 0, 0));
            g.FillRectangle(blackout, 0, 0, ClientSize.Width, ClientSize.Height);

            var textReveal = (progress - LevelIntroTextRevealStartProgress) / (LevelIntroTextRevealEndProgress - LevelIntroTextRevealStartProgress);
            textReveal = Math.Clamp(textReveal, 0f, 1f);
            var textEase = textReveal * textReveal * (3f - (2f * textReveal));

            var centerX = ClientSize.Width * 0.5f;
            var centerY = ClientSize.Height * 0.5f;
            var levelTitle = $"LEVEL {game.CurrentLevelNumber} / {game.TotalLevelCount}";
            var subtitle = "ECHO PROTOCOL ONLINE";
            var objective = "FIND THE KEYS • UNLOCK THE EXIT";
            var continueHint = "PRESS SPACE";
            var accentWidth = ClientSize.Width * (0.14f + (0.32f * textEase));
            var accentAlpha = (int)(180f * holdAlpha * textEase);
            if (accentAlpha < 0) accentAlpha = 0;

            using (var accentPen = new Pen(Color.FromArgb(accentAlpha, 150, 210, 255), 2.2f))
            {
                g.DrawLine(accentPen, centerX - accentWidth, centerY - 62f, centerX + accentWidth, centerY - 62f);
                g.DrawLine(accentPen, centerX - accentWidth, centerY + 64f, centerX + accentWidth, centerY + 64f);
            }

            var glowAlpha = (int)(85f * holdAlpha * textEase);
            if (glowAlpha < 0) glowAlpha = 0;
            using (var glowPen = new Pen(Color.FromArgb(glowAlpha, 110, 190, 255), 3.2f))
            {
                g.DrawEllipse(glowPen, centerX - 165f, centerY - 165f, 330f, 330f);
            }

            var titleAlpha = (int)(255f * holdAlpha * textEase);
            var subtitleAlpha = (int)(170f * holdAlpha * textEase);
            if (titleAlpha < 0) titleAlpha = 0;
            if (subtitleAlpha < 0) subtitleAlpha = 0;

            using var titleFont = new Font("Arial", 62, FontStyle.Bold);
            using var subtitleFont = new Font("Arial", 16, FontStyle.Bold);
            using var objectiveFont = new Font("Arial", 13, FontStyle.Regular);
            using var hintFont = new Font("Arial", 12, FontStyle.Bold);
            using var titleBrush = new SolidBrush(Color.FromArgb(titleAlpha, 255, 255, 255));
            using var subtitleBrush = new SolidBrush(Color.FromArgb(subtitleAlpha, 160, 215, 255));
            using var objectiveBrush = new SolidBrush(Color.FromArgb((int)(subtitleAlpha * 0.9f), 230, 230, 230));
            using var hintBrush = new SolidBrush(Color.FromArgb((int)(195f * textEase), 180, 220, 255));

            var titleSize = g.MeasureString(levelTitle, titleFont);
            var subtitleSize = g.MeasureString(subtitle, subtitleFont);
            var objectiveSize = g.MeasureString(objective, objectiveFont);
            var hintSize = g.MeasureString(continueHint, hintFont);

            g.DrawString(levelTitle, titleFont, titleBrush, centerX - (titleSize.Width * 0.5f), centerY - 48f - (titleSize.Height * 0.5f));
            g.DrawString(subtitle, subtitleFont, subtitleBrush, centerX - (subtitleSize.Width * 0.5f), centerY + 26f);
            g.DrawString(objective, objectiveFont, objectiveBrush, centerX - (objectiveSize.Width * 0.5f), centerY + 78f);
            g.DrawString(continueHint, hintFont, hintBrush, centerX - (hintSize.Width * 0.5f), centerY + 110f);
        }

        private void DrawDeathOverlay(Graphics g)
        {
            var progress = game.DeathAnimationProgress;
            var easeOut = 1f - MathF.Pow(1f - progress, 3f);
            var fadeOut = 1f - progress;

            var worldCenter = game.DeathAnimationCenter;
            var centerX = (ClientSize.Width * 0.5f) + (worldCenter.X - game.Player.Position.X);
            var centerY = (ClientSize.Height * 0.5f) + (worldCenter.Y - game.Player.Position.Y);
            var center = new Vec2(centerX, centerY);

            var blackoutAlpha = (int)(220f * easeOut);
            if (blackoutAlpha < 0) blackoutAlpha = 0;
            using (var blackout = new SolidBrush(Color.FromArgb(blackoutAlpha, 0, 0, 0)))
            {
                g.FillRectangle(blackout, 0, 0, ClientSize.Width, ClientSize.Height);
            }

            var ringRadius = 28f + (DeathOverlayRingMaxRadius * easeOut);
            var ringAlpha = (int)(210f * fadeOut);
            if (ringAlpha < 0) ringAlpha = 0;
            using (var ringPen = new Pen(Color.FromArgb(ringAlpha, 255, 255, 255), 3.6f))
            {
                g.DrawEllipse(ringPen, center.X - ringRadius, center.Y - ringRadius, ringRadius * 2f, ringRadius * 2f);
            }

            var baseAngle = MathF.Atan2(game.DeathAnimationDirection.Y, game.DeathAnimationDirection.X) * (180f / MathF.PI);
            var sweep = 360f / DeathOverlayRayCount;
            for (var i = 0; i < DeathOverlayRayCount; i++)
            {
                var pulse = 0.72f + (0.28f * MathF.Sin((progress * 9f) + (i * 0.6f)));
                var length = (DeathOverlayBaseRayLength + (DeathOverlayExtraRayLength * easeOut)) * pulse;
                var angle = baseAngle + (i * sweep);
                var dir = Vec2.FromAngleDeg(angle);
                var rayEnd = center + (dir * length);
                var rayAlpha = (int)((150f + (80f * pulse)) * fadeOut);
                if (rayAlpha <= 0) continue;
                if (rayAlpha > 255) rayAlpha = 255;

                var rayColor = i % 3 == 0
                    ? Color.FromArgb(rayAlpha, 255, 92, 92)
                    : Color.FromArgb(rayAlpha, 255, 255, 255);
                using var rayPen = new Pen(rayColor, 2.8f)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                g.DrawLine(rayPen, center.ToPointF(), rayEnd.ToPointF());
            }

            var coreRadius = 12f + (56f * easeOut);
            var coreAlpha = (int)(230f * fadeOut);
            if (coreAlpha < 0) coreAlpha = 0;
            using var coreBrush = new SolidBrush(Color.FromArgb(coreAlpha, 255, 255, 255));
            g.FillEllipse(coreBrush, center.X - coreRadius, center.Y - coreRadius, coreRadius * 2f, coreRadius * 2f);
        }

        private void DrawObjectiveEchoes(Graphics g)
        {
            DrawRaySet(g, game.FinishEchoRays, Color.FromArgb(140, 255, 255, 255), FinishObjectiveRayWidth);
            DrawRaySet(g, game.KeyEchoRays, Color.FromArgb(145, 255, 215, 0), KeyObjectiveRayWidth);

            if (game.Maze.DangerCells.Count == 0)
            {
                return;
            }

            var clipState = g.Save();
            using (var dangerClipPath = new System.Drawing.Drawing2D.GraphicsPath())
            {
                for (var i = 0; i < game.Maze.DangerCells.Count; i++)
                {
                    var cell = game.Maze.DangerCells[i];
                    dangerClipPath.AddRectangle(new RectangleF(
                        cell.X * game.Maze.TileSize,
                        cell.Y * game.Maze.TileSize,
                        game.Maze.TileSize,
                        game.Maze.TileSize));
                }

                g.SetClip(dangerClipPath, System.Drawing.Drawing2D.CombineMode.Intersect);
                DrawRaySet(g, game.DangerEchoRays, Color.FromArgb(180, 255, 50, 50), DangerObjectiveRayWidth);
            }
            g.Restore(clipState);
        }

        private void DrawEnemyVisuals(Graphics g)
        {
            EnsureEnemyPreviousFramePositions();

            for (var enemyIndex = 0; enemyIndex < game.Enemies.Count; enemyIndex++)
            {
                var center = game.Enemies[enemyIndex].Position;
                var previous = enemyPreviousFramePositions[enemyIndex];
                var movement = center - previous;
                var movementLength = movement.Length;

                if (movementLength > 0.35f)
                {
                    var movementDir = movement.Normalized();
                    for (var layer = EnemyMotionTrailLayers; layer >= 1; layer--)
                    {
                        var ghostCenter = center - (movementDir * (EnemyMotionTrailSpacing * layer));
                        var ghostAlpha = EnemyMotionTrailBaseAlphaScale * MathF.Pow(EnemyMotionTrailAlphaFalloff, layer - 1);
                        DrawEnemyBurst(g, ghostCenter, enemyIndex + (layer * 101), ghostAlpha);
                    }
                }

                DrawEnemyBurst(g, center, enemyIndex, 1f);
                enemyPreviousFramePositions[enemyIndex] = center;
            }
        }

        private void DrawPatrolEnemyVisuals(Graphics g)
        {
            for (var i = 0; i < game.PatrolEnemies.Count; i++)
            {
                var center = game.PatrolEnemies[i].Position;
                DrawPatrolEnemyBurst(g, center, i, 1f);
            }
        }

        private void DrawPatrolEnemyBurst(Graphics g, Vec2 center, int enemyIndex, float opacityScale)
        {
            var angleStep = 360f / EnemyVisualLineCount;
            var startDistance = EnemyVisualRadius * EnemyVisualLineStartRadiusFactor * 0.85f;

            for (var trailIndex = EnemyVisualTrailLayers; trailIndex >= 0; trailIndex--)
            {
                var sampleTime = enemyVisualTime - (trailIndex * EnemyVisualTrailTimeStep);
                if (sampleTime < 0f) continue;

                var sampleCycleValue = (sampleTime * EnemyVisualPulseSpeed * 0.7f) + (enemyIndex * EnemyVisualEnemyPhaseStep);
                var pulse = sampleCycleValue - MathF.Floor(sampleCycleValue);
                var sampleBaseAngle = GetEnemyNeedleBaseAngle(enemyIndex + 500, sampleTime);
                var alphaScale = MathF.Pow(EnemyVisualTrailAlphaFalloff, trailIndex);
                var layerAlpha = (int)((EnemyVisualAlphaMin + ((EnemyVisualAlphaMax - EnemyVisualAlphaMin) * pulse)) * alphaScale * opacityScale);
                if (layerAlpha <= 2) continue;

                var visibleLength = EnemyVisualLineMinExtension + (pulse * (EnemyVisualLineExtension - EnemyVisualLineMinExtension)) * 0.8f;
                var layerWidth = EnemyVisualLineWidth * (0.7f + (0.3f * alphaScale));

                for (var lineIndex = 0; lineIndex < EnemyVisualLineCount; lineIndex++)
                {
                    var angle = sampleBaseAngle + (lineIndex * angleStep);
                    var direction = Vec2.FromAngleDeg(angle);
                    var start = center + (direction * startDistance);
                    DrawPatrolFadingNeedle(g, start, direction, visibleLength, layerAlpha, layerWidth);
                }
            }
        }

        private void DrawPatrolFadingNeedle(Graphics g, Vec2 start, Vec2 direction, float length, int sourceAlpha, float width)
        {
            var segmentLength = length / EnemyVisualNeedleSegments;
            for (var i = 0; i < EnemyVisualNeedleSegments; i++)
            {
                var t0 = i / (float)EnemyVisualNeedleSegments;
                var t1 = (i + 1) / (float)EnemyVisualNeedleSegments;
                var p1 = start + (direction * (segmentLength * i));
                var p2 = start + (direction * (segmentLength * (i + 1)));
                var fade = 1f - ((t0 + t1) * 0.5f);
                var alpha = (int)(sourceAlpha * fade);
                if (alpha <= 0) continue;

                using var pen = new Pen(Color.FromArgb(alpha, 80, 200, 255), width)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                g.DrawLine(pen, p1.ToPointF(), p2.ToPointF());
            }
        }

        private void DrawEnemyBurst(Graphics g, Vec2 center, int enemyIndex, float opacityScale = 1f)
        {
            var angleStep = 360f / EnemyVisualLineCount;
            var startDistance = EnemyVisualRadius * EnemyVisualLineStartRadiusFactor;

            for (var trailIndex = EnemyVisualTrailLayers; trailIndex >= 0; trailIndex--)
            {
                var sampleTime = enemyVisualTime - (trailIndex * EnemyVisualTrailTimeStep);
                if (sampleTime < 0f) continue;

                var sampleCycleValue = (sampleTime * EnemyVisualPulseSpeed) + (enemyIndex * EnemyVisualEnemyPhaseStep);
                var pulse = sampleCycleValue - MathF.Floor(sampleCycleValue);
                var sampleBaseAngle = GetEnemyNeedleBaseAngle(enemyIndex, sampleTime);
                var alphaScale = MathF.Pow(EnemyVisualTrailAlphaFalloff, trailIndex);
                var layerAlpha = (int)((EnemyVisualAlphaMin + ((EnemyVisualAlphaMax - EnemyVisualAlphaMin) * pulse)) * alphaScale * opacityScale);
                if (layerAlpha <= 2) continue;

                var visibleLength = EnemyVisualLineMinExtension + (pulse * (EnemyVisualLineExtension - EnemyVisualLineMinExtension));
                var layerWidth = EnemyVisualLineWidth * (0.7f + (0.3f * alphaScale));

                for (var lineIndex = 0; lineIndex < EnemyVisualLineCount; lineIndex++)
                {
                    var angle = sampleBaseAngle + (lineIndex * angleStep);
                    var direction = Vec2.FromAngleDeg(angle);
                    var start = center + (direction * startDistance);
                    DrawFadingNeedle(g, start, direction, visibleLength, layerAlpha, layerWidth);
                }
            }
        }

        private void DrawFadingNeedle(Graphics g, Vec2 start, Vec2 direction, float length, int sourceAlpha, float width)
        {
            var segmentLength = length / EnemyVisualNeedleSegments;
            for (var i = 0; i < EnemyVisualNeedleSegments; i++)
            {
                var t0 = i / (float)EnemyVisualNeedleSegments;
                var t1 = (i + 1) / (float)EnemyVisualNeedleSegments;
                var p1 = start + (direction * (segmentLength * i));
                var p2 = start + (direction * (segmentLength * (i + 1)));
                var fade = 1f - ((t0 + t1) * 0.5f);
                var alpha = (int)(sourceAlpha * fade);
                if (alpha <= 0) continue;

                using var pen = new Pen(Color.FromArgb(alpha, 255, 90, 90), width)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                g.DrawLine(pen, p1.ToPointF(), p2.ToPointF());
            }
        }

        private float GetEnemyNeedleBaseAngle(int enemyIndex, float sampleTime)
        {
            var cycleValue = (sampleTime * EnemyVisualPulseSpeed) + (enemyIndex * EnemyVisualEnemyPhaseStep);
            var cycleIndex = (int)MathF.Floor(cycleValue);
            var angleRandom = new Random(((enemyIndex + 1) * 7919) + ((cycleIndex + 1) * 104729));
            return ((float)angleRandom.NextDouble() * 360f) - 90f;
        }

        private void EnsureEnemyPreviousFramePositions()
        {
            while (enemyPreviousFramePositions.Count < game.Enemies.Count)
            {
                enemyPreviousFramePositions.Add(game.Enemies[enemyPreviousFramePositions.Count].Position);
            }

            while (enemyPreviousFramePositions.Count > game.Enemies.Count)
            {
                enemyPreviousFramePositions.RemoveAt(enemyPreviousFramePositions.Count - 1);
            }
        }

        private void DrawRaySet(Graphics g, List<Ray> rays, Color color, float width)
        {
            using var pen = new Pen(color, width);

            for (var i = 0; i < rays.Count; i++)
            {
                var ray = rays[i];
                var alpha = (int)(ray.FadeAlpha * color.A);
                if (alpha <= 0) continue;
                if (alpha > 255) alpha = 255;
                pen.Color = Color.FromArgb(alpha, color.R, color.G, color.B);

                var drawn = 0f;
                for (var j = 0; j < ray.Path.Count - 1; j++)
                {
                    var p1 = ray.Path[j];
                    var p2 = ray.Path[j + 1];
                    var segmentLen = Vec2.Distance(p1, p2);

                    if (drawn + segmentLen <= ray.RevealedLength)
                    {
                        g.DrawLine(pen, p1.ToPointF(), p2.ToPointF());
                        drawn += segmentLen;
                    }
                    else
                    {
                        var remaining = ray.RevealedLength - drawn;
                        var t = remaining / segmentLen;
                        var tip = Vec2.Lerp(p1, p2, t);
                        g.DrawLine(pen, p1.ToPointF(), tip.ToPointF());
                        break;
                    }
                }
            }
        }

        private void DrawEnergyBar(Graphics g)
        {
            var maxEnergy = game.Player.MaxEnergy;
            var energy = Math.Clamp(game.Player.Energy, 0, maxEnergy);

            var squareSize = 34;
            var spacing = 9;
            var leftMargin = 24;
            var bottomMargin = 24;
            var y = ClientSize.Height - bottomMargin - squareSize;

            using var filledBrush = new SolidBrush(Color.White);
            using var emptyPen = new Pen(Color.DarkGray, 2);

            for (var i = 0; i < maxEnergy; i++)
            {
                var x = leftMargin + i * (squareSize + spacing);
                var rect = new Rectangle(x, y, squareSize, squareSize);

                if (i < energy)
                    g.FillRectangle(filledBrush, rect);
                else
                    g.DrawRectangle(emptyPen, rect);
            }

            var staminaBarWidth = 280;
            var staminaBarHeight = 18;
            var staminaX = leftMargin;
            var staminaY = y - staminaBarHeight - 14;
            var staminaPercent = game.Player.Stamina / game.Player.MaxStamina;

            using var staminaBackBrush = new SolidBrush(Color.FromArgb(60, 255, 255, 255));
            using var staminaFillBrush = new SolidBrush(Color.FromArgb(200, 100, 200, 255));
            using var staminaBorderPen = new Pen(Color.FromArgb(120, 255, 255, 255), 1);

            g.FillRectangle(staminaBackBrush, staminaX, staminaY, staminaBarWidth, staminaBarHeight);

            var filledWidth = (int)(staminaBarWidth * staminaPercent);
            if (filledWidth > 0)
            {
                g.FillRectangle(staminaFillBrush, staminaX, staminaY, filledWidth, staminaBarHeight);
            }

            g.DrawRectangle(staminaBorderPen, staminaX, staminaY, staminaBarWidth, staminaBarHeight);

            using var levelFont = new Font("Arial", 14, FontStyle.Bold);
            using var levelBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
            var levelText = $"LEVEL {game.CurrentLevelNumber} / {game.TotalLevelCount}";
            var levelSize = g.MeasureString(levelText, levelFont);
            var levelX = ClientSize.Width - levelSize.Width - 20f;
            var levelY = 18f;
            g.DrawString(levelText, levelFont, levelBrush, levelX, levelY);
        }

        private void DrawPauseOverlay(Graphics g)
        {
            var centerX = ClientSize.Width / 2f;
            var centerY = ClientSize.Height / 2f;
            var ease = pauseTransitionProgress * pauseTransitionProgress * (3f - 2f * pauseTransitionProgress);

            var overlayAlpha = (int)(170 * ease);
            using var overlay = new SolidBrush(Color.FromArgb(overlayAlpha, 0, 0, 0));
            g.FillRectangle(overlay, 0, 0, ClientSize.Width, ClientSize.Height);

            var rayCount = 12;
            var rayLength = MathF.Max(ClientSize.Width, ClientSize.Height) * 0.7f;
            var baseAngle = pauseAnimTime * 6f;
            for (var i = 0; i < rayCount; i++)
            {
                var angle = baseAngle + i * (360f / rayCount);
                var rad = angle * MathF.PI / 180f;
                var pulse = 0.5f + 0.5f * MathF.Sin(pauseAnimTime * 2f + i * 0.5f);
                var rayAlpha = (int)(25 * ease * pulse);
                if (rayAlpha <= 0) continue;
                using var rayPen = new Pen(Color.FromArgb(rayAlpha, 150, 200, 255), 1.5f);
                var endX = centerX + MathF.Cos(rad) * rayLength;
                var endY = centerY + MathF.Sin(rad) * rayLength;
                g.DrawLine(rayPen, centerX, centerY, endX, endY);
            }

            var titleAlpha = (int)(255 * ease);
            using var titleFont = new Font("Arial", 52, FontStyle.Bold);
            using var titleBrush = new SolidBrush(Color.FromArgb(titleAlpha, 255, 255, 255));
            var title = "PAUSED";
            var titleSize = g.MeasureString(title, titleFont);
            var titleY = centerY - 130f;
            g.DrawString(title, titleFont, titleBrush, centerX - titleSize.Width / 2f, titleY);

            var lineAlpha = (int)(120 * ease);
            using var linePen = new Pen(Color.FromArgb(lineAlpha, 150, 210, 255), 2f);
            var lineWidth = 140f * ease;
            var linePosY = titleY + titleSize.Height + 6f;
            g.DrawLine(linePen, centerX - lineWidth, linePosY, centerX + lineWidth, linePosY);

            var pauseItems = new[] { "RESUME", "LEVELS", "MENU" };
            using var menuFont = new Font("Arial", 26, FontStyle.Regular);
            var menuStartY = centerY + 10f;
            var menuSpacing = 55f;

            for (var i = 0; i < pauseItems.Length; i++)
            {
                var isSelected = i == pauseSelectedIndex;
                var itemSize = g.MeasureString(pauseItems[i], menuFont);
                var itemX = centerX - itemSize.Width / 2f;
                var itemY = menuStartY + i * menuSpacing;

                var itemAlpha = isSelected ? (int)(255 * ease) : (int)(160 * ease);
                var itemColor = isSelected
                    ? Color.FromArgb(itemAlpha, 200, 240, 255)
                    : Color.FromArgb(itemAlpha, 200, 200, 200);
                using var itemBrush = new SolidBrush(itemColor);
                g.DrawString(pauseItems[i], menuFont, itemBrush, itemX, itemY);

                if (isSelected)
                {
                    var pulse = 0.6f + 0.4f * MathF.Sin(pauseAnimTime * 4f);
                    var underlineAlpha = (int)(255 * pulse * ease);
                    using var underlinePen = new Pen(Color.FromArgb(underlineAlpha, 150, 220, 255), 2.5f);
                    var underlineY = itemY + itemSize.Height - 4f;
                    g.DrawLine(underlinePen, itemX, underlineY, itemX + itemSize.Width, underlineY);

                    var arrowOffset = 20f + 5f * MathF.Sin(pauseAnimTime * 3f);
                    var arrowAlpha = (int)(200 * pulse * ease);
                    using var arrowPen = new Pen(Color.FromArgb(arrowAlpha, 150, 220, 255), 2f);
                    var arrowY = itemY + itemSize.Height / 2f;
                    g.DrawLine(arrowPen, itemX - arrowOffset, arrowY, itemX - arrowOffset + 8f, arrowY - 6f);
                    g.DrawLine(arrowPen, itemX - arrowOffset, arrowY, itemX - arrowOffset + 8f, arrowY + 6f);
                    var rightX = itemX + itemSize.Width + arrowOffset;
                    g.DrawLine(arrowPen, rightX, arrowY, rightX - 8f, arrowY - 6f);
                    g.DrawLine(arrowPen, rightX, arrowY, rightX - 8f, arrowY + 6f);
                }
            }

            using var hintFont = new Font("Arial", 11, FontStyle.Regular);
            var hintAlpha = (int)(140 * ease);
            using var hintBrush = new SolidBrush(Color.FromArgb(hintAlpha, 160, 180, 200));
            var hint = "ESC — RESUME";
            var hintSize = g.MeasureString(hint, hintFont);
            g.DrawString(hint, hintFont, hintBrush, centerX - hintSize.Width / 2f, ClientSize.Height - 45f);
        }
    }
}
