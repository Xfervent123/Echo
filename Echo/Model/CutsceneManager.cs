using System;

namespace Echo
{
    public enum CutsceneKind
    {
        Intro,
        Ending
    }

    public enum CutsceneSceneType
    {
        Newsroom,
        RknLogo,
        BlackoutCity,
        VibeCoders,
        TrueProgrammer,
        HeroOath,
        // Сцены концовки.
        RknDoors,
        SwitchRoom,
        SwitchPulled,
        CityAlive
    }

    public class CutsceneScene
    {
        public CutsceneSceneType Type;
        public string Title = "";
        public string Speaker = "";
        public string Text = "";
        public string ImageName = "";
    }

    public class CutsceneManager
    {
        private readonly CutsceneScene[] scenes;
        private readonly CutsceneKind kind;
        private int currentSceneIndex;
        private float typeTimer;
        private int revealedCharCount;
        private float sceneEnterProgress;
        private bool isFullyRevealed;
        private bool isFinished;
        private float backgroundTime;
        private int previousRevealedCharCount;

        // Скорость "печати" символов в секунду
        private const float CharsPerSecond = 38f;
        private const float SceneEnterSpeed = 1.4f;

        public CutsceneManager() : this(CutsceneKind.Intro) { }

        public CutsceneManager(CutsceneKind kind)
        {
            this.kind = kind;
            scenes = BuildScenes(kind);
            currentSceneIndex = 0;
            revealedCharCount = 0;
            typeTimer = 0f;
            sceneEnterProgress = 0f;
            isFullyRevealed = false;
            isFinished = false;
            backgroundTime = 0f;
            previousRevealedCharCount = 0;
        }

        public CutsceneKind Kind => kind;
        public CutsceneScene CurrentScene => scenes[currentSceneIndex];
        public int CurrentSceneIndex => currentSceneIndex;
        public int SceneCount => scenes.Length;
        public int RevealedCharCount => revealedCharCount;
        public bool IsFullyRevealed => isFullyRevealed;
        public bool IsFinished => isFinished;
        public float SceneEnterProgress => Math.Clamp(sceneEnterProgress, 0f, 1f);
        public float BackgroundTime => backgroundTime;

        public string GetSceneText(int index)
        {
            if (index < 0 || index >= scenes.Length) return string.Empty;
            return scenes[index].Text ?? string.Empty;
        }

        public string GetSceneImageName(int index)
        {
            if (index < 0 || index >= scenes.Length) return string.Empty;
            return scenes[index].ImageName ?? string.Empty;
        }

        // Возвращает количество новых символов, открывшихся на этом кадре, и сбрасывает счётчик.
        public int ConsumeNewlyRevealedCount()
        {
            var diff = revealedCharCount - previousRevealedCharCount;
            previousRevealedCharCount = revealedCharCount;
            if (diff < 0) diff = 0;
            return diff;
        }

        public void Update(InputManager input, float dt)
        {
            if (isFinished) return;

            backgroundTime += dt;
            sceneEnterProgress = MathF.Min(1f, sceneEnterProgress + dt * SceneEnterSpeed);

            if (!isFullyRevealed)
            {
                typeTimer += dt;
                var totalToReveal = (int)(typeTimer * CharsPerSecond);
                var len = CurrentScene.Text.Length;
                if (totalToReveal >= len)
                {
                    revealedCharCount = len;
                    isFullyRevealed = true;
                }
                else
                {
                    revealedCharCount = totalToReveal;
                }
            }

            if (input.IsKeyJustPressed(Keys.Space) || input.IsKeyJustPressed(Keys.Enter))
            {
                if (!isFullyRevealed)
                {
                    revealedCharCount = CurrentScene.Text.Length;
                    isFullyRevealed = true;
                }
                else
                {
                    AdvanceScene();
                }
            }
        }

        private void AdvanceScene()
        {
            if (currentSceneIndex < scenes.Length - 1)
            {
                currentSceneIndex++;
                revealedCharCount = 0;
                previousRevealedCharCount = 0;
                typeTimer = 0f;
                sceneEnterProgress = 0f;
                isFullyRevealed = false;
                backgroundTime = 0f;
            }
            else
            {
                isFinished = true;
            }
        }

        private static CutsceneScene[] BuildScenes(CutsceneKind kind)
        {
            return kind == CutsceneKind.Ending ? BuildEndingScenes() : BuildIntroScenes();
        }

        private static CutsceneScene[] BuildIntroScenes()
        {
            return new[]
            {
                new CutsceneScene
                {
                    Type = CutsceneSceneType.Newsroom,
                    Title = "СРОЧНЫЕ НОВОСТИ",
                    Speaker = "ВЕДУЩИЙ",
                    Text = "Сегодня вечером компании \"РКН\" — РыбКомНадзор — переданы расширенные полномочия по контролю за всеми электросетями страны.",
                    ImageName = "1"
                },
                new CutsceneScene
                {
                    Type = CutsceneSceneType.RknLogo,
                    Title = "РЫБКОМНАДЗОР",
                    Speaker = "ПРЕСС-РЕЛИЗ РКН",
                    Text = "С этой минуты ни один сервер, ни один маршрутизатор, ни одна нейросеть не работают без нашего разрешения. Мы решаем, кому будет свет.",
                    ImageName = "2"
                },
                new CutsceneScene
                {
                    Type = CutsceneSceneType.BlackoutCity,
                    Title = "ОТКЛЮЧЕНИЕ",
                    Speaker = "* * *",
                    Text = "По всему городу одно за другим гаснут окна. Дата-центры замолкают. Чат-боты перестают отвечать. На улицах остаётся только тишина.",
                    ImageName = "3"
                },
                new CutsceneScene
                {
                    Type = CutsceneSceneType.VibeCoders,
                    Title = "ВАЙБ-КОДЕРЫ",
                    Speaker = "* * *",
                    Text = "Программисты, привыкшие просить нейросеть сделать всё за них, не выдержали. Они бродят во мраке, бормочут промпты и охотятся на любого, кто ещё умеет думать сам.",
                    ImageName = "4"
                },
                new CutsceneScene
                {
                    Type = CutsceneSceneType.TrueProgrammer,
                    Title = "НАСТОЯЩИЙ ПРОГРАММИСТ",
                    Speaker = "* * *",
                    Text = "Но один из них помнит, как писать код руками. Без подсказок. Без автодополнения. Только он, клавиатура и тёмный экран.",
                    ImageName = "5"
                },
                new CutsceneScene
                {
                    Type = CutsceneSceneType.HeroOath,
                    Title = "ПУТЬ К РКН",
                    Speaker = "ГЕРОЙ",
                    Text = "Я доберусь до офиса РКН. Я включу рубильник. Я верну городу электричество — и людям разум.",
                    ImageName = "6"
                }
            };
        }

        private static CutsceneScene[] BuildEndingScenes()
        {
            return new[]
            {
                new CutsceneScene
                {
                    Type = CutsceneSceneType.RknDoors,
                    Title = "ОФИС РКН",
                    Speaker = "ГЕРОЙ",
                    Text = "Вот он — офис РКН. Огромный, тёмный, и почему-то открытый. Кажется, никто и не думал, что досюда кто-то дойдёт без подсказки нейросети.",
                    ImageName = "end1"
                },
                new CutsceneScene
                {
                    Type = CutsceneSceneType.SwitchRoom,
                    Title = "ЦЕНТР УПРАВЛЕНИЯ",
                    Speaker = "* * *",
                    Text = "В тёмном зале — один-единственный рубильник. Над ним табличка: «ГОРОДСКАЯ СЕТЬ». Рядом аквариумы с холодными лампами и пустые костюмы чиновников.",
                    ImageName = "end2"
                },
                new CutsceneScene
                {
                    Type = CutsceneSceneType.SwitchPulled,
                    Title = "ЩЕЛЧОК",
                    Speaker = "ГЕРОЙ",
                    Text = "Я кладу руку на рукоять. Один щелчок — и по проводам бежит свет, как кровь, возвращающаяся в онемевшие пальцы.",
                    ImageName = "end3"
                },
                new CutsceneScene
                {
                    Type = CutsceneSceneType.CityAlive,
                    Title = "ПРОБУЖДЕНИЕ",
                    Speaker = "* * *",
                    Text = "Вайб-кодеры замирают. Один за другим они снимают капюшоны, смотрят на свои руки, на пустые поля промптов — и впервые за долгое время начинают думать сами.",
                    ImageName = "end4"
                }
            };
        }
    }
}
