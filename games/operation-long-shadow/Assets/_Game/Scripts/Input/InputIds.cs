namespace LongShadow.Input
{
    /// <summary>Map and action names in Assets/_Game/Settings/Input/LongShadow.inputactions (TECH_PLAN §7.2).</summary>
    public static class InputIds
    {
        /// <summary>Gameplay map.</summary>
        public const string PlayerMap = "Player";
        /// <summary>UI map (InputSystemUIInputModule).</summary>
        public const string UIMap = "UI";

        public const string Move = "Move";
        public const string Look = "Look";
        public const string Fire = "Fire";
        public const string Aim = "Aim";
        public const string Reload = "Reload";
        public const string Sprint = "Sprint";
        public const string Jump = "Jump";
        /// <summary>C (never Ctrl: Ctrl+W closes the tab).</summary>
        public const string Crouch = "Crouch";
        public const string Frag = "Frag";
        public const string Melee = "Melee";
        public const string Interact = "Interact";
        public const string Weapon1 = "Weapon1";
        public const string Weapon2 = "Weapon2";
        public const string WeaponScroll = "WeaponScroll";
        public const string Objectives = "Objectives";
        /// <summary>Esc, P as backup.</summary>
        public const string Pause = "Pause";
        /// <summary>Space, Hold(duration=1).</summary>
        public const string Skip = "Skip";

        public const string UINavigate = "Navigate";
        public const string UISubmit = "Submit";
        public const string UICancel = "Cancel";
        public const string UIPoint = "Point";
        public const string UIClick = "Click";
        public const string UIRightClick = "RightClick";
        public const string UIMiddleClick = "MiddleClick";
        public const string UIScrollWheel = "ScrollWheel";

        /// <summary>Every Player action name (tests check the asset contains them).</summary>
        public static readonly string[] PlayerActions =
        {
            Move, Look, Fire, Aim, Reload, Sprint, Jump, Crouch, Frag, Melee, Interact, Weapon1, Weapon2, WeaponScroll,
            Objectives, Pause, Skip,
        };

        /// <summary>Every UI action name.</summary>
        public static readonly string[] UIActions =
        {
            UINavigate, UISubmit, UICancel, UIPoint, UIClick, UIRightClick, UIMiddleClick, UIScrollWheel,
        };

        /// <summary>Seconds Space must be held to skip a cinematic (GDD §12).</summary>
        public const float SkipHoldSeconds = 1f;
    }
}
