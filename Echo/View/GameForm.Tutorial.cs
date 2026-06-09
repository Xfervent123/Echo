using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Echo
{
    // Туториальное окно поверх игры: для первых уровней показывает,
    // какая новая механика появилась и как ей пользоваться.
    public partial class GameForm
    {
        // Размер окна туториала (доли экрана).
        private const float TutorialBoxWidthRatio = 0.7f;
        private const float TutorialBoxHeightRatio = 0.62f;
        private const float TutorialBoxMaxWidth = 780f;
        private const float TutorialBoxMaxHeight = 500f;

        private const int TutorialFrameCount = 3;

        private static int GetTutorialFrameCount(int level)
        {
            switch (level)
            {
                case 4:
                case 10:
                    return 2;
                default:
                    return 3;
            }
        }

        private void DrawTutorialOverlay(Graphics g)
        {
            var w = ClientSize.Width;
            var h = ClientSize.Height;

            using (var dim = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
            {
                g.FillRectangle(dim, 0, 0, w, h);
            }

            var boxW = MathF.Min(TutorialBoxMaxWidth, w * TutorialBoxWidthRatio);
            var boxH = MathF.Min(TutorialBoxMaxHeight, h * TutorialBoxHeightRatio);
            var boxX = (w - boxW) * 0.5f;
            var boxY = (h - boxH) * 0.5f;
            var box = new RectangleF(boxX, boxY, boxW, boxH);

            using (var bg = new SolidBrush(Color.FromArgb(235, 8, 8, 14)))
            {
                g.FillRectangle(bg, box);
            }
            using (var outerPen = new Pen(Color.White, 4f))
            {
                g.DrawRectangle(outerPen, box.X, box.Y, box.Width, box.Height);
            }
            using (var innerPen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f))
            {
                var inset = 6f;
                g.DrawRectangle(innerPen, box.X + inset, box.Y + inset, box.Width - inset * 2f, box.Height - inset * 2f);
            }

            var level = game.CurrentLevelNumber;
            var info = GetTutorialInfo(level);

            using (var titleBg = new SolidBrush(Color.FromArgb(255, 0, 0, 0)))
            using (var titlePen = new Pen(Color.FromArgb(220, 255, 230, 130), 2f))
            using (var titleFont = new Font("Consolas", 14f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(Color.FromArgb(255, 255, 230, 130)))
            {
                var titleSize = g.MeasureString(info.Title, titleFont);
                var pad = 14f;
                var titleRect = new RectangleF(
                    box.X + 24f,
                    box.Y - titleSize.Height - 4f,
                    titleSize.Width + pad * 2f,
                    titleSize.Height + 6f);
                g.FillRectangle(titleBg, titleRect);
                g.DrawRectangle(titlePen, titleRect.X, titleRect.Y, titleRect.Width, titleRect.Height);
                g.DrawString(info.Title, titleFont, titleBrush, titleRect.X + pad, titleRect.Y + 2f);
            }

            var textRect = new RectangleF(
                box.X + 28f,
                box.Y + box.Height * 0.55f,
                box.Width - 56f,
                box.Height * 0.45f - 32f);

            using (var fmt = new StringFormat
            {
                Trimming = StringTrimming.None,
                FormatFlags = StringFormatFlags.LineLimit,
                Alignment = StringAlignment.Center
            })
            using (var bodyFont = new Font("Consolas", 14f, FontStyle.Regular))
            using (var bodyBrush = new SolidBrush(Color.FromArgb(235, 230, 230, 230)))
            {
                g.DrawString(info.Body, bodyFont, bodyBrush, textRect, fmt);
            }

            var animRect = new RectangleF(
                box.X + 28f,
                box.Y + 22f,
                box.Width - 56f,
                box.Height * 0.5f);
            DrawTutorialAnimation(g, animRect, level, game.TutorialTime);

            var blink = (MathF.Sin(game.TutorialTime * 6f) + 1f) * 0.5f;
            var hintAlpha = (int)(140f + 100f * blink);
            using (var hintFont = new Font("Consolas", 11.5f, FontStyle.Bold))
            using (var hintBrush = new SolidBrush(Color.FromArgb(hintAlpha, 200, 220, 255)))
            {
                var hint = "SPACE — продолжить";
                var hintSize = g.MeasureString(hint, hintFont);
                g.DrawString(hint, hintFont, hintBrush,
                    box.X + (box.Width - hintSize.Width) * 0.5f,
                    box.Bottom - hintSize.Height - 14f);
            }
        }

        private (string Title, string Body) GetTutorialInfo(int level)
        {
            switch (level)
            {
                case 1:
                    return ("УРОВЕНЬ 1 — ЭХО",
                        "Двигайся клавишами WASD. Каждый шаг рождает маленькое эхо.\nНажми SPACE — посылается мощный импульс, очерчивающий стены.\nДойди до выхода.");
                case 2:
                    return ("УРОВЕНЬ 2 — КЛЮЧ",
                        "Подбери ключ — и выход появится.\nКлюч сразу же излучает эхо, выход — только после подбора всех ключей. Иди на свет.");
                case 3:
                    return ("УРОВЕНЬ 3 — ОПАСНАЯ ЗОНА",
                        "Красные клетки убивают мгновенно.\nОни тоже шумят красным эхом — слушай и обходи.");
                case 4:
                    return ("УРОВЕНЬ 4 — ВРАГ",
                        "Враг ловит твоё эхо и движется по нему.\nЗажми SHIFT, чтобы красться: эхо тише, но тратится выносливость.");
                case 10:
                    return ("УРОВЕНЬ 10 — ПАТРУЛЬ",
                        "В голубой зоне живёт патрульный — он ходит по ней туда-сюда.\nИз зоны он не выходит, но в её пределах быстро бросается на эхо.");
                default:
                    return ("ТУТОРИАЛ", "");
            }
        }

        // Длительность одного кадра анимации в зависимости от уровня.
        private static float GetTutorialFrameDuration(int level)
        {
            switch (level)
            {
                case 4:
                case 10:
                    return 1.6f;
                default:
                    return 0.85f;
            }
        }

        private void DrawTutorialAnimation(Graphics g, RectangleF area, int level, float time)
        {
            var frameCount = GetTutorialFrameCount(level);
            var frameDuration = GetTutorialFrameDuration(level);
            var cycleDuration = frameDuration * frameCount;
            var cycleTime = time % cycleDuration;
            var frameIndex = Math.Min(frameCount - 1, (int)(cycleTime / frameDuration));
            var frameProgress = (cycleTime - frameIndex * frameDuration) / frameDuration;
            frameProgress = Math.Clamp(frameProgress, 0f, 1f);

            // Клипуем содержимое строго в область анимации.
            var savedState = g.Save();
            g.SetClip(area);

            switch (level)
            {
                case 1:
                    DrawTutorialAnim_Level1(g, area, frameIndex, frameProgress, time);
                    break;
                case 2:
                    DrawTutorialAnim_Level2(g, area, frameIndex, frameProgress, time);
                    break;
                case 3:
                    DrawTutorialAnim_Level3(g, area, frameIndex, frameProgress, time);
                    break;
                case 4:
                    DrawTutorialAnim_Level4(g, area, frameIndex, frameProgress, time);
                    break;
                case 10:
                    DrawTutorialAnim_Level10(g, area, frameIndex, frameProgress, time);
                    break;
            }

            DrawFrameDots(g, area, frameIndex, frameCount);

            g.Restore(savedState);
        }

        // -------- Уровень 1: WASD + SPACE (эхо-импульс) --------
        private void DrawTutorialAnim_Level1(Graphics g, RectangleF area, int frame, float frameProgress, float time)
        {
            var leftCx = area.X + area.Width * 0.22f;
            var rightCx = area.X + area.Width * 0.78f;
            var cy = area.Y + area.Height * 0.5f;

            DrawKeyCluster_WASD(g, leftCx, cy + 4f, 36f, frame);

            var spaceRect = new RectangleF(rightCx - 60f, cy - 18f, 120f, 36f);
            DrawKeyboardKey(g, spaceRect, "SPACE", true, pulseHighlight: frame == 2 ? 1f : 0f);

            var playerArea = new RectangleF(area.X + area.Width * 0.40f, area.Y + 10f, area.Width * 0.20f, area.Height - 20f);
            var playerCx = playerArea.X + playerArea.Width * 0.5f;
            var playerCy = playerArea.Y + playerArea.Height * 0.5f;

            Vec2 playerCenter;
            Vec2 playerDir;
            if (frame == 0)
            {
                playerDir = new Vec2(1, 0);
                playerCenter = new Vec2(playerCx - 30f + 60f * frameProgress, playerCy);
            }
            else if (frame == 1)
            {
                playerDir = new Vec2(0, -1);
                playerCenter = new Vec2(playerCx, playerCy + 24f - 48f * frameProgress);
            }
            else
            {
                playerDir = new Vec2(1, 0);
                playerCenter = new Vec2(playerCx, playerCy);
            }

            if (frame < 2)
            {
                var stepPhase = (time * 2f) % 1f;
                DrawEchoRing(g, playerCenter, 22f + 28f * stepPhase, 1f - stepPhase, 1.8f, Color.FromArgb(220, 255, 255, 255));
            }
            else
            {
                var pulseRadius = 30f + 130f * frameProgress;
                DrawEchoRing(g, playerCenter, pulseRadius, 1f - frameProgress * 0.95f, 3.2f, Color.FromArgb(255, 255, 255, 255));
                DrawEchoRing(g, playerCenter, pulseRadius * 0.65f, 1f - frameProgress * 0.95f, 2.4f, Color.FromArgb(255, 255, 255, 255));
            }

            DrawTutorialPlayer(g, playerCenter, playerDir, 1f);
        }

        // -------- Уровень 2: ключ + выход --------
        // Кадр 0: ключ светится, выход тусклый. Игрок идёт к ключу.
        // Кадр 1: игрок касается ключа — вспышка, ключ исчезает.
        // Кадр 2: выход теперь яркий, игрок идёт к нему.
        private void DrawTutorialAnim_Level2(Graphics g, RectangleF area, int frame, float frameProgress, float time)
        {
            var blockSize = 64f;
            var keyCx = area.X + area.Width * 0.30f;
            var exitCx = area.X + area.Width * 0.78f;
            var cy = area.Y + area.Height * 0.50f;

            var keyRect = new RectangleF(keyCx - blockSize * 0.5f, cy - blockSize * 0.5f, blockSize, blockSize);
            var exitRect = new RectangleF(exitCx - blockSize * 0.5f, cy - blockSize * 0.5f, blockSize, blockSize);

            // Цвета такие же, как в игре (KeyEchoRays — жёлтый, FinishEchoRays — белый).
            var keyColor = Color.FromArgb(220, 255, 215, 60);
            var exitColor = Color.FromArgb(220, 255, 255, 255);

            // Видимость ключа: исчезает в первой половине кадра 1.
            var keyVisible = frame == 0 || (frame == 1 && frameProgress < 0.5f);
            if (keyVisible)
            {
                DrawObjectiveEchoBlock(g, keyRect, keyColor, time, rayCount: 6, seed: 17);
            }

            // Выход разблокирован, когда ключ собран.
            var exitUnlocked = frame == 2 || (frame == 1 && frameProgress >= 0.5f);
            if (exitUnlocked)
            {
                DrawObjectiveEchoBlock(g, exitRect, exitColor, time, rayCount: 6, seed: 41);
            }
            else
            {
                // Тусклый выход без лучей — как заблокированный.
                using var dim = new SolidBrush(Color.FromArgb(35, 200, 200, 200));
                g.FillRectangle(dim, exitRect);
                using var border = new Pen(Color.FromArgb(140, 180, 180, 180), 2f);
                g.DrawRectangle(border, exitRect.X, exitRect.Y, exitRect.Width, exitRect.Height);
            }

            DrawCaption(g, keyCx, area.Y + area.Height - 6f, "КЛЮЧ", Color.FromArgb(220, 255, 220, 90));
            DrawCaption(g, exitCx, area.Y + area.Height - 6f,
                exitUnlocked ? "ВЫХОД ОТКРЫТ" : "ВЫХОД ЗАПЕРТ",
                exitUnlocked ? Color.FromArgb(220, 255, 230, 130) : Color.FromArgb(180, 180, 180, 180));

            Vec2 playerCenter;
            Vec2 playerDir;
            if (frame == 0)
            {
                playerCenter = new Vec2(keyCx + 110f - 80f * frameProgress, cy);
                playerDir = new Vec2(-1, 0);
            }
            else if (frame == 1)
            {
                if (frameProgress < 0.5f)
                {
                    playerCenter = new Vec2(keyCx + 30f, cy);
                    playerDir = new Vec2(-1, 0);
                    var pickupT = frameProgress * 2f;
                    DrawEchoRing(g, new Vec2(keyCx, cy), 24f + 60f * pickupT, 1f - pickupT, 3f, Color.FromArgb(255, 255, 230, 130));
                }
                else
                {
                    var t = (frameProgress - 0.5f) * 2f;
                    playerCenter = new Vec2(keyCx + 30f + (exitCx - keyCx - 30f) * 0.4f * t, cy);
                    playerDir = new Vec2(1, 0);
                }
            }
            else
            {
                var startX = keyCx + 30f + (exitCx - keyCx - 30f) * 0.4f;
                playerCenter = new Vec2(startX + (exitCx - 30f - startX) * frameProgress, cy);
                playerDir = new Vec2(1, 0);
                var stepPhase = (time * 2f) % 1f;
                DrawEchoRing(g, playerCenter, 18f + 24f * stepPhase, 1f - stepPhase, 1.6f, Color.FromArgb(180, 255, 255, 255));
            }

            DrawTutorialPlayer(g, playerCenter, playerDir, 1f);
        }

        // -------- Уровень 3: красная клетка убивает --------
        private void DrawTutorialAnim_Level3(Graphics g, RectangleF area, int frame, float frameProgress, float time)
        {
            var cx = area.X + area.Width * 0.5f;
            var cy = area.Y + area.Height * 0.55f;

            var dangerSize = 84f;
            var dangerRect = new RectangleF(cx - dangerSize * 0.5f, cy - dangerSize * 0.5f, dangerSize, dangerSize);
            DrawObjectiveEchoBlock(g, dangerRect, Color.FromArgb(220, 255, 70, 70), time, rayCount: 7, seed: 91);

            Vec2 playerCenter = new Vec2(cx, cy);
            float playerOpacity = 1f;
            Vec2 playerDir = new Vec2(1, 0);

            if (frame == 0)
            {
                playerCenter = new Vec2(cx - 130f + 80f * frameProgress, cy);
                DrawDangerWarningArrow(g, new Vec2(cx, cy - dangerSize * 0.5f - 14f), 0.5f + 0.5f * MathF.Sin(time * 8f));
            }
            else if (frame == 1)
            {
                var t = frameProgress;
                playerCenter = new Vec2(cx - 50f + 50f * t, cy);
            }
            else
            {
                playerCenter = new Vec2(cx, cy);
                playerOpacity = MathF.Max(0f, 1f - frameProgress * 2f);
            }

            if (playerOpacity > 0.01f)
            {
                DrawTutorialPlayer(g, playerCenter, playerDir, playerOpacity);
            }

            DrawCaption(g, cx, area.Y + area.Height - 6f, "НЕ НАСТУПАЙ", Color.FromArgb(220, 255, 90, 90));
        }

        // -------- Уровень 4: враг + Shift (стелс) --------
        // 2 кадра: громкий ход без shift (враг "взрывается") и тихий ход с shift (враг спокоен).
        private void DrawTutorialAnim_Level4(Graphics g, RectangleF area, int frame, float frameProgress, float time)
        {
            var cy = area.Y + area.Height * 0.5f;
            var enemyPos = new Vec2(area.X + area.Width * 0.78f, cy);
            var playerStartX = area.X + area.Width * 0.18f;
            var playerEndX = area.X + area.Width * 0.55f;

            var alarmed = frame == 0 && frameProgress > 0.7f;
            DrawTutorialEnemy(g, enemyPos, time, alarmed);

            var px = playerStartX + (playerEndX - playerStartX) * frameProgress;
            var playerCenter = new Vec2(px, cy);

            if (frame == 0)
            {
                var phase = (time * 1.0f) % 1f;
                DrawEchoRing(g, playerCenter, 30f + 90f * phase, 1f - phase * 0.6f, 2.6f, Color.FromArgb(220, 255, 255, 255));
                var phase2 = (phase + 0.5f) % 1f;
                DrawEchoRing(g, playerCenter, 30f + 90f * phase2, 1f - phase2 * 0.6f, 2f, Color.FromArgb(180, 255, 255, 255));

                DrawTutorialPlayer(g, playerCenter, new Vec2(1, 0), 1f);

                DrawCaption(g, area.X + area.Width * 0.5f, area.Y + area.Height - 6f,
                    "БЕЗ SHIFT — ВРАГ СЛЫШИТ", Color.FromArgb(220, 255, 100, 100));
            }
            else
            {
                var phase = (time * 1.0f) % 1f;
                DrawEchoRing(g, playerCenter, 14f + 22f * phase, (1f - phase) * 0.6f, 1.4f, Color.FromArgb(140, 130, 200, 255));

                DrawTutorialPlayer(g, playerCenter, new Vec2(1, 0), 0.55f);

                var shiftRect = new RectangleF(playerCenter.X - 38f, playerCenter.Y + 26f, 76f, 22f);
                DrawKeyboardKey(g, shiftRect, "SHIFT", false, pulseHighlight: 0.7f);

                DrawCaption(g, area.X + area.Width * 0.5f, area.Y + area.Height - 6f,
                    "С SHIFT — ВРАГ НЕ СЛЫШИТ", Color.FromArgb(220, 130, 200, 255));
            }
        }

        // -------- Уровень 10: патрульный --------
        // 2 кадра: 1) спокойно ходит по зоне; 2) услышал эхо, рванул к границе зоны, но не вышел из неё.
        private void DrawTutorialAnim_Level10(Graphics g, RectangleF area, int frame, float frameProgress, float time)
        {
            var cy = area.Y + area.Height * 0.5f;
            var zoneRect = new RectangleF(area.X + area.Width * 0.50f, cy - 60f, area.Width * 0.32f, 120f);
            DrawPatrolZone(g, zoneRect, time);
            DrawCaption(g, zoneRect.X + zoneRect.Width * 0.5f, zoneRect.Y + zoneRect.Height + 20f,
                "ЗОНА ПАТРУЛЯ", Color.FromArgb(220, 130, 200, 255));

            // Игрок снаружи, слева — стоит на месте. На втором кадре подаёт эхо.
            var playerCenter = new Vec2(area.X + area.Width * 0.18f, cy);

            Vec2 patrolPos;
            bool patrolAlerted = false;
            if (frame == 0)
            {
                // Спокойно ходит туда-сюда внутри зоны.
                var t = MathF.Sin(time * 0.55f) * 0.5f + 0.5f;
                patrolPos = new Vec2(zoneRect.X + 24f + (zoneRect.Width - 48f) * t, zoneRect.Y + zoneRect.Height * 0.5f);

                // Игрока показываем тускло — чтобы было видно, что он не шумит.
                DrawTutorialPlayer(g, playerCenter, new Vec2(1, 0), 0.45f);

                DrawCaption(g, area.X + area.Width * 0.5f, area.Y + area.Height - 6f,
                    "ХОДИТ ВНУТРИ ЗОНЫ", Color.FromArgb(220, 130, 200, 255));
            }
            else
            {
                // Игрок шумит — расходится белое эхо.
                var phase = (time * 1.2f) % 1f;
                DrawEchoRing(g, playerCenter, 25f + 60f * phase, 1f - phase * 0.6f, 2.2f, Color.FromArgb(220, 255, 255, 255));
                DrawTutorialPlayer(g, playerCenter, new Vec2(1, 0), 1f);

                // Патрульный рвётся к ближайшей точке зоны со стороны игрока, но упирается в её границу.
                var startX = zoneRect.X + zoneRect.Width * 0.5f;
                var targetX = zoneRect.X + 8f; // у самой границы, ближе к игроку, но всё ещё внутри
                var x = startX + (targetX - startX) * frameProgress;
                patrolPos = new Vec2(x, zoneRect.Y + zoneRect.Height * 0.5f);
                patrolAlerted = true;

                // Маленькая красная "стенка" на границе зоны — патруль упирается.
                if (frameProgress > 0.7f)
                {
                    var bumpAlpha = (int)(180 * (frameProgress - 0.7f) / 0.3f);
                    using var bumpPen = new Pen(Color.FromArgb(bumpAlpha, 255, 130, 130), 4f);
                    g.DrawLine(bumpPen, zoneRect.X, zoneRect.Y + 14f, zoneRect.X, zoneRect.Y + zoneRect.Height - 14f);
                }

                DrawCaption(g, area.X + area.Width * 0.5f, area.Y + area.Height - 6f,
                    "БРОСАЕТСЯ, НО НЕ ВЫХОДИТ", Color.FromArgb(220, 255, 200, 80));
            }

            DrawPatrolEnemyMarker(g, patrolPos, time, patrolAlerted);
        }

        // ============== HELPERS ==============

        private static void DrawKeyboardKey(Graphics g, RectangleF rect, string label, bool largeFont, float pulseHighlight)
        {
            using (var fill = new SolidBrush(Color.FromArgb(235, 20, 20, 28)))
            {
                g.FillRectangle(fill, rect);
            }
            if (pulseHighlight > 0.01f)
            {
                using var glowFill = new SolidBrush(Color.FromArgb((int)(80 * pulseHighlight), 150, 220, 255));
                g.FillRectangle(glowFill, rect);
            }
            using (var border = new Pen(Color.FromArgb(220, 200, 220, 240), 2.2f))
            {
                g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
            }
            if (pulseHighlight > 0.01f)
            {
                var highlightColor = Color.FromArgb((int)(220 * pulseHighlight), 150, 220, 255);
                using var glowPen = new Pen(highlightColor, 3f);
                g.DrawRectangle(glowPen, rect.X - 2f, rect.Y - 2f, rect.Width + 4f, rect.Height + 4f);
            }
            using var font = new Font("Consolas", largeFont ? 14f : 11f, FontStyle.Bold);
            using var brush = new SolidBrush(Color.White);
            var sz = g.MeasureString(label, font);
            g.DrawString(label, font, brush,
                rect.X + (rect.Width - sz.Width) * 0.5f,
                rect.Y + (rect.Height - sz.Height) * 0.5f);
        }

        private static void DrawKeyCluster_WASD(Graphics g, float cx, float cy, float size, int frame)
        {
            var spacing = size + 6f;
            float wHi = 0f, aHi = 0f, sHi = 0f, dHi = 0f;
            if (frame == 0) dHi = 1f;
            else if (frame == 1) wHi = 1f;

            DrawKeyboardKey(g, new RectangleF(cx - size * 0.5f, cy - spacing - size * 0.5f, size, size), "W", false, wHi);
            DrawKeyboardKey(g, new RectangleF(cx - spacing - size * 0.5f, cy - size * 0.5f, size, size), "A", false, aHi);
            DrawKeyboardKey(g, new RectangleF(cx - size * 0.5f, cy - size * 0.5f, size, size), "S", false, sHi);
            DrawKeyboardKey(g, new RectangleF(cx + spacing - size * 0.5f, cy - size * 0.5f, size, size), "D", false, dHi);
        }

        private static void DrawTutorialPlayer(Graphics g, Vec2 center, Vec2 dir, float opacity)
        {
            var alpha = (int)(255 * Math.Clamp(opacity, 0f, 1f));
            if (alpha <= 0) return;
            var d = dir.Length > 0.001f ? dir.Normalized() : new Vec2(1, 0);
            var right = new Vec2(-d.Y, d.X);
            var radius = 14f;
            var tip = center + d * (radius + 10f);
            var wingBack = center - d * (radius * 0.7f);
            var leftWing = wingBack + right * (radius * 0.85f);
            var rightWing = wingBack - right * (radius * 0.85f);
            var tail = center - d * (radius * 1.25f);
            using var brush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255));
            g.FillPolygon(brush, new[] { tip.ToPointF(), leftWing.ToPointF(), tail.ToPointF(), rightWing.ToPointF() });
        }

        private static void DrawEchoRing(Graphics g, Vec2 center, float radius, float opacity, float width, Color color)
        {
            var clamped = Math.Clamp(opacity, 0f, 1f);
            var alpha = (int)(color.A * clamped);
            if (alpha <= 0 || radius <= 0f) return;
            using var pen = new Pen(Color.FromArgb(alpha, color.R, color.G, color.B), width);
            g.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2f, radius * 2f);
        }

        // Прямоугольник с заливкой и бегущей диагональной штриховкой —
        // тот же визуальный паттерн, что у зоны патруля.
        private static void DrawObjectiveEchoBlock(Graphics g, RectangleF rect, Color color, float time, int rayCount, int seed)
        {
            using (var fill = new SolidBrush(Color.FromArgb(70, color.R, color.G, color.B)))
            {
                g.FillRectangle(fill, rect);
            }

            var spacing = 14f;
            using var stripePen = new Pen(Color.FromArgb(140, color.R, color.G, color.B), 2.5f);
            var offset = (time * 22f) % spacing;
            var saved = g.Save();
            g.SetClip(rect);
            for (var x = rect.Left - rect.Height + offset; x < rect.Right + rect.Height; x += spacing)
            {
                g.DrawLine(stripePen, x, rect.Bottom, x + rect.Height, rect.Top);
            }
            g.Restore(saved);

            using var border = new Pen(Color.FromArgb(220, color.R, color.G, color.B), 2.5f);
            g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
        }

        private static void DrawDangerWarningArrow(Graphics g, Vec2 tip, float intensity)
        {
            var alpha = (int)(220 * Math.Clamp(intensity, 0f, 1f));
            using var brush = new SolidBrush(Color.FromArgb(alpha, 255, 80, 80));
            var pts = new[]
            {
                new PointF(tip.X, tip.Y),
                new PointF(tip.X - 10f, tip.Y - 14f),
                new PointF(tip.X + 10f, tip.Y - 14f)
            };
            g.FillPolygon(brush, pts);
            using var font = new Font("Consolas", 13f, FontStyle.Bold);
            using var fontBrush = new SolidBrush(Color.FromArgb(alpha, 255, 200, 200));
            var sz = g.MeasureString("!", font);
            g.DrawString("!", font, fontBrush, tip.X - sz.Width * 0.5f, tip.Y - 30f);
        }

        // Враг — иголки + чёрное тело. Без буквы.
        private static void DrawTutorialEnemy(Graphics g, Vec2 center, float time, bool alarmed)
        {
            var radius = alarmed ? 28f : 22f;
            var pulse = (MathF.Sin(time * 4f) + 1f) * 0.5f;
            var needleExtra = alarmed ? 1.0f : 0.6f;
            for (var i = 0; i < 6; i++)
            {
                var angle = i * 60f + time * 18f;
                var dir = Vec2.FromAngleDeg(angle);
                var start = center + dir * (radius * 0.8f);
                var end = center + dir * (radius * (1.4f + needleExtra * pulse));
                using var pen = new Pen(Color.FromArgb(220, 255, 90, 90), 3f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                g.DrawLine(pen, start.ToPointF(), end.ToPointF());
            }
            using var bodyBrush = new SolidBrush(Color.FromArgb(230, 0, 0, 0));
            g.FillEllipse(bodyBrush, center.X - radius * 0.5f, center.Y - radius * 0.5f, radius, radius);
            using var bodyPen = new Pen(Color.FromArgb(220, 255, 90, 90), 2f);
            g.DrawEllipse(bodyPen, center.X - radius * 0.5f, center.Y - radius * 0.5f, radius, radius);
        }

        // Зона патруля — голубой блок с бегущей штриховкой.
        private static void DrawPatrolZone(Graphics g, RectangleF rect, float time)
        {
            using (var fill = new SolidBrush(Color.FromArgb(90, 80, 140, 220)))
            {
                g.FillRectangle(fill, rect);
            }
            var spacing = 14f;
            using var stripePen = new Pen(Color.FromArgb(110, 130, 200, 255), 2f);
            var offset = (time * 18f) % spacing;
            var saved = g.Save();
            g.SetClip(rect);
            for (var x = rect.Left - rect.Height + offset; x < rect.Right + rect.Height; x += spacing)
            {
                g.DrawLine(stripePen, x, rect.Bottom, x + rect.Height, rect.Top);
            }
            g.Restore(saved);
            using var border = new Pen(Color.FromArgb(220, 130, 200, 255), 2.5f);
            g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
        }

        // Патрульный — голубой собрат врага. Без буквы.
        private static void DrawPatrolEnemyMarker(Graphics g, Vec2 center, float time, bool alarmed)
        {
            var radius = 22f;
            var pulse = (MathF.Sin(time * 4f) + 1f) * 0.5f;
            for (var i = 0; i < 6; i++)
            {
                var angle = i * 60f + time * 18f;
                var dir = Vec2.FromAngleDeg(angle);
                var start = center + dir * (radius * 0.8f);
                var end = center + dir * (radius * (1.3f + 0.6f * pulse));
                using var pen = new Pen(Color.FromArgb(220, 130, 200, 255), 3f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                g.DrawLine(pen, start.ToPointF(), end.ToPointF());
            }
            using var bodyBrush = new SolidBrush(Color.FromArgb(230, 0, 0, 0));
            g.FillEllipse(bodyBrush, center.X - radius * 0.5f, center.Y - radius * 0.5f, radius, radius);
            using var bodyPen = new Pen(Color.FromArgb(220, 130, 200, 255), 2f);
            g.DrawEllipse(bodyPen, center.X - radius * 0.5f, center.Y - radius * 0.5f, radius, radius);

            if (alarmed)
            {
                using var font = new Font("Consolas", 16f, FontStyle.Bold);
                using var brush = new SolidBrush(Color.FromArgb(230, 255, 220, 80));
                var sz = g.MeasureString("!", font);
                g.DrawString("!", font, brush, center.X - sz.Width * 0.5f, center.Y - radius - sz.Height - 2f);
            }
        }

        private static void DrawCaption(Graphics g, float cx, float baselineY, string text, Color color)
        {
            using var font = new Font("Consolas", 11.5f, FontStyle.Bold);
            using var brush = new SolidBrush(color);
            var sz = g.MeasureString(text, font);
            g.DrawString(text, font, brush, cx - sz.Width * 0.5f, baselineY - sz.Height);
        }

        private static void DrawFrameDots(Graphics g, RectangleF area, int frameIndex, int frameCount)
        {
            var dotSize = 6f;
            var spacing = 12f;
            var totalW = frameCount * dotSize + (frameCount - 1) * spacing;
            var startX = area.X + (area.Width - totalW) * 0.5f;
            var y = area.Bottom - dotSize - 2f;
            for (var i = 0; i < frameCount; i++)
            {
                var x = startX + i * (dotSize + spacing);
                if (i == frameIndex)
                {
                    using var brush = new SolidBrush(Color.FromArgb(220, 255, 230, 130));
                    g.FillEllipse(brush, x, y, dotSize, dotSize);
                }
                else
                {
                    using var pen = new Pen(Color.FromArgb(120, 180, 180, 180), 1.4f);
                    g.DrawEllipse(pen, x, y, dotSize, dotSize);
                }
            }
        }
    }
}
