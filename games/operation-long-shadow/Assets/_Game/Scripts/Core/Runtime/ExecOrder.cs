namespace LongShadow.Core
{
    /// <summary>
    /// Script execution order constants (TECH_PLAN §5.3). Use with <c>[DefaultExecutionOrder(ExecOrder.Player)]</c>.
    /// </summary>
    public static class ExecOrder
    {
        /// <summary>Library hosts register before anything reads them.</summary>
        public const int Libraries = -2000;
        public const int Platform = -1000;
        public const int Input = -900;
        public const int LightStage = -500;
        public const int Player = -200;
        public const int Weapons = -100;
        public const int AI = 0;
        public const int Squad = 10;
        public const int Mission = 100;
        public const int Audio = 200;
        public const int UI = 300;
        public const int Cinematics = 400;
    }
}
