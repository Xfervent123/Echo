using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace Echo
{
    // Рендер вступительной катсцены: картинка-фон под каждый кадр + диалоговое окно как в Undertale.
    public partial class GameForm
    {
        private const float CutsceneTextBoxHeightRatio = 0.28f; // Высота диалогового окна как доля экрана.
        private const float CutsceneTextBoxMargin = 28f;
        private const float CutsceneTextPadding = 26f;
        private const float CutsceneTitleTopMargin = 36f;
        private const float CutsceneCharBlinkSpeed = 6f;

        // Храним заранее уменьшенные Bitmap'ы — без ресайза каждый кадр (иначе тормозит на больших PNG).
        private Dictionary<string, Bitmap>? cutsceneImages;
        private bool cutsceneImagesLoaded;
        private const int CutsceneImageMaxDimension = 1600; // Максимальная сторона картинки в кэше.

        // Кэш полностью отрендеренного фона (картинка + виньетка + градиент) под текущий размер окна.
        private Bitmap? cutsceneBackgroundCache;
        private string cutsceneBackgroundCacheKey = string.Empty;
        private Size cutsceneBackgroundCacheSize;
        private float cutsceneBackgroundCacheBoxHeight;

        // Высота диалогового окна, единая для всех сцен — рассчитывается под максимальное число строк.
        private float cutsceneTextBoxHeight;
        private int cutsceneTextBoxHeightForWidth = -1;
        private CutsceneKind cutsceneTextBoxHeightForKind;

        private void EnsureCutsceneImagesLoaded()
        {
            if (cutsceneImagesLoaded) return;
            cutsceneImagesLoaded = true;
            cutsceneImages = new Dictionary<string, Bitmap>();

            var imageFolder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Image"));
            if (!Directory.Exists(imageFolder)) return;

            var names = new HashSet<string>();
            foreach (var kind in new[] { CutsceneKind.Intro, CutsceneKind.Ending })
            {
                var manager = new CutsceneManager(kind);
                for (var i = 0; i < manager.SceneCount; i++)
                {
                    var name = manager.GetSceneImageName(i);
                    if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
                }
            }

            foreach (var name in names)
            {
                var path = Path.Combine(imageFolder, name + ".png");
                if (!File.Exists(path)) continue;
                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                    using var original = Image.FromStream(fs);
                    var scaled = CreateScaledCopy(original, CutsceneImageMaxDimension);
                    cutsceneImages[name] = scaled;
                }
                catch
                {
                }
            }
        }

        private static Bitmap CreateScaledCopy(Image source, int maxDimension)
        {
            var srcW = source.Width;
            var srcH = source.Height;
            float scale = 1f;
            if (srcW > maxDimension || srcH > maxDimension)
            {
                scale = MathF.Min(maxDimension / (float)srcW, maxDimension / (float)srcH);
            }
            var dstW = MathF.Max(1f, srcW * scale);
            var dstH = MathF.Max(1f, srcH * scale);

            var bmp = new Bitmap((int)dstW, (int)dstH, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, bmp.Width, bmp.Height));
            }
            return bmp;
        }

        private void DrawCutscene(Graphics g)
        {
            EnsureCutsceneImagesLoaded();

            var cutscene = game.Cutscene;
            var scene = cutscene.CurrentScene;
            var w = ClientSize.Width;
            var h = ClientSize.Height;

            EnsureCutsceneTextBoxHeight(g, w);
            var textBoxHeight = cutsceneTextBoxHeight;

            // Готовим (или берём из кэша) заранее отрендеренный фон сцены.
            var imageName = game.Cutscene.GetSceneImageName(cutscene.CurrentSceneIndex);
            EnsureCutsceneBackgroundCache(imageName, w, h, textBoxHeight);
            if (cutsceneBackgroundCache != null)
            {
                g.DrawImageUnscaled(cutsceneBackgroundCache, 0, 0);
            }
            else
            {
                using var black = new SolidBrush(Color.Black);
                g.FillRectangle(black, 0, 0, w, h);
            }

            var enter = cutscene.SceneEnterProgress;
            var ease = enter * enter * (3f - 2f * enter);
            var fadeAlpha = (int)(255f * (1f - ease));
            if (fadeAlpha > 0)
            {
                using var fade = new SolidBrush(Color.FromArgb(fadeAlpha, 0, 0, 0));
                g.FillRectangle(fade, 0, 0, w, h);
            }

            DrawCutsceneTitle(g, scene.Title, w, enter);

            DrawCutsceneProgress(g, w, h, cutscene.CurrentSceneIndex, cutscene.SceneCount);

            DrawCutsceneTextBox(
                g,
                scene.Speaker,
                scene.Text,
                cutscene.RevealedCharCount,
                cutscene.IsFullyRevealed,
                cutscene.BackgroundTime,
                w,
                h,
                textBoxHeight);

            using var hintFont = new Font("Consolas", 11f, FontStyle.Regular);
            using var hintBrush = new SolidBrush(Color.FromArgb(160, 220, 220, 220));
            var hintText = "ESC — пропустить";
            var hintSize = g.MeasureString(hintText, hintFont);
            g.DrawString(hintText, hintFont, hintBrush, w - hintSize.Width - 14f, 14f);
        }

        // Высчитывает максимальную высоту текстового окна так, чтобы в него поместился самый длинный текст
        // среди всех сцен — без переноса размера между сценами.
        private void EnsureCutsceneTextBoxHeight(Graphics g, int w)
        {
            var kind = game.Cutscene.Kind;
            if (cutsceneTextBoxHeightForWidth == w
                && cutsceneTextBoxHeight > 0f
                && cutsceneTextBoxHeightForKind == kind) return;

            var boxWidth = w - (CutsceneTextBoxMargin * 2f);
            var textWidth = boxWidth - (CutsceneTextPadding * 2f);
            if (textWidth <= 0f)
            {
                cutsceneTextBoxHeight = 160f;
                cutsceneTextBoxHeightForWidth = w;
                cutsceneTextBoxHeightForKind = kind;
                return;
            }

            using var textFont = new Font("Consolas", 18f, FontStyle.Regular);
            using var fmt = new StringFormat
            {
                Trimming = StringTrimming.None,
                FormatFlags = StringFormatFlags.LineLimit
            };

            var maxTextHeight = 0f;
            var sceneCount = game.Cutscene.SceneCount;
            for (var i = 0; i < sceneCount; i++)
            {
                var text = game.Cutscene.GetSceneText(i);
                if (string.IsNullOrEmpty(text)) continue;
                var size = g.MeasureString(text, textFont, (int)textWidth, fmt);
                if (size.Height > maxTextHeight) maxTextHeight = size.Height;
            }

            // Если по какой-то причине ничего не намерили, оставляем безопасный минимум.
            if (maxTextHeight <= 0f) maxTextHeight = textFont.GetHeight(g) * 3f;

            // Запас под курсор-стрелочку и небольшой воздух снизу.
            var arrowReserve = 18f;
            cutsceneTextBoxHeight = maxTextHeight + (CutsceneTextPadding * 2f) + arrowReserve;
            cutsceneTextBoxHeightForWidth = w;
            cutsceneTextBoxHeightForKind = kind;
        }

        private void EnsureCutsceneBackgroundCache(string imageName, int w, int h, float textBoxHeight)
        {
            if (w <= 0 || h <= 0) return;

            var size = new Size(w, h);
            if (cutsceneBackgroundCache != null
                && cutsceneBackgroundCacheKey == imageName
                && cutsceneBackgroundCacheSize == size
                && MathF.Abs(cutsceneBackgroundCacheBoxHeight - textBoxHeight) < 0.5f)
            {
                return;
            }

            cutsceneBackgroundCache?.Dispose();
            cutsceneBackgroundCache = RenderCutsceneBackground(imageName, w, h, textBoxHeight);
            cutsceneBackgroundCacheKey = imageName;
            cutsceneBackgroundCacheSize = size;
            cutsceneBackgroundCacheBoxHeight = textBoxHeight;
        }

        private Bitmap RenderCutsceneBackground(string imageName, int w, int h, float textBoxHeight)
        {
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            var area = new RectangleF(0, 0, w, h);

            using (var black = new SolidBrush(Color.Black))
            {
                g.FillRectangle(black, area);
            }

            if (!string.IsNullOrEmpty(imageName)
                && cutsceneImages != null
                && cutsceneImages.TryGetValue(imageName, out var image))
            {
                var imgW = image.Width;
                var imgH = image.Height;
                if (imgW > 0 && imgH > 0)
                {
                    var screenAspect = w / (float)h;
                    var imageAspect = imgW / (float)imgH;
                    float drawW, drawH;
                    if (imageAspect > screenAspect)
                    {
                        drawH = h;
                        drawW = drawH * imageAspect;
                    }
                    else
                    {
                        drawW = w;
                        drawH = drawW / imageAspect;
                    }

                    var dx = (w - drawW) * 0.5f;
                    var dy = (h - drawH) * 0.5f;

                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.DrawImage(image, new Rectangle((int)dx, (int)dy, (int)drawW, (int)drawH));
                }
            }

            // Тёмный градиент снизу под текстовым окном — высота тянется под него.
            var gradHeight = textBoxHeight + (CutsceneTextBoxMargin * 2f) + (h * 0.18f);
            if (gradHeight > h) gradHeight = h;
            var gradTop = h - gradHeight;
            using (var gradient = new LinearGradientBrush(
                new RectangleF(0, gradTop, w, gradHeight),
                Color.FromArgb(0, 0, 0, 0),
                Color.FromArgb(180, 0, 0, 0),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(gradient, 0, gradTop, w, gradHeight);
            }

            using (var path = new GraphicsPath())
            {
                path.AddEllipse(-w * 0.2f, -h * 0.4f, w * 1.4f, h * 1.8f);
                using var brush = new PathGradientBrush(path)
                {
                    CenterColor = Color.FromArgb(0, 0, 0, 0),
                    SurroundColors = new[] { Color.FromArgb(180, 0, 0, 0) }
                };
                g.FillRectangle(brush, 0, 0, w, h);
            }

            return bmp;
        }

        private void DrawCutsceneTitle(Graphics g, string title, int w, float enter)
        {
            var ease = enter * enter * (3f - 2f * enter);
            var alpha = (int)(220f * ease);
            if (alpha <= 0) return;

            using var font = new Font("Consolas", 16f, FontStyle.Bold);
            using var brush = new SolidBrush(Color.FromArgb(alpha, 255, 230, 130));
            using var underlinePen = new Pen(Color.FromArgb((int)(160f * ease), 255, 230, 130), 1.5f);

            // Тёмная подложка под заголовок, чтобы читался даже на светлой картинке.
            var size = g.MeasureString(title, font);
            var x = (w - size.Width) * 0.5f;
            var y = CutsceneTitleTopMargin * 0.5f + (1f - ease) * -10f;

            using (var bg = new SolidBrush(Color.FromArgb((int)(160f * ease), 0, 0, 0)))
            {
                g.FillRectangle(bg, x - 14f, y - 4f, size.Width + 28f, size.Height + 8f);
            }

            g.DrawString(title, font, brush, x, y);
            g.DrawLine(underlinePen, x - 8f, y + size.Height + 2f, x + size.Width + 8f, y + size.Height + 2f);
        }

        private void DrawCutsceneProgress(Graphics g, int w, int h, int currentIndex, int total)
        {
            var dotSize = 7f;
            var spacing = 14f;
            var totalWidth = (total * dotSize) + ((total - 1) * spacing);
            var startX = (w - totalWidth) * 0.5f;
            var y = h - 12f;

            for (var i = 0; i < total; i++)
            {
                var x = startX + i * (dotSize + spacing);
                if (i == currentIndex)
                {
                    using var brush = new SolidBrush(Color.FromArgb(220, 255, 230, 130));
                    g.FillEllipse(brush, x, y, dotSize, dotSize);
                }
                else if (i < currentIndex)
                {
                    using var brush = new SolidBrush(Color.FromArgb(140, 200, 200, 200));
                    g.FillEllipse(brush, x, y, dotSize, dotSize);
                }
                else
                {
                    using var pen = new Pen(Color.FromArgb(110, 200, 200, 200), 1.4f);
                    g.DrawEllipse(pen, x, y, dotSize, dotSize);
                }
            }
        }

        private void DrawCutsceneTextBox(
            Graphics g,
            string speaker,
            string fullText,
            int revealedCount,
            bool isFullyRevealed,
            float time,
            int w,
            int h,
            float boxHeight)
        {
            var box = new RectangleF(
                CutsceneTextBoxMargin,
                h - boxHeight - CutsceneTextBoxMargin,
                w - (CutsceneTextBoxMargin * 2f),
                boxHeight);

            // Фон коробки — белая рамка на чёрном
            using (var bgBrush = new SolidBrush(Color.FromArgb(235, 0, 0, 0)))
            {
                g.FillRectangle(bgBrush, box);
            }

            using (var outerPen = new Pen(Color.White, 4f))
            {
                g.DrawRectangle(outerPen, box.X, box.Y, box.Width, box.Height);
            }

            using (var innerPen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f))
            {
                var inset = 6f;
                g.DrawRectangle(innerPen, box.X + inset, box.Y + inset, box.Width - (inset * 2f), box.Height - (inset * 2f));
            }

            if (!string.IsNullOrEmpty(speaker))
            {
                using var speakerFont = new Font("Consolas", 13f, FontStyle.Bold);
                using var speakerBg = new SolidBrush(Color.FromArgb(255, 0, 0, 0));
                using var speakerBrush = new SolidBrush(Color.FromArgb(255, 255, 230, 130));
                using var speakerPen = new Pen(Color.White, 2f);

                var speakerSize = g.MeasureString(speaker, speakerFont);
                var speakerPad = 10f;
                var speakerRect = new RectangleF(
                    box.X + 18f,
                    box.Y - speakerSize.Height - 4f,
                    speakerSize.Width + speakerPad * 2f,
                    speakerSize.Height + 6f);

                g.FillRectangle(speakerBg, speakerRect);
                g.DrawRectangle(speakerPen, speakerRect.X, speakerRect.Y, speakerRect.Width, speakerRect.Height);
                g.DrawString(speaker, speakerFont, speakerBrush, speakerRect.X + speakerPad, speakerRect.Y + 2f);
            }

            // Текст с переносами по словам и эффектом печати.
            var revealed = revealedCount >= fullText.Length ? fullText : fullText.Substring(0, revealedCount);
            using var textFont = new Font("Consolas", 18f, FontStyle.Regular);
            using var textBrush = new SolidBrush(Color.White);

            var textRect = new RectangleF(
                box.X + CutsceneTextPadding,
                box.Y + CutsceneTextPadding,
                box.Width - (CutsceneTextPadding * 2f),
                box.Height - (CutsceneTextPadding * 2f));

            using var fmt = new StringFormat
            {
                Trimming = StringTrimming.None,
                FormatFlags = StringFormatFlags.LineLimit
            };

            g.DrawString(revealed, textFont, textBrush, textRect, fmt);

            if (isFullyRevealed)
            {
                var blink = (MathF.Sin(time * CutsceneCharBlinkSpeed) + 1f) * 0.5f;
                var alpha = (int)(120f + 135f * blink);
                using var arrowBrush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255));

                var arrowX = box.Right - 30f;
                var arrowY = box.Bottom - 28f;
                var arrowPoints = new[]
                {
                    new PointF(arrowX, arrowY),
                    new PointF(arrowX + 14f, arrowY + 7f),
                    new PointF(arrowX, arrowY + 14f)
                };
                g.FillPolygon(arrowBrush, arrowPoints);

                using var hintFont = new Font("Consolas", 10.5f, FontStyle.Regular);
                using var hintBrush = new SolidBrush(Color.FromArgb(alpha, 200, 200, 200));
                var hint = "SPACE";
                var hintSize = g.MeasureString(hint, hintFont);
                g.DrawString(hint, hintFont, hintBrush, arrowX - hintSize.Width - 6f, arrowY - 2f);
            }
        }
    }
}
