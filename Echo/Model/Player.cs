using System;
using System.Collections.Generic;
using System.Text;

namespace Echo
{
    public class Player
    {
        public Vec2 Position;  // позиция центра игрока в пикселях
        public float Speed;    // скорость движения (пикселей/сек)
        public int Radius;   // радиус хитбокса

        public int Energy; // текущая энергия для импульсов
        public int MaxEnergy;  // максимальная энергия
        float RechargeTimer;    // накопленное время перезарядки
        float RechargeTime; // сколько секунд на 1 заряд

        public float Stamina; // текущая выносливость для стелса
        public float MaxStamina; // максимальная выносливость
        private const float StaminaDrainRate = 20f; // расход выносливости в секунду
        private const float StaminaRegenRate = 33.3f; // восстановление выносливости в секунду
        private const float StaminaRegenDelay = 1f; // задержка перед началом восстановления
        private float StaminaRegenTimer; // таймер задержки восстановления

        public Player(Vec2 startPos)
        {
            Position = startPos;
            Speed = 150;
            Radius = 16;
            MaxEnergy = 3;
            Energy = MaxEnergy;
            RechargeTimer = 0;
            RechargeTime = 2.1f;
            MaxStamina = 100f;
            Stamina = MaxStamina;
            StaminaRegenTimer = 0f;
        }

        // Перезаряжает энергию по таймеру
        public void Update(float dt)
        {
            if (Energy < MaxEnergy) RechargeTimer += dt;
            else Energy = MaxEnergy;
            if (RechargeTimer >= RechargeTime)
            {
                RechargeTimer = 0;
                Energy++;
            }

            // Восстановление выносливости
            if (StaminaRegenTimer > 0f)
            {
                StaminaRegenTimer -= dt;
            }
            else if (Stamina < MaxStamina)
            {
                Stamina += StaminaRegenRate * dt;
                if (Stamina > MaxStamina) Stamina = MaxStamina;
            }
        }

        public bool CanSneak()
        {
            return Stamina > 0f;
        }

        public void UseSneakStamina(float dt)
        {
            Stamina -= StaminaDrainRate * dt;
            if (Stamina < 0f) Stamina = 0f;
            StaminaRegenTimer = StaminaRegenDelay;
        }

        // Двигает игрока по inputDir с учётом коллизий со стенами.
        // X и Y двигаются раздельно — чтобы было скольжение вдоль стен
        public bool Move(Vec2 inputDir, Maze maze, float dt, float speedMultiplier = 1f)
        {
            if (inputDir.Length == 0) return false;

            var moveDelta = inputDir.Normalized() * Speed * speedMultiplier * dt;
            var moved = false;

            // Создаем "виртуальную" точку: где будет игрок, если сдвинется только по X
            var nextPosX = new Vec2(Position.X + moveDelta.X, Position.Y);

            if (!CollidesWithWall(nextPosX, maze))
            {
                Position.X = nextPosX.X;
                moved = true;
            }

            // Создаем "виртуальную" точку: где будет игрок, если сдвинется только по Y 
            var nextPosY = new Vec2(Position.X, Position.Y + moveDelta.Y);

            if (!CollidesWithWall(nextPosY, maze))
            {
                Position.Y = nextPosY.Y;
                moved = true;
            }

            return moved;
        }

        // Проверяет столкновение игрока (Круг) с окружающими стенами
        bool CollidesWithWall(Vec2 pos, Maze maze)
        {
            var playerGridX = (int)(pos.X / maze.TileSize);
            var playerGridY = (int)(pos.Y / maze.TileSize);

            // Проверяем квадрат 3х3 клетки вокруг игрока
            for (int x = playerGridX - 1; x <= playerGridX + 1; x++)
            {
                for (int y = playerGridY - 1; y <= playerGridY + 1; y++)
                {
                    if (!maze.IsWall(x, y)) continue;

                    // пиксельные границы стены
                    float wallLeft = x * maze.TileSize;
                    float wallRight = wallLeft + maze.TileSize;
                    float wallTop = y * maze.TileSize;
                    float wallBottom = wallTop + maze.TileSize;

                    // Ищем ближайшую точку на стене к центру игрока.
                    var closestX = pos.X;
                    if (closestX < wallLeft) closestX = wallLeft;
                    else if (closestX > wallRight) closestX = wallRight;

                    var closestY = pos.Y;
                    if (closestY < wallTop) closestY = wallTop;
                    else if (closestY > wallBottom) closestY = wallBottom;

                    // измеряем дистанцию от неё до центра игрока.
                    var distanceX = pos.X - closestX;
                    var distanceY = pos.Y - closestY;

                    var distanceSquared = (distanceX * distanceX) + (distanceY * distanceY);

                    if (distanceSquared < (Radius * Radius))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool CanPulse()
        {
            if (Energy > 0) return true;
            return false;
        }

        public void UseEnergy()
        {
            if (Energy - 1 >= 0)
            {
                Energy -= 1;
                RechargeTimer = 0;
            }
        }
    }
}
