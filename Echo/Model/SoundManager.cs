using System;
using System.IO;
using System.Windows.Media;

namespace Echo
{
    public class SoundManager
    {
        private MediaPlayer enemyPlayer;
        private MediaPlayer menuMusicPlayer;
        private MediaPlayer enteringPlayer;
        private MediaPlayer fireMovePlayer;
        private MediaPlayer fireSpacePlayer;
        private MediaPlayer keyFoundPlayer;
        private MediaPlayer cutsceneTypePlayer;
        private bool cutsceneTypeLoopActive;
        private bool enemyLoopStarted;
        private bool menuMusicPlaying;

        private float enemyVolumeSmoothed;

        private string musicFolderPath;
        private const float EnemyMaxHearDistance = 250f; // Максимальное расстояние, на котором слышен враг
        private const float EnemyMinHearDistance = 100f; // Расстояние, на котором громкость максимальна
        private const float EnemyVolumeSmoothing = 0.16f;
        private static readonly TimeSpan EnemyLoopRestartOffset = TimeSpan.FromSeconds(0.05);


        public SoundManager()
        {
            musicFolderPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Music"));

            enemyPlayer = CreatePlayer("enemy.mp3");
            enemyPlayer.MediaEnded += (s, e) =>
            {
                enemyPlayer.Position = EnemyLoopRestartOffset;
                enemyPlayer.Play();
            };
            enemyLoopStarted = false;
            enemyVolumeSmoothed = 0f;

            menuMusicPlayer = CreatePlayer("main.mp3");
            menuMusicPlayer.MediaEnded += (s, e) =>
            {
                menuMusicPlayer.Position = TimeSpan.Zero;
                menuMusicPlayer.Play();
            };
            menuMusicPlaying = false;

            enteringPlayer = CreatePlayer("Entering.mp3");
            fireMovePlayer = CreatePlayer("fireMove.mp3");
            fireSpacePlayer = CreatePlayer("fireSpace.mp3");
            keyFoundPlayer = CreatePlayer("keyFound.mp3");
            cutsceneTypePlayer = CreatePlayer("text.mp3");
            cutsceneTypePlayer.Volume = 0.55;
            cutsceneTypePlayer.MediaEnded += (s, e) =>
            {
                if (cutsceneTypeLoopActive)
                {
                    cutsceneTypePlayer.Position = TimeSpan.Zero;
                    cutsceneTypePlayer.Play();
                }
            };
            cutsceneTypeLoopActive = false;
        }

        private MediaPlayer CreatePlayer(string fileName)
        {
            var player = new MediaPlayer();
            var filePath = Path.Combine(musicFolderPath, fileName);
            if (File.Exists(filePath))
            {
                player.Open(new Uri(filePath, UriKind.Absolute));
            }
            return player;
        }

        public void UpdateEnemySound(float closestEnemyDistance)
        {
            try
            {
                if (closestEnemyDistance > EnemyMaxHearDistance)
                {
                    enemyVolumeSmoothed += (0f - enemyVolumeSmoothed) * EnemyVolumeSmoothing;
                    enemyPlayer.Volume = Math.Clamp(enemyVolumeSmoothed, 0, 1);
                    return;
                }

                var volume = 0f;
                if (closestEnemyDistance <= EnemyMinHearDistance)
                {
                    volume = 1f;
                }
                else
                {
                    var normalizedDistance = (closestEnemyDistance - EnemyMinHearDistance) / (EnemyMaxHearDistance - EnemyMinHearDistance);
                    volume = 1f - normalizedDistance;
                }

                enemyVolumeSmoothed += (volume - enemyVolumeSmoothed) * EnemyVolumeSmoothing;
                enemyPlayer.Volume = Math.Clamp(enemyVolumeSmoothed, 0, 1);

                if (!enemyLoopStarted)
                {
                    enemyPlayer.Position = EnemyLoopRestartOffset;
                    enemyPlayer.Play();
                    enemyLoopStarted = true;
                }
            }
            catch
            {
            }
        }

        public void StopEnemySound()
        {
            try
            {
                enemyPlayer.Stop();
                enemyPlayer.Volume = 0;
                enemyVolumeSmoothed = 0f;
                enemyLoopStarted = false;
            }
            catch
            {
            }
        }

        public void PlayMenuMusic()
        {
            try
            {
                if (!menuMusicPlaying)
                {
                    menuMusicPlayer.Position = TimeSpan.Zero;
                    menuMusicPlayer.Play();
                    menuMusicPlaying = true;
                }
            }
            catch
            {
            }
        }

        public void StopMenuMusic()
        {
            try
            {
                menuMusicPlayer.Stop();
                menuMusicPlaying = false;
            }
            catch
            {
            }
        }


        public void PlayEntering()
        {
            PlaySound(enteringPlayer);
        }

        public void PlayFireMove()
        {
            PlaySound(fireMovePlayer);
        }

        public void PlayFireSpace()
        {
            PlaySound(fireSpacePlayer);
        }

        public void PlayKeyFound()
        {
            PlaySound(keyFoundPlayer);
        }

        public void StartCutsceneType()
        {
            try
            {
                if (cutsceneTypeLoopActive) return;
                cutsceneTypeLoopActive = true;
                cutsceneTypePlayer.Position = TimeSpan.Zero;
                cutsceneTypePlayer.Play();
            }
            catch
            {
            }
        }

        public void StopCutsceneType()
        {
            try
            {
                cutsceneTypeLoopActive = false;
                cutsceneTypePlayer.Stop();
            }
            catch
            {
            }
        }

        private void PlaySound(MediaPlayer player)
        {
            try
            {
                player.Stop();
                player.Position = TimeSpan.Zero;
                player.Play();
            }
            catch
            {
            }
        }
    }
}
